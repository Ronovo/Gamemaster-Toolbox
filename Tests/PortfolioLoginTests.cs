using System.Security.Claims;
using System.Security.Cryptography;
using GM_Toolbox.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GM_Toolbox.Tests;

public class PortfolioLoginTests
{
    private readonly RSA _key = RSA.Create(2048);

    private PortfolioLogin Login() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["PORTFOLIO_SSO_URL"] = "http://localhost:4482",
        ["PORTFOLIO_SSO_PUBLIC_KEY"] = Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo())
    }).Build(), NullLogger<PortfolioLogin>.Instance);

    private string Token(string audience = "gm-toolbox") => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "portfolio",
        Audience = audience,
        Subject = new ClaimsIdentity([new Claim("sub", "7"), new Claim("name", "player7")]),
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(new RsaSecurityKey(_key), SecurityAlgorithms.RsaSha256)
    });

    [Fact]
    public async Task A_token_logs_in_once()
    {
        var login = Login();
        var token = Token();

        Assert.Equal(("7", "player7"), await login.VerifyAsync(token));
        Assert.Null(await login.VerifyAsync(token));
    }

    [Fact]
    public async Task A_token_for_another_app_is_refused()
    {
        Assert.Null(await Login().VerifyAsync(Token("casino")));
    }
}
