using System.Security.Cryptography;
using System.Text;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[Route("sso")]
[AllowAnonymous]
public class PortfolioController : Controller
{
    private const string StateCookie = "gm_toolbox_sso_state";
    private static readonly TimeSpan LoginLifetime = TimeSpan.FromMinutes(10);
    // The first portfolio login here asks whether to link an account the player already has or make a new
    // one, so it waits in the session until they choose.
    private const string PendingSub = "PortfolioPendingSub";
    private const string PendingName = "PortfolioPendingName";
    private const string PendingExpires = "PortfolioPendingExpires";

    private readonly PortfolioLogin _portfolio;
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;

    public PortfolioController(PortfolioLogin portfolio, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn)
    {
        _portfolio = portfolio;
        _users = users;
        _signIn = signIn;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (!_portfolio.Enabled)
        {
            return NotFound();
        }
        var state = RandomNumberGenerator.GetHexString(32);
        Response.Cookies.Append(StateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/sso",
            MaxAge = LoginLifetime,
            Secure = Request.IsHttps,
        });
        return Redirect(_portfolio.AuthorizeUrl($"{Request.Scheme}://{Request.Host}/sso/callback", state));
    }

    // Posted by the portfolio, so there is no antiforgery token. The signed login token and the state
    // cookie set by Login are what prove it.
    [HttpPost("callback")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Callback([FromForm] string? state, [FromForm] string? token)
    {
        if (!_portfolio.Enabled)
        {
            return NotFound();
        }
        var expected = Request.Cookies[StateCookie];
        Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/sso" });
        if (string.IsNullOrEmpty(expected) || state == null ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(state)))
        {
            return BadRequest();
        }
        if (await _portfolio.VerifyAsync(token) is not var (sub, name))
        {
            return BadRequest();
        }

        var user = await _users.FindByLoginAsync(PortfolioLogin.Provider, sub);
        if (user != null)
        {
            await _signIn.SignInAsync(user, isPersistent: true);
            return Redirect("/");
        }
        HttpContext.Session.SetString(PendingSub, sub);
        HttpContext.Session.SetString(PendingName, name);
        HttpContext.Session.SetString(PendingExpires, DateTimeOffset.UtcNow.Add(LoginLifetime).ToUnixTimeSeconds().ToString());
        return RedirectToAction(nameof(Link));
    }

    [HttpGet("link")]
    public IActionResult Link()
    {
        if (Pending() is not var (_, name))
        {
            return LoginAgain();
        }
        return View(new PortfolioLinkModel { Name = name, UserName = name });
    }

    [HttpPost("link")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Link(string? choice, string? userName, string? password)
    {
        if (Pending() is not var (sub, name))
        {
            return LoginAgain();
        }
        if (choice == "cancel")
        {
            ClearPending();
            return Redirect("/Identity/Account/Login");
        }
        if (choice == "new")
        {
            ClearPending();
            await _signIn.SignInAsync(await CreateUserAsync(sub, name), isPersistent: true);
            return Redirect("/");
        }

        var model = new PortfolioLinkModel { Name = name, UserName = userName ?? "" };
        var user = string.IsNullOrEmpty(userName) ? null : await _users.FindByNameAsync(userName);
        var check = user == null ? Microsoft.AspNetCore.Identity.SignInResult.Failed : await _signIn.CheckPasswordSignInAsync(user, password ?? "", lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            model.Error = "Too many wrong passwords. Try again later.";
        }
        else if (!check.Succeeded)
        {
            model.Error = "Wrong username or password.";
        }
        else if ((await _users.GetLoginsAsync(user!)).Any(login => login.LoginProvider == PortfolioLogin.Provider))
        {
            model.Error = "That account is already linked to another portfolio account.";
        }
        else if (!(await _users.AddLoginAsync(user!, new UserLoginInfo(PortfolioLogin.Provider, sub, PortfolioLogin.Provider))).Succeeded)
        {
            model.Error = "Your portfolio account is already linked to another account here.";
        }
        if (model.Error != null)
        {
            return View(model);
        }

        ClearPending();
        await _signIn.SignInAsync(user!, isPersistent: true);
        return Redirect("/");
    }

    private (string Sub, string Name)? Pending()
    {
        var sub = HttpContext.Session.GetString(PendingSub);
        var expires = long.TryParse(HttpContext.Session.GetString(PendingExpires), out var seconds) ? seconds : 0;
        if (string.IsNullOrEmpty(sub) || expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return null;
        }
        return (sub, HttpContext.Session.GetString(PendingName) ?? "");
    }

    private void ClearPending()
    {
        HttpContext.Session.Remove(PendingSub);
        HttpContext.Session.Remove(PendingName);
        HttpContext.Session.Remove(PendingExpires);
    }

    private IActionResult LoginAgain()
    {
        ClearPending();
        return Redirect("/Identity/Account/Login");
    }

    // A new account linked to a portfolio account and named after it. It has no password, so logging in
    // with the portfolio account is the only way in.
    private async Task<IdentityUser> CreateUserAsync(string sub, string name)
    {
        var baseName = System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_-]{3,20}$") ? name : "player";
        for (var n = 1; n < 1000; n++)
        {
            var userName = n == 1 ? baseName : $"{baseName[..Math.Min(baseName.Length, 20 - n.ToString().Length)]}{n}";
            if (await _users.FindByNameAsync(userName) != null)
            {
                continue;
            }
            var user = new IdentityUser { UserName = userName };
            var created = await _users.CreateAsync(user);
            if (!created.Succeeded)
            {
                continue;
            }
            var linked = await _users.AddLoginAsync(user, new UserLoginInfo(PortfolioLogin.Provider, sub, PortfolioLogin.Provider));
            if (linked.Succeeded)
            {
                return user;
            }
            // A second login for this portfolio account got there first.
            await _users.DeleteAsync(user);
            return await _users.FindByLoginAsync(PortfolioLogin.Provider, sub)
                ?? throw new InvalidOperationException("Could not create the account.");
        }
        throw new InvalidOperationException("Could not find a free username for the portfolio account.");
    }
}

public class PortfolioLinkModel
{
    public string Name { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? Error { get; set; }
}
