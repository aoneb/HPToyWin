namespace HPToy.Core.Ble;

public sealed class BlePacket
{
    public byte[] Data { get; }
    public bool Response { get; }

    public BlePacket(byte[] data, bool response)
    {
        Data = data;
        Response = response;
    }

    public BlePacket(byte[] value, int length, bool response)
    {
        // Match Android Arrays.copyOf(value, length): pad with zeros when shorter.
        Data = new byte[length];
        if (value.Length > 0)
        {
            Array.Copy(value, 0, Data, 0, Math.Min(value.Length, length));
        }
        Response = response;
    }
}
