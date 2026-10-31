using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Tests;

public class FlowWebTests(AppFactory app) : IClassFixture<AppFactory>
{
    private async Task<int> CreateFlowAsync(HttpClient client, string name)
    {
        var response = await client.PostAsync("/Flows/Create", await AppFactory.FormAsync(client, "/Flows/Create", new()
        {
            ["Name"] = name, ["Description"] = "", ["GameSystemID"] = (await app.SystemIdAsync(client)).ToString(),
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return int.Parse(response.RequestMessage!.RequestUri!.Segments[^1]);
    }

    private static async Task<string> AddStepAsync(HttpClient client, int flowId, string kind, Dictionary<string, string>? extra = null)
    {
        var fields = new Dictionary<string, string> { ["AddKind"] = kind };
        foreach (var (key, value) in extra ?? new()) fields[key] = value;
        var response = await AppFactory.PostAsync(client, $"/Flows/AddStep/{flowId}", $"/Flows/Steps/{flowId}", fields);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private List<int> StepIds(int flowId) =>
        app.Query(db => db.FlowSteps.IgnoreQueryFilters().Where(s => s.FlowID == flowId)
            .OrderBy(s => s.SortOrder).Select(s => s.ID).ToList());

    private static async Task<string> PostTextAsync(HttpClient client, string url, string tokenPage, Dictionary<string, string> fields) =>
        WebUtility.HtmlDecode(await (await AppFactory.PostAsync(client, url, tokenPage, fields)).Content.ReadAsStringAsync());

    [Fact]
    public async Task Each_kind_of_step_checks_what_it_needs()
    {
        var client = await app.LoggedInAsync("flow-builder");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var flow = await CreateFlowAsync(client, "Adventure");

        Assert.Contains("Pick an oracle to roll.", await AddStepAsync(client, flow, "Roll"));
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString(), ["AddLabel"] = "Draw" });
        var choice = await AddStepAsync(client, flow, "Choice", new() { ["AddQuestion"] = "Which way?" });
        Assert.Contains("Which way?", choice);
        await AddStepAsync(client, flow, "Text", new() { ["AddQuestion"] = "Your name?" });
        var steps = StepIds(flow);
        Assert.Equal(3, steps.Count);

        Assert.Contains("Say which step this one takes its oracle from.", await AddStepAsync(client, flow, "RollChosen"));
        Assert.Contains("That step is not in this flow.",
            await AddStepAsync(client, flow, "RollChosen", new() { ["AddSourceStepID"] = "999999" }));
        Assert.Contains("This step takes its oracle from a dropdown question or a roll.",
            await AddStepAsync(client, flow, "RollChosen", new() { ["AddSourceStepID"] = steps[2].ToString() }));
        await AddStepAsync(client, flow, "RollChosen", new() { ["AddSourceStepID"] = steps[1].ToString() });
        await AddStepAsync(client, flow, "Loop");
        Assert.Equal(5, StepIds(flow).Count);
        Assert.Contains("Draw", await client.GetStringAsync($"/Flows/Steps/{flow}"));
        Assert.Contains("Adventure", await client.GetStringAsync($"/Flows/Details/{flow}"));
    }

    [Fact]
    public async Task Steps_move_one_place_at_a_time_and_removing_one_clears_what_pointed_at_it()
    {
        var client = await app.LoggedInAsync("flow-mover");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var flow = await CreateFlowAsync(client, "Shuffle");
        await AddStepAsync(client, flow, "Choice", new() { ["AddQuestion"] = "Which deck?" });
        var choice = StepIds(flow)[0];
        await AddStepAsync(client, flow, "RollChosen", new() { ["AddSourceStepID"] = choice.ToString() });
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString() });
        var steps = StepIds(flow);

        Assert.Contains("A step can only move one place at a time.", await PostTextAsync(client, $"/Flows/MoveStep/{flow}",
            $"/Flows/Steps/{flow}", new() { ["stepId"] = steps[2].ToString(), ["delta"] = "2" }));
        Assert.Contains("That step is no longer part of this flow.", await PostTextAsync(client, $"/Flows/MoveStep/{flow}",
            $"/Flows/Steps/{flow}", new() { ["stepId"] = "999999", ["delta"] = "1" }));
        await PostTextAsync(client, $"/Flows/MoveStep/{flow}", $"/Flows/Steps/{flow}", new() { ["stepId"] = steps[0].ToString(), ["delta"] = "-1" });
        Assert.Equal(steps, StepIds(flow));
        await PostTextAsync(client, $"/Flows/MoveStep/{flow}", $"/Flows/Steps/{flow}", new() { ["stepId"] = steps[2].ToString(), ["delta"] = "-1" });
        Assert.Equal([steps[0], steps[2], steps[1]], StepIds(flow));

        await PostTextAsync(client, $"/Flows/RemoveStep/{flow}", $"/Flows/Steps/{flow}", new() { ["stepId"] = choice.ToString() });
        Assert.Null(app.Query(db => db.FlowSteps.IgnoreQueryFilters().Single(s => s.ID == steps[1]).SourceStepID));
        Assert.Contains("That step is no longer part of this flow.", await PostTextAsync(client, $"/Flows/RemoveStep/{flow}",
            $"/Flows/Steps/{flow}", new() { ["stepId"] = choice.ToString() }));
        await PostTextAsync(client, $"/Flows/RemoveStep/{flow}", $"/Flows/Steps/{flow}", new() { ["stepId"] = steps[2].ToString() });
        Assert.Equal([steps[1]], StepIds(flow));
    }

    [Fact]
    public async Task Editing_a_step_checks_it_the_way_adding_one_does()
    {
        var client = await app.LoggedInAsync("flow-step-editor");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var tarot = await app.CreateOracleAsync(client, "Tarot", "TarotCards");
        var flow = await CreateFlowAsync(client, "Edits");
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString() });
        await AddStepAsync(client, flow, "Text", new() { ["AddQuestion"] = "Why?" });
        await AddStepAsync(client, flow, "RollChosen", new() { ["AddSourceStepID"] = StepIds(flow)[0].ToString() });
        var (roll, text, chosen) = (StepIds(flow)[0], StepIds(flow)[1], StepIds(flow)[2]);

        Assert.Contains("Edit", await client.GetStringAsync($"/Flows/EditStep/{roll}"));
        Assert.Contains("Pick an oracle to roll.", await PostTextAsync(client, "/Flows/EditStep", $"/Flows/EditStep/{roll}",
            new() { ["StepID"] = roll.ToString(), ["Label"] = "Kept", ["OracleID"] = "" }));
        await PostTextAsync(client, "/Flows/EditStep", $"/Flows/EditStep/{roll}",
            new() { ["StepID"] = roll.ToString(), ["Label"] = "Tarot draw", ["OracleID"] = tarot.ToString() });
        Assert.Equal(tarot, app.Query(db => db.FlowSteps.IgnoreQueryFilters().Single(s => s.ID == roll).OracleID));

        await PostTextAsync(client, "/Flows/EditStep", $"/Flows/EditStep/{text}",
            new() { ["StepID"] = text.ToString(), ["Question"] = "Why not?" });
        Assert.Equal("Why not?", app.Query(db => db.FlowSteps.IgnoreQueryFilters().Single(s => s.ID == text).Question));
        Assert.Contains("This step takes its oracle from a dropdown question or a roll.", await PostTextAsync(client,
            "/Flows/EditStep", $"/Flows/EditStep/{chosen}", new() { ["StepID"] = chosen.ToString(), ["SourceStepID"] = text.ToString() }));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Flows/EditStep/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AppFactory.PostAsync(client, "/Flows/EditStep", $"/Flows/Steps/{flow}",
            new() { ["StepID"] = "999999" })).StatusCode);
    }

    [Fact]
    public async Task Branches_send_an_outcome_to_a_step_to_the_end_or_to_another_oracle()
    {
        var client = await app.LoggedInAsync("flow-brancher");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var tarot = await app.CreateOracleAsync(client, "Tarot", "TarotCards");
        var flow = await CreateFlowAsync(client, "Branching");
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString() });
        await AddStepAsync(client, flow, "Text", new() { ["AddQuestion"] = "Then what?" });
        var (roll, text) = (StepIds(flow)[0], StepIds(flow)[1]);
        var entries = app.EntryIds(deck);
        Assert.Contains("Deck", await client.GetStringAsync($"/Flows/Branches/{roll}"));

        Dictionary<string, string> Rows(params (int entry, string? oracle, string? target)[] rows)
        {
            var fields = new Dictionary<string, string> { ["StepID"] = roll.ToString() };
            for (var i = 0; i < rows.Length; i++)
            {
                fields[$"Rows[{i}].EntryID"] = rows[i].entry.ToString();
                fields[$"Rows[{i}].OracleID"] = rows[i].oracle ?? "";
                fields[$"Rows[{i}].Target"] = rows[i].target ?? "";
            }
            return fields;
        }
        var page = $"/Flows/Branches/{roll}";
        await PostTextAsync(client, "/Flows/Branches", page,
            Rows((entries[0], null, "end"), (entries[1], null, text.ToString()), (entries[2], tarot.ToString(), null)));
        var branches = app.Query(db => db.FlowBranches.IgnoreQueryFilters().Where(b => b.FlowStepID == roll)
            .OrderBy(b => b.OracleEntryID).Select(b => new { b.EndsFlow, b.TargetStepID, b.OracleID }).ToList());
        Assert.Equal([(true, (int?)null, (int?)null), (false, text, null), (false, null, tarot)],
            branches.Select(b => (b.EndsFlow, b.TargetStepID, b.OracleID)).ToList());

        var details = WebUtility.HtmlDecode(await client.GetStringAsync($"/Flows/Details/{flow}"));
        Assert.Contains("Then what?", details);
        Assert.Contains("Tarot", details);

        await PostTextAsync(client, "/Flows/Branches", page, Rows((entries[0], null, null)));
        Assert.Equal(2, app.Query(db => db.FlowBranches.IgnoreQueryFilters().Count(b => b.FlowStepID == roll)));

        Assert.Contains("One of these routes points at a step that is not in this flow.",
            await PostTextAsync(client, "/Flows/Branches", page, Rows((entries[3], null, "999999"))));
        Assert.Contains("One of these outcomes no longer exists. Reload the page and try again.",
            await PostTextAsync(client, "/Flows/Branches", page, Rows((999999, null, "end"))));
        Assert.Contains("That oracle no longer exists.",
            await PostTextAsync(client, "/Flows/Branches", page, Rows((entries[3], "999999", null))));
        Assert.Contains("This kind of step has no fixed outcomes to branch on.", await PostTextAsync(client, "/Flows/Branches",
            $"/Flows/Branches/{text}", new() { ["StepID"] = text.ToString() }));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Flows/Branches/999999")).StatusCode);
    }

    [Fact]
    public async Task A_dropdown_question_has_options_that_are_added_named_ordered_and_removed()
    {
        var client = await app.LoggedInAsync("flow-options");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var tarot = await app.CreateOracleAsync(client, "Tarot", "TarotCards");
        var flow = await CreateFlowAsync(client, "Options");
        await AddStepAsync(client, flow, "Choice", new() { ["AddQuestion"] = "Which deck?" });
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString() });
        var (choice, roll) = (StepIds(flow)[0], StepIds(flow)[1]);
        var page = $"/Flows/Options/{choice}";
        Assert.Contains("Which deck?", await client.GetStringAsync(page));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Flows/Options/{roll}")).StatusCode);

        Assert.Contains("Give the option something to call it.",
            await PostTextAsync(client, $"/Flows/AddOption/{choice}", page, new() { ["AddLabel"] = " " }));
        await PostTextAsync(client, $"/Flows/AddOption/{choice}", page, new() { ["AddLabel"] = "Playing", ["AddOracleID"] = deck.ToString() });
        Assert.Contains("Tick at least one oracle to add.", await PostTextAsync(client, $"/Flows/AddOptionsFromOracles/{choice}", page, new()));
        Assert.Contains("None of those oracles still exist.",
            await PostTextAsync(client, $"/Flows/AddOptionsFromOracles/{choice}", page, new() { ["AddOracleIds"] = "999999" }));
        Assert.Contains("Every oracle you ticked is already an option on this question.",
            await PostTextAsync(client, $"/Flows/AddOptionsFromOracles/{choice}", page, new() { ["AddOracleIds"] = deck.ToString() }));
        Assert.Contains("Added 1 option(s).",
            await PostTextAsync(client, $"/Flows/AddOptionsFromOracles/{choice}", page, new() { ["AddOracleIds"] = tarot.ToString() }));

        var options = app.Query(db => db.FlowChoiceOptions.IgnoreQueryFilters().Where(o => o.FlowStepID == choice)
            .OrderBy(o => o.SortOrder).Select(o => o.ID).ToList());
        Assert.Equal(2, options.Count);
        Dictionary<string, string> Named(string first) => new()
        {
            ["StepID"] = choice.ToString(),
            ["Rows[0].OptionID"] = options[0].ToString(), ["Rows[0].Label"] = first, ["Rows[0].Target"] = roll.ToString(),
            ["Rows[1].OptionID"] = options[1].ToString(), ["Rows[1].Label"] = "Tarot", ["Rows[1].Target"] = "end",
        };
        Assert.Contains("Every option needs something to call it.", await PostTextAsync(client, "/Flows/Options", page, Named("")));
        await PostTextAsync(client, "/Flows/Options", page, Named("Standard"));
        Assert.Equal("Standard", app.Query(db => db.FlowChoiceOptions.IgnoreQueryFilters().Single(o => o.ID == options[0]).Label));

        var withOptions = WebUtility.HtmlDecode(await client.GetStringAsync($"/Flows/Details/{flow}"));
        Assert.Contains("Standard", withOptions);
        Assert.Contains("Which deck?", withOptions);
        Assert.Contains("Which deck?", await client.GetStringAsync($"/Flows/Run/{flow}"));

        Assert.Contains("An option can only move one place at a time.", await PostTextAsync(client, $"/Flows/MoveOption/{choice}",
            page, new() { ["optionId"] = options[1].ToString(), ["delta"] = "-2" }));
        await PostTextAsync(client, $"/Flows/MoveOption/{choice}", page, new() { ["optionId"] = options[1].ToString(), ["delta"] = "-1" });
        Assert.Equal([options[1], options[0]], app.Query(db => db.FlowChoiceOptions.IgnoreQueryFilters()
            .Where(o => o.FlowStepID == choice).OrderBy(o => o.SortOrder).Select(o => o.ID).ToList()));

        Assert.Contains("That option is no longer part of this question.",
            await PostTextAsync(client, $"/Flows/RemoveOption/{choice}", page, new() { ["optionId"] = "999999" }));
        await PostTextAsync(client, $"/Flows/RemoveOption/{choice}", page, new() { ["optionId"] = options[0].ToString() });
        Assert.Equal(1, app.Query(db => db.FlowChoiceOptions.IgnoreQueryFilters().Count(o => o.FlowStepID == choice)));
        Assert.Contains("Only a dropdown question has options.",
            await PostTextAsync(client, $"/Flows/AddOption/{roll}", page, new() { ["AddLabel"] = "Nope" }));
    }

    [Fact]
    public async Task Running_a_flow_asks_and_rolls_in_order_and_can_start_again()
    {
        var client = await app.LoggedInAsync("flow-runner");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var flow = await CreateFlowAsync(client, "Quick run");
        var empty = await CreateFlowAsync(client, "Nothing to run");
        var blocked = await AppFactory.PostAsync(client, $"/Flows/Run/{empty}", $"/Flows/Run/{empty}", new(), ajax: true);
        Assert.NotNull(JsonNode.Parse(await blocked.Content.ReadAsStringAsync())!["error"]);
        var page = $"/Flows/Run/{flow}";

        await AddStepAsync(client, flow, "Text", new() { ["AddQuestion"] = "Hero name?" });
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString() });
        Assert.Contains("Hero name?", await client.GetStringAsync(page));
        var answered = await AppFactory.PostAsync(client, page, page, new() { ["answer"] = "Rowan" }, ajax: true);
        Assert.Contains("Rowan", await answered.Content.ReadAsStringAsync());
        var drawn = await AppFactory.PostAsync(client, page, page, new(), ajax: true);
        Assert.Equal(HttpStatusCode.OK, drawn.StatusCode);
        Assert.NotNull(JsonNode.Parse(await drawn.Content.ReadAsStringAsync()));

        var finished = await AppFactory.PostAsync(client, page, page, new());
        Assert.EndsWith(page, finished.RequestMessage!.RequestUri!.AbsolutePath);

        var restarted = await AppFactory.PostAsync(client, $"/Flows/Restart/{flow}", page, new(), ajax: true);
        Assert.False(JsonNode.Parse(await restarted.Content.ReadAsStringAsync())!["finished"]!.GetValue<bool>());
        var again = await AppFactory.PostAsync(client, $"/Flows/Restart/{flow}", page, new());
        Assert.EndsWith(page, again.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task A_flow_can_be_listed_renamed_and_deleted_and_missing_ones_are_not_found()
    {
        var client = await app.LoggedInAsync("flow-keeper");
        var flow = await CreateFlowAsync(client, "Keeper");
        await CreateFlowAsync(client, "Other");
        var partial = await client.GetStringAsync("/Flows?name=keep&partial=1");
        Assert.Contains("Keeper", partial);
        Assert.DoesNotContain("Other", partial);
        Assert.Contains("<html", await client.GetStringAsync("/Flows"));

        var system = (await app.SystemIdAsync(client)).ToString();
        Assert.Contains("Keeper", await client.GetStringAsync($"/Flows/Edit/{flow}"));
        var invalid = await PostTextAsync(client, $"/Flows/Edit/{flow}", $"/Flows/Edit/{flow}",
            new() { ["ID"] = flow.ToString(), ["Name"] = "", ["GameSystemID"] = system });
        Assert.Contains("<form", invalid);
        Assert.Equal("Keeper", app.Query(db => db.Flows.IgnoreQueryFilters().Single(f => f.ID == flow).Name));
        await PostTextAsync(client, $"/Flows/Edit/{flow}", $"/Flows/Edit/{flow}",
            new() { ["ID"] = flow.ToString(), ["Name"] = "Renamed", ["GameSystemID"] = system });
        Assert.Equal("Renamed", app.Query(db => db.Flows.IgnoreQueryFilters().Single(f => f.ID == flow).Name));

        Assert.Contains("Renamed", await client.GetStringAsync($"/Flows/Delete/{flow}"));
        await PostTextAsync(client, $"/Flows/Delete/{flow}", $"/Flows/Delete/{flow}", new());
        Assert.False(app.Query(db => db.Flows.IgnoreQueryFilters().Any(f => f.ID == flow)));

        foreach (var action in new[] { "Details", "Edit", "Delete", "Steps", "Run", "EditStep", "Branches", "Options" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Flows/{action}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Flows/{action}/999999")).StatusCode);
        }
        Assert.Contains("Create", await client.GetStringAsync("/Flows/Create"));
        var bad = await PostTextAsync(client, "/Flows/Create", "/Flows/Create", new() { ["Name"] = "" });
        Assert.Contains("<form", bad);
    }

    [Fact]
    public async Task The_step_and_option_forms_refuse_what_does_not_fit()
    {
        var client = await app.LoggedInAsync("flow-refusals");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var tarot = await app.CreateOracleAsync(client, "Tarot", "TarotCards");
        var flow = await CreateFlowAsync(client, "Refusals");
        var tooLong = new string('l', 101);
        Assert.Contains("A step label cannot be longer than 100 characters.",
            await AddStepAsync(client, flow, "Text", new() { ["AddQuestion"] = "Why?", ["AddLabel"] = tooLong }));
        Assert.Contains("Type the question this step asks.", await AddStepAsync(client, flow, "Choice"));
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString() });
        await AddStepAsync(client, flow, "Choice", new() { ["AddQuestion"] = "Which?" });
        await AddStepAsync(client, flow, "RollChosen", new() { ["AddSourceStepID"] = StepIds(flow)[0].ToString() });
        var (roll, choice, chosen) = (StepIds(flow)[0], StepIds(flow)[1], StepIds(flow)[2]);

        Task<string> Edit(int step, Dictionary<string, string> fields)
        {
            fields["StepID"] = step.ToString();
            return PostTextAsync(client, "/Flows/EditStep", $"/Flows/EditStep/{step}", fields);
        }
        Assert.Contains("A step label cannot be longer than 100 characters.", await Edit(roll, new() { ["Label"] = tooLong, ["OracleID"] = deck.ToString() }));
        Assert.Contains("Type the question this step asks.", await Edit(choice, new() { ["Question"] = " " }));
        await Edit(chosen, new() { ["SourceStepID"] = choice.ToString() });
        Assert.Equal(choice, app.Query(db => db.FlowSteps.IgnoreQueryFilters().Single(s => s.ID == chosen).SourceStepID));

        var entries = app.EntryIds(deck);
        await PostTextAsync(client, "/Flows/Branches", $"/Flows/Branches/{roll}",
            new() { ["StepID"] = roll.ToString(), ["Rows[0].EntryID"] = entries[0].ToString(), ["Rows[0].Target"] = "end" });
        await Edit(roll, new() { ["OracleID"] = tarot.ToString() });
        Assert.Equal(0, app.Query(db => db.FlowBranches.IgnoreQueryFilters().Count(b => b.FlowStepID == roll)));

        var page = $"/Flows/Options/{choice}";
        Assert.Contains("Only a dropdown question has options.", await PostTextAsync(client, "/Flows/Options", page, new() { ["StepID"] = roll.ToString() }));
        Assert.Contains("Only a dropdown question has options.", await PostTextAsync(client, $"/Flows/AddOptionsFromOracles/{roll}", page,
            new() { ["AddOracleIds"] = deck.ToString() }));
        Assert.Contains("That oracle no longer exists.", await PostTextAsync(client, $"/Flows/AddOption/{choice}", page,
            new() { ["AddLabel"] = "Ghost", ["AddOracleID"] = "999999" }));
        Assert.Contains("An option label cannot be longer than 100 characters.", await PostTextAsync(client, $"/Flows/AddOption/{choice}", page,
            new() { ["AddLabel"] = tooLong }));
        await PostTextAsync(client, $"/Flows/AddOption/{choice}", page, new() { ["AddLabel"] = "Only" });
        var option = app.Query(db => db.FlowChoiceOptions.IgnoreQueryFilters().Single(o => o.FlowStepID == choice).ID);

        Task<string> SetOptions(string id, string label, string oracle = "", string target = "") => PostTextAsync(client, "/Flows/Options", page, new()
        {
            ["StepID"] = choice.ToString(), ["Rows[0].OptionID"] = id, ["Rows[0].Label"] = label, ["Rows[0].OracleID"] = oracle, ["Rows[0].Target"] = target,
        });
        Assert.Contains("One of these options no longer exists. Reload the page and try again.", await SetOptions("999999", "Only"));
        Assert.Contains("An option label cannot be longer than 100 characters.", await SetOptions(option.ToString(), tooLong));
        Assert.Contains("That oracle no longer exists.", await SetOptions(option.ToString(), "Only", oracle: "999999"));
        Assert.Contains("One of these routes points at a step that is not in this flow.", await SetOptions(option.ToString(), "Only", target: "999999"));

        Assert.Contains("That option is no longer part of this question.", await PostTextAsync(client, $"/Flows/MoveOption/{choice}", page,
            new() { ["optionId"] = "999999", ["delta"] = "1" }));
        await PostTextAsync(client, $"/Flows/MoveOption/{choice}", page, new() { ["optionId"] = option.ToString(), ["delta"] = "-1" });
        Assert.Equal(1, app.Query(db => db.FlowChoiceOptions.IgnoreQueryFilters().Single(o => o.ID == option).SortOrder));
    }

    [Fact]
    public async Task Removing_a_step_says_how_many_references_to_it_were_cleared()
    {
        var client = await app.LoggedInAsync("flow-remover");
        var flow = await CreateFlowAsync(client, "Orphans");
        await AddStepAsync(client, flow, "Choice", new() { ["AddQuestion"] = "Which deck?" });
        var choice = StepIds(flow)[0];
        await AddStepAsync(client, flow, "RollChosen", new() { ["AddSourceStepID"] = choice.ToString() });
        var removed = await PostTextAsync(client, $"/Flows/RemoveStep/{flow}", $"/Flows/Steps/{flow}", new() { ["stepId"] = choice.ToString() });
        Assert.Contains("Step removed. 1 reference(s) to it have been cleared.", removed);
    }

    [Fact]
    public async Task Removing_a_step_clears_the_branches_and_options_that_went_to_it()
    {
        var client = await app.LoggedInAsync("flow-route-remover");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var flow = await CreateFlowAsync(client, "Routes");
        await AddStepAsync(client, flow, "Choice", new() { ["AddQuestion"] = "Which way?" });
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString() });
        await AddStepAsync(client, flow, "Text", new() { ["AddQuestion"] = "Where to?" });
        var (choice, roll, text) = (StepIds(flow)[0], StepIds(flow)[1], StepIds(flow)[2]);
        await PostTextAsync(client, $"/Flows/AddOption/{choice}", $"/Flows/Options/{choice}",
            new() { ["AddLabel"] = "Onward", ["AddOracleID"] = deck.ToString() });
        var option = app.Query(db => db.FlowChoiceOptions.IgnoreQueryFilters().Single(o => o.FlowStepID == choice).ID);
        await PostTextAsync(client, "/Flows/Options", $"/Flows/Options/{choice}", new()
        {
            ["StepID"] = choice.ToString(),
            ["Rows[0].OptionID"] = option.ToString(), ["Rows[0].Label"] = "Onward", ["Rows[0].Target"] = text.ToString(),
        });
        await PostTextAsync(client, "/Flows/Branches", $"/Flows/Branches/{roll}", new()
        {
            ["StepID"] = roll.ToString(),
            ["Rows[0].EntryID"] = app.EntryIds(deck)[0].ToString(), ["Rows[0].OracleID"] = "", ["Rows[0].Target"] = text.ToString(),
        });

        var removed = await PostTextAsync(client, $"/Flows/RemoveStep/{flow}", $"/Flows/Steps/{flow}", new() { ["stepId"] = text.ToString() });
        Assert.Contains("Step removed. 2 reference(s) to it have been cleared.", removed);
        Assert.Null(app.Query(db => db.FlowChoiceOptions.IgnoreQueryFilters().Single(o => o.ID == option).TargetStepID));
        Assert.False(app.Query(db => db.FlowBranches.IgnoreQueryFilters().Any(b => b.FlowStepID == roll && b.TargetStepID != null)));
    }

    [Fact]
    public async Task A_step_that_rolls_what_another_picks_stays_after_that_step()
    {
        var client = await app.LoggedInAsync("flow-order");
        var deck = await app.CreateOracleAsync(client, "Deck", "PlayingCards");
        var flow = await CreateFlowAsync(client, "Order");
        await AddStepAsync(client, flow, "Choice", new() { ["AddQuestion"] = "Which deck?" });
        await AddStepAsync(client, flow, "RollChosen", new() { ["AddSourceStepID"] = StepIds(flow)[0].ToString() });
        await AddStepAsync(client, flow, "Roll", new() { ["AddOracleID"] = deck.ToString() });
        var (choice, chosen, roll) = (StepIds(flow)[0], StepIds(flow)[1], StepIds(flow)[2]);
        const string order = "A step can only take its oracle from a step that comes before it.";

        Assert.Contains(order, await PostTextAsync(client, $"/Flows/MoveStep/{flow}", $"/Flows/Steps/{flow}",
            new() { ["stepId"] = chosen.ToString(), ["delta"] = "-1" }));
        Assert.Contains(order, await PostTextAsync(client, $"/Flows/MoveStep/{flow}", $"/Flows/Steps/{flow}",
            new() { ["stepId"] = choice.ToString(), ["delta"] = "1" }));
        Assert.Equal([choice, chosen, roll], StepIds(flow));

        Assert.Contains(order, await PostTextAsync(client, "/Flows/EditStep", $"/Flows/EditStep/{chosen}",
            new() { ["StepID"] = chosen.ToString(), ["SourceStepID"] = roll.ToString() }));
        Assert.Equal(choice, app.Query(db => db.FlowSteps.IgnoreQueryFilters().Single(s => s.ID == chosen).SourceStepID));
    }

    [Fact]
    public void A_step_kind_that_does_not_exist_is_refused()
    {
        Assert.Equal("That is not a kind of step.",
            new GM_Toolbox.Services.FlowConfigurator(null!).AddStep(new GM_Toolbox.Models.Flow(), (GM_Toolbox.Models.Enums.FlowStepKind)3, 0, null, null, null));
    }
}
