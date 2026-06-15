using System.Buffers.Binary;
using System.Globalization;
using HPToy.Core.Ble;
using HPToy.Core.Numbers;
using HPToy.Core.Xml;

namespace HPToy.Core.Objects;

public sealed class Volume : IHiFiToyObject, ICloneable
{
    private const float HwMaxDb = 18.0f;
    private const float HwMinDb = -127.0f;
    public const float HwMuteDb = -81.0f;

    private byte _address;
    private float _db;
    private float _maxDb;
    private float _minDb;

    public Volume(byte address, float db, float maxDb, float minDb)
    {
        _address = address;
        if (maxDb > HwMaxDb) maxDb = HwMaxDb;
        if (minDb < HwMinDb) minDb = HwMinDb;
        _maxDb = maxDb;
        _minDb = minDb;
        SetDb(db);
    }

    public Volume(byte address, float db) : this(address, db, HwMaxDb, HwMinDb)
    {
    }

    public Volume(byte address) : this(address, 0.0f, HwMaxDb, HwMinDb)
    {
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Volume volume) return false;
        return _address == volume._address &&
               FloatUtility.IsFloatDiffLessThan(volume._db, _db, 0.02f) &&
               FloatUtility.IsFloatDiffLessThan(volume._maxDb, _maxDb, 0.02f) &&
               FloatUtility.IsFloatDiffLessThan(volume._minDb, _minDb, 0.02f);
    }

    public override int GetHashCode() => _address.GetHashCode();

    public object Clone() => MemberwiseClone();

    public void SetDb(float db)
    {
        if (db > _maxDb) db = _maxDb;
        if (db < _minDb) db = _minDb;
        _db = db;
    }

    public float GetDb() => _db;

    public void SetDbPercent(float percent)
    {
        if (percent > 1.0f) percent = 1.0f;
        if (percent < 0.0f) percent = 0.0f;
        SetDb(percent * (_maxDb - _minDb) + _minDb);
    }

    public float GetDbPercent() => (_db - _minDb) / (_maxDb - _minDb);

    public float DbToAmpl(float db) => (float)Math.Pow(10, db / 20);
    public float AmplToDb(float ampl) => (float)(20 * Math.Log10(ampl));

    public byte GetAddress() => _address;

    public string GetInfo() =>
        _db > HwMuteDb ? string.Format(CultureInfo.CurrentCulture, "{0:F1}", _db) : "Mute";

    public void SendToPeripheral(bool response)
    {
        // Must match the original: always pad to a fixed 20-byte write. The CC2540
        // firmware ignores the short (6-byte) volume packet, which left the slider
        // doing nothing while the device stayed at its loud power-on level.
        var binary = BinaryOperation.GetBinary(GetDataBufs())!;
        var p = new BlePacket(binary, 20, response);
        BleClient.Instance.SendDataToDsp(p);
    }

    public List<HiFiToyDataBuf> GetDataBufs()
    {
        var v = 0x245;
        if (_db > HwMuteDb)
        {
            v = (int)((18.0f - _db) / 0.25);
            if (v < 1) v = 1;
            if (v > 0x245) v = 0x245;
        }

        var b = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(b, v);
        return new List<HiFiToyDataBuf> { new(_address, b) };
    }

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        if (dataBufs == null) return false;

        foreach (var buf in dataBufs)
        {
            if (buf.GetAddr() == GetAddress() && buf.GetLength() == 4)
            {
                var v = BinaryPrimitives.ReadInt32BigEndian(buf.GetData().AsSpan());
                if (v < 1) v = 1;
                if (v > 0x245) v = 0x245;

                _db = v != 0x245 ? 18.0f - v * 0.25f : HwMuteDb;
                return true;
            }
        }
        return false;
    }

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();
        xmlData.AddXmlElement("MaxDb", _maxDb);
        xmlData.AddXmlElement("MinDb", _minDb);
        xmlData.AddXmlElement("Db", _db);

        var gainXmlData = new XmlData();
        var attrib = new Dictionary<string, string> { ["Address"] = ByteUtility.ToString(_address) };
        gainXmlData.AddXmlElement("Volume", xmlData, attrib);
        return gainXmlData;
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
                if (xmlParser.Name.Equals("Volume", StringComparison.Ordinal)) break;
                elementName = null;
            }

            if (xmlParser.EventType == XmlImportEventType.Text && elementName != null)
            {
                var elementValue = xmlParser.Text;
                if (elementValue == null) continue;

                if (elementName.Equals("MaxDb", StringComparison.Ordinal))
                {
                    _maxDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("MinDb", StringComparison.Ordinal))
                {
                    _minDb = float.Parse(elementValue, CultureInfo.InvariantCulture);
                    count++;
                }
                if (elementName.Equals("Db", StringComparison.Ordinal))
                {
                    SetDb(float.Parse(elementValue, CultureInfo.InvariantCulture));
                    count++;
                }
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 3)
        {
            throw new IOException("Volume=" + _address + ". Import from xml is not success.");
        }
    }
}
