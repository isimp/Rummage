namespace Rummage.Core;

/// <summary>How a grab is named in its lines.</summary>
internal static class Labels
{
    /// <summary>Whether a grab of <paramref name="count"/> tops the carried stack <paramref name="into"/> up to a full one.</summary>
    public static bool FillsStack(int count, ItemStack into) => into != null && count >= into.MaxStack - into.Count;

    /// <summary>"Wood x20" for more than one, "Wood" for one, "Wood to a full stack" for a top-up to full.</summary>
    public static string Of(string shown, int count, ItemStack into) =>
        FillsStack(count, into) ? $"{shown} to a full stack" : count > 1 ? $"{shown} x{count}" : shown;

    /// <summary>A grab is always for at least one.</summary>
    public static int Count(int count) => count < 1 ? 1 : count;
}
