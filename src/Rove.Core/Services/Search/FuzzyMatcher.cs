namespace Rove.Core.Services;

public static class FuzzyMatcher
{
    private const int WordBoundaryBonus = 8;
    private const int ConsecutiveBonus = 5;
    private const int PrefixBonus = 10;
    private const int GapPenalty = 1;

    public static bool TryMatch(string query, string candidate, out int score) =>
        TryMatchWithPositions(query, candidate, out score, out _);

    public static bool TryMatchWithPositions(string query, string candidate, out int score, out int[] positions)
    {
        score = 0;
        positions = [];
        if (string.IsNullOrEmpty(query))
            return true;
        if (string.IsNullOrEmpty(candidate))
            return false;

        List<int> hits = [];
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

            hits.Add(ci);
            lastHit = ci;
            qi++;
        }

        if (qi < query.Length)
        {
            score = 0;
            return false;
        }
        score -= Math.Min(candidate.Length / 8, 6);
        positions = [.. hits];
        return true;
    }

    public static bool TryMatchAnyOrder(string query, IReadOnlyList<string> fields, out int score)
    {
        score = 0;
        if (string.IsNullOrWhiteSpace(query))
            return true;

        foreach (string word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int best = -1;
            foreach (string field in fields)
            {
                if (TryMatch(word, field, out int wordScore) && wordScore > best)
                    best = wordScore;
            }
            if (best < 0)
            {
                score = 0;
                return false;
            }
            score += best;
        }
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
