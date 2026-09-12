namespace Launcher.Search;

/// <summary>
/// Lightweight fuzzy string matcher tuned for app-launcher queries: it favors
/// exact prefix matches, then word-boundary/acronym matches (e.g. "vsc" for
/// "Visual Studio Code"), then subsequence matches, and returns null for no match.
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>
    /// Scores how well <paramref name="query"/> matches <paramref name="target"/>.
    /// Returns null if there is no match at all. Higher score = better match.
    /// </summary>
    public static int? Score(string query, string target)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 0;

        var q = query.Trim();
        var t = target;

        if (t.Length == 0)
            return null;

        // 1. Exact match — best possible.
        if (string.Equals(t, q, StringComparison.OrdinalIgnoreCase))
            return 1000;

        // 2. Exact prefix match — very strong signal ("vs" -> "VS Code").
        if (t.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return 900 - (t.Length - q.Length); // shorter target wins ties

        // 3. Acronym match against word initials ("vsc" -> "Visual Studio Code").
        var initials = GetInitials(t);
        if (initials.Length > 0 && initials.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return 800;

        // 4. Word-start match anywhere ("code" -> "Visual Studio Code").
        var wordStartIndex = FindWordStart(t, q);
        if (wordStartIndex >= 0)
            return 700 - wordStartIndex;

        // 5. Contains match ("studio" -> "Visual Studio Code").
        var containsIndex = t.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        if (containsIndex >= 0)
            return 500 - containsIndex;

        // 6. Subsequence fuzzy match ("vscd" -> "Visual Studio Code").
        return SubsequenceScore(q, t);
    }

    private static string GetInitials(string text)
    {
        var chars = new List<char>();
        bool atWordStart = true;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || c is '-' or '_' or '.')
            {
                atWordStart = true;
                continue;
            }
            if (atWordStart)
            {
                chars.Add(c);
                atWordStart = false;
            }
        }
        return new string(chars.ToArray());
    }

    private static int FindWordStart(string text, string query)
    {
        bool atWordStart = true;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c) || c is '-' or '_' or '.')
            {
                atWordStart = true;
                continue;
            }
            if (atWordStart)
            {
                if (i + query.Length <= text.Length &&
                    string.Compare(text, i, query, 0, query.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return i;
                }
                atWordStart = false;
            }
        }
        return -1;
    }

    private static int? SubsequenceScore(string query, string text)
    {
        int qi = 0;
        int lastMatch = -1;
        int gapPenalty = 0;
        int consecutiveBonus = 0;

        for (int ti = 0; ti < text.Length && qi < query.Length; ti++)
        {
            if (char.ToLowerInvariant(text[ti]) == char.ToLowerInvariant(query[qi]))
            {
                if (lastMatch == ti - 1)
                    consecutiveBonus += 3;
                else if (lastMatch >= 0)
                    gapPenalty += (ti - lastMatch);

                lastMatch = ti;
                qi++;
            }
        }

        if (qi < query.Length)
            return null; // not all query characters were found in order

        var baseScore = 200 + consecutiveBonus - gapPenalty;
        return Math.Max(baseScore, 1);
    }
}
