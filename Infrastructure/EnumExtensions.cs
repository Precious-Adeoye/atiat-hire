using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AtiatHire.Infrastructure;

public static class EnumExtensions
{
    /// <summary>Friendly label: uses [Display(Name)] when present, otherwise splits PascalCase.</summary>
    public static string Humanize(this Enum value)
    {
        var name = value.ToString();
        var member = value.GetType().GetMember(name).FirstOrDefault();
        var display = member?.GetCustomAttribute<DisplayAttribute>()?.GetName();
        return !string.IsNullOrWhiteSpace(display)
            ? display
            : Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
    }

    /// <summary>CSS-friendly slug, e.g. RequestStatus.Confirmed -> "confirmed".</summary>
    public static string ToCssClass(this Enum value) => value.ToString().ToLowerInvariant();
}
