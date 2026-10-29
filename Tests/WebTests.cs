using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GM_Toolbox.Data;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GM_Toolbox.Tests;

public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly string _folder = Directory.CreateTempSubdirectory("gm-toolbox-tests-").FullName;

    public string SheetsRoot => Path.Combine(_folder, "Sheets");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "GM Toolbox.csproj")))
        {
            repo = Path.GetDirectoryName(repo)!;
        }
        builder.UseContentRoot(repo);
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(_folder, "test.db")};Pooling=False");
        builder.UseSetting("SheetsRoot", SheetsRoot);
    }

    public T Query<T>(Func<ApplicationDbContext, T> read)
    {
        using var scope = Services.CreateScope();
        return read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public static async Task<HttpResponseMessage> AddSystemAsync(HttpClient client, string name) =>
        await client.PostAsync("/GameSystems/Create",
            await FormAsync(client, "/GameSystems/Create", new() { ["Name"] = name, ["Genre"] = "Horror" }));

    public static async Task<HttpResponseMessage> UploadSheetAsync(HttpClient client, int? gameSystemId)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(await TokenAsync(client, "/Sheets/Upload")), "__RequestVerificationToken" },
            { new StringContent(gameSystemId?.ToString() ?? ""), "gameSystemId" }
        };
        var content = new ByteArrayContent("%PDF-1.4 test sheet"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "file", "hero.pdf");
        return await client.PostAsync("/Sheets/Upload", form);
    }

    public async Task<HttpClient> LoggedInAsync(string userName)
    {
        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.CreateAsync(new IdentityUser(userName), "Passw0rd!")).Succeeded);
        }

        var client = CreateClient();
        var login = await client.PostAsync("/Identity/Account/Login", await FormAsync(client, "/Identity/Account/Login",
            new() { ["Input.UserName"] = userName, ["Input.Password"] = "Passw0rd!" }));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains($"Hello {userName}!", await login.Content.ReadAsStringAsync());
        return client;
    }

    public static async Task<string> TokenAsync(HttpClient client, string page)
    {
        var html = await client.GetStringAsync(page);
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    public static async Task<FormUrlEncodedContent> FormAsync(HttpClient client, string page, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await TokenAsync(client, page);
        return new FormUrlEncodedContent(fields);
    }

    public static async Task<HttpResponseMessage> ImportAsync(HttpClient client, byte[] file)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(await TokenAsync(client, "/Oracles/Import")), "__RequestVerificationToken" }
        };
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(content, "file", "upload.json");
        return await client.PostAsync("/Oracles/Import", form);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}

public class WebTests(AppFactory app) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task About_and_login_open_without_logging_in()
    {
        var client = app.CreateClient();

        Assert.Contains("A private workbench", await client.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Identity/Account/Login")).StatusCode);
    }

    [Theory]
    [InlineData("/Oracles")]
    [InlineData("/Flows")]
    [InlineData("/GameSystems")]
    [InlineData("/Sheets")]
    [InlineData("/Oracles/Export")]
    public async Task Everything_else_sends_visitors_to_login(string page)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(page);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Identity/Account/Login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task An_uploaded_file_imports_exports_as_a_download_and_stays_private()
    {
        var alice = await app.LoggedInAsync("alice");
        var example = await alice.GetByteArrayAsync("/Oracles/ExampleFile");

        var imported = await AppFactory.ImportAsync(alice, example);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Contains("Imported", await imported.Content.ReadAsStringAsync());

        var export = await alice.PostAsync("/Oracles/Export",
            await AppFactory.FormAsync(alice, "/Oracles/Export", new() { ["Scope"] = "Everything" }));
        Assert.Equal("application/json", export.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", export.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains("\"oracles\"", await export.Content.ReadAsStringAsync());

        var bob = await app.LoggedInAsync("bob");
        Assert.Contains("No oracles yet", await bob.GetStringAsync("/Oracles"));
        Assert.Contains("No flows yet", await bob.GetStringAsync("/Flows"));
    }

    [Fact]
    public async Task Deleting_your_account_logs_you_out_and_takes_your_items()
    {
        var dave = await app.LoggedInAsync("dave");
        await AppFactory.ImportAsync(dave, await dave.GetByteArrayAsync("/Oracles/ExampleFile"));
        await AppFactory.UploadSheetAsync(dave, null);
        var daveId = app.Query(db => db.Users.Single(u => u.UserName == "dave").Id);
        var sheetId = app.Query(db => db.Sheets.IgnoreQueryFilters().Single(s => s.OwnerId == daveId).ID);
        Assert.True(Directory.Exists(Path.Combine(app.SheetsRoot, sheetId.ToString())));

        var page = "/Identity/Account/Manage/DeletePersonalData";
        Assert.Contains("Delete my account", await dave.GetStringAsync(page));
        var deleted = await dave.PostAsync(page, await AppFactory.FormAsync(dave, page, new() { ["Password"] = "Passw0rd!" }));

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.DoesNotContain("Hello dave!", await deleted.Content.ReadAsStringAsync());
        Assert.Null(app.Query(db => db.Users.SingleOrDefault(u => u.UserName == "dave")));
        Assert.False(app.Query(db => db.Oracles.IgnoreQueryFilters().Any(o => o.OwnerId == daveId)));
        Assert.False(app.Query(db => db.Sheets.IgnoreQueryFilters().Any(s => s.OwnerId == daveId)));
        Assert.False(Directory.Exists(Path.Combine(app.SheetsRoot, sheetId.ToString())));
    }

    [Fact]
    public async Task Another_accounts_items_are_not_found()
    {
        var alice = await app.LoggedInAsync("hana");
        await AppFactory.ImportAsync(alice, await alice.GetByteArrayAsync("/Oracles/ExampleFile"));
        await AppFactory.UploadSheetAsync(alice, null);
        var aliceId = app.Query(db => db.Users.Single(u => u.UserName == "hana").Id);
        var oracleId = app.Query(db => db.Oracles.IgnoreQueryFilters().First(o => o.OwnerId == aliceId).ID);
        var stepId = app.Query(db => db.FlowSteps.IgnoreQueryFilters().First(s => s.Flow!.OwnerId == aliceId).ID);
        var sheetId = app.Query(db => db.Sheets.IgnoreQueryFilters().First(s => s.OwnerId == aliceId).ID);

        var bob = await app.LoggedInAsync("ivan");
        foreach (var page in new[] { $"/Oracles/Details/{oracleId}", $"/Oracles/Edit/{oracleId}", $"/Flows/EditStep/{stepId}", $"/Sheets/Download/{sheetId}" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(page)).StatusCode);
        }
    }

    [Fact]
    public async Task A_flow_can_branch_on_an_oracle_you_already_have()
    {
        var jack = await app.LoggedInAsync("jack");
        var example = await jack.GetByteArrayAsync("/Oracles/ExampleFile");
        await AppFactory.ImportAsync(jack, example);

        var flowsOnly = JsonNode.Parse(Encoding.UTF8.GetString(example).TrimStart('﻿'))!;
        flowsOnly["oracles"] = new JsonArray();
        flowsOnly["gameSystems"] = new JsonArray();
        var response = await AppFactory.ImportAsync(jack, Encoding.UTF8.GetBytes(flowsOnly.ToJsonString()));

        Assert.Contains("Imported", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_game_system_used_by_a_sheet_is_not_deleted()
    {
        var kate = await app.LoggedInAsync("kate");
        await AppFactory.AddSystemAsync(kate, "Mothership");
        var systemId = app.Query(db => db.GameSystems.IgnoreQueryFilters().Single(g => g.Name == "Mothership").ID);
        await AppFactory.UploadSheetAsync(kate, systemId);

        var page = $"/GameSystems/Delete/{systemId}";
        var response = await kate.PostAsync(page, await AppFactory.FormAsync(kate, "/GameSystems/Create", new() { ["ID"] = systemId.ToString() }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("1 sheet(s)", await response.Content.ReadAsStringAsync());
        Assert.True(app.Query(db => db.GameSystems.IgnoreQueryFilters().Any(g => g.ID == systemId)));
    }

    [Fact]
    public async Task An_account_with_no_password_cannot_set_a_security_question()
    {
        var lena = await app.LoggedInAsync("lena");
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            await users.RemovePasswordAsync((await users.FindByNameAsync("lena"))!);
        }

        Assert.Contains("has no password here", await lena.GetStringAsync("/Identity/Account/SecurityQuestion"));
    }

    [Fact]
    public async Task Wrong_answers_lock_password_resets()
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var questions = scope.ServiceProvider.GetRequiredService<SecurityQuestions>();
        var mona = new IdentityUser("mona");
        await users.CreateAsync(mona, "Passw0rd!");
        await questions.SetAsync(mona, SecurityQuestions.All[0], "blue");

        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull(await questions.ResetAsync("mona", "red", "NewPassw0rd!"));
        }
        Assert.NotNull(await questions.ResetAsync("mona", "blue", "NewPassw0rd!"));
        Assert.True(await users.CheckPasswordAsync((await users.FindByNameAsync("mona"))!, "Passw0rd!"));
    }

    [Fact]
    public async Task A_file_that_is_not_json_shows_an_error()
    {
        var carol = await app.LoggedInAsync("carol");

        var response = await AppFactory.ImportAsync(carol, Encoding.UTF8.GetBytes("not json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not valid JSON", await response.Content.ReadAsStringAsync());
    }
}
