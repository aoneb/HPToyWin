using System.Globalization;
using HPToy.Core.Xml;

namespace HPToy.Core.Objects;

public sealed class BassTrebleChannel : ICloneable
{
    private const byte HwBassTrebleMaxDb = 18;
    private const byte HwBassTrebleMinDb = unchecked((byte)-18);

    private byte _channel;
    private byte _bassFreq;
    private byte _bassDb;
    private byte _trebleFreq;
    private byte _trebleDb;
    private byte _maxBassDb;
    private byte _minBassDb;
    private byte _maxTrebleDb;
    private byte _minTrebleDb;

    public BassTrebleChannel(byte channel, byte bassFreq, byte bassDb, byte trebleFreq, byte trebleDb,
        byte maxBassDb, byte minBassDb, byte maxTrebleDb, byte minTrebleDb)
    {
        SetChannel(channel);
        SetBassFreq(bassFreq);
        SetTrebleFreq(trebleFreq);

        if (maxBassDb > HwBassTrebleMaxDb) maxBassDb = HwBassTrebleMaxDb;
        if (maxTrebleDb > HwBassTrebleMaxDb) maxTrebleDb = HwBassTrebleMaxDb;
        if (minBassDb < HwBassTrebleMinDb) minBassDb = HwBassTrebleMinDb;
        if (minTrebleDb < HwBassTrebleMinDb) minTrebleDb = HwBassTrebleMinDb;
        _maxBassDb = maxBassDb;
        _minBassDb = minBassDb;
        _maxTrebleDb = maxTrebleDb;
        _minTrebleDb = minTrebleDb;

        SetBassDb(bassDb);
        SetTrebleDb(trebleDb);
    }

    public BassTrebleChannel(byte channel, byte bassFreq, byte bassDb, byte trebleFreq, byte trebleDb)
        : this(channel, bassFreq, bassDb, trebleFreq, trebleDb,
            HwBassTrebleMaxDb, HwBassTrebleMinDb, HwBassTrebleMaxDb, HwBassTrebleMinDb)
    {
    }

    public BassTrebleChannel(byte channel)
        : this(channel, BassFreq.BASS_FREQ_NONE, 0, TrebleFreq.TREBLE_FREQ_NONE, 0,
            HwBassTrebleMaxDb, HwBassTrebleMinDb, HwBassTrebleMaxDb, HwBassTrebleMinDb)
    {
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BassTrebleChannel that) return false;
        return _channel == that._channel &&
               _bassFreq == that._bassFreq &&
               _bassDb == that._bassDb &&
               _trebleFreq == that._trebleFreq &&
               _trebleDb == that._trebleDb &&
               _maxBassDb == that._maxBassDb &&
               _minBassDb == that._minBassDb &&
               _maxTrebleDb == that._maxTrebleDb &&
               _minTrebleDb == that._minTrebleDb;
    }

    public override int GetHashCode()
    {
        var hash = HashCode.Combine(_channel, _bassFreq, _bassDb, _trebleFreq, _trebleDb, _maxBassDb, _minBassDb, _maxTrebleDb);
        return HashCode.Combine(hash, _minTrebleDb);
    }

    public object Clone() => MemberwiseClone();

    private void SetChannel(byte channel)
    {
        if (channel > BassTrebleCh.BASS_TREBLE_CH_8) channel = BassTrebleCh.BASS_TREBLE_CH_8;
        if (channel < BassTrebleCh.BASS_TREBLE_CH_127) channel = BassTrebleCh.BASS_TREBLE_CH_127;
        _channel = channel;
    }

    public byte GetChannel() => _channel;

    public void SetBassFreq(byte bassFreq)
    {
        if (bassFreq > BassFreq.BASS_FREQ_500) bassFreq = BassFreq.BASS_FREQ_500;
        if (bassFreq < BassFreq.BASS_FREQ_NONE) bassFreq = BassFreq.BASS_FREQ_NONE;
        _bassFreq = bassFreq;
    }

    public byte GetBassFreq() => _bassFreq;

    public void SetTrebleFreq(byte trebleFreq)
    {
        if (trebleFreq > TrebleFreq.TREBLE_FREQ_13000) trebleFreq = TrebleFreq.TREBLE_FREQ_13000;
        if (trebleFreq < TrebleFreq.TREBLE_FREQ_NONE) trebleFreq = TrebleFreq.TREBLE_FREQ_NONE;
        _trebleFreq = trebleFreq;
    }

    public byte GetTrebleFreq() => _trebleFreq;

    public void SetBassDb(byte db)
    {
        var signed = SignedDb(db);
        var min = SignedDb(_minBassDb);
        var max = SignedDb(_maxBassDb);
        if (signed < min) signed = min;
        if (signed > max) signed = max;
        _bassDb = ToDbByte(signed);
    }

    public byte GetBassDb() => _bassDb;

    public void SetTrebleDb(byte db)
    {
        var signed = SignedDb(db);
        var min = SignedDb(_minTrebleDb);
        var max = SignedDb(_maxTrebleDb);
        if (signed < min) signed = min;
        if (signed > max) signed = max;
        _trebleDb = ToDbByte(signed);
    }

    public byte GetTrebleDb() => _trebleDb;

    public void SetBassDbPercent(float percent)
    {
        if (percent > 1.0f) percent = 1.0f;
        if (percent < 0.0f) percent = 0.0f;
        var min = SignedDb(_minBassDb);
        var max = SignedDb(_maxBassDb);
        var signed = (sbyte)(percent * (max - min) + min);
        SetBassDb(ToDbByte(signed));
    }

    public float GetBassDbPercent()
    {
        var min = SignedDb(_minBassDb);
        var max = SignedDb(_maxBassDb);
        return (SignedDb(_bassDb) - min) / (float)(max - min);
    }

    public void SetTrebleDbPercent(float percent)
    {
        if (percent > 1.0f) percent = 1.0f;
        if (percent < 0.0f) percent = 0.0f;
        var min = SignedDb(_minTrebleDb);
        var max = SignedDb(_maxTrebleDb);
        var signed = (sbyte)(percent * (max - min) + min);
        SetTrebleDb(ToDbByte(signed));
    }

    public float GetTrebleDbPercent()
    {
        var min = SignedDb(_minTrebleDb);
        var max = SignedDb(_maxTrebleDb);
        return (SignedDb(_trebleDb) - min) / (float)(max - min);
    }

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();
        xmlData.AddXmlElement("BassFreq", _bassFreq);
        xmlData.AddXmlElement("BassDb", _bassDb);
        xmlData.AddXmlElement("TrebleFreq", _trebleFreq);
        xmlData.AddXmlElement("TrebleDb", _trebleDb);
        xmlData.AddXmlElement("maxBassDb", _maxBassDb);
        xmlData.AddXmlElement("minBassDb", _minBassDb);
        xmlData.AddXmlElement("maxTrebleDb", _maxTrebleDb);
        xmlData.AddXmlElement("minTrebleDb", _minTrebleDb);

        var bassTrebleXmlData = new XmlData();
        var attrib = new Dictionary<string, string> { ["Channel"] = _channel.ToString(CultureInfo.InvariantCulture) };
        bassTrebleXmlData.AddXmlElement("BassTrebleChannel", xmlData, attrib);
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
            }
            if (xmlParser.EventType == XmlImportEventType.EndElement)
            {
                if (xmlParser.Name.Equals("BassTrebleChannel", StringComparison.Ordinal)) break;
                elementName = null;
            }

            if (xmlParser.EventType == XmlImportEventType.Text && elementName != null)
            {
                var elementValue = xmlParser.Text;
                if (elementValue == null) continue;

                if (elementName.Equals("BassFreq", StringComparison.Ordinal)) { _bassFreq = byte.Parse(elementValue, CultureInfo.InvariantCulture); count++; }
                if (elementName.Equals("BassDb", StringComparison.Ordinal)) { _bassDb = ParseSignedDb(elementValue); count++; }
                if (elementName.Equals("TrebleFreq", StringComparison.Ordinal)) { _trebleFreq = byte.Parse(elementValue, CultureInfo.InvariantCulture); count++; }
                if (elementName.Equals("TrebleDb", StringComparison.Ordinal)) { _trebleDb = ParseSignedDb(elementValue); count++; }
                if (elementName.Equals("maxBassDb", StringComparison.Ordinal)) { _maxBassDb = ParseSignedDb(elementValue); count++; }
                if (elementName.Equals("minBassDb", StringComparison.Ordinal)) { _minBassDb = ParseSignedDb(elementValue); count++; }
                if (elementName.Equals("maxTrebleDb", StringComparison.Ordinal)) { _maxTrebleDb = ParseSignedDb(elementValue); count++; }
                if (elementName.Equals("minTrebleDb", StringComparison.Ordinal)) { _minTrebleDb = ParseSignedDb(elementValue); count++; }
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 8)
        {
            throw new IOException("BassTrebleChannel=" + _channel + ". Import from xml is not success.");
        }
    }

    public static class BassFreq
    {
        public const byte BASS_FREQ_NONE = 0;
        public const byte BASS_FREQ_125 = 1;
        public const byte BASS_FREQ_250 = 2;
        public const byte BASS_FREQ_375 = 3;
        public const byte BASS_FREQ_438 = 4;
        public const byte BASS_FREQ_500 = 5;
    }

    public static class TrebleFreq
    {
        public const byte TREBLE_FREQ_NONE = 0;
        public const byte TREBLE_FREQ_2750 = 1;
        public const byte TREBLE_FREQ_5500 = 2;
        public const byte TREBLE_FREQ_9000 = 3;
        public const byte TREBLE_FREQ_11000 = 4;
        public const byte TREBLE_FREQ_13000 = 5;
    }

    public static class BassTrebleCh
    {
        public const byte BASS_TREBLE_CH_127 = 0;
        public const byte BASS_TREBLE_CH_34 = 1;
        public const byte BASS_TREBLE_CH_56 = 2;
        public const byte BASS_TREBLE_CH_8 = 3;
    }

    // Java stores dB in signed bytes; C# byte is unsigned — compare as sbyte.
    private static sbyte SignedDb(byte db) => unchecked((sbyte)db);
    private static byte ToDbByte(sbyte db) => unchecked((byte)db);
    private static byte ParseSignedDb(string value) =>
        unchecked((byte)sbyte.Parse(value, CultureInfo.InvariantCulture));
}
