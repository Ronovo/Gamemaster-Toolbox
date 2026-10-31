using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Http;

namespace GM_Toolbox.Tests;

public class FlowRunnerTests
{
    private int _nextId = 100;

    /// <summary>A d6 oracle with one outcome covering every roll, so a roll always lands on it.</summary>
    private Oracle Table(string name, out OracleEntry entry)
    {
        entry = new OracleEntry { ID = _nextId++, SortOrder = 1, RangeStart = 1, RangeEnd = 6, Result = $"{name} result" };
        var oracle = new Oracle { ID = _nextId++, Name = name, OracleSubtype = OracleSubtype.Dice, DiceCount = 1, DiceFaces = 6 };
        oracle.OracleEntries.Add(entry);
        return oracle;
    }

    private Oracle Table(string name) => Table(name, out _);

    private static Flow Flow(params FlowStep[] steps)
    {
        var flow = new Flow { ID = 1, Name = "Encounter" };
        var order = 1;
        foreach (var step in steps)
        {
            step.SortOrder = order++;
            flow.Steps.Add(step);
        }
        return flow;
    }

    private FlowStep Roll(Oracle? oracle) => new() { ID = _nextId++, Kind = FlowStepKind.Roll, Oracle = oracle, OracleID = oracle?.ID };

    private FlowStep Question(FlowStepKind kind, string text = "What now?") =>
        new() { ID = _nextId++, Kind = kind, Question = text };

    private FlowChoiceOption Option(string label, Oracle? oracle = null) =>
        new() { ID = _nextId++, Label = label, Oracle = oracle, OracleID = oracle?.ID };

    private static (string? Error, FlowRunStep? Step) Advance(Flow flow, FlowRunState state, MemorySession session, string? answer = null)
    {
        var error = FlowRunner.TryAdvance(flow, state, session, answer, out var step);
        return (error, step);
    }

    [Fact]
    public void Steps_run_in_order_until_the_last()
    {
        var first = Roll(Table("Weather"));
        var second = Roll(Table("Mood"));
        var flow = Flow(first, second);
        var session = new MemorySession();
        var state = FlowRunner.Load(flow, session);
        Assert.Equal(first.ID, state.CurrentStepID);

        var (error, step) = Advance(flow, state, session);

        Assert.Null(error);
        Assert.Equal(("Weather", "Weather result"), (step!.OracleName, step.Outcome.Result));
        Assert.Equal(second.ID, FlowRunner.CurrentStep(flow, state)!.ID);
        Advance(flow, state, session);
        Assert.True(state.Finished);
        Assert.Equal(2, state.Transcript.Count);
        Assert.Equal("This run has finished. Start over to run the flow again.", Advance(flow, state, session).Error);
    }

    [Fact]
    public void An_outcome_can_send_the_run_to_another_step_or_end_it()
    {
        var weather = Table("Weather", out var entry);
        var first = Roll(weather);
        var skipped = Roll(Table("Skipped"));
        var last = Roll(Table("Last"));
        first.Branches.Add(new FlowBranch { OracleEntryID = entry.ID, TargetStepID = last.ID });
        var flow = Flow(first, skipped, last);
        var state = FlowRunner.Load(flow, new MemorySession());

        Advance(flow, state, new MemorySession());
        Assert.Equal(last.ID, state.CurrentStepID);

        first.Branches.Clear();
        first.Branches.Add(new FlowBranch { OracleEntryID = entry.ID, EndsFlow = true });
        var ending = FlowRunner.Load(flow, new MemorySession());
        Advance(flow, ending, new MemorySession());
        Assert.True(ending.Finished);
    }

    [Fact]
    public void A_branch_that_only_names_a_table_goes_straight_on()
    {
        var weather = Table("Weather", out var entry);
        var picked = Table("Storms");
        var first = Roll(weather);
        var next = Roll(Table("Next"));
        first.Branches.Add(new FlowBranch { OracleEntryID = entry.ID, OracleID = picked.ID, Oracle = picked });
        var flow = Flow(first, next);
        var state = FlowRunner.Load(flow, new MemorySession());

        Advance(flow, state, new MemorySession());

        Assert.Equal(next.ID, state.CurrentStepID);
        Assert.Equal(picked.ID, state.ChosenOracles[first.ID]);
    }

    [Fact]
    public void A_dropdown_answer_records_the_option_and_any_table_it_names()
    {
        var goblins = Table("Goblins");
        var choice = Question(FlowStepKind.Choice, "Who attacks?");
        var plain = Option("Nobody");
        var named = Option("Goblins", goblins);
        choice.Options.Add(plain);
        choice.Options.Add(named);
        var flow = Flow(choice, Roll(Table("After")));
        var session = new MemorySession();
        var state = FlowRunner.Load(flow, session);

        Assert.Equal("Choose one of the options before carrying on.", Advance(flow, state, session, "goblins").Error);
        Assert.Equal("That is not one of this question's options. Reload the page and try again.",
            Advance(flow, state, session, "9999").Error);
        var (error, step) = Advance(flow, state, session, named.ID.ToString());

        Assert.Null(error);
        Assert.True(step!.IsQuestion);
        Assert.Equal(("Goblins", "Who attacks?", "Rolls on Goblins"), (step.Outcome.Key, step.Outcome.Caption, step.Outcome.Result));
        Assert.Single(step.Outcomes);
        Assert.Equal(goblins.ID, state.ChosenOracles[choice.ID]);
    }

    [Fact]
    public void A_dropdown_option_without_its_oracle_loaded_still_says_it_rolls_one()
    {
        var choice = Question(FlowStepKind.Choice);
        var option = new FlowChoiceOption { ID = _nextId++, Label = "Something", OracleID = 555 };
        choice.Options.Add(option);
        var flow = Flow(choice);
        var state = FlowRunner.Load(flow, new MemorySession());

        Assert.Equal("Rolls on the chosen oracle", Advance(flow, state, new MemorySession(), option.ID.ToString()).Step!.Outcome.Result);
    }

    [Fact]
    public void A_dropdown_option_can_jump_ahead_or_end_the_run()
    {
        var choice = Question(FlowStepKind.Choice);
        var skipped = Roll(Table("Skipped"));
        var last = Roll(Table("Last"));
        var jump = Option("Skip ahead");
        jump.TargetStepID = last.ID;
        var stop = Option("Stop");
        stop.EndsFlow = true;
        choice.Options.Add(jump);
        choice.Options.Add(stop);
        var flow = Flow(choice, skipped, last);

        var jumping = FlowRunner.Load(flow, new MemorySession());
        Advance(flow, jumping, new MemorySession(), jump.ID.ToString());
        Assert.Equal(last.ID, jumping.CurrentStepID);

        var stopping = FlowRunner.Load(flow, new MemorySession());
        Advance(flow, stopping, new MemorySession(), stop.ID.ToString());
        Assert.True(stopping.Finished);
    }

    [Theory]
    [InlineData(null, "Answer the question before carrying on.")]
    [InlineData("   ", "Answer the question before carrying on.")]
    public void A_text_question_needs_an_answer(string? answer, string message)
    {
        var flow = Flow(Question(FlowStepKind.Text));
        var state = FlowRunner.Load(flow, new MemorySession());

        Assert.Equal(message, Advance(flow, state, new MemorySession(), answer).Error);
        Assert.Empty(state.Transcript);
    }

    [Fact]
    public void A_text_answer_is_kept_trimmed_and_short()
    {
        var flow = Flow(Question(FlowStepKind.Text, "Name the villain"));
        var state = FlowRunner.Load(flow, new MemorySession());

        Assert.Equal("That answer is longer than 200 characters.", Advance(flow, state, new MemorySession(), new string('x', 201)).Error);
        var (_, step) = Advance(flow, state, new MemorySession(), "  Strahd  ");
        Assert.Equal(("Strahd", "Name the villain"), (step!.Outcome.Key, step.Outcome.Caption));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("51")]
    [InlineData("three")]
    [InlineData("+3")]
    [InlineData(null)]
    public void A_loop_needs_a_whole_number_from_1_to_50(string? answer)
    {
        var flow = Flow(Question(FlowStepKind.Loop, null!), Roll(Table("Loot")));
        var state = FlowRunner.Load(flow, new MemorySession());

        Assert.Equal($"Enter a whole number from 1 to {FlowRunner.LoopLimit}.", Advance(flow, state, new MemorySession(), answer).Error);
    }

    [Fact]
    public void A_loop_rolls_the_next_step_that_many_times()
    {
        var loot = Roll(Table("Loot"));
        var loop = Question(FlowStepKind.Loop, null!);
        var flow = Flow(loop, loot);
        var session = new MemorySession();
        var state = FlowRunner.Load(flow, session);

        var (_, asked) = Advance(flow, state, session, " 3 ");
        Assert.Equal(("×3", "Loop Step 2 (Loot): how many times?"), (asked!.Outcome.Key, asked.Outcome.Caption));
        var (_, rolled) = Advance(flow, state, session);

        Assert.Equal(3, rolled!.Outcomes.Count);
        Assert.Same(rolled.Outcomes[^1], rolled.Outcome);
        Assert.Null(state.PendingRepeat);
    }

    [Fact]
    public void A_roll_on_another_oracle_rolls_what_the_earlier_step_picked()
    {
        var goblins = Table("Goblins");
        var choice = Question(FlowStepKind.Choice);
        var option = Option("Goblins", goblins);
        choice.Options.Add(option);
        var chosen = new FlowStep { ID = _nextId++, Kind = FlowStepKind.RollChosen, SourceStepID = choice.ID };
        var flow = Flow(choice, chosen);
        var session = new MemorySession();
        var state = FlowRunner.Load(flow, session);

        Advance(flow, state, session, option.ID.ToString());
        var (error, rolled) = Advance(flow, state, session);

        Assert.Null(error);
        Assert.Equal("Goblins", rolled!.OracleName);
    }

    [Fact]
    public void A_roll_on_another_oracle_explains_what_is_missing()
    {
        var choice = Question(FlowStepKind.Choice);
        var chosen = new FlowStep { ID = _nextId++, Kind = FlowStepKind.RollChosen, SourceStepID = choice.ID, Label = "Encounter" };
        var flow = Flow(choice, chosen);
        var state = new FlowRunState { FlowID = flow.ID, CurrentStepID = chosen.ID };

        Assert.Equal("Step 2 (Encounter) rolls what Step 1 (What now?) picks, and nothing has been picked yet.",
            Advance(flow, state, new MemorySession()).Error);

        state.ChosenOracles[choice.ID] = 777;
        Assert.Equal("Step 2 (Encounter) was sent to an oracle that no longer exists.", Advance(flow, state, new MemorySession()).Error);

        chosen.SourceStepID = 999;
        Assert.Equal("Step 2 (Encounter) rolls what an earlier step picked, but that step is no longer in this flow.",
            Advance(flow, state, new MemorySession()).Error);
    }

    [Fact]
    public void A_roll_step_whose_oracle_is_gone_or_unready_explains_why()
    {
        var gone = Roll(null);
        var empty = new Oracle { ID = _nextId++, Name = "Empty", OracleSubtype = OracleSubtype.Dice, DiceCount = 1, DiceFaces = 6 };
        var unready = Roll(empty);
        var flow = Flow(gone, unready);

        Assert.Equal("Step 1 points at an oracle that no longer exists.",
            Advance(flow, new FlowRunState { FlowID = 1, CurrentStepID = gone.ID }, new MemorySession()).Error);
        Assert.Equal("Step 2 (Empty): This oracle has no outcomes yet. Use Configure to create them.",
            Advance(flow, new FlowRunState { FlowID = 1, CurrentStepID = unready.ID }, new MemorySession()).Error);
    }

    [Fact]
    public void A_roll_that_lands_outside_every_band_stops_the_run_there()
    {
        var gap = new Oracle { ID = _nextId++, Name = "Gap", OracleSubtype = OracleSubtype.Dice, DiceCount = 1, DiceFaces = 6 };
        gap.OracleEntries.Add(new OracleEntry { ID = _nextId++, RangeStart = 7, RangeEnd = 8, Result = "Never" });
        var step = Roll(gap);
        var flow = Flow(step);
        var state = FlowRunner.Load(flow, new MemorySession());

        Assert.StartsWith("Rolled ", Advance(flow, state, new MemorySession()).Error);
        Assert.Equal(step.ID, state.CurrentStepID);
    }

    [Fact]
    public void A_run_that_loops_back_forever_stops_at_the_step_limit()
    {
        var table = Table("Again", out var entry);
        var step = Roll(table);
        step.Branches.Add(new FlowBranch { OracleEntryID = entry.ID, TargetStepID = step.ID });
        var flow = Flow(step);
        var session = new MemorySession();
        var state = FlowRunner.Load(flow, session);

        for (var i = 0; i < FlowRunner.StepLimit; i++)
        {
            Assert.Null(Advance(flow, state, session).Error);
        }

        Assert.True(state.HitStepLimit);
        Assert.True(state.Finished);
    }

    [Fact]
    public void A_run_whose_step_was_deleted_is_reset()
    {
        var flow = Flow(Roll(Table("Weather")));
        var session = new MemorySession();
        var state = new FlowRunState { FlowID = flow.ID, CurrentStepID = 4242 };
        FlowRunner.Save(state, session);

        Assert.Equal("This flow changed while the run was open, so the run has been reset. Start it again.",
            Advance(flow, state, session).Error);
        Assert.Empty(session.Keys);
    }

    [Fact]
    public void A_saved_run_comes_back_unless_it_no_longer_fits_the_flow()
    {
        var first = Roll(Table("Weather"));
        var second = Roll(Table("Mood"));
        var flow = Flow(first, second);
        var session = new MemorySession();
        var state = FlowRunner.Load(flow, session);
        Advance(flow, state, session);
        FlowRunner.Save(state, session);

        Assert.Equal(second.ID, FlowRunner.Load(flow, session).CurrentStepID);

        FlowRunner.Save(new FlowRunState { FlowID = flow.ID, CurrentStepID = 4242 }, session);
        Assert.Equal(first.ID, FlowRunner.Load(flow, session).CurrentStepID);

        FlowRunner.Save(new FlowRunState { FlowID = 99, CurrentStepID = second.ID }, session);
        session.SetString("flow-run-1", session.GetString("flow-run-99")!);
        Assert.Equal(first.ID, FlowRunner.Load(flow, session).CurrentStepID);

        session.SetString("flow-run-1", "not json");
        Assert.Equal(first.ID, FlowRunner.Load(flow, session).CurrentStepID);

        FlowRunner.Save(new FlowRunState { FlowID = flow.ID, CurrentStepID = null }, session);
        Assert.True(FlowRunner.Load(flow, session).Finished);
        FlowRunner.Restart(flow.ID, session);
        Assert.Equal(first.ID, FlowRunner.Load(flow, session).CurrentStepID);
    }
}
