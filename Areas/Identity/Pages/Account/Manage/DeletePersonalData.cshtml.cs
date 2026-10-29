using System.ComponentModel.DataAnnotations;
using GM_Toolbox.Data;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Areas.Identity.Pages.Account.Manage;

// Replaces the Identity UI page so the account's sheet files go with it. Its game systems, oracles, flows and
// sheet rows are deleted by the database, which cascades from the account.
public class DeletePersonalDataModel(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn,
    ApplicationDbContext context, SheetStorageService sheets) : PageModel
{
    [BindProperty, DataType(DataType.Password)]
    public string? Password { get; set; }

    public bool RequirePassword { get; set; }

    [BindProperty]
    public bool Confirm { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }
        RequirePassword = await users.HasPasswordAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }
        RequirePassword = await users.HasPasswordAsync(user);
        if (RequirePassword && !await users.CheckPasswordAsync(user, Password ?? ""))
        {
            ModelState.AddModelError(nameof(Password), "Wrong password.");
            return Page();
        }
        if (!RequirePassword && !Confirm)
        {
            ModelState.AddModelError(string.Empty, "Tick the box to confirm.");
            return Page();
        }

        var sheetIds = await context.Sheets.Select(s => s.ID).ToListAsync();
        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            throw new InvalidOperationException("Could not delete the account.");
        }
        foreach (var id in sheetIds)
        {
            sheets.DeleteSheetFiles(id);
        }

        await signIn.SignOutAsync();
        return Redirect("~/");
    }
}
