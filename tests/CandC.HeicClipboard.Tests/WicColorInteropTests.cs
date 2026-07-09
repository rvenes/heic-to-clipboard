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

    [Fact]
    public void GetColorContexts_CanBeCalledOnLocalHeicFrame()
    {
        // Null when the sample folder is missing or the HEIF codec is absent.
        var samplePath = LocalHeicSamples.GetDecodableFiles().FirstOrDefault();
        if (samplePath is null)
        {
            return;
        }

        IWICBitmapDecoder? decoder = null;
        IWICBitmapFrameDecode? frame = null;
        var colorContexts = new List<IWICColorContext>();
        var factory = WicCodecProbe.CreateImagingFactory();
        try
        {
            factory.CreateDecoderFromFilename(
                samplePath,
                IntPtr.Zero,
                WicCodecProbe.GenericReadAccess,
                WICDecodeOptions.WICDecodeMetadataCacheOnLoad,
                out decoder);

            decoder.GetFrame(0, out frame);
            frame.GetColorContexts(0, null, out var contextCount);

            if (contextCount == 0)
            {
                return;
            }

            for (var index = 0; index < contextCount; index++)
            {
                factory.CreateColorContext(out var colorContext);
                colorContexts.Add(colorContext);
            }

            frame.GetColorContexts(contextCount, colorContexts.ToArray(), out var actualCount);

            Assert.Equal(contextCount, actualCount);
            foreach (var colorContext in colorContexts)
            {
                colorContext.GetType(out var type);
                Assert.True(
                    type is WICColorContextType.WICColorContextProfile or WICColorContextType.WICColorContextExifColorSpace,
                    $"Unexpected color context type: {type}");
            }
        }
        finally
        {
            foreach (var colorContext in colorContexts)
            {
                WicCodecProbe.ReleaseComObject(colorContext);
            }

            WicCodecProbe.ReleaseComObject(frame);
            WicCodecProbe.ReleaseComObject(decoder);
            WicCodecProbe.ReleaseComObject(factory);
        }
    }
}
