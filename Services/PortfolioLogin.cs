using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GM_Toolbox.Services;

// Log in with a portfolio account. /sso/login sends the browser to the portfolio, which posts
// a short-lived RS256 token back to /sso/callback, checked here against PORTFOLIO_SSO_PUBLIC_KEY. Without
// PORTFOLIO_SSO_URL and PORTFOLIO_SSO_PUBLIC_KEY set, portfolio login is off and the app runs on its own.
public class PortfolioLogin
{
    // The provider name Identity stores portfolio links under, in AspNetUserLogins.
    public const string Provider = "Portfolio";
    private const string AppId = "gm-toolbox";
    private const string Issuer = "portfolio";

    private readonly string? _url;
    private readonly RsaSecurityKey? _key;

    public PortfolioLogin(IConfiguration configuration)
    {
        _url = configuration["PORTFOLIO_SSO_URL"]?.Trim().TrimEnd('/');
        var publicKey = configuration["PORTFOLIO_SSO_PUBLIC_KEY"]?.Trim();
        if (string.IsNullOrEmpty(_url) || string.IsNullOrEmpty(publicKey))
        {
            return;
        }
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        _key = new RsaSecurityKey(rsa);
    }

    public bool Enabled => _key != null;

    /// <summary>The portfolio's address. Its /sso/me tells a page which portfolio account the browser is logged in to.</summary>
    public string? Url => Enabled ? _url : null;

    public string AuthorizeUrl(string callbackUrl, string state) =>
        $"{_url}/sso/authorize?app={Uri.EscapeDataString(AppId)}" +
        $"&return={Uri.EscapeDataString(callbackUrl)}&state={Uri.EscapeDataString(state)}";

    /// <summary>The portfolio account a token was issued to for this app, or null.</summary>
    public async Task<(string Sub, string Name)?> VerifyAsync(string? token)
    {
        if (_key == null || string.IsNullOrEmpty(token))
        {
            return null;
        }
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = AppId,
            IssuerSigningKey = _key,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
        });
        if (!result.IsValid || !result.Claims.TryGetValue("sub", out var subClaim) || subClaim is not string sub || sub.Length == 0)
        {
            return null;
        }
        return (sub, result.Claims.TryGetValue("name", out var name) ? name as string ?? "" : "");
    }
}
