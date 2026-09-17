namespace Rove.Core.Services;

/// <summary>
/// Small fzf-style subsequence matcher shared by local search, global search
/// and the command palette. Every query character must appear in order in the
/// candidate; the score rewards prefix matches, word-boundary hits and
/// consecutive runs, and penalizes gaps.
/// </summary>
public static class FuzzyMatcher
{
    private const int WordBoundaryBonus = 8;
    private const int ConsecutiveBonus = 5;
    private const int PrefixBonus = 10;
    private const int GapPenalty = 1;

    /// <summary>
    /// Returns true when every char of <paramref name="query"/> appears in
    /// order (case-insensitive) in <paramref name="candidate"/>. An empty
    /// query matches everything with score 0.
    /// </summary>
    public static bool TryMatch(string query, string candidate, out int score)
    {
        score = 0;
        if (string.IsNullOrEmpty(query))
            return true;
        if (string.IsNullOrEmpty(candidate))
            return false;

        int qi = 0;
        int lastHit = -1;
        for (int ci = 0; ci < candidate.Length && qi < query.Length; ci++)
        {
            if (char.ToLowerInvariant(candidate[ci]) != char.ToLowerInvariant(query[qi]))
                continue;

            if (ci == 0)
                score += PrefixBonus;
            else if (IsWordBoundary(candidate, ci))
                score += WordBoundaryBonus;

            if (lastHit >= 0)
            {
                if (ci == lastHit + 1)
                    score += ConsecutiveBonus;
                else
                    score -= Math.Min(ci - lastHit - 1, 10) * GapPenalty;
            }

            lastHit = ci;
            qi++;
        }

        if (qi < query.Length)
        {
            score = 0;
            return false;
        }
        // Slightly prefer shorter candidates when everything else ties.
        score -= Math.Min(candidate.Length / 8, 6);
        return true;
    }

    private static bool IsWordBoundary(string s, int i)
    {
        char prev = s[i - 1];
        char cur = s[i];
        if (prev is ' ' or '.' or '-' or '_' or '\\' or '/')
            return true;
        return char.IsLower(prev) && char.IsUpper(cur);
    }
}
