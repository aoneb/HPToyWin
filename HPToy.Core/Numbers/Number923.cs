using System.Buffers.Binary;

namespace HPToy.Core.Numbers;

public static class Number923
{
    public static byte[] Get923LittleEnd(float num)
    {
        var n = (int)(num * 0x800000);
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, n);
        return buf;
    }

    public static byte[] Get923BigEnd(float num)
    {
        var n = (int)(num * 0x800000);
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, n);
        return buf;
    }

    public static float ToFloat(ReadOnlySpan<byte> buf, bool bigEndian = true)
    {
        var num = bigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(buf)
            : BinaryPrimitives.ReadInt32LittleEndian(buf);
        return (float)num / 0x800000;
    }
}
