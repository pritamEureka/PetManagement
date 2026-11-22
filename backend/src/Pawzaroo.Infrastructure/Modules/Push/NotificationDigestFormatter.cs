namespace Pawzaroo.Infrastructure.Modules.Push;

/// <summary>Pure formatter for the per-user digest summary entry (title/body with counts per category).</summary>
public static class NotificationDigestFormatter
{
    public static string CategoryLabel(string category) => category.Replace('_', ' ');

    /// <summary>"5 new notifications" / "1 new notification".</summary>
    public static string Title(IReadOnlyDictionary<string, int> countsByCategory)
    {
        var total = countsByCategory.Values.Sum();
        return total == 1 ? "1 new notification" : $"{total} new notifications";
    }

    /// <summary>
    /// "3 orders, 2 messages, 1 social" ordered by count desc then name; shows at most
    /// <paramref name="maxCategories"/> categories, folding the rest into "and N more".
    /// </summary>
    public static string Body(IReadOnlyDictionary<string, int> countsByCategory, int maxCategories = 4)
    {
        var ordered = countsByCategory.Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        if (ordered.Count == 0) return "";
        var shown = ordered.Take(maxCategories).Select(kv => $"{kv.Value} {CategoryLabel(kv.Key)}");
        var rest = ordered.Skip(maxCategories).Sum(kv => kv.Value);
        var text = string.Join(", ", shown);
        return rest > 0 ? $"{text} and {rest} more" : text;
    }
}
