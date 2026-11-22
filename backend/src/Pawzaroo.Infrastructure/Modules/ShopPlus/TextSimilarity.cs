namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

/// <summary>pg_trgm-free fuzzy matching helpers (Levenshtein + trigram Jaccard).</summary>
public static class TextSimilarity
{
    public static string[] Tokenize(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? Array.Empty<string>()
            : s.ToLowerInvariant()
                .Split(new[] { ' ', '\t', ',', '.', ';', ':', '/', '\\', '-', '_', '(', ')', '"', '\'', '+', '&' },
                    StringSplitOptions.RemoveEmptyEntries);

    public static int Levenshtein(string a, string b, int max = int.MaxValue)
    {
        if (a == b) return 0;
        if (Math.Abs(a.Length - b.Length) > max) return max + 1;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            int rowMin = cur[0];
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                if (cur[j] < rowMin) rowMin = cur[j];
            }
            if (rowMin > max) return max + 1;
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    private static HashSet<string> Trigrams(string s)
    {
        var padded = "  " + s + " ";
        var set = new HashSet<string>();
        for (int i = 0; i + 3 <= padded.Length; i++) set.Add(padded.Substring(i, 3));
        return set;
    }

    /// <summary>Jaccard similarity of trigram sets, 0..1.</summary>
    public static double Trigram(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var ta = Trigrams(a);
        var tb = Trigrams(b);
        int inter = ta.Count(tb.Contains);
        int union = ta.Count + tb.Count - inter;
        return union == 0 ? 0 : (double)inter / union;
    }

    /// <summary>Allowed edit distance for a typed word.</summary>
    public static int MaxEdits(int length) => length <= 3 ? 0 : length <= 6 ? 1 : 2;

    /// <summary>Best fuzzy replacement for <paramref name="word"/> among known terms, or null.</summary>
    public static string? BestCorrection(string word, IEnumerable<KeyValuePair<string, int>> terms)
    {
        var max = MaxEdits(word.Length);
        if (max == 0) return null;
        string? best = null;
        int bestDist = int.MaxValue;
        int bestFreq = 0;
        foreach (var (term, freq) in terms)
        {
            if (term == word) return null; // already a known word
            if (Math.Abs(term.Length - word.Length) > max) continue;
            var d = Levenshtein(word, term, max);
            if (d > max) continue;
            if (d < bestDist || (d == bestDist && freq > bestFreq))
            {
                best = term; bestDist = d; bestFreq = freq;
            }
        }
        return best;
    }
}
