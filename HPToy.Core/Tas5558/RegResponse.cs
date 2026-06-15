namespace HPToy.Core.Tas5558;

public sealed class RegResponse
{
    public RegRequest Req { get; }
    public byte[] Data { get; }

    public RegResponse(byte[] bin)
    {
        if (bin.Length < 20)
        {
            throw new Exception("DspRegResponse init error.");
        }
        Req = new RegRequest(bin);
        Data = bin.AsSpan(3, 17).ToArray();
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder("Register " + Req.Addr +
                                                ": range [" + Req.From + ", " + Req.To + "]:");

        for (var i = Req.From; i < Req.To; i++)
        {
            sb.Append(' ').Append(Data[i - Req.From]);
        }

        return sb.ToString();
    }
}
