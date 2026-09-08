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
    /// oracles and options, and those oracles' entries, and hands it over. The actual rolling is
    /// OracleRoller's job -- this only decides which step is next and keeps the transcript.
    ///
    /// Returns an error message or null, the same contract as OracleRoller.TryRoll.
    /// </summary>
    public static class FlowRunner
    {
        /// <summary>
        /// How many steps one run may roll before it is stopped.
        ///
        /// A branch may point backwards, which is a legitimate "roll that again" loop and not
        /// something to forbid. The user clicks for every step, so a loop cannot hang anything;
        /// this is here to stop an afternoon of clicking growing an unbounded transcript in the
        /// session store.
        /// </summary>
        public const int StepLimit = 50;

        /// <summary>
        /// Rolls or answers whichever step the run is sitting on, and moves it along.
        ///
        /// `answer` is what the person sent with the step: the ID of a FlowChoiceOption on a
        /// dropdown question, the typed value on a text or number one, and nothing at all on a
        /// step that rolls.
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

            var error = step.Kind.IsQuestion()
                ? TryAnswer(step, state, answer, out rolled)
                : TryRoll(flow, step, state, session, out rolled);

            if (error != null || rolled == null)
            {
                return error ?? "That step produced no outcome.";
            }

            state.Transcript.Add(rolled);
            state.StepsRolled++;

            var next = NextStep(flow, step, rolled.Outcome.EntryID);

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

            // Assigned once the null has been ruled out, so the compiler can see it too rather
            // than being told to look away with a !.
            var oracle = resolved;

            var blocked = oracle.RollBlockedReason();
            if (blocked != null)
            {
                return $"Step {step.SortOrder} ({step.DisplayLabel()}): {blocked}";
            }

            var rollError = OracleRoller.TryRoll(oracle, session, out var outcome);
            if (rollError != null || outcome == null)
            {
                return rollError ?? "That roll produced no outcome.";
            }

            rolled = new FlowRunStep
            {
                StepID = step.ID,
                Label = step.DisplayLabel(),
                OracleName = oracle.Name,
                Outcome = outcome
            };

            return null;
        }

        /// <summary>
        /// Which oracle this step rolls: its own on a Roll step, and on a RollChosen step
        /// whichever one the question it names landed on.
        /// </summary>
        private static string? ResolveOracle(Flow flow, FlowStep step, FlowRunState state, out Oracle? oracle)
        {
            oracle = null;

            if (step.Kind == FlowStepKind.Roll)
            {
                oracle = step.Oracle;
                return oracle == null
                    ? $"Step {step.SortOrder} points at an oracle that no longer exists."
                    : null;
            }

            var source = flow.Steps.FirstOrDefault(s => s.SortOrder == step.SourceStepSortOrder);
            if (source == null)
            {
                return $"Step {step.SortOrder} ({step.DisplayLabel()}) rolls what an earlier question picked, but that question is no longer in this flow.";
            }

            if (!state.ChosenOracles.TryGetValue(source.ID, out var oracleId))
            {
                return $"Step {step.SortOrder} ({step.DisplayLabel()}) rolls what step {source.SortOrder} ({source.DisplayLabel()}) picks, and nothing has been picked yet.";
            }

            // Found through the options rather than a separate lookup: the flow graph already
            // carries every oracle its questions can reach, entries and all.
            oracle = flow.Steps
                .SelectMany(s => s.Options)
                .Select(o => o.Oracle)
                .FirstOrDefault(o => o != null && o.ID == oracleId);

            return oracle == null
                ? $"Step {step.SortOrder} ({step.DisplayLabel()}) was sent to an oracle that no longer exists."
                : null;
        }

        // ------------------------------------------------------------------ questions

        private static string? TryAnswer(FlowStep step, FlowRunState state, string? answer,
            out FlowRunStep? answered)
        {
            answered = null;

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

                if (step.Kind == FlowStepKind.Number && !int.TryParse(answer.Trim(), out _))
                {
                    return "That question wants a whole number.";
                }

                headline = answer.Trim();
            }

            answered = new FlowRunStep
            {
                StepID = step.ID,
                Label = step.DisplayLabel(),
                OracleName = string.Empty,
                IsQuestion = true,
                // An answer wearing a roll's clothes -- see the comment on FlowRunStep.Outcome.
                Outcome = new RollOutcome
                {
                    IsDice = false,
                    Key = headline,
                    Caption = question,
                    Result = note
                }
            };

            return null;
        }

        // ------------------------------------------------------------------ where next

        /// <summary>
        /// Where a step sends the run after landing on a given outcome.
        ///
        /// A branch matching that outcome wins; a branch with no target ends the run. Otherwise
        /// the run falls through to the next step by SortOrder, which is what makes a flow with
        /// no branches at all a plain ordered chain. Question steps carry no branches, so they
        /// always take the fall-through and need no special case here.
        /// </summary>
        public static FlowStep? NextStep(Flow flow, FlowStep step, int? matchedEntryId)
        {
            var branch = matchedEntryId == null
                ? null
                : step.Branches.FirstOrDefault(b => b.OracleEntryID == matchedEntryId.Value);

            if (branch != null)
            {
                return branch.TargetStepSortOrder == null
                    ? null
                    : flow.Steps.FirstOrDefault(s => s.SortOrder == branch.TargetStepSortOrder.Value);
            }

            return flow.Steps.FirstOrDefault(s => s.SortOrder == step.SortOrder + 1);
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
                CurrentStepID = flow.Steps.OrderBy(s => s.SortOrder).FirstOrDefault()?.ID
            };
        }

        private static string RunKey(int flowId) => $"flow-run-{flowId}";
    }
}
