namespace CandC.HeicClipboard.Tests;

/// <summary>
/// Smoke tests that call the extended IWICImagingFactory vtable against real WIC.
/// If the placeholder method ordering in the interop declaration were wrong,
/// CreateColorContext/CreateColorTransformer would land on the wrong native slot
/// and these tests would fail or crash immediately.
/// </summary>
public sealed class WicColorInteropTests
{
    [Fact]
    public void CreateColorContext_InitializesAsSrgbExifColorSpace()
    {
        var factory = WicCodecProbe.CreateImagingFactory();
        try
        {
            factory.CreateColorContext(out var colorContext);
            try
            {
                colorContext.InitializeFromExifColorSpace(1);
                colorContext.GetType(out var type);
                colorContext.GetExifColorSpace(out var exifColorSpace);

                Assert.Equal(WICColorContextType.WICColorContextExifColorSpace, type);
                Assert.Equal(1u, exifColorSpace);
            }
            finally
            {
                WicCodecProbe.ReleaseComObject(colorContext);
            }
        }
        finally
        {
            WicCodecProbe.ReleaseComObject(factory);
        }
    }

    [Fact]
    public void CreateColorTransformer_ReturnsTransform()
    {
        var factory = WicCodecProbe.CreateImagingFactory();
        try
        {
            factory.CreateColorTransformer(out var colorTransform);

            Assert.NotNull(colorTransform);
            WicCodecProbe.ReleaseComObject(colorTransform);
        }
        finally
        {
            WicCodecProbe.ReleaseComObject(factory);
        }
    }
}
