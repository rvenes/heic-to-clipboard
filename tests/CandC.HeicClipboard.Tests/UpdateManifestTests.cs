using System.Security.Cryptography;

namespace CandC.HeicClipboard.Tests;

public sealed class UpdateManifestTests
{
    private const string ValidJson = """
        {
          "version": "0.3.0",
          "file": "HeicToClipboard-0.3.0.exe",
          "sha256": "AB12ab12AB12ab12AB12ab12AB12ab12AB12ab12AB12ab12AB12ab12AB12ab12",
          "size": 12345678,
          "releaseDate": "2026-07-12T00:00:00Z"
        }
        """;

    [Fact]
    public void TryParse_AcceptsValidManifest()
    {
        var parsed = UpdateManifest.TryParse(ValidJson, out var manifest, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(manifest);
        Assert.Equal(new Version(0, 3, 0), manifest!.Version);
        Assert.Equal("HeicToClipboard-0.3.0.exe", manifest.FileName);
        Assert.Equal("ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12", manifest.Sha256);
        Assert.Equal(12345678, manifest.Size);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void TryParse_RejectsInvalidDocuments(string json)
    {
        Assert.False(UpdateManifest.TryParse(json, out var manifest, out var error));
        Assert.Null(manifest);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("version", "\"not-a-version\"")]
    [InlineData("version", "\"\"")]
    [InlineData("file", "\"..\\\\evil.exe\"")]
    [InlineData("file", "\"folder/evil.exe\"")]
    [InlineData("file", "\"\"")]
    [InlineData("sha256", "\"abc123\"")]
    [InlineData("sha256", "\"zz12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12\"")]
    [InlineData("size", "0")]
    [InlineData("size", "\"12345\"")]
    public void TryParse_RejectsInvalidFieldValues(string field, string replacementValue)
    {
        var json = $$"""
            {
              "version": {{(field == "version" ? replacementValue : "\"0.3.0\"")}},
              "file": {{(field == "file" ? replacementValue : "\"HeicToClipboard-0.3.0.exe\"")}},
              "sha256": {{(field == "sha256" ? replacementValue : "\"ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12ab12\"")}},
              "size": {{(field == "size" ? replacementValue : "12345678")}}
            }
            """;

        Assert.False(UpdateManifest.TryParse(json, out var manifest, out _));
        Assert.Null(manifest);
    }

    [Theory]
    [InlineData("0.2.0", true)]
    [InlineData("0.3.0", false)]
    [InlineData("0.4.0", false)]
    public void IsNewerThan_ComparesVersions(string currentVersion, bool expected)
    {
        Assert.True(UpdateManifest.TryParse(ValidJson, out var manifest, out _));

        Assert.Equal(expected, manifest!.IsNewerThan(Version.Parse(currentVersion)));
    }

    [Fact]
    public void ResolveDownloadUri_CombinesBaseUrlAndFileName()
    {
        Assert.True(UpdateManifest.TryParse(ValidJson, out var manifest, out _));

        var uri = manifest!.ResolveDownloadUri(new Uri("https://venes.org/heictoclipboard/"));

        Assert.Equal("https://venes.org/heictoclipboard/HeicToClipboard-0.3.0.exe", uri.ToString());
    }

    [Fact]
    public void VerifyDownload_AcceptsMatchingFileAndRejectsTamperedFile()
    {
        var directory = Directory.CreateTempSubdirectory("HeicToClipboardTests_");
        try
        {
            var filePath = Path.Combine(directory.FullName, "update.exe");
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            File.WriteAllBytes(filePath, payload);
            var sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

            var json = $$"""
                {
                  "version": "0.3.0",
                  "file": "update.exe",
                  "sha256": "{{sha256}}",
                  "size": {{payload.Length}}
                }
                """;
            Assert.True(UpdateManifest.TryParse(json, out var manifest, out _));

            UpdateService.VerifyDownload(filePath, manifest!);

            File.WriteAllBytes(filePath, [1, 2, 3, 4, 6]);
            Assert.Throws<InvalidOperationException>(() => UpdateService.VerifyDownload(filePath, manifest!));

            File.WriteAllBytes(filePath, [1, 2, 3, 4]);
            Assert.Throws<InvalidOperationException>(() => UpdateService.VerifyDownload(filePath, manifest!));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
