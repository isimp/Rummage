using System.Text.RegularExpressions;

namespace Rummage.Core;

/// <summary>Names as Rummage searches, completes and shows them.</summary>
internal static class Names
{
    // A rich-text tag: <color=#CBE86A>, </color>, <b>, <size=120%>, or the short colour form <#FF0000>.
    // A "<" not followed by a letter, a slash or a "#" colour, as in "<3", is text.
    private static readonly Regex Tag = new(@"<(?:/?[A-Za-z][A-Za-z0-9-]*(?:=[^<>]*)?|#[0-9A-Fa-f]{3,8})>", RegexOptions.CultureInvariant);

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);

    /// <summary>
    /// The name's words without the markup a mod may write into it, such as a colour, so that it
    /// is matched and completed as the player reads it.
    /// </summary>
    public static string Plain(string localized)
        => string.IsNullOrEmpty(localized) ? localized ?? string.Empty : Spaces.Replace(Tag.Replace(localized, string.Empty), " ").Trim();
}
