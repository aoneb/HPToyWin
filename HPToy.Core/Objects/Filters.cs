using System.Globalization;
using HPToy.Core.Device;
using HPToy.Core.Numbers;
using HPToy.Core.Tas5558;
using HPToy.Core.Xml;
using static HPToy.Core.Objects.Biquad.BiquadParam.Type;

namespace HPToy.Core.Objects;

public sealed class Filters : IHiFiToyObject, ICloneable
{
    private Biquad[] _biquads = Array.Empty<Biquad>();
    private byte _address0;
    private byte _address1;
    private byte _activeBiquadIndex;
    private bool _activeNullLp;
    private bool _activeNullHp;

    public Filters(byte address0, byte address1)
    {
        _address0 = address0;
        _address1 = address1;

        _biquads = new Biquad[7];

        for (byte i = 0; i < 7; i++)
        {
            _biquads[i] = new Biquad((byte)(address0 + i), address1 != 0 ? (byte)(address1 + i) : (byte)0);

            var p = _biquads[i].GetParams();
            p.SetBorderFreq(20000, 20);
            p.SetTypeValue(BIQUAD_PARAMETRIC);
            p.SetFreq((short)(100 * (i + 1)));
            p.SetQFac(1.41f);
            p.SetDbVolume(0.0f);
        }

        _activeBiquadIndex = 0;
    }

    public Filters() : this(TAS5558.BIQUAD_FILTER_REG, (byte)(TAS5558.BIQUAD_FILTER_REG + 7))
    {
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Filters filters) return false;
        return _address0 == filters._address0 &&
               _address1 == filters._address1 &&
               _biquads.SequenceEqual(filters._biquads);
    }

    public override int GetHashCode()
    {
        var result = HashCode.Combine(_address0, _address1);
        foreach (var b in _biquads)
        {
            result = HashCode.Combine(result, b);
        }
        return result;
    }

    public object Clone()
    {
        var p = (Filters)MemberwiseClone();
        p._biquads = new Biquad[7];
        for (var i = 0; i < 7; i++)
        {
            p._biquads[i] = (Biquad)_biquads[i].Clone();
        }
        return p;
    }

    public bool IsActiveNullHP() => _activeNullHp;

    public void SetActiveNullHP(bool active)
    {
        _activeNullHp = active;
        if (active) _activeNullLp = false;
    }

    public bool IsActiveNullLP() => _activeNullLp;

    public void SetActiveNullLP(bool active)
    {
        _activeNullLp = active;
        if (active) _activeNullHp = false;
    }

    public void SetActiveBiquadIndex(byte index)
    {
        if (index < _biquads.Length)
        {
            _activeBiquadIndex = index;
        }
    }

    public byte GetActiveBiquadIndex() => _activeBiquadIndex;

    public int GetBiquadLength() => 7;

    public void SetBiquad(byte index, Biquad b)
    {
        if (index < _biquads.Length)
        {
            _biquads[index] = b;
        }
    }

    public Biquad? GetBiquad(byte index) =>
        index < _biquads.Length ? _biquads[index] : null;

    public Biquad? GetActiveBiquad() => GetBiquad(_activeBiquadIndex);

    public Biquad[] GetBiquads() => _biquads;

    internal byte GetBiquadIndex(Biquad b)
    {
        for (byte i = 0; i < _biquads.Length; i++)
        {
            if (_biquads[i] == b)
            {
                return i;
            }
        }
        return 255;
    }

    public byte[] GetBiquadTypes()
    {
        var types = new byte[_biquads.Length];
        for (byte i = 0; i < _biquads.Length; i++)
        {
            types[i] = _biquads[i].GetParams().GetTypeValue();
        }
        return types;
    }

    public bool SetBiquadTypes(byte[] types)
    {
        if (types.Length != 7) return false;

        for (byte i = 0; i < _biquads.Length; i++)
        {
            _biquads[i].GetParams().SetTypeValue(types[i]);
        }
        return true;
    }

    public void IncActiveBiquadIndex()
    {
        if (++_activeBiquadIndex > 6) _activeBiquadIndex = 0;
        _activeNullHp = false;
        _activeNullLp = false;
    }

    public void DecActiveBiquadIndex()
    {
        if (_activeBiquadIndex == 0)
        {
            _activeBiquadIndex = 6;
        }
        else
        {
            _activeBiquadIndex--;
        }
        _activeNullHp = false;
        _activeNullLp = false;
    }

    public void NextActiveBiquadIndex()
    {
        var type = GetActiveBiquad()!.GetParams().GetTypeValue();
        byte nextType;
        Biquad? b;
        var counter = 0;

        do
        {
            if (++counter > 7) break;

            _activeBiquadIndex++;
            if (_activeBiquadIndex > 6) _activeBiquadIndex = 0;

            b = GetActiveBiquad();
            nextType = b!.GetParams().GetTypeValue();
        } while (((type == BIQUAD_LOWPASS) && (nextType == BIQUAD_LOWPASS)) ||
                 ((type == BIQUAD_HIGHPASS) && (nextType == BIQUAD_HIGHPASS)) ||
                 !b!.IsEnabled());
    }

    public void PrevActiveBiquadIndex()
    {
        var type = GetActiveBiquad()!.GetParams().GetTypeValue();
        byte nextType;
        Biquad? b;
        var counter = 0;

        do
        {
            if (++counter > 7) break;

            _activeBiquadIndex--;
            if (_activeBiquadIndex < 0) _activeBiquadIndex = 6;

            b = GetActiveBiquad();
            nextType = b!.GetParams().GetTypeValue();
        } while (((type == BIQUAD_LOWPASS) && (nextType == BIQUAD_LOWPASS)) ||
                 ((type == BIQUAD_HIGHPASS) && (nextType == BIQUAD_HIGHPASS)) ||
                 !b!.IsEnabled());
    }

    public List<Biquad> GetBiquads(byte typeValue)
    {
        var result = new List<Biquad>();
        for (byte i = 0; i < 7; i++)
        {
            var b = GetBiquad(i);
            if (b!.GetParams().GetTypeValue() == typeValue)
            {
                result.Add(b);
            }
        }
        return result;
    }

    private Biquad? GetFreeBiquad()
    {
        var offBiquads = GetBiquads(BIQUAD_OFF);
        if (offBiquads.Count > 0)
        {
            return offBiquads[0];
        }

        var paramBiquads = GetBiquads(BIQUAD_PARAMETRIC);
        if (paramBiquads.Count > 0)
        {
            foreach (var p in paramBiquads)
            {
                if (FloatUtility.IsFloatNull(p.GetParams().GetDbVolume()))
                {
                    return p;
                }
            }

            var min = paramBiquads[0];
            for (var i = 1; i < paramBiquads.Count; i++)
            {
                var current = paramBiquads[i];
                if (Math.Abs(min.GetParams().GetDbVolume()) > Math.Abs(current.GetParams().GetDbVolume()))
                {
                    min = current;
                }
            }
            return min;
        }

        var allpassBiquads = GetBiquads(BIQUAD_ALLPASS);
        if (allpassBiquads.Count > 0)
        {
            return allpassBiquads[0];
        }

        return null;
    }

    public PassFilter? GetLowpass()
    {
        var lpBiquads = GetBiquads(BIQUAD_LOWPASS);
        if (lpBiquads.Count == 0) return null;
        return new PassFilter(lpBiquads, BIQUAD_LOWPASS);
    }

    public PassFilter? GetHighpass()
    {
        var hpBiquads = GetBiquads(BIQUAD_HIGHPASS);
        if (hpBiquads.Count == 0) return null;
        return new PassFilter(hpBiquads, BIQUAD_HIGHPASS);
    }

    private bool IsLowpassFull() => GetBiquads(BIQUAD_LOWPASS).Count >= 2;

    private bool IsHighpassFull() => GetBiquads(BIQUAD_HIGHPASS).Count >= 2;

    public void UpOrderFor(byte type)
    {
        short freq;

        if (type == BIQUAD_LOWPASS)
        {
            if (IsLowpassFull()) return;
            var lp = GetLowpass();
            freq = lp != null ? lp.GetFreq() : (short)20000;
        }
        else if (type == BIQUAD_HIGHPASS)
        {
            if (IsHighpassFull()) return;
            var hp = GetHighpass();
            freq = hp != null ? hp.GetFreq() : (short)20;
        }
        else
        {
            return;
        }

        var biquads = GetBiquads(type);

        if (biquads.Count < 2)
        {
            var b = GetFreeBiquad();
            if (b != null)
            {
                b.SetEnabled(true);
                b.GetParams().SetTypeValue(type);
                b.GetParams().SetFreq(freq);
            }
        }
        else
        {
            return;
        }

        biquads = GetBiquads(type);
        var index = GetBiquadIndex(biquads[0]);
        if (index != 255)
        {
            _activeBiquadIndex = index;
            if (type == BIQUAD_LOWPASS && _activeNullLp) _activeNullLp = false;
            if (type == BIQUAD_HIGHPASS && _activeNullHp) _activeNullHp = false;
        }

        var p = new PassFilter(biquads, type);
        p.SendToPeripheral(true);
    }

    public void DownOrderFor(byte type)
    {
        if (type != BIQUAD_LOWPASS && type != BIQUAD_HIGHPASS) return;

        var biquads = GetBiquads(type);
        if (biquads.Count == 0) return;

        int s;
        if (biquads.Count > 4)
        {
            s = 4;
        }
        else if (biquads.Count > 2)
        {
            s = 2;
        }
        else if (biquads.Count > 1)
        {
            s = 1;
        }
        else
        {
            s = 0;
        }

        for (var i = s; i < biquads.Count; i++)
        {
            var b = biquads[i];
            b.SetEnabled(IsPEQEnabled());
            b.GetParams().SetTypeValue(BIQUAD_PARAMETRIC);

            var freq = GetBetterNewFreqForBiquad(b);
            b.GetParams().SetFreq(freq != -1 ? freq : (short)100);
            b.GetParams().SetQFac(1.41f);
            b.GetParams().SetDbVolume(0.0f);
            b.SendToPeripheral(true);
        }

        biquads = GetBiquads(type);
        if (biquads.Count == 0)
        {
            if (type == BIQUAD_LOWPASS) _activeNullLp = true;
            if (type == BIQUAD_HIGHPASS) _activeNullHp = true;
        }

        var passFilter = new PassFilter(biquads, type);
        passFilter.SendToPeripheral(true);
    }

    public bool IsPEQEnabled()
    {
        var biquads = GetBiquads(BIQUAD_PARAMETRIC);
        biquads.AddRange(GetBiquads(BIQUAD_ALLPASS));
        if (biquads.Count == 0) return false;

        var result = true;
        foreach (var b in biquads)
        {
            if (!b.IsEnabled())
            {
                result = false;
                break;
            }
        }

        if (!result)
        {
            foreach (var b in biquads)
            {
                if (b.IsEnabled())
                {
                    b.SetEnabled(false);
                    b.SendToPeripheral(true);
                }
            }
        }
        return result;
    }

    public void SetPEQEnabled(bool enabled)
    {
        var biquads = GetBiquads(BIQUAD_PARAMETRIC);
        biquads.AddRange(GetBiquads(BIQUAD_ALLPASS));

        foreach (var b in biquads)
        {
            if (b.IsEnabled() != enabled)
            {
                b.SetEnabled(enabled);
                b.SendToPeripheral(true);
            }
        }

        if (!GetActiveBiquad()!.IsEnabled()) NextActiveBiquadIndex();
    }

    public short GetBetterNewFreqForBiquad(Biquad? b)
    {
        var freqs = new List<short>();
        short freq = -1;

        for (var i = 0; i < 7; i++)
        {
            var bType = _biquads[i].GetParams().GetTypeValue();
            var enabled = _biquads[i].IsEnabled();
            var typeCondition = bType == BIQUAD_HIGHPASS || bType == BIQUAD_LOWPASS ||
                                bType == BIQUAD_PARAMETRIC || bType == BIQUAD_BANDPASS;
            var biquadCondition = b == null || _biquads[i] != b;

            if (enabled && typeCondition && biquadCondition)
            {
                freqs.Add(_biquads[i].GetParams().GetFreq());
            }
        }
        if (GetLowpass() == null) freqs.Add(20000);
        if (GetHighpass() == null) freqs.Add(20);

        if (freqs.Count < 2) return freq;
        freqs.Sort();

        var maxDLogFreq = 0.0f;
        for (var i = 0; i < freqs.Count - 1; i++)
        {
            var f0 = freqs[i];
            var f1 = freqs[i + 1];

            if (Math.Log10(f1) - Math.Log10(f0) > maxDLogFreq)
            {
                maxDLogFreq = (float)(Math.Log10(f1) - Math.Log10(f0));
                var logFreq = Math.Log10(f0) + maxDLogFreq / 2;
                freq = (short)Math.Pow(10, logFreq);
            }
        }
        return freq;
    }

    public float GetAFR(float freqX)
    {
        var resultAfr = 1.0f;
        for (var i = 0; i < 7; i++)
        {
            resultAfr *= _biquads[i].GetAFR(freqX);
        }
        return resultAfr;
    }

    public byte GetAddress() => _address0;

    public string GetInfo() => "Filters is 7 biquads";

    public void SendToPeripheral(bool response)
    {
        for (var i = 0; i < 7; i++)
        {
            _biquads[i].SendToPeripheral(true);
        }
    }

    public List<HiFiToyDataBuf> GetDataBufs()
    {
        var l = new List<HiFiToyDataBuf>();
        for (var i = 0; i < 7; i++)
        {
            l.AddRange(_biquads[i].GetDataBufs());
        }
        return l;
    }

    public bool ImportFromDataBufs(List<HiFiToyDataBuf> dataBufs)
    {
        if (dataBufs == null) return false;

        for (var i = 0; i < 7; i++)
        {
            if (!_biquads[i].ImportFromDataBufs(dataBufs))
            {
                return false;
            }
        }

        return true;
    }

    public XmlData ToXmlData()
    {
        var xmlData = new XmlData();
        for (var i = 0; i < 7; i++)
        {
            xmlData.AddXmlData(_biquads[i].ToXmlData());
        }

        var filtersXmlData = new XmlData();
        var attrib = new Dictionary<string, string>
        {
            ["Address"] = ByteUtility.ToString(_address0),
            ["Address1"] = ByteUtility.ToString(_address1)
        };
        filtersXmlData.AddXmlElement("Filters", xmlData, attrib);
        return filtersXmlData;
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

                if (elementName.Equals("Biquad", StringComparison.Ordinal))
                {
                    var addrStr = xmlParser.GetAttributeValue(null, "Address");
                    if (addrStr == null) continue;
                    var addr = ByteUtility.Parse(addrStr);

                    for (var i = 0; i < 7; i++)
                    {
                        if (_biquads[i].GetAddress() == addr)
                        {
                            _biquads[i].ImportFromXml(xmlParser);
                            count++;
                        }
                    }
                }
            }
            if (xmlParser.EventType == XmlImportEventType.EndElement)
            {
                if (xmlParser.Name.Equals("Filters", StringComparison.Ordinal)) break;
                elementName = null;
            }
        } while (xmlParser.EventType != XmlImportEventType.EndDocument);

        if (count != 7)
        {
            throw new IOException("Filters=" + _address0 + ". Import from xml is not success.");
        }
    }
}
