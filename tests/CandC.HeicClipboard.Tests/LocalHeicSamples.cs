using System.Runtime.InteropServices;

namespace CandC.HeicClipboard.Tests;

internal static class LocalHeicSamples
{
    private static string? ExplicitSamplesDirectory => Environment.GetEnvironmentVariable("HEICTOCLIPBOARD_TEST_SAMPLES");

    public static string SamplesDirectory => ExplicitSamplesDirectory ?? @"H:\Koding\CandC-Samples";

    public static IReadOnlyList<string> GetFiles()
    {
        if (!Directory.Exists(SamplesDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(SamplesDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(static path =>
            {
                var extension = Path.GetExtension(path);
                return extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Returns the sample files only when this machine can actually decode them with
    /// WIC; empty when the folder is missing or the HEIF codec is not installed
    /// (e.g. CI runners without HEIF Image Extensions), so sample-gated tests skip
    /// safely instead of throwing from inside the test body.
    /// </summary>
    public static IReadOnlyList<string> GetDecodableFiles()
    {
        var files = GetFiles();
        if (files.Count == 0)
        {
            if (ExplicitSamplesDirectory is not null)
            {
                throw new InvalidOperationException("The explicitly configured HEIC sample directory contains no samples.");
            }
            return [];
        }

        if (CanDecode(files[0]))
        {
            return files;
        }

        if (ExplicitSamplesDirectory is not null)
        {
            throw new InvalidOperationException("The explicitly configured HEIC samples cannot be decoded on this machine.");
        }

        return [];
    }

    private static bool CanDecode(string sourcePath)
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
            return true;
        }
        catch (COMException)
        {
            // Typically WINCODEC_ERR_COMPONENTNOTFOUND: no HEIF codec present.
            return false;
        }
        finally
        {
            WicCodecProbe.ReleaseComObject(frame);
            WicCodecProbe.ReleaseComObject(decoder);
            WicCodecProbe.ReleaseComObject(factory);
        }
    }
}
