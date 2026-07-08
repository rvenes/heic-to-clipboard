using System.Runtime.InteropServices;

namespace CandC.HeicClipboard.Tests;

public sealed class WicCodecProbeTests
{
    [Fact]
    public void IsMissingHeifCodec_ReturnsTrueForComponentNotFound()
    {
        var exception = new COMException("Component not found.", unchecked((int)0x88982F50));

        Assert.True(WicCodecProbe.IsMissingHeifCodec(exception));
    }

    [Theory]
    [InlineData(unchecked((int)0x88982F07))] // WINCODEC_ERR_UNKNOWNIMAGEFORMAT (corrupt file)
    [InlineData(unchecked((int)0x88982F8B))] // WINCODEC_ERR_COMPONENTINITIALIZEFAILURE
    [InlineData(unchecked((int)0x88982F60))] // WINCODEC_ERR_BADIMAGE
    public void IsMissingHeifCodec_ReturnsFalseForDecodeErrors(int hresult)
    {
        var exception = new COMException("Decode failed.", hresult);

        Assert.False(WicCodecProbe.IsMissingHeifCodec(exception));
    }

    [Fact]
    public void FormatDecodeError_IncludesHResultAndErrorNameWhenKnown()
    {
        var exception = new COMException("The image data is invalid.", unchecked((int)0x88982F60));

        var message = WicCodecProbe.FormatDecodeError(exception);

        Assert.Equal("HEIC decode failed (WINCODEC_ERR_BADIMAGE, 0x88982F60): The image data is invalid.", message);
    }
}
