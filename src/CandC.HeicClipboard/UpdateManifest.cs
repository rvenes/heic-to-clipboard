using System.Text.Json;

namespace CandC.HeicClipboard;

public sealed class UpdateManifest
{
    private UpdateManifest(Version version, string fileName, string sha256, long size)
    {
        Version = version;
        FileName = fileName;
        Sha256 = sha256;
        Size = size;
    }

    public Version Version { get; }
    public string FileName { get; }
    public string Sha256 { get; }
    public long Size { get; }

    public bool IsNewerThan(Version currentVersion) => Version > currentVersion;

    public Uri ResolveDownloadUri(Uri baseUri) => new(baseUri, Uri.EscapeDataString(FileName));

    public static bool TryParse(string json, out UpdateManifest? manifest, out string error)
    {
        manifest = null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = "The update manifest is not valid JSON.";
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The update manifest is not a JSON object.";
                return false;
            }

            if (!TryGetString(root, "version", out var versionText) ||
                !Version.TryParse(versionText, out var version))
            {
                error = "The update manifest has a missing or invalid version.";
                return false;
            }

            if (!TryGetString(root, "file", out var fileName) ||
                fileName.IndexOfAny(['/', '\\']) >= 0 ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "The update manifest has a missing or invalid file name.";
                return false;
            }

            if (!TryGetString(root, "sha256", out var sha256) || !IsSha256Hex(sha256))
            {
                error = "The update manifest has a missing or invalid sha256 checksum.";
                return false;
            }

            if (!root.TryGetProperty("size", out var sizeProperty) ||
                sizeProperty.ValueKind != JsonValueKind.Number ||
                !sizeProperty.TryGetInt64(out var size) ||
                size <= 0)
            {
                error = "The update manifest has a missing or invalid file size.";
                return false;
            }

            manifest = new UpdateManifest(version, fileName, sha256.ToLowerInvariant(), size);
            error = string.Empty;
            return true;
        }
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = (property.GetString() ?? string.Empty).Trim();
        return value.Length > 0;
    }

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}
