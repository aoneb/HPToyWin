namespace HPToy.Core.Ble;

public static class CommonCommand
{
    public const byte Length = 5;

    public const byte EstablishPair = 0x00;
    public const byte SetPairCode = 0x01;
    public const byte SetWriteFlag = 0x02;
    public const byte GetWriteFlag = 0x03;
    public const byte GetVersion = 0x04;
    public const byte GetChecksum = 0x05;
    public const byte InitDsp = 0x06;
    public const byte SetAudioSource = 0x07;
    public const byte GetAudioSource = 0x08;
    public const byte GetEnergyConfig = 0x09;
    public const byte SetAdvertiseMode = 0x0A;
    public const byte GetAdvertiseMode = 0x0B;
    public const byte SetTas5558Ch3Mixer = 0x0C;
    public const byte GetTas5558Ch3Mixer = 0x0D;
    public const byte SetOutputMode = 0x0E;
    public const byte GetOutputMode = 0x0F;

    public const byte ClipDetection = 0xFD;
    public const byte OtwDetection = 0xFE;
    public const byte ParamConnectionEnabled = 0xFF;
}
