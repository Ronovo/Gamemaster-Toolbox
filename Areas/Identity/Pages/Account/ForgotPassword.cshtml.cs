using Microsoft.AspNetCore.Authorization;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GM_Toolbox.Areas.Identity.Pages.Account;

// Two steps: the username brings up its security question, then the answer and a new password change it.
[AllowAnonymous]
public class ForgotPasswordModel(SecurityQuestions questions) : PageModel
{
    [BindProperty]
    public string UserName { get; set; } = "";

    [BindProperty]
    public string? Question { get; set; }

    [BindProperty]
    public string? Answer { get; set; }

    [BindProperty]
    public string NewPassword { get; set; } = "";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(UserName))
        {
            ModelState.AddModelError(string.Empty, "Enter your username.");
            return Page();
        }
        Question = await questions.QuestionForAsync(UserName.Trim());
        return Page();
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        ModelState.Clear();
        Question = await questions.QuestionForAsync(UserName.Trim());
        if (await questions.ResetAsync(UserName.Trim(), Answer, NewPassword) is string error)
        {
            ModelState.AddModelError(string.Empty, error);
            return Page();
        }
        return RedirectToPage("./Login", new { reset = true });
    }
}
