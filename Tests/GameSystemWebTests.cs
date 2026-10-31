using System.Net;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Tests;

public class GameSystemWebTests(AppFactory app) : IClassFixture<AppFactory>
{
    private int SystemId(string name) =>
        app.Query(db => db.GameSystems.IgnoreQueryFilters().Where(g => g.Name == name).Select(g => g.ID).Single());

    private static async Task<string> PostPageAsync(HttpClient client, string url, string tokenPage, Dictionary<string, string> fields) =>
        WebUtility.HtmlDecode(await (await AppFactory.PostAsync(client, url, tokenPage, fields)).Content.ReadAsStringAsync());

    [Fact]
    public async Task Names_are_unique_per_account_when_adding_or_renaming()
    {
        var client = await app.LoggedInAsync("systems-namer");
        Assert.Contains("<html", await client.GetStringAsync("/GameSystems"));
        await AppFactory.AddSystemAsync(client, "Mothership");
        await AppFactory.AddSystemAsync(client, "Cairn");
        Assert.Contains("A game system with that name already exists.",
            await PostPageAsync(client, "/GameSystems/Create", "/GameSystems/Create", new() { ["Name"] = "Cairn", ["Genre"] = "Fantasy" }));

        var cairn = SystemId("Cairn");
        Assert.Contains("Cairn", await client.GetStringAsync($"/GameSystems/Edit/{cairn}"));
        Assert.Contains("A game system with that name already exists.", await PostPageAsync(client, $"/GameSystems/Edit/{cairn}",
            $"/GameSystems/Edit/{cairn}", new() { ["ID"] = cairn.ToString(), ["Name"] = "Mothership", ["Genre"] = "Fantasy" }));
        await PostPageAsync(client, $"/GameSystems/Edit/{cairn}", $"/GameSystems/Edit/{cairn}",
            new() { ["ID"] = cairn.ToString(), ["Name"] = "Cairn 2e", ["Genre"] = "Fantasy" });
        Assert.Equal("Cairn 2e", app.Query(db => db.GameSystems.IgnoreQueryFilters().Single(g => g.ID == cairn).Name));

        var partial = await client.GetStringAsync("/GameSystems?name=cairn&genre=Fantasy&partial=1");
        Assert.Contains("Cairn 2e", partial);
        Assert.DoesNotContain("Mothership", partial);

        var elsewhere = await app.LoggedInAsync("systems-elsewhere");
        Assert.DoesNotContain("data-live-filter", await elsewhere.GetStringAsync("/GameSystems"));
        await AppFactory.AddSystemAsync(elsewhere, "Mothership");
        Assert.Equal(2, app.Query(db => db.GameSystems.IgnoreQueryFilters().Count(g => g.Name == "Mothership")));
    }

    [Fact]
    public async Task A_game_system_in_use_is_kept_and_an_unused_one_is_deleted()
    {
        var client = await app.LoggedInAsync("systems-deleter");
        var used = await app.SystemIdAsync(client);
        await app.CreateOracleAsync(client, "Holds the system");
        var page = await client.GetStringAsync($"/GameSystems/Delete/{used}");
        Assert.Contains("1", page);
        await PostPageAsync(client, $"/GameSystems/Delete/{used}", $"/GameSystems/Delete/{used}", new());
        Assert.True(app.Query(db => db.GameSystems.IgnoreQueryFilters().Any(g => g.ID == used)));

        await AppFactory.AddSystemAsync(client, "Spare");
        var spare = SystemId("Spare");
        await PostPageAsync(client, $"/GameSystems/Delete/{spare}", $"/GameSystems/Delete/{spare}", new());
        Assert.False(app.Query(db => db.GameSystems.IgnoreQueryFilters().Any(g => g.ID == spare)));
        var again = await AppFactory.PostAsync(client, $"/GameSystems/Delete/{spare}", "/GameSystems/Create", new());
        Assert.Equal("/GameSystems", again.RequestMessage!.RequestUri!.AbsolutePath);

        foreach (var action in new[] { "Edit", "Delete" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/GameSystems/{action}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/GameSystems/{action}/999999")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await AppFactory.PostAsync(client, "/GameSystems/Edit/999999", "/GameSystems/Create",
            new() { ["ID"] = "999999", ["Name"] = "Ghost", ["Genre"] = "Horror" })).StatusCode);
    }
}
