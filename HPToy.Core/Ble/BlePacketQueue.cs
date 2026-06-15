namespace HPToy.Core.Ble;

public sealed class BlePacketQueue
{
    private readonly LinkedList<BlePacket> _packets = new();

    public int Count => _packets.Count;

    public BlePacket? Peek() => _packets.Count > 0 ? _packets.First!.Value : null;

    public void Add(BlePacket packet)
    {
        // Mirrors the original BlePacketQueue: response packets are always queued,
        // while a no-response packet collapses an earlier no-response packet that
        // targets the same register (same data[0]) by replacing it in place,
        // preserving queue order. The in-flight packet is already pulled out of
        // this queue before sending, so every node here is safe to replace.
        if (packet.Response || packet.Data.Length == 0)
        {
            // Coalesce rapid energy-config writes (12-byte payload, same as Android sendToDsp).
            if (packet.Response && packet.Data.Length == 12)
            {
                for (var node = _packets.Last; node != null; node = node.Previous)
                {
                    if (node.Value.Response && node.Value.Data.Length == 12)
                    {
                        node.Value = packet;
                        return;
                    }
                }
            }

            _packets.AddLast(packet);
            return;
        }

        var cmd = packet.Data[0];
        for (var node = _packets.Last; node != null; node = node.Previous)
        {
            if (!node.Value.Response && node.Value.Data.Length > 0 && node.Value.Data[0] == cmd)
            {
                node.Value = packet;
                return;
            }
        }

        _packets.AddLast(packet);
    }

    public void AddFirst(BlePacket packet) => _packets.AddFirst(packet);

    public BlePacket? RemoveFirst()
    {
        if (_packets.Count == 0) return null;
        var p = _packets.First!.Value;
        _packets.RemoveFirst();
        return p;
    }

    public void Clear() => _packets.Clear();
}
