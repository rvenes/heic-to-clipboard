using System.Drawing;
using System.Drawing.Imaging;

namespace CandC.HeicClipboard.Tests;

public sealed class HeicConverterTests
{
    [Fact]
    public void CreateCandidateBitmap_FullScale_NormalizesTo24bppWithSourceCopySemantics()
    {
        // Full-scale attempts must go through the same normalization as scaled ones:
        // 24bpp RGB via a SourceCopy draw, which discards alpha and keeps the source's
        // raw RGB values (a fully transparent pixel has RGB 0,0,0 and so becomes black).
        // This pins the long-standing production behavior; changing the compositing
        // semantics deliberately would be a separate, visible decision.
        using var source = new Bitmap(10, 8, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.Clear(Color.Transparent);
        }
        source.SetPixel(3, 2, Color.FromArgb(255, 200, 10, 10));

        using var candidate = HeicConverter.CreateCandidateBitmap(source, 100);

        Assert.Equal(PixelFormat.Format24bppRgb, candidate.PixelFormat);
        Assert.Equal(10, candidate.Width);
        Assert.Equal(8, candidate.Height);
        Assert.Equal(Color.FromArgb(255, 200, 10, 10).ToArgb(), candidate.GetPixel(3, 2).ToArgb());
        Assert.Equal(Color.FromArgb(255, 0, 0, 0).ToArgb(), candidate.GetPixel(9, 7).ToArgb());
    }

    [Fact]
    public void CreateCandidateBitmap_ScalesDimensionsByPercent()
    {
        using var source = new Bitmap(100, 60, PixelFormat.Format32bppArgb);

        using var candidate = HeicConverter.CreateCandidateBitmap(source, 50);

        Assert.Equal(PixelFormat.Format24bppRgb, candidate.PixelFormat);
        Assert.Equal(50, candidate.Width);
        Assert.Equal(30, candidate.Height);
    }

    [Fact]
    public void Convert_LocalHeicSamples_WritesReadableJpegsWithOrientedDimensions()
    {
        var samplePaths = LocalHeicSamples.GetFiles();
        if (samplePaths.Count == 0)
        {
            return;
        }

        var workingDirectory = CreateWorkingDirectory();
        try
        {
            var converter = new HeicConverter(
                new TempFileManager(workingDirectory, cleanupEnabled: false),
                new HeicConversionOptions(long.MaxValue, AppConstants.DefaultInitialJpegQuality, true, null));

            foreach (var samplePath in samplePaths)
            {
                var expectedDimensions = ReadOrientedFrameDimensions(samplePath);

                var result = converter.Convert(samplePath);

                Assert.True(result.Success, result.ErrorMessage);
                Assert.NotNull(result.OutputPath);
                Assert.True(File.Exists(result.OutputPath));
                Assert.True(new FileInfo(result.OutputPath).Length > 0);

                using var outputImage = Image.FromFile(result.OutputPath);
                Assert.Equal(ImageFormat.Jpeg.Guid, outputImage.RawFormat.Guid);
                Assert.Equal(expectedDimensions.Width, outputImage.Width);
                Assert.Equal(expectedDimensions.Height, outputImage.Height);
            }
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public void Convert_LocalHeicSamples_KeepsJpegsUnderPointFifteenMb()
    {
        var samplePaths = LocalHeicSamples.GetFiles();
        if (samplePaths.Count == 0)
        {
            return;
        }

        var maximumBytes = AppConstants.ToBytes(0.15m);
        var workingDirectory = CreateWorkingDirectory();
        try
        {
            var converter = new HeicConverter(
                new TempFileManager(workingDirectory, cleanupEnabled: false),
                new HeicConversionOptions(maximumBytes, AppConstants.DefaultInitialJpegQuality, true, null));

            foreach (var samplePath in samplePaths)
            {
                var expectedDimensions = ReadOrientedFrameDimensions(samplePath);

                var result = converter.Convert(samplePath);

                Assert.True(result.Success, result.ErrorMessage);
                Assert.NotNull(result.OutputPath);

                var outputLength = new FileInfo(result.OutputPath).Length;
                Assert.InRange(outputLength, 1, maximumBytes);

                using var outputImage = Image.FromFile(result.OutputPath);
                Assert.Equal(ImageFormat.Jpeg.Guid, outputImage.RawFormat.Guid);
                Assert.InRange(outputImage.Width, 1, expectedDimensions.Width);
                Assert.InRange(outputImage.Height, 1, expectedDimensions.Height);
                Assert.Equal(expectedDimensions.Width >= expectedDimensions.Height, outputImage.Width >= outputImage.Height);
            }
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public void Convert_NonHeifFile_ReturnsInvalidHeaderMessage()
    {
        var workingDirectory = CreateWorkingDirectory();
        try
        {
            var sourcePath = Path.Combine(workingDirectory, "not-heic.heic");
            File.WriteAllText(sourcePath, "this is not a HEIC file");

            var converter = new HeicConverter(
                new TempFileManager(workingDirectory, cleanupEnabled: false),
                new HeicConversionOptions(long.MaxValue, AppConstants.DefaultInitialJpegQuality, true, null));

            var result = converter.Convert(sourcePath);

            Assert.False(result.Success);
            Assert.Null(result.OutputPath);
            Assert.Equal("Not a valid HEIC/HEIF file (unrecognized file header).", result.ErrorMessage);
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    [Fact]
    public void Convert_CorruptHeifLookingFile_DoesNotReportMissingCodec()
    {
        var workingDirectory = CreateWorkingDirectory();
        try
        {
            var sourcePath = Path.Combine(workingDirectory, "corrupt.heic");
            File.WriteAllBytes(
                sourcePath,
                [
                    0x00, 0x00, 0x00, 0x18,
                    (byte)'f', (byte)'t', (byte)'y', (byte)'p',
                    (byte)'h', (byte)'e', (byte)'i', (byte)'c',
                    0x00, 0x00, 0x00, 0x00,
                    (byte)'h', (byte)'e', (byte)'i', (byte)'c'
                ]);

            var converter = new HeicConverter(
                new TempFileManager(workingDirectory, cleanupEnabled: false),
                new HeicConversionOptions(long.MaxValue, AppConstants.DefaultInitialJpegQuality, true, null));

            var result = converter.Convert(sourcePath);

            Assert.False(result.Success);
            Assert.Null(result.OutputPath);

            if (result.ErrorMessage == AppConstants.MissingHeifSupportMessage)
            {
                // Machines without the HEIF codec (e.g. CI runners) get
                // COMPONENTNOTFOUND for a HEIF-looking file, and the install hint
                // is the correct answer there; the decode-error assertion below
                // only applies when a codec actually tried to decode the file.
                return;
            }

            Assert.StartsWith("HEIC decode failed", result.ErrorMessage);
        }
        finally
        {
            DeleteWorkingDirectory(workingDirectory);
        }
    }

    private static string CreateWorkingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "CandC.HeicClipboard.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteWorkingDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static ImageDimensions ReadOrientedFrameDimensions(string sourcePath)
    {
        IWICBitmapDecoder? decoder = null;
        IWICBitmapFrameDecode? frame = null;
        var factory = WicCodecProbe.CreateImagingFactory();
        try
        {
            factory.CreateDecoderFromFilename(
                sourcePath,
                IntPtr.Zero,
                WicCodecProbe.GenericReadAccess,
                WICDecodeOptions.WICDecodeMetadataCacheOnLoad,
                out decoder);

            decoder.GetFrame(0, out frame);
            frame.GetSize(out var width, out var height);
            var orientation = ReadOrientation(frame);

            return orientation is >= 5 and <= 8
                ? new ImageDimensions((int)height, (int)width)
                : new ImageDimensions((int)width, (int)height);
        }
        finally
        {
            WicCodecProbe.ReleaseComObject(frame);
            WicCodecProbe.ReleaseComObject(decoder);
            WicCodecProbe.ReleaseComObject(factory);
        }
    }

    private static ushort ReadOrientation(IWICBitmapFrameDecode frame)
    {
        IWICMetadataQueryReader? metadataQueryReader = null;
        try
        {
            frame.GetMetadataQueryReader(out metadataQueryReader);

            foreach (var query in new[] { "/ifd/{ushort=274}", "/app1/ifd/{ushort=274}", "/xmp/tiff:Orientation" })
            {
                try
                {
                    metadataQueryReader.GetMetadataByName(query, out var value);
                    try
                    {
                        var orientation = value.GetUInt16OrDefault();
                        if (orientation is >= 1 and <= 8)
                        {
                            return orientation;
                        }
                    }
                    finally
                    {
                        value.Dispose();
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
        finally
        {
            WicCodecProbe.ReleaseComObject(metadataQueryReader);
        }

        return 1;
    }
}
