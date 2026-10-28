using System.Security.Cryptography;
using System.Text;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[Route("sso")]
public class RonovoController : Controller
{
    private const string StateCookie = "gm_toolbox_sso_state";
    private static readonly TimeSpan LoginLifetime = TimeSpan.FromMinutes(10);

    private readonly RonovoLogin _ronovo;
    private readonly UserManager<IdentityUser> _users;
    private readonly SignInManager<IdentityUser> _signIn;

    public RonovoController(RonovoLogin ronovo, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn)
    {
        _ronovo = ronovo;
        _users = users;
        _signIn = signIn;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (!_ronovo.Enabled)
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
        return Redirect(_ronovo.AuthorizeUrl($"{Request.Scheme}://{Request.Host}/sso/callback", state));
    }

    // Posted by the portfolio, so there is no antiforgery token. The signed login token and the state
    // cookie set by Login are what prove it.
    [HttpPost("callback")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Callback([FromForm] string? state, [FromForm] string? token)
    {
        if (!_ronovo.Enabled)
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
        if (await _ronovo.VerifyAsync(token) is not var (sub, name))
        {
            return BadRequest();
        }

        var user = await _users.FindByLoginAsync(RonovoLogin.Provider, sub) ?? await CreateUserAsync(sub, name);
        await _signIn.SignInAsync(user, isPersistent: true);
        return Redirect("/");
    }

    // The account tied to a Ronovo account is created on its first login and named after it. It has no
    // password, so logging in with Ronovo is the only way in.
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
            var linked = await _users.AddLoginAsync(user, new UserLoginInfo(RonovoLogin.Provider, sub, RonovoLogin.Provider));
            if (linked.Succeeded)
            {
                return user;
            }
            // A second login for this Ronovo account got there first.
            await _users.DeleteAsync(user);
            return await _users.FindByLoginAsync(RonovoLogin.Provider, sub)
                ?? throw new InvalidOperationException("Could not create the Ronovo account.");
        }
        throw new InvalidOperationException("Could not find a free username for the Ronovo account.");
    }
}
