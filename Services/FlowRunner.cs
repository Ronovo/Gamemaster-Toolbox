using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;
using System.Text.Json;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Runs a flow one step at a time.
    ///
    /// Pure with respect to the database: the caller loads the flow with its steps, their
    /// oracles and options, and those oracles' entries, and hands it over. The actual rolling
    /// is OracleRoller's job -- this only decides which step is next and keeps the transcript.
    ///
    /// A flow is a flat list. Steps run in order by default; an outcome can carry a branch that
    /// sends the run to another step or ends it early.
    ///
    /// Returns an error message or null, the same contract as OracleRoller.TryRoll.
    /// </summary>
    public static class FlowRunner
    {
        /// <summary>
        /// How many steps one run may roll before it is stopped. A branch may point backwards,
        /// which is a legitimate "roll that again" loop; this only stops an afternoon of
        /// clicking growing an unbounded transcript in the session store.
        /// </summary>
        public const int StepLimit = 200;

        /// <summary>
        /// Rolls or answers whichever step the run is sitting on, and moves it along.
        ///
        /// `answer` is what the person sent with the step: the ID of a FlowChoiceOption on a
        /// dropdown question, the typed value on a text question, and nothing on a roll.
        /// </summary>
        public static string? TryAdvance(Flow flow, FlowRunState state, ISession session,
            string? answer, out FlowRunStep? rolled)
        {
            rolled = null;

            if (state.Finished)
            {
                return "This run has finished. Start over to run the flow again.";
            }

            var step = flow.Steps.FirstOrDefault(s => s.ID == state.CurrentStepID);
            if (step == null)
            {
                // The flow was edited in another tab while this run was open.
                Restart(flow.ID, session);
                return "This flow changed while the run was open, so the run has been reset. Start it again.";
            }

            var jump = Jump.None;

            string? error;
            if (step.Kind.IsQuestion())
            {
                error = TryAnswer(step, state, answer, out rolled, out jump);
            }
            else
            {
                error = TryRoll(flow, step, state, session, out rolled);
            }

            if (error != null || rolled == null)
            {
                return error ?? "That step produced no outcome.";
            }

            state.Transcript.Add(rolled);
            state.StepsRolled++;

            var next = NextStep(flow, step, rolled.Outcome.EntryID, jump);

            if (next != null && state.StepsRolled >= StepLimit)
            {
                state.HitStepLimit = true;
                next = null;
            }

            state.CurrentStepID = next?.ID;
            return null;
        }

        // ------------------------------------------------------------------ rolling

        private static string? TryRoll(Flow flow, FlowStep step, FlowRunState state,
            ISession session, out FlowRunStep? rolled)
        {
            rolled = null;

            var error = ResolveOracle(flow, step, state, out var resolved);
            if (error != null || resolved == null)
            {
                return error ?? "That step has no oracle to roll.";
            }

            var oracle = resolved;

            var blocked = oracle.RollBlockedReason();
            if (blocked != null)
            {
                return $"{step.PositionPath()} ({step.DisplayLabel()}): {blocked}";
            }

            var rollError = OracleRoller.TryRoll(oracle, session, out var outcome);
            if (rollError != null || outcome == null)
            {
                return rollError ?? "That roll produced no outcome.";
            }

            // If the matched outcome names a table, remember it so a later RollChosen step
            // pointed at this one can roll it -- exactly as TryAnswer does for a picked option.
            if (outcome.EntryID != null)
            {
                var branch = step.Branches.FirstOrDefault(b => b.OracleEntryID == outcome.EntryID.Value);
                if (branch?.OracleID != null)
                {
                    state.ChosenOracles[step.ID] = branch.OracleID.Value;
                }
            }

            rolled = new FlowRunStep
            {
                StepID = step.ID,
                Label = step.DisplayLabel(),
                OracleName = oracle.Name,
                Outcome = outcome,
                Outcomes = new List<RollOutcome> { outcome }
            };

            return null;
        }

        /// <summary>
        /// Which oracle this step rolls: its own on a Roll step, and on a RollChosen step
        /// whichever one the step it names landed on -- a Choice option or a Roll outcome.
        /// </summary>
        private static string? ResolveOracle(Flow flow, FlowStep step, FlowRunState state, out Oracle? oracle)
        {
            oracle = null;

            if (step.Kind == FlowStepKind.Roll)
            {
                oracle = step.Oracle;
                return oracle == null
                    ? $"{step.PositionPath()} points at an oracle that no longer exists."
                    : null;
            }

            var source = step.SourceStep(flow);
            if (source == null)
            {
                return $"{step.PositionPath()} ({step.DisplayLabel()}) rolls what an earlier step picked, but that step is no longer in this flow.";
            }

            if (!state.ChosenOracles.TryGetValue(source.ID, out var oracleId))
            {
                return $"{step.PositionPath()} ({step.DisplayLabel()}) rolls what {source.PositionPath()} ({source.DisplayLabel()}) picks, and nothing has been picked yet.";
            }

            // The flow graph already carries every oracle its steps can reach, entries and
            // all -- through a question's options and a roll's branches.
            oracle = flow.Steps
                .SelectMany(s => s.Options.Select(o => o.Oracle).Concat(s.Branches.Select(b => b.Oracle)))
                .FirstOrDefault(o => o != null && o.ID == oracleId);

            return oracle == null
                ? $"{step.PositionPath()} ({step.DisplayLabel()}) was sent to an oracle that no longer exists."
                : null;
        }

        // ------------------------------------------------------------------ questions

        private static string? TryAnswer(FlowStep step, FlowRunState state, string? answer,
            out FlowRunStep? answered, out Jump jump)
        {
            answered = null;
            jump = Jump.None;

            var question = step.Question ?? string.Empty;
            string headline;
            string note = string.Empty;

            if (step.Kind == FlowStepKind.Choice)
            {
                if (!int.TryParse(answer, out var optionId))
                {
                    return "Choose one of the options before carrying on.";
                }

                var option = step.Options.FirstOrDefault(o => o.ID == optionId);
                if (option == null)
                {
                    return "That is not one of this question's options. Reload the page and try again.";
                }

                headline = option.Label;

                if (option.OracleID != null)
                {
                    state.ChosenOracles[step.ID] = option.OracleID.Value;
                    note = $"Rolls on {option.Oracle?.Name ?? "the chosen oracle"}";
                }

                if (option.Routes)
                {
                    jump = new Jump(option.EndsFlow ? null : option.TargetStepID);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(answer))
                {
                    return "Answer the question before carrying on.";
                }

                if (answer.Length > 200)
                {
                    return "That answer is longer than 200 characters.";
                }

                headline = answer.Trim();
            }

            answered = new FlowRunStep
            {
                StepID = step.ID,
                Label = step.DisplayLabel(),
                OracleName = string.Empty,
                IsQuestion = true,
                Outcome = new RollOutcome
                {
                    IsDice = false,
                    Key = headline,
                    Caption = question,
                    Result = note
                }
            };
            answered.Outcomes.Add(answered.Outcome);

            return null;
        }

        // ------------------------------------------------------------------ where next

        /// <summary>One step's answer routing somewhere other than straight on.</summary>
        private readonly struct Jump
        {
            public static readonly Jump None = default;

            public Jump(int? target)
            {
                Jumps = true;
                Target = target;
            }

            public bool Jumps { get; }

            /// <summary>The step ID to go to, or null to end the run.</summary>
            public int? Target { get; }
        }

        /// <summary>
        /// Where a step sends the run once it has produced its outcome. An explicit jump wins
        /// -- a picked option or a matched branch that routes -- and one with no target ends
        /// the run. Otherwise the run moves to the next step in order.
        /// </summary>
        private static FlowStep? NextStep(Flow flow, FlowStep step, int? matchedEntryId, Jump jump)
        {
            if (!jump.Jumps && matchedEntryId != null && step.AllowsBranches())
            {
                var branch = step.Branches.FirstOrDefault(b => b.OracleEntryID == matchedEntryId.Value);
                if (branch != null && branch.Routes)
                {
                    jump = new Jump(branch.EndsFlow ? null : branch.TargetStepID);
                }
            }

            if (jump.Jumps)
            {
                return jump.Target == null
                    ? null
                    : flow.Steps.FirstOrDefault(s => s.ID == jump.Target.Value);
            }

            var ordered = flow.Steps.OrderBy(s => s.SortOrder).ToList();
            var index = ordered.FindIndex(s => s.ID == step.ID);
            return index >= 0 && index + 1 < ordered.Count ? ordered[index + 1] : null;
        }

        public static FlowStep? CurrentStep(Flow flow, FlowRunState state)
        {
            return flow.Steps.FirstOrDefault(s => s.ID == state.CurrentStepID);
        }

        // ------------------------------------------------------------------ session

        /// <summary>
        /// The run held in this browser session, or a fresh one pointing at the first step.
        ///
        /// A stored run whose next step is no longer in the flow is discarded rather than
        /// repaired: the flow was edited underneath it, and guessing where the run should
        /// resume is worse than starting over.
        /// </summary>
        public static FlowRunState Load(Flow flow, ISession session)
        {
            var json = session.GetString(RunKey(flow.ID));

            FlowRunState? stored = null;
            if (json != null)
            {
                try
                {
                    stored = JsonSerializer.Deserialize<FlowRunState>(json);
                }
                catch (JsonException)
                {
                    stored = null;
                }
            }

            if (stored == null || stored.FlowID != flow.ID)
            {
                return Fresh(flow);
            }

            if (stored.CurrentStepID != null && !flow.Steps.Any(s => s.ID == stored.CurrentStepID))
            {
                return Fresh(flow);
            }

            return stored;
        }

        public static void Save(FlowRunState state, ISession session)
        {
            session.SetString(RunKey(state.FlowID), JsonSerializer.Serialize(state));
        }

        public static void Restart(int flowId, ISession session)
        {
            session.Remove(RunKey(flowId));
        }

        private static FlowRunState Fresh(Flow flow)
        {
            return new FlowRunState
            {
                FlowID = flow.ID,
                CurrentStepID = flow.InRunOrder().FirstOrDefault()?.ID
            };
        }

        // "v3": the state shape changed with the move to a flat list, so any run held in an
        // older shape is abandoned rather than half-read.
        private static string RunKey(int flowId) => $"flow-run-v3-{flowId}";
    }
}
