using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GM_Toolbox.Tests;

public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly string _folder = Directory.CreateTempSubdirectory("gm-toolbox-tests-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "GM Toolbox.csproj")))
        {
            repo = Path.GetDirectoryName(repo)!;
        }
        builder.UseContentRoot(repo);
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(_folder, "test.db")};Pooling=False");
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

        var page = "/Identity/Account/Manage/DeletePersonalData";
        Assert.Contains("Delete my account", await dave.GetStringAsync(page));
        var deleted = await dave.PostAsync(page, await AppFactory.FormAsync(dave, page, new() { ["Password"] = "Passw0rd!" }));

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.DoesNotContain("Hello dave!", await deleted.Content.ReadAsStringAsync());
        using var scope = app.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByNameAsync("dave"));
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
