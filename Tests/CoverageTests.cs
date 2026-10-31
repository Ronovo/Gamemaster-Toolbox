using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GM_Toolbox.Tests;

public sealed class CoverageTests(AppFactory app) : IClassFixture<AppFactory>, IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("gm-toolbox-coverage-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private HttpClient Client(string coverageDir) =>
        app.WithWebHostBuilder(builder => builder.UseSetting("CoverageDir", coverageDir))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task The_coverage_reports_are_served_from_their_folder_to_anyone()
    {
        var reports = Path.Combine(_folder, "coverage");
        Directory.CreateDirectory(Path.Combine(reports, "dotnet"));
        File.WriteAllText(Path.Combine(reports, "index.html"), "landing page");
        File.WriteAllText(Path.Combine(reports, "dotnet", "index.html"), "dotnet report");
        File.WriteAllText(Path.Combine(_folder, "secret.txt"), "secret");
        var client = Client(reports);

        var bare = await client.GetAsync("/coverage");
        Assert.Equal(HttpStatusCode.MovedPermanently, bare.StatusCode);
        Assert.EndsWith("/coverage/", bare.Headers.Location!.ToString());
        Assert.Equal("landing page", await client.GetStringAsync("/coverage/"));
        Assert.Equal("dotnet report", await client.GetStringAsync("/coverage/dotnet/"));
        foreach (var path in new[] { "/coverage/js/", "/coverage/../secret.txt", "/coverage/%2e%2e/secret.txt" })
        {
            var response = await client.GetAsync(path);
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual("secret", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Without_a_coverage_folder_the_address_is_like_any_unknown_page()
    {
        var response = await Client(Path.Combine(_folder, "missing")).GetAsync("/coverage/index.html");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Identity/Account/Login", response.Headers.Location!.PathAndQuery);
    }
}
