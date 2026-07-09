using System.Runtime.InteropServices;

namespace CandC.HeicClipboard.Tests;

internal static class LocalHeicSamples
{
    public const string SamplesDirectory = @"H:\Koding\CandC-Samples";

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
            return [];
        }

        return CanDecode(files[0]) ? files : [];
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
