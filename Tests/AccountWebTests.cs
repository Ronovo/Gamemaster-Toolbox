using System.Net;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GM_Toolbox.Tests;

public class AccountWebTests(AppFactory app) : IClassFixture<AppFactory>
{
    private static readonly string Question = SecurityQuestions.All[1];

    private static Dictionary<string, string> Registration(string userName, string password = "Passw0rd!", string? confirm = null,
        string? question = null, string answer = "Rex") => new()
    {
        ["Input.UserName"] = userName, ["Input.Password"] = password, ["Input.ConfirmPassword"] = confirm ?? password,
        ["Input.Question"] = question ?? Question, ["Input.Answer"] = answer,
    };

    private static async Task<string> PostPageAsync(HttpClient client, string url, string tokenPage, Dictionary<string, string> fields) =>
        WebUtility.HtmlDecode(await (await AppFactory.PostAsync(client, url, tokenPage, fields)).Content.ReadAsStringAsync());

    private bool Exists(string userName)
    {
        using var scope = app.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByNameAsync(userName).Result != null;
    }

    [Fact]
    public async Task Registering_checks_every_field_then_signs_in()
    {
        var client = app.CreateClient();
        const string page = "/Identity/Account/Register";
        Assert.Contains("Input.UserName", await client.GetStringAsync(page));
        Assert.Contains("Usernames are 3-20 letters, numbers, _ or -.", await PostPageAsync(client, page, page, Registration("ab")));
        Assert.Contains("The passwords do not match.", await PostPageAsync(client, page, page, Registration("newbie", confirm: "Other1!x")));
        Assert.Contains("Pick a security question.", await PostPageAsync(client, page, page, Registration("newbie", question: "Favourite colour?")));
        Assert.Contains("Answer your security question.", await PostPageAsync(client, page, page, Registration("newbie", answer: "  ")));
        Assert.Contains("Keep the security answer to", await PostPageAsync(client, page, page, Registration("newbie", answer: new string('x', 500))));
        Assert.Contains("Passwords must", await PostPageAsync(client, page, page, Registration("newbie", password: "short")));
        Assert.False(Exists("newbie"));

        var made = await AppFactory.PostAsync(client, $"{page}?returnUrl=/Flows", page, Registration("newbie"));
        Assert.Equal("/Flows", made.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.True(Exists("newbie"));

        var other = app.CreateClient();
        Assert.Contains("is already taken", await PostPageAsync(other, page, page, Registration("newbie")));
        var away = await AppFactory.PostAsync(other, $"{page}?returnUrl=http://evil.test/", page, Registration("second"));
        Assert.Equal("/", away.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task A_forgotten_password_is_reset_with_the_security_answer()
    {
        var setup = app.CreateClient();
        await AppFactory.PostAsync(setup, "/Identity/Account/Register", "/Identity/Account/Register", Registration("forgetful"));
        var client = app.CreateClient();
        const string page = "/Identity/Account/ForgotPassword";
        Assert.Contains("UserName", await client.GetStringAsync(page));
        Assert.Contains("Enter your username.", await PostPageAsync(client, page, page, new() { ["UserName"] = " " }));
        Assert.Contains(Question, await PostPageAsync(client, page, page, new() { ["UserName"] = " forgetful " }));

        var stranger = await PostPageAsync(client, page, page, new() { ["UserName"] = "nobody-here" });
        Assert.Contains(SecurityQuestions.All.First(stranger.Contains), await PostPageAsync(client, page, page, new() { ["UserName"] = "nobody-here" }));

        Assert.Contains("That answer is wrong.", await PostPageAsync(client, $"{page}?handler=Reset", page,
            new() { ["UserName"] = "forgetful", ["Answer"] = "Fido", ["NewPassword"] = "N3wPassw0rd!" }));
        var reset = await AppFactory.PostAsync(client, $"{page}?handler=Reset", page,
            new() { ["UserName"] = "forgetful", ["Answer"] = " REX ", ["NewPassword"] = "N3wPassw0rd!" });
        Assert.Equal("/Identity/Account/Login", reset.RequestMessage!.RequestUri!.AbsolutePath);

        var login = await client.PostAsync("/Identity/Account/Login", await AppFactory.FormAsync(client, "/Identity/Account/Login",
            new() { ["Input.UserName"] = "forgetful", ["Input.Password"] = "N3wPassw0rd!" }));
        Assert.Contains("Hello forgetful!", await login.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Changing_the_security_question_needs_the_current_password_once_one_is_set()
    {
        var client = app.CreateClient();
        await AppFactory.PostAsync(client, "/Identity/Account/Register", "/Identity/Account/Register", Registration("guarded"));
        const string page = "/Identity/Account/SecurityQuestion";
        Assert.Contains("CurrentPassword", await client.GetStringAsync(page));

        Dictionary<string, string> Change(string password, string question) => new()
        {
            ["Input.CurrentPassword"] = password, ["Input.Question"] = question, ["Input.Answer"] = "Tabby",
        };
        Assert.Contains("Your current password is wrong.", await PostPageAsync(client, page, page, Change("nope", SecurityQuestions.All[3])));
        Assert.Contains("Pick a security question.", await PostPageAsync(client, page, page, Change("Passw0rd!", "Made up?")));
        var changed = await AppFactory.PostAsync(client, $"{page}?returnUrl=/Sheets", page, Change("Passw0rd!", SecurityQuestions.All[3]));
        Assert.Equal("/Sheets", changed.RequestMessage!.RequestUri!.AbsolutePath);

        var anonymous = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync(page)).StatusCode);
    }

    [Fact]
    public async Task An_account_without_a_question_sets_one_without_its_password()
    {
        var client = await app.LoggedInAsync("unquestioned");
        const string page = "/Identity/Account/SecurityQuestion";
        var set = await AppFactory.PostAsync(client, page, page, new() { ["Input.Question"] = Question, ["Input.Answer"] = "Rex" });
        Assert.Equal("/", set.RequestMessage!.RequestUri!.AbsolutePath);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await scope.ServiceProvider.GetRequiredService<SecurityQuestions>().HasAsync((await users.FindByNameAsync("unquestioned"))!));
    }
}
