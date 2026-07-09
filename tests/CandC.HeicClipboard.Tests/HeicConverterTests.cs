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
}
