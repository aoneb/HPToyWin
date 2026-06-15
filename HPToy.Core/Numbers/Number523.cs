using System.Buffers.Binary;

namespace HPToy.Core.Numbers;

public static class Number523
{
    private static float GetMaxFloatFor523()
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(b, 16.0f);
        var temp = BinaryPrimitives.ReadInt32LittleEndian(b) - 1;
        BinaryPrimitives.WriteInt32LittleEndian(b, temp);
        return BinaryPrimitives.ReadSingleLittleEndian(b);
    }

    private static float GetMinFloatFor523() => -16.0f;

    public static float CheckRange(float num)
    {
        var max = GetMaxFloatFor523();
        var min = GetMinFloatFor523();
        if (num > max) num = max;
        if (num < min) num = min;
        return num;
    }

    public static byte[] Get523LittleEnd(float num)
    {
        num = CheckRange(num);
        var n = (int)(num * 0x800000);
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, n);
        return buf;
    }

    public static byte[] Get523BigEnd(float num)
    {
        num = CheckRange(num);
        var n = (int)(num * 0x800000);
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, n);
        return buf;
    }

    public static float ToFloat(byte[] buf, bool bigEndian = true)
    {
        if (buf.Length != 4) return 0.0f;

        var data = (byte[])buf.Clone();
        var headIndex = bigEndian ? 0 : 3;

        if ((data[headIndex] & 0x80) != 0)
        {
            data[headIndex] |= 0xF0;
        }

        var num = bigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(data)
            : BinaryPrimitives.ReadInt32LittleEndian(data);
        return (float)num / 0x800000;
    }
}
