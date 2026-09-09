using System.Globalization;
using System.Text.RegularExpressions;

namespace DD3.CoverScope.Services.Reviews;

public static partial class ReviewDiffPresentation
{
    public sealed record Line(int Number, int? BeforeNumber, string Text, string Change = "");

    // Number is the reviewed source line, never the patch row. Removed lines have
    // only a baseline number, so reviewed coverage cannot be assigned to them.
    public static List<Line> Parse(string patch)
    {
        var result = new List<Line>();
        var before = 0; var after = 0; var beforeRemaining = 0; var afterRemaining = 0;
        var hasHunk = false;
        foreach (var text in patch.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var match = Hunk().Match(text);
            if (match.Success)
            {
                if (hasHunk) { result.Add(new(0, null, "⋯", "separator")); }
                hasHunk = true;
                before = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                after = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                beforeRemaining = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
                afterRemaining = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 1;
                continue;
            }
            if (text.StartsWith('+') && afterRemaining > 0)
            { result.Add(new(after++, null, text[1..], "added")); afterRemaining--; }
            else if (text.StartsWith('-') && beforeRemaining > 0)
            { result.Add(new(0, before++, text[1..], "removed")); beforeRemaining--; }
            else if (text.StartsWith(' ') && beforeRemaining > 0 && afterRemaining > 0)
            { result.Add(new(after++, before++, text[1..])); beforeRemaining--; afterRemaining--; }
        }
        return result;
    }

    [GeneratedRegex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")]
    private static partial Regex Hunk();
}
