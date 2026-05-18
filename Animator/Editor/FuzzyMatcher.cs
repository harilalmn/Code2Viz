using System.Collections.Generic;

namespace Animator.Editor;

/// <summary>
/// Subsequence fuzzy matcher with scoring. Adapted from Code2Viz.
/// "clr" matches "color" and "clear"; "VPt" matches "VPoint".
/// </summary>
public static class FuzzyMatcher
{
    public static int? Score(string pattern, string candidate)
    {
        if (string.IsNullOrEmpty(pattern)) return 0;
        if (string.IsNullOrEmpty(candidate)) return null;

        var patternLower = pattern.ToLowerInvariant();
        var candidateLower = candidate.ToLowerInvariant();

        int pi = 0;
        for (int ci = 0; ci < candidateLower.Length && pi < patternLower.Length; ci++)
            if (candidateLower[ci] == patternLower[pi]) pi++;
        if (pi < patternLower.Length) return null;

        int score = 0; pi = 0; int last = -1;
        for (int ci = 0; ci < candidate.Length && pi < pattern.Length; ci++)
        {
            if (char.ToLowerInvariant(candidate[ci]) == char.ToLowerInvariant(pattern[pi]))
            {
                if (ci == pi) score += 10;
                if (ci == 0 || candidate[ci - 1] == '_' || candidate[ci - 1] == '.'
                    || (char.IsUpper(candidate[ci]) && ci > 0 && char.IsLower(candidate[ci - 1])))
                    score += 8;
                if (last >= 0 && ci == last + 1) score += 5;
                if (candidate[ci] == pattern[pi]) score += 1;
                if (last >= 0 && ci > last + 1) score -= (ci - last - 1);
                last = ci;
                pi++;
            }
        }

        if (candidateLower == patternLower) score += 50;
        if (candidateLower.StartsWith(patternLower)) score += 30;
        score -= (int)(candidate.Length * 0.1);
        return score;
    }
}
