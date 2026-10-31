using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GM_Toolbox.Tests;

public sealed class PortfolioAppFactory : AppFactory
{
    public static readonly RSA Key = RSA.Create(2048);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("PORTFOLIO_SSO_URL", "http://portfolio.test");
        builder.UseSetting("PORTFOLIO_SSO_PUBLIC_KEY", Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo()));
    }
}

public class PortfolioWebTests(PortfolioAppFactory app, AppFactory plain) : IClassFixture<PortfolioAppFactory>, IClassFixture<AppFactory>
{
    // A login is refused as a replay when the same person's token carries the same issue time (in whole seconds), so
    // each token here is issued a second before the last, counted back from a fixed start rather than from now.
    private static readonly DateTime Start = DateTime.UtcNow;
    private static int _issued;

    private static string Token(string sub, string name) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "portfolio",
        Audience = "gm-toolbox",
        Subject = new ClaimsIdentity([new Claim("sub", sub), new Claim("name", name)]),
        IssuedAt = Start.AddSeconds(-Interlocked.Increment(ref _issued)),
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(new RsaSecurityKey(PortfolioAppFactory.Key), SecurityAlgorithms.RsaSha256),
    });

    private HttpClient Browser() => app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> StartAsync(HttpClient browser)
    {
        var login = await browser.GetAsync("/sso/login");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var location = login.Headers.Location!;
        Assert.StartsWith("http://portfolio.test/sso/authorize?", location.ToString());
        return System.Web.HttpUtility.ParseQueryString(location.Query)["state"]!;
    }

    private static Task<HttpResponseMessage> CallbackAsync(HttpClient browser, string? state, string token) =>
        browser.PostAsync("/sso/callback", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["state"] = state ?? "", ["token"] = token,
        }));

    private static async Task<HttpResponseMessage> PortfolioLoginAsync(HttpClient browser, string sub, string name) =>
        await CallbackAsync(browser, await StartAsync(browser), Token(sub, name));

    private static async Task<HttpResponseMessage> LinkAsync(HttpClient browser, Dictionary<string, string> fields) =>
        await browser.PostAsync("/sso/link", await AppFactory.FormAsync(browser, "/sso/link", fields));

    private async Task CreateUserAsync(string userName, string password = "Passw0rd!")
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await users.CreateAsync(new IdentityUser(userName), password)).Succeeded);
    }

    private async Task<bool> SignedInAsync(HttpClient browser) =>
        (await browser.GetAsync("/Oracles")).StatusCode == HttpStatusCode.OK;

    [Fact]
    public async Task Portfolio_login_is_not_there_when_it_is_not_set_up()
    {
        var browser = plain.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/sso/login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CallbackAsync(browser, "x", "y")).StatusCode);
    }

    [Fact]
    public async Task The_callback_refuses_a_wrong_state_a_missing_cookie_and_a_bad_token()
    {
        var browser = Browser();
        await StartAsync(browser);
        Assert.Equal(HttpStatusCode.BadRequest, (await CallbackAsync(browser, "wrong", Token("1", "one"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CallbackAsync(Browser(), null, Token("1", "one"))).StatusCode);
        var state = await StartAsync(browser);
        Assert.Equal(HttpStatusCode.BadRequest, (await CallbackAsync(browser, state, "not-a-token")).StatusCode);
    }

    [Fact]
    public async Task A_first_portfolio_login_can_make_a_new_account_that_later_logins_reuse()
    {
        var browser = Browser();
        var first = await PortfolioLoginAsync(browser, "100", "Ronovo");
        Assert.Equal("/sso/link", first.Headers.Location!.ToString());
        Assert.Contains("Ronovo", await browser.GetStringAsync("/sso/link"));
        var made = await LinkAsync(browser, new() { ["choice"] = "new" });
        Assert.Equal("/", made.Headers.Location!.ToString());
        Assert.True(await SignedInAsync(browser));

        var again = Browser();
        var back = await PortfolioLoginAsync(again, "100", "Ronovo");
        Assert.Equal("/", back.Headers.Location!.ToString());
        Assert.True(await SignedInAsync(again));
    }

    [Fact]
    public async Task A_new_account_gets_a_free_name_based_on_the_portfolio_one()
    {
        await CreateUserAsync("Taken");
        foreach (var (sub, name) in new[] { ("200", "Taken"), ("201", "not a valid name!") })
        {
            var browser = Browser();
            await PortfolioLoginAsync(browser, sub, name);
            await LinkAsync(browser, new() { ["choice"] = "new" });
        }
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.Equal("Taken2", (await users.FindByLoginAsync("Portfolio", "200"))?.UserName);
        Assert.Equal("player", (await users.FindByLoginAsync("Portfolio", "201"))?.UserName);
    }

    [Fact]
    public async Task An_existing_account_is_linked_with_its_password()
    {
        await CreateUserAsync("linker");
        var browser = Browser();
        await PortfolioLoginAsync(browser, "300", "Linker");
        var wrong = await LinkAsync(browser, new() { ["choice"] = "link", ["userName"] = "linker", ["password"] = "nope" });
        Assert.Contains("Wrong username or password.", await wrong.Content.ReadAsStringAsync());
        var nobody = await LinkAsync(browser, new() { ["choice"] = "link", ["userName"] = "", ["password"] = "x" });
        Assert.Contains("Wrong username or password.", await nobody.Content.ReadAsStringAsync());
        var linked = await LinkAsync(browser, new() { ["choice"] = "link", ["userName"] = "linker", ["password"] = "Passw0rd!" });
        Assert.Equal("/", linked.Headers.Location!.ToString());

        var other = Browser();
        await PortfolioLoginAsync(other, "301", "Someone");
        var twice = await LinkAsync(other, new() { ["choice"] = "link", ["userName"] = "linker", ["password"] = "Passw0rd!" });
        Assert.Contains("That account is already linked to another portfolio account.", WebUtility.HtmlDecode(await twice.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task A_portfolio_account_linked_meanwhile_elsewhere_is_not_linked_twice()
    {
        await CreateUserAsync("first-home");
        await CreateUserAsync("second-home");
        var one = Browser();
        var two = Browser();
        await PortfolioLoginAsync(one, "400", "Racer");
        await PortfolioLoginAsync(two, "400", "Racer");
        await LinkAsync(one, new() { ["choice"] = "link", ["userName"] = "first-home", ["password"] = "Passw0rd!" });
        var late = await LinkAsync(two, new() { ["choice"] = "link", ["userName"] = "second-home", ["password"] = "Passw0rd!" });
        Assert.Contains("Your portfolio account is already linked to another account here.", await late.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Too_many_wrong_passwords_lock_the_account()
    {
        await CreateUserAsync("lockme");
        var browser = Browser();
        await PortfolioLoginAsync(browser, "500", "Locker");
        string page = "";
        for (var i = 0; i < 6; i++)
        {
            page = await (await LinkAsync(browser, new() { ["choice"] = "link", ["userName"] = "lockme", ["password"] = "wrong" }))
                .Content.ReadAsStringAsync();
        }
        Assert.Contains("Too many wrong passwords. Try again later.", page);
    }

    [Fact]
    public async Task Cancelling_or_arriving_without_a_portfolio_login_goes_back_to_log_in()
    {
        var browser = Browser();
        Assert.Equal("/Identity/Account/Login", (await browser.GetAsync("/sso/link")).Headers.Location!.ToString());
        await PortfolioLoginAsync(browser, "600", "Quitter");
        var cancelled = await LinkAsync(browser, new() { ["choice"] = "cancel" });
        Assert.Equal("/Identity/Account/Login", cancelled.Headers.Location!.ToString());
        var after = await browser.PostAsync("/sso/link", await AppFactory.FormAsync(browser, "/Identity/Account/Login", new() { ["choice"] = "new" }));
        Assert.Equal("/Identity/Account/Login", after.Headers.Location!.ToString());
    }
}
