using System.Runtime.InteropServices;

namespace CandC.HeicClipboard.Tests;

public sealed class PropVariantTests
{
    [Fact]
    public void Layout_MatchesNativePropVariant()
    {
        Assert.Equal(IntPtr.Size == 8 ? 24 : 16, Marshal.SizeOf<PropVariant>());
        Assert.Equal(IntPtr.Size == 8 ? 16 : 8, Marshal.SizeOf<PropVariantUnion>());
        Assert.Equal(8, Marshal.OffsetOf<PropVariant>("_value").ToInt32());
        Assert.Equal(IntPtr.Size, Marshal.OffsetOf<PropVariantCountedArray>(nameof(PropVariantCountedArray.Elements)).ToInt32());
    }

    [Fact]
    public void NativeMetadataValueCopy_PreservesAdjacentMemoryAndReadsValue()
    {
        // Reserve a full native source and a canary after the managed destination.
        // The former undersized definition overwrote this canary on x64.
        var source = Marshal.AllocHGlobal(24);
        try
        {
            Marshal.Copy(new byte[24], 0, source, 24);
            Marshal.WriteInt16(source, (short)VarEnum.VT_UI2);
            Marshal.WriteInt16(source, 8, 6);
            var destination = new GuardedVariant { Canary = 0x1122334455667788 };

            Marshal.ThrowExceptionForHR(PropVariantCopy(ref destination, source));

            try
            {
                Assert.Equal(0x1122334455667788UL, destination.Canary);
                Assert.Equal((ushort)6, destination.Value.GetUInt16OrDefault());
            }
            finally
            {
                destination.Value.Dispose();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(source);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuardedVariant
    {
        public PropVariant Value;
        public ulong Canary;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantCopy(ref GuardedVariant destination, IntPtr source);
}
