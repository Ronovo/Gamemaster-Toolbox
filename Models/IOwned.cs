using System.Security.Claims;
using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// Belongs to the account that made it. Items from before accounts have no owner.
    /// </summary>
    public interface IOwned
    {
        string? OwnerId { get; set; }
        Visibility Visibility { get; set; }
    }

    public static class OwnedExtensions
    {
        // The same rule OwnershipRules enforces on save, for deciding which links to show.
        public static bool CanEdit(this ClaimsPrincipal user, IOwned item) =>
            user.Identity?.IsAuthenticated == true
            && (item.OwnerId == null || item.OwnerId == user.FindFirstValue(ClaimTypes.NameIdentifier));
    }
}
