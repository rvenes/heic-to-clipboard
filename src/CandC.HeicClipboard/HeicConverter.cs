using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CandC.HeicClipboard;

public sealed class HeicConverter
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
            using var baseBitmap = ApplyDimensionCap(sourceBitmap, _conversionOptions);
            foreach (var attempt in JpegEncodingPlanner.CreateAttempts(_conversionOptions.InitialJpegQuality))
            {
                using var candidateBitmap = CreateCandidateBitmap(baseBitmap, attempt.ScalePercent);
                using var encodedStream = EncodeJpeg(candidateBitmap, attempt.Quality);

                if (encodedStream.Length > _conversionOptions.MaximumBytes)
                {
                    continue;
                }

                var outputPath = _tempFileManager.CreateOutputPath(sourcePath);
                File.WriteAllBytes(outputPath, encodedStream.ToArray());
                return ConversionResult.Succeeded(sourcePath, outputPath);
            }

            return ConversionResult.Failed(sourcePath, "Could not keep the JPEG under 9.8 MB.");
        }
        catch (COMException exception) when (WicCodecProbe.IsMissingHeifCodec(exception))
        {
            return ConversionResult.Failed(sourcePath, AppConstants.MissingHeifSupportMessage);
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

    private static Bitmap ApplyDimensionCap(Bitmap sourceBitmap, HeicConversionOptions options)
    {
        if (options.KeepOriginalResolution)
        {
            return (Bitmap)sourceBitmap.Clone();
        }

        var fitted = ImageResizePlanner.FitWithinLongestSide(sourceBitmap.Width, sourceBitmap.Height, options.MaxLongestSidePx);
        if (fitted.Width == sourceBitmap.Width && fitted.Height == sourceBitmap.Height)
        {
            return (Bitmap)sourceBitmap.Clone();
        }

        return ResizeBitmap(sourceBitmap, fitted.Width, fitted.Height);
    }

    private static Bitmap LoadSourceBitmap(string sourcePath)
    {
        IWICImagingFactory? factory = null;
        IWICBitmapDecoder? decoder = null;
        IWICBitmapFrameDecode? frame = null;
        IWICFormatConverter? formatConverter = null;

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
            factory.CreateFormatConverter(out formatConverter);

            var targetPixelFormat = WicCodecProbe.PixelFormat32bppBGRA;
            formatConverter.Initialize(
                frame,
                ref targetPixelFormat,
                WICBitmapDitherType.WICBitmapDitherTypeNone,
                null,
                0d,
                WICBitmapPaletteType.WICBitmapPaletteTypeCustom);

            var orientation = GetOrientation(frame);
            using var bitmap = ConvertToBitmap(formatConverter);
            ApplyOrientation(bitmap, orientation);
            return (Bitmap)bitmap.Clone();
        }
        finally
        {
            WicCodecProbe.ReleaseComObject(formatConverter);
            WicCodecProbe.ReleaseComObject(frame);
            WicCodecProbe.ReleaseComObject(decoder);
            WicCodecProbe.ReleaseComObject(factory);
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

    private static Bitmap CreateCandidateBitmap(Bitmap sourceBitmap, int scalePercent)
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
