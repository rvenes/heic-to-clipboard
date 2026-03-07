using System.Runtime.InteropServices;

namespace CandC.HeicClipboard;

public static class WicCodecProbe
{
    private static readonly Guid ImagingFactory2Clsid = new("317D06E8-5F24-433D-BDF7-79CE68D8ABC2");
    private static readonly Guid ImagingFactoryClsid = new("CACAF262-9370-4615-A13B-9F5539DA4C0A");
    private static readonly HashSet<int> MissingCodecHResults =
    [
        unchecked((int)0x88982F07),
        unchecked((int)0x88982F50),
        unchecked((int)0x88982F8B)
    ];
    private static readonly Dictionary<int, string> ErrorNames = new()
    {
        [unchecked((int)0x88982F07)] = "WINCODEC_ERR_UNKNOWNIMAGEFORMAT",
        [unchecked((int)0x88982F50)] = "WINCODEC_ERR_COMPONENTNOTFOUND",
        [unchecked((int)0x88982F60)] = "WINCODEC_ERR_BADIMAGE",
        [unchecked((int)0x88982F61)] = "WINCODEC_ERR_BADHEADER",
        [unchecked((int)0x88982F62)] = "WINCODEC_ERR_FRAMEMISSING",
        [unchecked((int)0x88982F72)] = "WINCODEC_ERR_STREAMREAD",
        [unchecked((int)0x88982F8B)] = "WINCODEC_ERR_COMPONENTINITIALIZEFAILURE"
    };

    public static uint GenericReadAccess => 0x80000000u;

    public static Guid PixelFormat32bppBGRA { get; } = new("6FDDC324-4E03-4BFE-B185-3D77768DC90F");

    internal static IWICImagingFactory CreateImagingFactory()
    {
        return CreateImagingFactory(ImagingFactory2Clsid) ?? CreateImagingFactory(ImagingFactoryClsid)
            ?? throw new InvalidOperationException("Could not create the WIC imaging factory.");
    }

    public static bool IsMissingHeifCodec(COMException exception)
    {
        return MissingCodecHResults.Contains(exception.HResult);
    }

    public static string FormatDecodeError(COMException exception)
    {
        var hresult = unchecked((uint)exception.HResult);
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? "Unknown WIC decoder error."
            : exception.Message.Trim();

        if (ErrorNames.TryGetValue(exception.HResult, out var errorName))
        {
            return $"HEIC decode failed ({errorName}, 0x{hresult:X8}): {message}";
        }

        return $"HEIC decode failed (0x{hresult:X8}): {message}";
    }

    public static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static IWICImagingFactory? CreateImagingFactory(Guid clsid)
    {
        try
        {
            var factoryType = Type.GetTypeFromCLSID(clsid, throwOnError: true)
                ?? throw new InvalidOperationException("Could not resolve the WIC imaging factory type.");
            return (IWICImagingFactory?)Activator.CreateInstance(factoryType);
        }
        catch (COMException exception) when ((uint)exception.HResult == 0x80040154u)
        {
            return null;
        }
    }
}

internal enum WICDecodeOptions : uint
{
    WICDecodeMetadataCacheOnDemand = 0,
    WICDecodeMetadataCacheOnLoad = 1
}

internal enum WICBitmapDitherType
{
    WICBitmapDitherTypeNone = 0x00000000
}

internal enum WICBitmapPaletteType
{
    WICBitmapPaletteTypeCustom = 0
}

[ComImport]
[Guid("EC5EC8A9-C395-4314-9C77-54D7A935FF70")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWICImagingFactory
{
    void CreateDecoderFromFilename(
        [MarshalAs(UnmanagedType.LPWStr)] string wzFilename,
        IntPtr pguidVendor,
        uint desiredAccess,
        WICDecodeOptions metadataOptions,
        out IWICBitmapDecoder decoder);

    void CreateDecoderFromStream(IntPtr pIStream, IntPtr pguidVendor, WICDecodeOptions metadataOptions, out IntPtr decoder);

    void CreateDecoderFromFileHandle(IntPtr hFile, IntPtr pguidVendor, WICDecodeOptions metadataOptions, out IntPtr decoder);

    void CreateComponentInfo(ref Guid clsidComponent, out IntPtr componentInfo);

    void CreateDecoder(ref Guid guidContainerFormat, IntPtr pguidVendor, out IntPtr decoder);

    void CreateEncoder(ref Guid guidContainerFormat, IntPtr pguidVendor, out IntPtr encoder);

    void CreatePalette(out IntPtr palette);

    void CreateFormatConverter(out IWICFormatConverter formatConverter);
}

[ComImport]
[Guid("00000120-A8F2-4877-BA0A-FD2B6645FB94")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWICBitmapSource
{
    void GetSize(out uint width, out uint height);

    void GetPixelFormat(out Guid pixelFormat);

    void GetResolution(out double dpiX, out double dpiY);

    void CopyPalette(IntPtr palette);

    void CopyPixels(
        IntPtr prc,
        uint cbStride,
        uint cbBufferSize,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pixels);
}

[ComImport]
[Guid("3B16811B-6A43-4EC9-A813-3D930C13B940")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWICBitmapFrameDecode : IWICBitmapSource
{
    new void GetSize(out uint width, out uint height);

    new void GetPixelFormat(out Guid pixelFormat);

    new void GetResolution(out double dpiX, out double dpiY);

    new void CopyPalette(IntPtr palette);

    new void CopyPixels(
        IntPtr prc,
        uint cbStride,
        uint cbBufferSize,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pixels);

    void GetMetadataQueryReader(out IWICMetadataQueryReader metadataQueryReader);

    void GetColorContexts(uint colorContextCount, IntPtr colorContexts, out uint actualCount);

    void GetThumbnail(out IntPtr thumbnail);
}

[ComImport]
[Guid("00000301-A8F2-4877-BA0A-FD2B6645FB94")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWICFormatConverter : IWICBitmapSource
{
    new void GetSize(out uint width, out uint height);

    new void GetPixelFormat(out Guid pixelFormat);

    new void GetResolution(out double dpiX, out double dpiY);

    new void CopyPalette(IntPtr palette);

    new void CopyPixels(
        IntPtr prc,
        uint cbStride,
        uint cbBufferSize,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pixels);

    void Initialize(
        [MarshalAs(UnmanagedType.Interface)] IWICBitmapSource source,
        ref Guid destinationFormat,
        WICBitmapDitherType dither,
        [MarshalAs(UnmanagedType.Interface)] object? palette,
        double alphaThresholdPercent,
        WICBitmapPaletteType paletteTranslate);

    void CanConvert(ref Guid sourcePixelFormat, ref Guid destinationPixelFormat, out bool canConvert);
}

[ComImport]
[Guid("9EDDE9E7-8DEE-47EA-99DF-E6FAF2ED44BF")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWICBitmapDecoder
{
    void QueryCapability(IntPtr pIStream, out uint capability);

    void Initialize(IntPtr pIStream, WICDecodeOptions cacheOptions);

    void GetContainerFormat(out Guid guidContainerFormat);

    void GetDecoderInfo(out IntPtr decoderInfo);

    void CopyPalette(IntPtr palette);

    void GetMetadataQueryReader(out IWICMetadataQueryReader metadataQueryReader);

    void GetPreview(out IntPtr bitmapSource);

    void GetColorContexts(uint colorContextCount, IntPtr colorContexts, out uint actualCount);

    void GetThumbnail(out IntPtr bitmapSource);

    void GetFrameCount(out uint count);

    void GetFrame(uint index, out IWICBitmapFrameDecode bitmapFrame);
}

[ComImport]
[Guid("30989668-E1C9-4597-B395-458EEDB808DF")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWICMetadataQueryReader
{
    void GetContainerFormat(out Guid guidContainerFormat);

    void GetLocation(uint characterCount, [MarshalAs(UnmanagedType.LPWStr)] string location, out uint actualCount);

    void GetMetadataByName([MarshalAs(UnmanagedType.LPWStr)] string wzName, out PropVariant value);

    void GetEnumerator(out IntPtr enumerator);
}

[StructLayout(LayoutKind.Explicit)]
internal struct PropVariantUnion
{
    [FieldOffset(0)]
    public byte ByteValue;

    [FieldOffset(0)]
    public ushort UInt16Value;

    [FieldOffset(0)]
    public uint UInt32Value;

    [FieldOffset(0)]
    public IntPtr StringPointer;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant : IDisposable
{
    private readonly ushort _variantType;
    private readonly ushort _reserved1;
    private readonly ushort _reserved2;
    private readonly ushort _reserved3;
    private readonly PropVariantUnion _value;

    public ushort GetUInt16OrDefault()
    {
        return (VarEnum)_variantType switch
        {
            VarEnum.VT_UI1 => _value.ByteValue,
            VarEnum.VT_UI2 => _value.UInt16Value,
            VarEnum.VT_UI4 => (ushort)Math.Min(_value.UInt32Value, ushort.MaxValue),
            VarEnum.VT_LPWSTR when ushort.TryParse(Marshal.PtrToStringUni(_value.StringPointer), out var parsed) => parsed,
            _ => (ushort)1
        };
    }

    public void Dispose()
    {
        PropVariantClear(ref this);
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);
}
