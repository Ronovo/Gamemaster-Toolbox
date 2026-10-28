using System.ComponentModel.DataAnnotations;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GM_Toolbox.Areas.Identity.Pages.Account;

public class LoginModel(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, SecurityQuestions questions) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public bool WasReset { get; set; }

    // Accounts made before usernames log in with the email address they registered with.
    public class InputModel
    {
        [Required, Display(Name = "Username")]
        public string UserName { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null, bool reset = false)
    {
        ReturnUrl = returnUrl;
        WasReset = reset;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signIn.PasswordSignInAsync(Input.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, Input.RememberMe });
        }
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Too many wrong passwords. Try again later.");
            return Page();
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Wrong username or password.");
            return Page();
        }

        // Accounts made before security questions set one straight after logging in.
        var user = await users.FindByNameAsync(Input.UserName);
        if (user != null && !await questions.HasAsync(user))
        {
            return RedirectToPage("./SecurityQuestion", new { returnUrl });
        }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
