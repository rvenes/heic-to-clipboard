using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CandC.HeicClipboard;

public sealed class HeicConverter : IImageConverter
{
    private static readonly ImageCodecInfo JpegCodec = ImageCodecInfo.GetImageEncoders()
        .Single(static codec => codec.FormatID == ImageFormat.Jpeg.Guid);

    private readonly TempFileManager _tempFileManager;
    private readonly HeicConversionOptions _conversionOptions;

    public HeicConverter(TempFileManager tempFileManager, HeicConversionOptions conversionOptions)
    {
        _tempFileManager = tempFileManager;
        _conversionOptions = conversionOptions;
    }

    public ConversionResult Convert(string sourcePath)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return ConversionResult.Failed(sourcePath, "File not found.");
            }

            using var sourceBitmap = LoadSourceBitmap(sourcePath);
            using var dimensionCappedBitmap = TryCreateDimensionCappedBitmap(sourceBitmap, _conversionOptions);
            var baseBitmap = dimensionCappedBitmap ?? sourceBitmap;

            var maximumBytes = _conversionOptions.MaximumBytes;
            var qualitySteps = JpegEncodingPlanner.CreateQualitySteps(_conversionOptions.InitialJpegQuality);
            var floorQuality = qualitySteps[^1];
            long encodedBytes = 0;

            // Phase 1: walk the complete quality ladder at full scale, so the result
            // always uses the highest quality that fits. Full-scale attempts go through
            // the same candidate normalization (24bpp, white-composited) as scaled ones.
            using (var fullScaleBitmap = CreateCandidateBitmap(baseBitmap, 100))
            {
                foreach (var quality in qualitySteps)
                {
                    using var encodedStream = EncodeJpeg(fullScaleBitmap, quality);
                    encodedBytes = encodedStream.Length;
                    if (encodedBytes <= maximumBytes)
                    {
                        return SaveEncodedJpeg(sourcePath, encodedStream);
                    }
                }
            }

            // Phase 2: downscale at floor quality, scale estimated from the last size.
            var scalePercent = 100;
            for (var attempt = 0; attempt < JpegEncodingPlanner.MaxScaleAttempts; attempt++)
            {
                var nextScalePercent = JpegEncodingPlanner.EstimateNextScalePercent(scalePercent, encodedBytes, maximumBytes);
                if (nextScalePercent is null)
                {
                    break;
                }

                scalePercent = nextScalePercent.Value;
                using var candidateBitmap = CreateCandidateBitmap(baseBitmap, scalePercent);
                using var encodedStream = EncodeJpeg(candidateBitmap, floorQuality);
                encodedBytes = encodedStream.Length;
                if (encodedBytes <= maximumBytes)
                {
                    return SaveEncodedJpeg(sourcePath, encodedStream);
                }
            }

            return ConversionResult.Failed(sourcePath, _conversionOptions.SizeLimitExceededMessage);
        }
        catch (COMException exception) when (WicCodecProbe.IsMissingHeifCodec(exception))
        {
            // WIC raises COMPONENTNOTFOUND both when the HEIF codec is absent and
            // when no codec recognizes the file content, so check the file header
            // before telling the user to install the codec.
            return ConversionResult.Failed(
                sourcePath,
                HeifSignature.FileLooksLikeHeif(sourcePath)
                    ? AppConstants.MissingHeifSupportMessage
                    : "Not a valid HEIC/HEIF file (unrecognized file header).");
        }
        catch (COMException exception)
        {
            return ConversionResult.Failed(sourcePath, WicCodecProbe.FormatDecodeError(exception));
        }
        catch (Exception exception)
        {
            return ConversionResult.Failed(sourcePath, exception.Message);
        }
    }

    private ConversionResult SaveEncodedJpeg(string sourcePath, MemoryStream encodedStream)
    {
        var outputPath = _tempFileManager.CreateOutputPath(sourcePath);
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        if (encodedStream.TryGetBuffer(out var buffer))
        {
            fileStream.Write(buffer.Array!, buffer.Offset, buffer.Count);
        }
        else
        {
            encodedStream.Position = 0;
            encodedStream.CopyTo(fileStream);
        }

        return ConversionResult.Succeeded(sourcePath, outputPath);
    }

    // Returns null when no resize is needed, so the caller keeps using the source
    // bitmap directly instead of paying for a full-size clone.
    private static Bitmap? TryCreateDimensionCappedBitmap(Bitmap sourceBitmap, HeicConversionOptions options)
    {
        if (options.KeepOriginalResolution)
        {
            return null;
        }

        var fitted = ImageResizePlanner.FitWithinLongestSide(sourceBitmap.Width, sourceBitmap.Height, options.MaxLongestSidePx);
        if (fitted.Width == sourceBitmap.Width && fitted.Height == sourceBitmap.Height)
        {
            return null;
        }

        return ResizeBitmap(sourceBitmap, fitted.Width, fitted.Height);
    }

    private static Bitmap LoadSourceBitmap(string sourcePath)
    {
        IWICImagingFactory? factory = null;
        IWICBitmapDecoder? decoder = null;
        IWICBitmapFrameDecode? frame = null;
        IWICFormatConverter? formatConverter = null;
        var colorResources = new List<object>();

        try
        {
            factory = WicCodecProbe.CreateImagingFactory();
            factory.CreateDecoderFromFilename(
                sourcePath,
                IntPtr.Zero,
                WicCodecProbe.GenericReadAccess,
                WICDecodeOptions.WICDecodeMetadataCacheOnLoad,
                out decoder);

            decoder.GetFrame(0, out frame);
            var orientation = GetOrientation(frame);

            // Convert embedded color profiles (e.g. iPhone Display P3) to sRGB, so
            // consumers that treat untagged JPEG as sRGB show correct colors. Any
            // failure falls back to the plain format-converter path used before.
            IWICBitmapSource pixelSource;
            var transformedSource = TryCreateSrgbTransformedSource(factory, frame, colorResources);
            if (transformedSource is not null)
            {
                pixelSource = transformedSource;
            }
            else
            {
                factory.CreateFormatConverter(out formatConverter);
                var targetPixelFormat = WicCodecProbe.PixelFormat32bppBGRA;
                formatConverter.Initialize(
                    frame,
                    ref targetPixelFormat,
                    WICBitmapDitherType.WICBitmapDitherTypeNone,
                    null,
                    0d,
                    WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
                pixelSource = formatConverter;
            }

            var bitmap = ConvertToBitmap(pixelSource);
            try
            {
                ApplyOrientation(bitmap, orientation);
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        finally
        {
            foreach (var colorResource in colorResources)
            {
                WicCodecProbe.ReleaseComObject(colorResource);
            }

            WicCodecProbe.ReleaseComObject(formatConverter);
            WicCodecProbe.ReleaseComObject(frame);
            WicCodecProbe.ReleaseComObject(decoder);
            WicCodecProbe.ReleaseComObject(factory);
        }
    }

    // Returns a bitmap source that outputs sRGB pixels, or null when the file has no
    // usable embedded profile (or the transform cannot be built), in which case the
    // caller must use the ordinary format-converter path. Created COM objects are
    // added to colorResources; the caller releases them after the pixels are copied.
    private static IWICBitmapSource? TryCreateSrgbTransformedSource(
        IWICImagingFactory factory,
        IWICBitmapFrameDecode frame,
        List<object> colorResources)
    {
        try
        {
            frame.GetColorContexts(0, null, out var contextCount);
            if (contextCount == 0)
            {
                return null;
            }

            var colorContexts = new IWICColorContext[contextCount];
            for (var index = 0; index < colorContexts.Length; index++)
            {
                factory.CreateColorContext(out colorContexts[index]);
                colorResources.Add(colorContexts[index]);
            }

            frame.GetColorContexts(contextCount, colorContexts, out _);

            IWICColorContext? sourceContext = null;
            foreach (var colorContext in colorContexts)
            {
                colorContext.GetType(out var contextType);
                if (contextType == WICColorContextType.WICColorContextProfile)
                {
                    sourceContext = colorContext;
                    break;
                }

                if (contextType == WICColorContextType.WICColorContextExifColorSpace)
                {
                    colorContext.GetExifColorSpace(out var exifColorSpace);
                    if (exifColorSpace == 1)
                    {
                        // Already tagged as sRGB; the ordinary path is correct as-is.
                        return null;
                    }
                }
            }

            if (sourceContext is null)
            {
                return null;
            }

            factory.CreateColorContext(out var srgbContext);
            colorResources.Add(srgbContext);
            srgbContext.InitializeFromExifColorSpace(1);

            factory.CreateColorTransformer(out var colorTransform);
            colorResources.Add(colorTransform);

            var targetPixelFormat = WicCodecProbe.PixelFormat32bppBGRA;
            colorTransform.Initialize(frame, sourceContext, srgbContext, ref targetPixelFormat);
            return colorTransform;
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static Bitmap ConvertToBitmap(IWICBitmapSource source)
    {
        source.GetSize(out var width, out var height);
        var stride = checked((int)width * 4);
        var pixels = new byte[checked(stride * (int)height)];
        source.CopyPixels(IntPtr.Zero, (uint)stride, (uint)pixels.Length, pixels);

        var bitmap = new Bitmap((int)width, (int)height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            for (var row = 0; row < bitmap.Height; row++)
            {
                var sourceOffset = row * stride;
                var destinationOffset = data.Scan0 + (row * data.Stride);
                Marshal.Copy(pixels, sourceOffset, destinationOffset, stride);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static ushort GetOrientation(IWICBitmapFrameDecode frame)
    {
        IWICMetadataQueryReader? metadataQueryReader = null;

        try
        {
            frame.GetMetadataQueryReader(out metadataQueryReader);
        }
        catch (COMException)
        {
            return 1;
        }

        try
        {
            var queries = new[]
            {
                "/ifd/{ushort=274}",
                "/app1/ifd/{ushort=274}",
                "/xmp/tiff:Orientation"
            };

            foreach (var query in queries)
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
                catch (COMException)
                {
                }
            }

            return 1;
        }
        finally
        {
            WicCodecProbe.ReleaseComObject(metadataQueryReader);
        }
    }

    private static void ApplyOrientation(Image image, ushort orientation)
    {
        var rotateFlipType = orientation switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };

        image.RotateFlip(rotateFlipType);
    }

    public static Bitmap CreateCandidateBitmap(Bitmap sourceBitmap, int scalePercent)
    {
        var width = Math.Max(1, (int)Math.Round(sourceBitmap.Width * (scalePercent / 100d)));
        var height = Math.Max(1, (int)Math.Round(sourceBitmap.Height * (scalePercent / 100d)));

        return ResizeBitmap(sourceBitmap, width, height);
    }

    private static Bitmap ResizeBitmap(Bitmap sourceBitmap, int width, int height)
    {
        var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        bitmap.SetResolution(sourceBitmap.HorizontalResolution, sourceBitmap.VerticalResolution);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.White);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        using var imageAttributes = new ImageAttributes();
        imageAttributes.SetWrapMode(WrapMode.TileFlipXY);

        graphics.DrawImage(
            sourceBitmap,
            new Rectangle(0, 0, width, height),
            0,
            0,
            sourceBitmap.Width,
            sourceBitmap.Height,
            GraphicsUnit.Pixel,
            imageAttributes);

        return bitmap;
    }

    private static MemoryStream EncodeJpeg(Image image, int quality)
    {
        var stream = new MemoryStream();
        using var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        image.Save(stream, JpegCodec, encoderParameters);
        stream.Position = 0;
        return stream;
    }
}
