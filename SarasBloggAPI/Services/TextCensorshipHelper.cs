using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

public static class TextCensorshipHelper
{
public static string CensorForbiddenPatterns(string? text, IEnumerable<string> patterns)
{
    if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

    // Validera och bygg ett kombinerat regex: \b(?:p1|p2|p3)\b
    var valid = new List<string>();
    foreach (var p in patterns)
    {
        if (string.IsNullOrWhiteSpace(p)) continue;
        try
        {
            // Testkompilera – hoppa över ogiltiga mönster
            _ = new Regex(p);
            valid.Add($@"(?:{p})");
        }
        catch { /* skip invalid pattern */ }
    }
    if (valid.Count == 0) return text;

var combined = $@"\b{string.Join("|", valid)}\b";
    return Regex.Replace(
text,
combined,
m => new string('*', m.Length),
RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
}
}
