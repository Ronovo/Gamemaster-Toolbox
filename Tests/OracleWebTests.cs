using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Tests;

public class OracleWebTests(AppFactory app) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task The_flow_and_sheet_lists_filter_by_game_system()
    {
        var client = await app.LoggedInAsync("list-by-system");
        var system = await app.SystemIdAsync(client);
        await AppFactory.AddSystemAsync(client, "Other system");
        var other = app.Query(db => db.GameSystems.IgnoreQueryFilters().Single(g => g.Name == "Other system").ID);
        await client.PostAsync("/Flows/Create", await AppFactory.FormAsync(client, "/Flows/Create", new()
        {
            ["Name"] = "Journey", ["Description"] = "", ["GameSystemID"] = system.ToString(),
        }));
        await AppFactory.UploadSheetAsync(client, system, "ranger.pdf");

        foreach (var (list, item) in new[] { ("/Flows", "Journey"), ("/Sheets", "ranger.pdf") })
        {
            Assert.Contains(item, await client.GetStringAsync($"{list}?gameSystemId={system}"));
            Assert.DoesNotContain(item, await client.GetStringAsync($"{list}?gameSystemId={other}"));
        }
    }

    [Fact]
    public void The_ownership_filter_leaves_other_errors_alone()
    {
        var filter = new GM_Toolbox.Filters.OwnershipExceptionFilter(null!);
        var context = new Microsoft.AspNetCore.Mvc.Filters.ExceptionContext(
            new Microsoft.AspNetCore.Mvc.ActionContext(new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            []) { Exception = new InvalidOperationException("something else") };
        filter.OnException(context);
        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Saving_an_edit_for_a_different_or_missing_oracle_is_not_found()
    {
        var client = await app.LoggedInAsync("oracle-edit-mismatch");
        var oracle = await app.CreateOracleAsync(client, "Mine");
        var system = (await app.SystemIdAsync(client)).ToString();
        Dictionary<string, string> Fields(string prefix, int id) => new()
        {
            [$"{prefix}ID"] = id.ToString(), [$"{prefix}Name"] = "Renamed", [$"{prefix}OracleSubtype"] = "Dice",
            [$"{prefix}GameSystemID"] = system, [$"{prefix}DiceCount"] = "1", [$"{prefix}DiceFaces"] = "6",
        };

        var other = await AppFactory.PostAsync(client, $"/Oracles/Edit/{oracle}", $"/Oracles/Edit/{oracle}", Fields("oracle.", oracle + 1000));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        var missing = await AppFactory.PostAsync(client, "/Oracles/Edit/999999", $"/Oracles/Edit/{oracle}", Fields("", 999999));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Mine", app.Query(db => db.Oracles.IgnoreQueryFilters().Single(o => o.ID == oracle).Name));
    }

    [Fact]
    public async Task The_oracle_list_filters_by_game_system_and_by_dice()
    {
        var client = await app.LoggedInAsync("oracle-list-filters");
        await app.CreateOracleAsync(client, "Two dee six", count: 2, faces: 6);
        await app.CreateOracleAsync(client, "One dee twenty", count: 1, faces: 20);
        await app.CreateOracleAsync(client, "A deck", "PlayingCards");
        var system = await app.SystemIdAsync(client);
        await AppFactory.AddSystemAsync(client, "Empty system");
        var empty = app.Query(db => db.GameSystems.IgnoreQueryFilters().Single(g => g.Name == "Empty system").ID);

        async Task<string> ListAsync(string query) => WebUtility.HtmlDecode(await client.GetStringAsync($"/Oracles?{query}"));
        var twoDeeSix = await ListAsync("oracleType=2d6");
        Assert.Contains("Two dee six", twoDeeSix);
        Assert.DoesNotContain("One dee twenty", twoDeeSix);
        Assert.DoesNotContain("A deck", twoDeeSix);
        var nonsense = await ListAsync("oracleType=lots-of-dice");
        Assert.DoesNotContain("Two dee six", nonsense);
        Assert.DoesNotContain("A deck", nonsense);
        Assert.Contains("A deck", await ListAsync("oracleType=playingcards"));
        Assert.Contains("One dee twenty", await ListAsync($"gameSystemId={system}"));
        Assert.DoesNotContain("One dee twenty", await ListAsync($"gameSystemId={empty}"));
    }

    [Fact]
    public async Task Another_accounts_game_system_is_refused_with_a_message_on_the_page_you_came_from()
    {
        var theirs = await app.SystemIdAsync(await app.LoggedInAsync("system-owner"));
        var client = await app.LoggedInAsync("system-borrower");

        async Task<HttpResponseMessage> BorrowAsync(string? cameFrom)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/Oracles/Create")
            {
                Content = await AppFactory.FormAsync(client, "/Oracles/Create", new()
                {
                    ["Name"] = "Borrowed", ["OracleSubtype"] = "Dice", ["GameSystemID"] = theirs.ToString(),
                    ["DiceCount"] = "1", ["DiceFaces"] = "6",
                }),
            };
            if (cameFrom != null) request.Headers.Referrer = new Uri(cameFrom);
            return await client.SendAsync(request);
        }

        var refused = await BorrowAsync("http://localhost/Oracles/Create?from=picker");
        Assert.Equal("/Oracles/Create?from=picker", refused.RequestMessage!.RequestUri!.PathAndQuery);
        Assert.Contains("You can only use your own game systems, oracles and steps.",
            WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync()));
        Assert.Equal("/", (await BorrowAsync("http://elsewhere.test/Oracles")).RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal("/", (await BorrowAsync(null)).RequestMessage!.RequestUri!.AbsolutePath);
        Assert.False(app.Query(db => db.Oracles.IgnoreQueryFilters().Any(o => o.Name == "Borrowed")));
    }

    private static async Task<HttpResponseMessage> ConfigureAsync(HttpClient client, int id, string count) =>
        await AppFactory.PostAsync(client, $"/Oracles/Configure/{id}", $"/Oracles/Configure/{id}", new() { ["outcomeCount"] = count });

    private static Dictionary<string, string> Rows(int id, IReadOnlyList<int> entryIds, int[] starts, string[] results)
    {
        var fields = new Dictionary<string, string> { ["OracleID"] = id.ToString() };
        for (var i = 0; i < starts.Length; i++)
        {
            fields[$"Rows[{i}].EntryId"] = entryIds[i].ToString();
            fields[$"Rows[{i}].StartsAt"] = starts[i].ToString();
            fields[$"Rows[{i}].Result"] = results[i];
        }
        return fields;
    }

    [Fact]
    public async Task A_dice_oracle_is_set_up_in_steps_then_rolls_on_its_own_ranges()
    {
        var client = await app.LoggedInAsync("dice-maker");
        var id = await app.CreateOracleAsync(client, "Weather");

        var refused = await (await ConfigureAsync(client, id, "7")).Content.ReadAsStringAsync();
        Assert.Contains("Enter a number between 1 and 6.", refused);
        var configured = await ConfigureAsync(client, id, "3");
        Assert.EndsWith($"/Oracles/EditEntries/{id}", configured.RequestMessage!.RequestUri!.AbsolutePath);
        var ids = app.EntryIds(id);
        Assert.Equal(3, ids.Count);

        var bad = await AppFactory.PostAsync(client, "/Oracles/EditEntries", $"/Oracles/EditEntries/{id}",
            Rows(id, ids, [2, 9, 4], ["a", "b", "c"]));
        var badHtml = WebUtility.HtmlDecode(await bad.Content.ReadAsStringAsync());
        Assert.Contains("The first outcome must start at 1.", badHtml);
        Assert.Contains("Row 2: start must be between 1 and 6.", badHtml);
        Assert.Contains("Row 3: start must be greater than the row above.", badHtml);

        var saved = await AppFactory.PostAsync(client, "/Oracles/EditEntries", $"/Oracles/EditEntries/{id}",
            Rows(id, ids, [1, 3, 5], ["Clear skies", "Rain", "Storm"]));
        Assert.EndsWith($"/Oracles/Details/{id}", saved.RequestMessage!.RequestUri!.AbsolutePath);
        var ranges = app.Query(db => db.OracleEntries.IgnoreQueryFilters().Where(e => e.OracleID == id)
            .OrderBy(e => e.SortOrder).Select(e => new { e.RangeStart, e.RangeEnd, e.Result }).ToList());
        Assert.Equal([(1, 2, "Clear skies"), (3, 4, "Rain"), (5, 6, "Storm")],
            ranges.Select(r => (r.RangeStart, r.RangeEnd, r.Result)).ToList());

        var rolled = await AppFactory.PostAsync(client, $"/Oracles/Roll/{id}", $"/Oracles/Roll/{id}", new(), ajax: true);
        var roll = JsonNode.Parse(await rolled.Content.ReadAsStringAsync())!;
        var value = roll["value"]!.GetValue<int>();
        Assert.InRange(value, 1, 6);
        Assert.Equal(value <= 2 ? "Clear skies" : value <= 4 ? "Rain" : "Storm", roll["result"]!.GetValue<string>());

        var page = await AppFactory.PostAsync(client, $"/Oracles/Roll/{id}", $"/Oracles/Roll/{id}", new());
        Assert.EndsWith($"/Oracles/Roll/{id}", page.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Matches("Clear skies|Rain|Storm", await page.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_dice_oracle_with_no_outcomes_says_why_it_cannot_roll()
    {
        var client = await app.LoggedInAsync("dice-empty");
        var id = await app.CreateOracleAsync(client, "Unfinished");
        var json = await AppFactory.PostAsync(client, $"/Oracles/Roll/{id}", $"/Oracles/Roll/{id}", new(), ajax: true);
        Assert.NotNull(JsonNode.Parse(await json.Content.ReadAsStringAsync())!["error"]);
        var page = await AppFactory.PostAsync(client, $"/Oracles/Roll/{id}", $"/Oracles/Roll/{id}", new());
        Assert.Contains("alert", await page.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_card_oracle_gets_a_full_deck_deals_from_it_and_reshuffles()
    {
        var client = await app.LoggedInAsync("card-maker");
        var id = await app.CreateOracleAsync(client, "Fortune", "PlayingCards");
        Assert.Equal(52, app.EntryIds(id).Count);
        Assert.Contains("data-entry-row", await client.GetStringAsync($"/Oracles/EditEntries/{id}"));

        var dealt = JsonNode.Parse(await (await AppFactory.PostAsync(client, $"/Oracles/Roll/{id}", $"/Oracles/Roll/{id}", new(), ajax: true))
            .Content.ReadAsStringAsync())!;
        Assert.False(string.IsNullOrEmpty(dealt["key"]!.GetValue<string>()));
        Assert.Contains("51", await client.GetStringAsync($"/Oracles/Roll/{id}"));

        var reshuffled = await AppFactory.PostAsync(client, $"/Oracles/Reshuffle/{id}", $"/Oracles/Roll/{id}", new(), ajax: true);
        Assert.Equal(52, JsonNode.Parse(await reshuffled.Content.ReadAsStringAsync())!["cardsRemaining"]!.GetValue<int>());
        var back = await AppFactory.PostAsync(client, $"/Oracles/Reshuffle/{id}", $"/Oracles/Roll/{id}", new());
        Assert.EndsWith($"/Oracles/Roll/{id}", back.RequestMessage!.RequestUri!.AbsolutePath);

        var dice = await app.CreateOracleAsync(client, "Not cards");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await AppFactory.PostAsync(client, $"/Oracles/Reshuffle/{dice}", $"/Oracles/Roll/{dice}", new())).StatusCode);
    }

    [Fact]
    public async Task Editing_keeps_the_outcomes_unless_the_roll_changes()
    {
        var client = await app.LoggedInAsync("oracle-editor");
        var id = await app.CreateOracleAsync(client, "Encounters");
        await ConfigureAsync(client, id, "2");
        Assert.Contains("Changing the type or the dice clears", await client.GetStringAsync($"/Oracles/Edit/{id}"));

        var system = (await app.SystemIdAsync(client)).ToString();
        Dictionary<string, string> Fields(string name, string subtype, string faces) => new()
        {
            ["ID"] = id.ToString(), ["Name"] = name, ["OracleSubtype"] = subtype, ["GameSystemID"] = system,
            ["DiceCount"] = "1", ["DiceFaces"] = faces,
        };

        var renamed = await AppFactory.PostAsync(client, $"/Oracles/Edit/{id}", $"/Oracles/Edit/{id}", Fields("Random encounters", "Dice", "6"));
        Assert.Equal("/Oracles", renamed.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal(2, app.EntryIds(id).Count);

        var invalid = await AppFactory.PostAsync(client, $"/Oracles/Edit/{id}", $"/Oracles/Edit/{id}", Fields("", "Dice", "6"));
        Assert.Contains("Edit Oracle", await invalid.Content.ReadAsStringAsync());

        var redone = await AppFactory.PostAsync(client, $"/Oracles/Edit/{id}", $"/Oracles/Edit/{id}", Fields("Random encounters", "Dice", "8"));
        Assert.EndsWith($"/Oracles/Configure/{id}", redone.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Empty(app.EntryIds(id));

        var cards = await AppFactory.PostAsync(client, $"/Oracles/Edit/{id}", $"/Oracles/Edit/{id}", Fields("Random encounters", "TarotCards", "8"));
        Assert.EndsWith($"/Oracles/EditEntries/{id}", cards.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal(78, app.EntryIds(id).Count);

    }

    [Fact]
    public async Task Pages_for_a_missing_oracle_are_not_found()
    {
        var client = await app.LoggedInAsync("oracle-lost");
        foreach (var page in new[] { "Details", "Roll", "Edit", "Delete", "Configure", "EditEntries" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Oracles/{page}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Oracles/{page}/999999")).StatusCode);
        }
    }

    [Fact]
    public async Task The_list_filters_and_answers_with_just_the_table_when_asked()
    {
        var client = await app.LoggedInAsync("oracle-lister");
        await app.CreateOracleAsync(client, "Dungeon rooms");
        await app.CreateOracleAsync(client, "Tavern names");
        var partial = await client.GetStringAsync("/Oracles?name=tavern&oracleType=Dice&partial=1");
        Assert.Contains("Tavern names", partial);
        Assert.DoesNotContain("Dungeon rooms", partial);
        Assert.DoesNotContain("<html", partial);
        Assert.Contains("<html", await client.GetStringAsync("/Oracles"));
    }

    [Fact]
    public async Task Deleting_asks_first_and_then_removes_the_oracle()
    {
        var client = await app.LoggedInAsync("oracle-deleter");
        var id = await app.CreateOracleAsync(client, "Short lived");
        Assert.Contains("Short lived", await client.GetStringAsync($"/Oracles/Delete/{id}"));
        await AppFactory.PostAsync(client, $"/Oracles/Delete/{id}", $"/Oracles/Delete/{id}", new());
        Assert.Empty(app.Query(db => db.Oracles.IgnoreQueryFilters().Where(o => o.ID == id).ToList()));
        var again = await AppFactory.PostAsync(client, $"/Oracles/Delete/{id}", "/Oracles/Create", new());
        Assert.Equal("/Oracles", again.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Export_picks_what_to_download_and_says_when_there_is_nothing()
    {
        var empty = await app.LoggedInAsync("export-nothing");
        Assert.Contains("Nothing to export yet.", await empty.GetStringAsync("/Oracles/Export"));
        await app.SystemIdAsync(empty);
        var nothing = await AppFactory.PostAsync(empty, "/Oracles/Export", "/Oracles/Export", new() { ["Scope"] = "AllOracles" });
        Assert.Contains("Nothing to export.", await nothing.Content.ReadAsStringAsync());

        var client = await app.LoggedInAsync("export-maker");
        var id = await app.CreateOracleAsync(client, "Exported oracle");
        Assert.Contains("Exported oracle", await client.GetStringAsync("/Oracles/Export?partial=oracles"));
        Assert.DoesNotContain("<html", await client.GetStringAsync("/Oracles/Export?partial=flows"));
        Assert.Contains("<html", await client.GetStringAsync("/Oracles/Export?returnTo=/Oracles"));
        Assert.Equal($"[{id}]", await client.GetStringAsync("/Oracles/ExportOracleIds?oracleName=Exported"));
        Assert.Equal("[]", await client.GetStringAsync("/Oracles/ExportFlowIds?flowName=none"));

        foreach (var scope in new[] { "Everything", "AllOracles" })
        {
            var file = await AppFactory.PostAsync(client, "/Oracles/Export", "/Oracles/Export", new() { ["Scope"] = scope });
            Assert.Equal("application/json", file.Content.Headers.ContentType!.MediaType);
            Assert.Contains("Exported oracle", await file.Content.ReadAsStringAsync());
        }
        var picked = await AppFactory.PostAsync(client, "/Oracles/Export", "/Oracles/Export",
            new() { ["Scope"] = "SelectedOracles", ["SelectedIds"] = id.ToString() });
        Assert.Contains("Exported oracle", await picked.Content.ReadAsStringAsync());

        var noOracle = await AppFactory.PostAsync(client, "/Oracles/Export", "/Oracles/Export", new() { ["Scope"] = "SelectedOracles" });
        Assert.Contains("Pick at least one oracle to export.", await noOracle.Content.ReadAsStringAsync());
        var noFlow = await AppFactory.PostAsync(client, "/Oracles/Export", "/Oracles/Export", new() { ["Scope"] = "SelectedFlows" });
        Assert.Contains("Pick at least one flow to export.", await noFlow.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Clear_all_counts_what_goes_and_then_empties_the_account()
    {
        var client = await app.LoggedInAsync("oracle-clearer");
        await app.CreateOracleAsync(client, "Doomed", "PlayingCards");
        Assert.Contains("52", await client.GetStringAsync("/Oracles/ClearAll"));
        await AppFactory.PostAsync(client, "/Oracles/ClearAll", "/Oracles/ClearAll", new());
        Assert.Empty(app.Query(db => db.Oracles.IgnoreQueryFilters().Where(o => o.Name == "Doomed").ToList()));
    }

    [Fact]
    public async Task The_import_page_offers_an_example_file()
    {
        var client = await app.LoggedInAsync("oracle-importer");
        Assert.Contains("type=\"file\"", await client.GetStringAsync("/Oracles/Import?returnTo=/Flows"));
        var example = await client.GetAsync("/Oracles/ExampleFile");
        Assert.Equal("application/json", example.Content.Headers.ContentType!.MediaType);
        Assert.NotNull(JsonNode.Parse(await example.Content.ReadAsStringAsync()));
    }
}
