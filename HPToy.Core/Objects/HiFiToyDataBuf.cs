namespace HPToy.Core.Objects;

public sealed class HiFiToyDataBuf
{
    private byte _addr;
    private byte[]? _data;

    public HiFiToyDataBuf(byte addr, byte[] data)
    {
        _addr = addr;
        _data = data;
    }

    public HiFiToyDataBuf(byte[] b)
    {
        ParseBinary(b);
    }

    public void SetAddr(byte addr) => _addr = addr;
    public byte GetAddr() => _addr;
    public byte GetLength() => _data == null ? (byte)0 : (byte)_data.Length;
    public void SetData(byte[] data) => _data = data;
    public byte[] GetData() => _data ?? Array.Empty<byte>();

    public byte[] GetBinary()
    {
        var length = GetLength();
        var b = new byte[2 + length];
        b[0] = _addr;
        b[1] = length;
        if (length > 0 && _data != null)
        {
            Array.Copy(_data, 0, b, 2, length);
        }
        return b;
    }

    public bool ParseBinary(byte[] b)
    {
        var full = true;
        if (b.Length < 2) return false;

        _addr = b[0];
        var length = b[1];
        if (b.Length < length + 2)
        {
            full = false;
            length = (byte)(b.Length - 2);
        }

        _data = new byte[length];
        Array.Copy(b, 2, _data, 0, length);
        return full;
    }
}
