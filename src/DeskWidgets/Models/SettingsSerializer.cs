using System.Text.Json;

namespace DeskWidgets.Models;

/// <summary>Pure JSON conversion for settings, kept free of file and UI code so it can be unit-tested.</summary>
public static class SettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);

    /// <summary>Returns sanitized settings, or null when the text is empty or not valid settings JSON.</summary>
    public static AppSettings? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
            if (settings == null) return null;
            settings.Sanitize();
            return settings;
        }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
    }
}
