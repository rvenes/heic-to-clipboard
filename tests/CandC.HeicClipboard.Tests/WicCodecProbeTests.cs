using System.Runtime.InteropServices;

namespace CandC.HeicClipboard.Tests;

public sealed class WicCodecProbeTests
{
    [Fact]
    public void IsMissingHeifCodec_ReturnsTrueForKnownMissingCodecErrors()
    {
        var exception = new COMException("Component not found.", unchecked((int)0x88982F50));

        Assert.True(WicCodecProbe.IsMissingHeifCodec(exception));
    }

    [Fact]
    public void FormatDecodeError_IncludesHResultAndErrorNameWhenKnown()
    {
        var exception = new COMException("The image data is invalid.", unchecked((int)0x88982F60));

        var message = WicCodecProbe.FormatDecodeError(exception);

        Assert.Equal("HEIC decode failed (WINCODEC_ERR_BADIMAGE, 0x88982F60): The image data is invalid.", message);
    }
}
