using System.Buffers.Binary;

namespace HPToy.Core.Numbers;

public static class Number88
{
    public static byte[] Get88LittleEnd(float num)
    {
        var n = (short)(num * 0x100);
        var buf = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buf, n);
        return buf;
    }

    public static byte[] Get88BigEnd(float num)
    {
        var n = (short)(num * 0x100);
        var buf = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buf, n);
        return buf;
    }

    public static float ToFloat(ReadOnlySpan<byte> buf, bool littleEndian = true)
    {
        var num = littleEndian
            ? BinaryPrimitives.ReadInt16LittleEndian(buf)
            : BinaryPrimitives.ReadInt16BigEndian(buf);
        return (float)num / 0x100;
    }
}
