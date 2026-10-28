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

    public class InputModel
    {
        public string? Question { get; set; }
        public string? Answer { get; set; }
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (SecurityQuestions.Problem(Input.Question, Input.Answer) is string problem)
        {
            ModelState.AddModelError(string.Empty, problem);
            return Page();
        }
        var user = await users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }
        await questions.SetAsync(user, Input.Question!, Input.Answer!);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
