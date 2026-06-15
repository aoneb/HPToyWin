using System.Buffers.Binary;
using System.Globalization;
using HPToy.Core.Ble;
using HPToy.Core.Numbers;
using HPToy.Core.Tas5558;
using HPToy.Core.Xml;
using static HPToy.Core.Objects.BassTrebleChannel.BassFreq;
using static HPToy.Core.Objects.BassTrebleChannel.BassTrebleCh;
using static HPToy.Core.Objects.BassTrebleChannel.TrebleFreq;

namespace HPToy.Core.Objects;

public sealed class BassTreble : IHiFiToyObject, ICloneable
{
    private float[] _enabledCh = new float[8];

    private BassTrebleChannel _bassTreble127;
    private BassTrebleChannel _bassTreble34;
    private BassTrebleChannel _bassTreble56;
    private BassTrebleChannel _bassTreble8;

    public BassTreble()
    {
        for (byte i = 0; i < 8; i++)
        {
            SetEnabledChannel(i, 0.0f);
        }

        _bassTreble127 = new BassTrebleChannel(BASS_TREBLE_CH_127,
            BASS_FREQ_125, 0,
            TREBLE_FREQ_9000, 0,
            12, unchecked((byte)-12), 12, unchecked((byte)-12));
        _bassTreble34 = new BassTrebleChannel(BASS_TREBLE_CH_34);
        _bassTreble56 = new BassTrebleChannel(BASS_TREBLE_CH_56);
        _bassTreble8 = new BassTrebleChannel(BASS_TREBLE_CH_8);
    }

    public BassTreble(BassTrebleChannel? bassTreble127, BassTrebleChannel? bassTreble34,
        BassTrebleChannel? bassTreble56, BassTrebleChannel? bassTreble8) : this()
    {
        if (bassTreble127 != null) _bassTreble127 = bassTreble127;
        if (bassTreble34 != null) _bassTreble34 = bassTreble34;
        if (bassTreble56 != null) _bassTreble56 = bassTreble56;
        if (bassTreble8 != null) _bassTreble8 = bassTreble8;
    }

    public BassTreble(BassTrebleChannel? bassTreble127) : this()
    {
        if (bassTreble127 != null) _bassTreble127 = bassTreble127;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BassTreble that) return false;

        for (var i = 0; i < 8; i++)
        {
            if (!FloatUtility.IsFloatDiffLessThan(_enabledCh[i], that._enabledCh[i], 0.01f))
            {
                return false;
            }
        }
        return Equals(_bassTreble127, that._bassTreble127) &&
               Equals(_bassTreble34, that._bassTreble34) &&
               Equals(_bassTreble56, that._bassTreble56) &&
               Equals(_bassTreble8, that._bassTreble8);
    }

    public override int GetHashCode() =>
        HashCode.Combine(_bassTreble127, _bassTreble34, _bassTreble56, _bassTreble8);

    public object Clone()
    {
        var bt = (BassTreble)MemberwiseClone();
        bt._enabledCh = new float[8];
        Array.Copy(_enabledCh, bt._enabledCh, 8);
        bt._bassTreble127 = (BassTrebleChannel)_bassTreble127.Clone();
        bt._bassTreble34 = (BassTrebleChannel)_bassTreble34.Clone();
        bt._bassTreble56 = (BassTrebleChannel)_bassTreble56.Clone();
        bt._bassTreble8 = (BassTrebleChannel)_bassTreble8.Clone();
        return bt;
    }

    public BassTrebleChannel GetBassTreble127() => _bassTreble127;

    public void SetEnabledChannel(byte channel, float enabled)
    {
        if (channel > 7) return;

        if (enabled < 0.0f) enabled = 0.0f;
        if (enabled > 1.0f) enabled = 1.0f;

        _enabledCh[channel] = enabled;
    }

    public float GetEnabledChannel(byte channel)
    {
        if (channel > 7) return 0.0f;
        return _enabledCh[channel];
    }

    private static byte DbToTas5558Format(byte db) =>
        unchecked((byte)(18 - unchecked((sbyte)db)));

    private static byte Tas5558ToDbFormat(byte tas5558Db) =>
        unchecked((byte)(18 - tas5558Db));

    public byte GetAddress() => TAS5558.BASS_FILTER_SET_REG;

    public string GetInfo() => "BassTreble";

    public void SendToPeripheral(bool response)
    {
        var p = new BlePacket(GetFreqDbDataBuf().GetBinary(), 20, response);
        BleClient.Instance.SendDataToDsp(p);
    }

    public void SendEnabledToPeripheral(byte channel, bool response)
    {
        var p = new BlePacket(GetEnabledDataBuf(channel).GetBinary(), 20, response);
        BleClient.Instance.SendDataToDsp(p);
    }

    private HiFiToyDataBuf GetFreqDbDataBuf()
    {
        var b = new byte[16];
        var offset = 0;
        b[offset++] = _bassTreble8.GetBassFreq();
        b[offset++] = _bassTreble56.GetBassFreq();
        b[offset++] = _bassTreble34.GetBassFreq();
        b[offset++] = _bassTreble127.GetBassFreq();
        b[offset++] = DbToTas5558Format(_bassTreble8.GetBassDb());
        b[offset++] = DbToTas5558Format(_bassTreble56.GetBassDb());
        b[offset++] = DbToTas5558Format(_bassTreble34.GetBassDb());
        b[offset++] = DbToTas5558Format(_bassTreble127.GetBassDb());
        b[offset++] = _bassTreble8.GetTrebleFreq();
        b[offset++] = _bassTreble56.GetTrebleFreq();
        b[offset++] = _bassTreble34.GetTrebleFreq();
        b[offset++] = _bassTreble127.GetTrebleFreq();
        b[offset++] = DbToTas5558Format(_bassTreble8.GetTrebleDb());
        b[offset++] = DbToTas5558Format(_bassTreble56.GetTrebleDb());
        b[offset++] = DbToTas5558Format(_bassTreble34.GetTrebleDb());
        b[offset++] = DbToTas5558Format(_bassTreble127.GetTrebleDb());

        return new HiFiToyDataBuf(GetAddress(), b);
    }

    private HiFiToyDataBuf GetEnabledDataBuf(byte channel)
    {
        if (channel > 7) channel = 7;

        var val = (int)(0x800000 * _enabledCh[channel]);
        var ival = (int)(0x800000 - 0x800000 * _enabledCh[channel]);
        var b = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(0), ival);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(4), val);

        return new HiFiToyDataBuf((byte)(TAS5558.BASS_TREBLE_REG + channel), b);
    }

    public List<HiFiToyDataBuf> GetDataBufs()
    {
        var l = new List<HiFiToyDataBuf>();

        for (byte i = 0; i < 8; i++)
        {
            l.Add(GetEnabledDataBuf(i));
        }
        l.Add(GetFreqDbDataBuf());

        return l;
    }

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        if (dataBufs == null) return false;

        var importCounter = 0;

        foreach (var buf in dataBufs)
        {
            if (buf.GetAddr() == GetAddress() && buf.GetLength() == 16)
            {
                var b = buf.GetData();
                var offset = 0;

                _bassTreble8.SetBassFreq(b[offset++]);
                _bassTreble56.SetBassFreq(b[offset++]);
                _bassTreble34.SetBassFreq(b[offset++]);
                _bassTreble127.SetBassFreq(b[offset++]);

                _bassTreble8.SetBassDb(Tas5558ToDbFormat(b[offset++]));
                _bassTreble56.SetBassDb(Tas5558ToDbFormat(b[offset++]));
                _bassTreble34.SetBassDb(Tas5558ToDbFormat(b[offset++]));
                _bassTreble127.SetBassDb(Tas5558ToDbFormat(b[offset++]));

                _bassTreble8.SetTrebleFreq(b[offset++]);
                _bassTreble56.SetTrebleFreq(b[offset++]);
                _bassTreble34.SetTrebleFreq(b[offset++]);
                _bassTreble127.SetTrebleFreq(b[offset++]);

                _bassTreble8.SetTrebleDb(Tas5558ToDbFormat(b[offset++]));
                _bassTreble56.SetTrebleDb(Tas5558ToDbFormat(b[offset++]));
                _bassTreble34.SetTrebleDb(Tas5558ToDbFormat(b[offset++]));
                _bassTreble127.SetTrebleDb(Tas5558ToDbFormat(b[offset]));

                importCounter++;
            }

            if (buf.GetAddr() >= TAS5558.BASS_TREBLE_REG &&
                buf.GetAddr() < TAS5558.BASS_TREBLE_REG + 8 && buf.GetLength() == 8)
            {
                _enabledCh[buf.GetAddr() - TAS5558.BASS_TREBLE_REG] =
                    (float)BinaryPrimitives.ReadInt32BigEndian(buf.GetData().AsSpan(4)) / 0x800000;
                importCounter++;
            }

            if (importCounter >= 9)
            {
                return true;
            }
        }

        return false;
    }

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();

        for (var i = 0; i < 8; i++)
        {
            xmlData.AddXmlElement(string.Format(CultureInfo.CurrentCulture, "enabledCh{0}", i), _enabledCh[i]);
        }

        xmlData.AddXmlData(_bassTreble127.ToXmlData());
        xmlData.AddXmlData(_bassTreble34.ToXmlData());
        xmlData.AddXmlData(_bassTreble56.ToXmlData());
        xmlData.AddXmlData(_bassTreble8.ToXmlData());

        var bassTrebleXmlData = new XmlData();
        var attrib = new Dictionary<string, string> { ["Address"] = ByteUtility.ToString(GetAddress()) };
        bassTrebleXmlData.AddXmlElement("BassTreble", xmlData, attrib);
        return bassTrebleXmlData;
    }

    public void ImportFromXml(XmlImportReader xmlParser)
    {
        string? elementName = null;
        var count = 0;

        do
        {
            xmlParser.Next();

            if (xmlParser.EventType == XmlImportEventType.StartElement)
            {
                elementName = xmlParser.Name;

                if (elementName.Equals("BassTrebleChannel", StringComparison.Ordinal))
                {
                    var channelStr = xmlParser.GetAttributeValue(null, "Channel");
                    if (channelStr == null) continue;
                    var channel = byte.Parse(channelStr, CultureInfo.InvariantCulture);

                    if (_bassTreble127.GetChannel() == channel)
                    {
                        _bassTreble127.ImportFromXml(xmlParser);
                        count++;
                    }
                    if (_bassTreble34.GetChannel() == channel)
                    {
                        _bassTreble34.ImportFromXml(xmlParser);
                        count++;
                    }
                    if (_bassTreble56.GetChannel() == channel)
                    {
                        _bassTreble56.ImportFromXml(xmlParser);
                        count++;
                    }
                    if (_bassTreble8.GetChannel() == channel)
                    {
                        _bassTreble8.ImportFromXml(xmlParser);
                        count++;
                    }
                }
            }
            if (xmlParser.EventType == XmlImportEventType.EndElement)
            {
                if (xmlParser.Name.Equals("BassTreble", StringComparison.Ordinal)) break;
                elementName = null;
            }

            if (xmlParser.EventType == XmlImportEventType.Text && elementName != null)
            {
                var elementValue = xmlParser.Text;
                if (elementValue == null) continue;

                for (var i = 0; i < 8; i++)
                {
                    var keyStr = string.Format(CultureInfo.CurrentCulture, "enabledCh{0}", i);
                    if (elementName.Equals(keyStr, StringComparison.Ordinal))
                    {
                        _enabledCh[i] = float.Parse(elementValue, CultureInfo.InvariantCulture);
                        count++;
                    }
                }
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 12)
        {
            throw new IOException("BassTreble. Import from xml is not success.");
        }
    }
}
