using System.Text.RegularExpressions;

namespace OAI.Core.Services;

public static class BulletEnhancer
{
    private static readonly string[] WeakPrefixes =
    {
        "responsible for", "worked on", "helped with", "duties included", "tasked with"
    };

    public static string Enhance(string bullet)
    {
        if (string.IsNullOrWhiteSpace(bullet))
            return string.Empty;

        var trimmed = Regex.Replace(bullet.Trim(), @"\s+", " ");
        if (LooksStrong(trimmed))
            return trimmed;

        var body = trimmed;
        foreach (var prefix in WeakPrefixes)
        {
            if (body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                body = body[prefix.Length..].Trim(' ', ',', ':');
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(body))
            return trimmed;

        var noun = body.TrimEnd('.');
        return $"Delivered {noun} with measurable process improvement.";
    }

    private static bool LooksStrong(string bullet)
    {
        if (WeakPrefixes.Any(p => bullet.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (Regex.IsMatch(bullet, @"\d"))
            return true;
        return char.IsUpper(bullet[0]) && bullet.Split(' ').Length >= 8;
    }
}
