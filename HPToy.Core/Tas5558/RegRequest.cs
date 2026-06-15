namespace HPToy.Core.Tas5558;

public sealed class RegRequest
{
    public byte Addr { get; }
    public byte From { get; }
    public byte To { get; }

    public RegRequest(byte addr, byte from, byte to)
    {
        Addr = addr;
        From = from;
        To = to;
    }

    public RegRequest(byte[] bin)
    {
        if (bin.Length < 3)
        {
            throw new Exception("DspRegRequest init error.");
        }
        Addr = bin[0];
        From = bin[1];
        To = bin[2];
    }

    public byte[] GetBinary() => new[] { Addr, From, To };
}
