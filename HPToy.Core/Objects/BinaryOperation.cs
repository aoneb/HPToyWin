using System.Buffers.Binary;

namespace HPToy.Core.Objects;

public static class BinaryOperation
{
    public static byte[] ConcatData(byte[] data, byte[] appendData)
    {
        var concatData = new byte[data.Length + appendData.Length];
        Array.Copy(data, 0, concatData, 0, data.Length);
        Array.Copy(appendData, 0, concatData, data.Length, appendData.Length);
        return concatData;
    }

    public static float[] ConcatData(float[] data, float[] appendData)
    {
        var concatData = new float[data.Length + appendData.Length];
        Array.Copy(data, 0, concatData, 0, data.Length);
        Array.Copy(appendData, 0, concatData, data.Length, appendData.Length);
        return concatData;
    }

    public static byte[] ConcatData(byte[] data, byte[] appendData, int appendOffset, int appendCount)
    {
        var slice = new byte[appendCount];
        Array.Copy(appendData, appendOffset, slice, 0, appendCount);
        return ConcatData(data, slice);
    }

    public static byte[] ConcatData(byte[] data, short shortData, bool littleEndian = true)
    {
        var concatData = new byte[data.Length + 2];
        Array.Copy(data, 0, concatData, 0, data.Length);
        if (littleEndian)
        {
            BinaryPrimitives.WriteInt16LittleEndian(concatData.AsSpan(data.Length), shortData);
        }
        else
        {
            BinaryPrimitives.WriteInt16BigEndian(concatData.AsSpan(data.Length), shortData);
        }
        return concatData;
    }

    public static byte[] ConcatData(byte[] data, byte byteData)
    {
        var concatData = new byte[data.Length + 1];
        Array.Copy(data, 0, concatData, 0, data.Length);
        concatData[data.Length] = byteData;
        return concatData;
    }

    public static byte[]? GetBinary(List<HiFiToyDataBuf>? dataBufs)
    {
        if (dataBufs == null) return null;

        byte[] data = Array.Empty<byte>();
        foreach (var buf in dataBufs)
        {
            data = ConcatData(data, buf.GetBinary());
        }
        return data;
    }

    public static byte[] CopyOfRange(byte[] buf, int from, int to)
    {
        if (from > buf.Length) from = buf.Length - 1;
        if (to > buf.Length) to = buf.Length;
        var length = to - from;
        var result = new byte[length];
        Array.Copy(buf, from, result, 0, length);
        return result;
    }
}
