using System.Buffers.Binary;
using System.Globalization;
using HPToy.Core.Ble;
using HPToy.Core.Device;
using static HPToy.Core.Objects.Biquad.BiquadParam.Type;

namespace HPToy.Core.Objects;

public sealed class PassFilter : ICloneable
{
    public const byte PASSFILTER_ORDER_0 = 0;
    public const byte PASSFILTER_ORDER_2 = 1;
    public const byte PASSFILTER_ORDER_4 = 2;
    public const byte PASSFILTER_ORDER_8 = 3;
    public const byte PASSFILTER_ORDER_UNK = 4;

    private List<Biquad>? _biquads;

    public PassFilter(List<Biquad>? biquads, byte type)
    {
        short freq;
        type = type == BIQUAD_LOWPASS ? BIQUAD_LOWPASS : BIQUAD_HIGHPASS;

        if (biquads is { Count: > 0 })
        {
            freq = biquads[0].GetParams().GetFreq();

            Biquad b;
            switch (biquads.Count)
            {
                case 1:
                    b = biquads[0];
                    b.GetParams().SetTypeValue(type);
                    b.GetParams().SetFreq(freq);
                    b.GetParams().SetQFac(0.71f);
                    break;

                case 2:
                    b = biquads[0];
                    b.GetParams().SetTypeValue(type);
                    b.GetParams().SetFreq(freq);
                    b.GetParams().SetQFac(0.54f);

                    b = biquads[1];
                    b.GetParams().SetTypeValue(type);
                    b.GetParams().SetFreq(freq);
                    b.GetParams().SetQFac(1.31f);
                    break;

                case 4:
                    b = biquads[0];
                    b.GetParams().SetTypeValue(type);
                    b.GetParams().SetFreq(freq);
                    b.GetParams().SetQFac(0.90f);

                    b = biquads[1];
                    b.GetParams().SetTypeValue(type);
                    b.GetParams().SetFreq(freq);
                    b.GetParams().SetQFac(2.65f);

                    b = biquads[2];
                    b.GetParams().SetTypeValue(type);
                    b.GetParams().SetFreq(freq);
                    b.GetParams().SetQFac(0.51f);

                    b = biquads[3];
                    b.GetParams().SetTypeValue(type);
                    b.GetParams().SetFreq(freq);
                    b.GetParams().SetQFac(0.60f);
                    break;

                default:
                    biquads = null;
                    break;
            }
        }
        else
        {
            freq = type == BIQUAD_LOWPASS ? (short)20000 : (short)20;
        }

        _biquads = biquads;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not PassFilter that) return false;
        return Equals(_biquads, that._biquads);
    }

    public override int GetHashCode() => HashCode.Combine(_biquads);

    public object Clone()
    {
        var p = (PassFilter)MemberwiseClone();
        p._biquads = new List<Biquad>();
        if (_biquads != null)
        {
            foreach (var b in _biquads)
            {
                p._biquads.Add((Biquad)b.Clone());
            }
        }
        return p;
    }

    public byte GetOrder()
    {
        if (_biquads != null)
        {
            return _biquads.Count switch
            {
                0 => PASSFILTER_ORDER_0,
                1 => PASSFILTER_ORDER_2,
                2 => PASSFILTER_ORDER_4,
                4 => PASSFILTER_ORDER_8,
                _ => PASSFILTER_ORDER_UNK
            };
        }
        return PASSFILTER_ORDER_0;
    }

    public void SetType(byte type)
    {
        type = type == BIQUAD_LOWPASS ? BIQUAD_LOWPASS : BIQUAD_HIGHPASS;

        if (_biquads != null)
        {
            foreach (var b in _biquads)
            {
                b.GetParams().SetTypeValue(type);
            }
        }
    }

    public new byte GetType()
    {
        if (_biquads is { Count: > 0 })
        {
            return _biquads[0].GetParams().GetTypeValue();
        }
        return BIQUAD_OFF;
    }

    public void SetFreq(short freq)
    {
        if (_biquads != null)
        {
            foreach (var b in _biquads)
            {
                b.GetParams().SetFreq(freq);
            }
        }
    }

    public short GetFreq()
    {
        if (_biquads is { Count: > 0 })
        {
            return _biquads[0].GetParams().GetFreq();
        }
        return 0;
    }

    public string GetInfo()
    {
        int[] dbOnOctave = { 0, 12, 24, 48 };
        var index = GetOrder();
        if (index < 4)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}db/oct; Freq:{1}Hz", dbOnOctave[index], GetFreq());
        }
        return string.Format(CultureInfo.CurrentCulture, "???db/oct; Freq:{0}Hz", GetFreq());
    }

    public void SendToPeripheral(bool response)
    {
        if (_biquads == null || _biquads.Count == 0) return;

        var b = new byte[13];
        var offset = 0;
        for (var i = 0; i < 4; i++)
        {
            if (i < _biquads.Count)
            {
                b[offset++] = _biquads[i].GetAddress();
                b[offset++] = _biquads[i].GetAddress1();
            }
            else
            {
                b[offset++] = 0;
                b[offset++] = 0;
            }
        }
        b[offset++] = GetOrder();
        b[offset++] = GetType();
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(offset), GetFreq());
        offset += 2;
        b[offset] = 0;

        BleClient.Instance.SendDataToDsp(b, response);
    }
}
