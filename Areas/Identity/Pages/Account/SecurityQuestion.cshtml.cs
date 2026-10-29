using GM_Toolbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GM_Toolbox.Areas.Identity.Pages.Account;

[Authorize]
public class SecurityQuestionModel(UserManager<IdentityUser> users, SecurityQuestions questions) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    // Changing an answer that is already set takes the account's password, so a session left open
    // can't swap the answer and then reset the password with it.
    public bool HasQuestion { get; set; }

    // An account made through portfolio login has no password, so it can't have a question: setting one would
    // let whoever has the session give it a password through Forgot password.
    public bool HasPassword { get; set; }

    public class InputModel
    {
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? CurrentPassword { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        var user = await users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }
        HasQuestion = await questions.HasAsync(user);
        HasPassword = await users.HasPasswordAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        var user = await users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }
        HasQuestion = await questions.HasAsync(user);
        HasPassword = await users.HasPasswordAsync(user);
        if (!HasPassword)
        {
            return Page();
        }
        if (HasQuestion && !await users.CheckPasswordAsync(user, Input.CurrentPassword ?? ""))
        {
            ModelState.AddModelError(string.Empty, "Your current password is wrong.");
            return Page();
        }
        if (SecurityQuestions.Problem(Input.Question, Input.Answer) is string problem)
        {
            ModelState.AddModelError(string.Empty, problem);
            return Page();
        }
        await questions.SetAsync(user, Input.Question!, Input.Answer!);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
