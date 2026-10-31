using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GM_Toolbox.Areas.Identity.Pages.Account.Manage;

// Replaces the Identity UI page, which has a phone number field this app has no use for.
public class IndexModel(UserManager<IdentityUser> users) : PageModel
{
    public string UserName { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }
        UserName = user.UserName ?? "";
        return Page();
    }
}
