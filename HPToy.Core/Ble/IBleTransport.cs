namespace HPToy.Core.Ble;

public interface IBleTransport
{
    bool IsConnected { get; }
    bool IsConnectionReady { get; }

    void SendDataToDsp(byte[] data, bool response);
    void SendDataToDsp(BlePacket packet);
    void GetDspDataWithOffset(short offset);
    void SendWriteFlag(byte writeFlag);
    void SetInitDsp();
    void SendBufToDsp(short offsetInDspData, byte[] data);

    event Action<int>? WriteProgress;
    event Action? WriteCompleted;
    event Action<byte[]>? ParamDataReceived;
}
