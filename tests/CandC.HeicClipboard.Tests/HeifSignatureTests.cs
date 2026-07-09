namespace CandC.HeicClipboard.Tests;

public sealed class HeifSignatureTests
{
    private static byte[] BuildFtypHeader(string majorBrand, params string[] compatibleBrands)
    {
        var brands = new List<string> { majorBrand };
        brands.AddRange(compatibleBrands);

        var boxSize = 16 + (compatibleBrands.Length * 4);
        var header = new List<byte>
        {
            (byte)(boxSize >> 24), (byte)(boxSize >> 16), (byte)(boxSize >> 8), (byte)boxSize
        };
        header.AddRange("ftyp"u8.ToArray());
        header.AddRange(System.Text.Encoding.ASCII.GetBytes(majorBrand));
        header.AddRange(new byte[4]); // minor version
        foreach (var brand in compatibleBrands)
        {
            header.AddRange(System.Text.Encoding.ASCII.GetBytes(brand));
        }

        return header.ToArray();
    }

    [Theory]
    [InlineData("heic")]
    [InlineData("heix")]
    [InlineData("mif1")]
    [InlineData("heif")]
    public void LooksLikeHeif_AcceptsHeifMajorBrands(string majorBrand)
    {
        Assert.True(HeifSignature.LooksLikeHeif(BuildFtypHeader(majorBrand)));
    }

    [Fact]
    public void LooksLikeHeif_AcceptsHeifCompatibleBrand()
    {
        var header = BuildFtypHeader("XXXX", "mif1", "heic");

        Assert.True(HeifSignature.LooksLikeHeif(header));
    }

    [Fact]
    public void LooksLikeHeif_RejectsMp4Container()
    {
        var header = BuildFtypHeader("isom", "iso2", "mp41");

        Assert.False(HeifSignature.LooksLikeHeif(header));
    }

    [Fact]
    public void LooksLikeHeif_RejectsRandomBytes()
    {
        var random = new byte[64];
        new Random(Seed: 42).NextBytes(random);

        Assert.False(HeifSignature.LooksLikeHeif(random));
    }

    [Fact]
    public void LooksLikeHeif_RejectsShortInput()
    {
        Assert.False(HeifSignature.LooksLikeHeif(new byte[8]));
    }

    [Fact]
    public void LooksLikeHeif_RejectsJpegHeader()
    {
        var header = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00 };

        Assert.False(HeifSignature.LooksLikeHeif(header));
    }

    [Fact]
    public void FileLooksLikeHeif_ReturnsFalseForMissingFile()
    {
        Assert.False(HeifSignature.FileLooksLikeHeif(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".heic")));
    }
}
