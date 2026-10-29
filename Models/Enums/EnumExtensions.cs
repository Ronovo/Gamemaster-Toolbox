using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GM_Toolbox.Models.Enums
{
    public static class EnumExtensions
    {
        /// <summary>The value's [Display] name, or its own name when it has none.</summary>
        public static string DisplayName(this Enum value)
        {
            return value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name
                ?? value.ToString();
        }
    }
}
