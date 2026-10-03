using System.Text.Json;
using OAI.Core.Models;

namespace OAI.Core.Services;

public static class MasterProfileJsonMapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static MasterUserProfile FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new MasterUserProfile();

        var trimmed = json.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[7..];
        else if (trimmed.StartsWith("```"))
            trimmed = trimmed[3..];
        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3];

        try
        {
            return JsonSerializer.Deserialize<MasterUserProfile>(trimmed.Trim(), Options) ?? new MasterUserProfile();
        }
        catch
        {
            return new MasterUserProfile();
        }
    }
}
