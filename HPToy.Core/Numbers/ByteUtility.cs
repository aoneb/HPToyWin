using System.Buffers.Binary;
using System.Globalization;

namespace HPToy.Core.Numbers;

public static class ByteUtility
{
    private static readonly char[] HexArray = "0123456789ABCDEF".ToCharArray();

    public static string ToString(byte b)
    {
        Span<byte> bb = stackalloc byte[2];
        bb[0] = b;
        bb[1] = 0;
        var s = BinaryPrimitives.ReadInt16LittleEndian(bb);
        return s.ToString(CultureInfo.InvariantCulture);
    }

    public static string ToString(byte[] b)
    {
        var s = "";
        for (var i = 0; i < b.Length; i++)
        {
            s += string.Format(CultureInfo.InvariantCulture, "{0:x} ", b[i]);
            if (i % 4 == 3) s += "\n";
        }
        return s;
    }

    public static string ToHexString(byte b) =>
        string.Format(CultureInfo.InvariantCulture, "{0}{1}", HexArray[(b >> 4) & 0x0F], HexArray[b & 0xF]);

    public static string ToBinString(byte b)
    {
        var sb = new System.Text.StringBuilder();
        var num = ByteToInt(b);

        for (var i = 0; i < 8; i++)
        {
            if (i == 4) sb.Append(' ');
            sb.Append(num % 2 != 0 ? '1' : '0');
            num /= 2;
        }

        var result = sb.ToString();
        return string.Concat(result.Reverse());
    }

    public static byte Parse(string s)
    {
        try
        {
            var num = short.Parse(s, CultureInfo.InvariantCulture);
            if (num >= 0 && num < 256)
            {
                return (byte)num;
            }
        }
        catch (FormatException)
        {
        }

        return 0;
    }

    public static int ByteToInt(byte b)
    {
        Span<byte> buf = stackalloc byte[4];
        buf[0] = b;
        buf[1] = 0;
        buf[2] = 0;
        buf[3] = 0;
        return BinaryPrimitives.ReadInt32LittleEndian(buf);
    }

    public static byte GetIntLsb(int i)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, i);
        return buf[0];
    }
}
