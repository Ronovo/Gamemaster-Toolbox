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
    private const string LoginPage = "/Identity/Account/Login";

    private static string Token(string sub, string name, DateTime? issuedAt = null) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "portfolio",
        Audience = "gm-toolbox",
        Subject = new ClaimsIdentity([new Claim("sub", sub), new Claim("name", name), new Claim("jti", Guid.NewGuid().ToString("N"))]),
        IssuedAt = issuedAt,
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(new RsaSecurityKey(PortfolioAppFactory.Key), SecurityAlgorithms.RsaSha256),
    });

    private HttpClient Browser() => app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> StartAsync(HttpClient browser, string? returnUrl = null)
    {
        var login = await browser.GetAsync(returnUrl == null ? "/sso/login" : $"/sso/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
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

    private static async Task AssertRefusedAsync(HttpResponseMessage callback, HttpClient browser, string message)
    {
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(LoginPage, callback.Headers.Location!.ToString());
        Assert.Contains(message, WebUtility.HtmlDecode(await browser.GetStringAsync(LoginPage)));
        Assert.DoesNotContain(message, WebUtility.HtmlDecode(await browser.GetStringAsync(LoginPage)));
    }

    [Fact]
    public async Task The_callback_sends_a_wrong_state_a_missing_cookie_and_a_bad_token_back_to_log_in()
    {
        const string expired = "Your portfolio login expired. Try again.";
        var browser = Browser();
        await StartAsync(browser);
        await AssertRefusedAsync(await CallbackAsync(browser, "wrong", Token("1", "one")), browser, expired);
        var fresh = Browser();
        await AssertRefusedAsync(await CallbackAsync(fresh, null, Token("1", "one")), fresh, expired);
        var state = await StartAsync(browser);
        await AssertRefusedAsync(await CallbackAsync(browser, state, "not-a-token"), browser, "That portfolio login didn't check out. Try again.");
    }

    [Fact]
    public async Task A_portfolio_login_goes_back_to_the_page_it_started_from()
    {
        var browser = Browser();
        Assert.Contains("href=\"/sso/login?returnUrl=%2FSheets\"", await browser.GetStringAsync($"{LoginPage}?returnUrl=/Sheets"));
        await CallbackAsync(browser, await StartAsync(browser, "/Sheets"), Token("700", "Returner"));
        var made = await LinkAsync(browser, new() { ["choice"] = "new" });
        Assert.Equal("/Sheets", made.Headers.Location!.ToString());
        Assert.True(await SignedInAsync(browser));

        var again = Browser();
        var back = await CallbackAsync(again, await StartAsync(again, "/Flows"), Token("700", "Returner"));
        Assert.Equal("/Flows", back.Headers.Location!.ToString());

        var away = Browser();
        var home = await CallbackAsync(away, await StartAsync(away, "http://evil.test/"), Token("700", "Returner"));
        Assert.Equal("/", home.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Two_tokens_issued_in_the_same_second_both_log_in()
    {
        var issuedAt = DateTime.UtcNow;
        foreach (var browser in new[] { Browser(), Browser() })
        {
            var callback = await CallbackAsync(browser, await StartAsync(browser), Token("800", "Twin", issuedAt));
            Assert.Equal("/sso/link", callback.Headers.Location!.ToString());
        }
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
