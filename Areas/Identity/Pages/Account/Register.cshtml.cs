using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GM_Toolbox.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, SecurityQuestions questions) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required, Display(Name = "Username")]
        [RegularExpression("^[A-Za-z0-9_-]{3,20}$", ErrorMessage = "Usernames are 3-20 letters, numbers, _ or -.")]
        public string UserName { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [DataType(DataType.Password), Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";

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
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new IdentityUser { UserName = Input.UserName };
        var created = await users.CreateAsync(user, Input.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
        await questions.SetAsync(user, Input.Question!, Input.Answer!);
        await signIn.SignInAsync(user, isPersistent: false);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
