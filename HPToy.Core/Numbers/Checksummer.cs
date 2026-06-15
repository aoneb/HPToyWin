using System.Buffers.Binary;

namespace HPToy.Core.Numbers;

public static class Checksummer
{
    public static short Calc(byte[] data)
    {
        byte sum = 0;
        byte fibonacci = 0;

        for (var i = 0; i < data.Length; i++)
        {
            sum += data[i];
            fibonacci += sum;
        }

        short checkSum = (short)(sum & 0xFF);
        checkSum |= (short)((fibonacci << 8) & 0xFF00);
        return checkSum;
    }

    public static short SubtractData(short checksum, int originalLength, byte[] data)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(b, checksum);
        var sum = b[0];
        var fib = b[1];

        for (var i = 0; i < data.Length; i++)
        {
            sum -= data[i];
            fib -= (byte)(data[i] * (originalLength - i));
        }

        short checkSum = (short)(sum & 0xFF);
        checkSum |= (short)((fib << 8) & 0xFF00);
        return checkSum;
    }
}
