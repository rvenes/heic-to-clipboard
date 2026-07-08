using System.IO;

namespace CandC.HeicClipboard;

public static class HeifSignature
{
    private static readonly byte[][] HeifBrands =
    [
        "heic"u8.ToArray(),
        "heix"u8.ToArray(),
        "hevc"u8.ToArray(),
        "heim"u8.ToArray(),
        "heis"u8.ToArray(),
        "hevm"u8.ToArray(),
        "hevs"u8.ToArray(),
        "mif1"u8.ToArray(),
        "msf1"u8.ToArray(),
        "heif"u8.ToArray()
    ];

    public static bool FileLooksLikeHeif(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[64];
            var bytesRead = stream.Read(header, 0, header.Length);
            return LooksLikeHeif(header.AsSpan(0, bytesRead));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool LooksLikeHeif(ReadOnlySpan<byte> header)
    {
        if (header.Length < 12 || !header.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return false;
        }

        // Brands sit at offset 8 (major) and from 16 (compatible list);
        // offset 12 is the minor version and must be skipped.
        for (var offset = 8; offset + 4 <= header.Length; offset += 4)
        {
            if (offset == 12)
            {
                continue;
            }

            foreach (var brand in HeifBrands)
            {
                if (header.Slice(offset, 4).SequenceEqual(brand))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
