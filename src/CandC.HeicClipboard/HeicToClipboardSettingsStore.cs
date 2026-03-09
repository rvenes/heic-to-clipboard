using System.Text.Json;

namespace CandC.HeicClipboard;

public sealed class HeicToClipboardSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public HeicToClipboardSettingsStore(string settingsPath)
    {
        SettingsPath = settingsPath;
    }

    public string SettingsPath { get; }

    public HeicToClipboardSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return HeicToClipboardSettings.CreateDefault();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<HeicToClipboardSettings>(json) ?? HeicToClipboardSettings.CreateDefault();

            using var document = JsonDocument.Parse(json);
            if (TryGetPositiveInt32(document.RootElement, "maxDimensionPx", out var legacyMaxDimension))
            {
                settings.MaxLongestSidePx = legacyMaxDimension;
            }
            else
            {
                var hasWidth = TryGetPositiveInt32(document.RootElement, "maxWidthPx", out var legacyWidth);
                var hasHeight = TryGetPositiveInt32(document.RootElement, "maxHeightPx", out var legacyHeight);
                if (hasWidth || hasHeight)
                {
                    settings.MaxLongestSidePx = Math.Max(legacyWidth, legacyHeight);
                }
            }

            return settings.Sanitize();
        }
        catch
        {
            return HeicToClipboardSettings.CreateDefault();
        }
    }

    public void Save(HeicToClipboardSettings settings)
    {
        var sanitized = settings.Sanitize();
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(sanitized, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static bool TryGetPositiveInt32(JsonElement rootElement, string propertyName, out int value)
    {
        value = 0;
        return rootElement.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value) &&
            value > 0;
    }
}
