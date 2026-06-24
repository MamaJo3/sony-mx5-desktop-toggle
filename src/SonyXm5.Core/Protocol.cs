using System.Collections.Generic;

namespace SonyXm5.Core;

/// <summary>
/// Sony headphone control protocol (the "MDR" framing) and the v2 Ambient Sound Control
/// command, reverse-engineered/ported from the Gadgetbridge and SonyHeadphonesClient projects.
/// </summary>
public static class Protocol
{
    public const byte Start = 0x3E, End = 0x3C, Esc = 0x3D, DataMdr = 0x0C, Ack = 0x01;

    /// <summary>Wrap a payload in a full BT frame: START + escape(type seq size payload checksum) + END.</summary>
    public static byte[] Frame(byte dataType, byte seq, byte[] payload)
    {
        var inner = new List<byte> { dataType, seq };
        int n = payload.Length;
        inner.Add((byte)(n >> 24)); inner.Add((byte)(n >> 16)); inner.Add((byte)(n >> 8)); inner.Add((byte)n);
        inner.AddRange(payload);
        int sum = 0; foreach (var b in inner) sum += b;
        inner.Add((byte)(sum & 0xFF));

        var o = new List<byte> { Start };
        foreach (var b in inner)
        {
            if (b == End) { o.Add(Esc); o.Add(0x2C); }
            else if (b == Esc) { o.Add(Esc); o.Add(0x2D); }
            else if (b == Start) { o.Add(Esc); o.Add(0x2E); }
            else o.Add(b);
        }
        o.Add(End);
        return o.ToArray();
    }

    /// <summary>
    /// Build the v2 "Ambient Sound Control" set payload (wind-noise-capable variant).
    /// mode: "amb" | "nc" | "off" | "wind"; level 0-20 applies to ambient.
    /// </summary>
    public static byte[] AmbientSoundControl(string mode, int level) => new byte[]
    {
        0x68, 0x17, 0x01,
        (byte)(mode == "off" ? 0 : 1),    // ASC on/off
        (byte)(mode == "amb" ? 1 : 0),    // ambient (1) vs noise cancelling (0)
        (byte)(mode == "wind" ? 3 : 2),   // wind-noise reduction
        0x00,                             // focus-on-voice
        (byte)(mode == "amb" ? level : 0) // ambient level
    };

    /// <summary>If the payload is an ambient/NC state notification, return 1 (ambient) or 0 (NC); else -1.</summary>
    public static int AmbientFlag(byte[] payload) =>
        (payload.Length >= 5 && (payload[0] == 0x67 || payload[0] == 0x69) && payload[1] == 0x17) ? payload[4] : -1;
}

/// <summary>A decoded protocol frame: data type, sequence bit, and payload.</summary>
public readonly struct Packet
{
    public readonly byte Type;
    public readonly byte Seq;
    public readonly byte[] Payload;
    public Packet(byte type, byte seq, byte[] payload) { Type = type; Seq = seq; Payload = payload; }
}

/// <summary>Incrementally turns a Bluetooth byte stream into decoded packets, consuming each frame once.</summary>
public sealed class FrameReader
{
    private readonly List<byte> _buf = new();

    public IEnumerable<Packet> Feed(byte[] data)
    {
        _buf.AddRange(data);
        while (true)
        {
            int st = _buf.IndexOf(Protocol.Start);
            if (st < 0) { _buf.Clear(); yield break; }
            if (st > 0) _buf.RemoveRange(0, st);
            int en = _buf.IndexOf(Protocol.End, 1);
            if (en < 0) { if (_buf.Count > 2048) _buf.Clear(); yield break; }

            var inner = new List<byte>();
            for (int k = 1; k < en; k++)
            {
                if (_buf[k] == Protocol.Esc && k + 1 < en)
                { byte e = _buf[k + 1]; inner.Add((byte)(e == 0x2C ? Protocol.End : e == 0x2D ? Protocol.Esc : Protocol.Start)); k++; }
                else inner.Add(_buf[k]);
            }
            _buf.RemoveRange(0, en + 1);
            if (inner.Count < 7) continue;
            int size = (inner[2] << 24) | (inner[3] << 16) | (inner[4] << 8) | inner[5];
            if (size < 0 || 6 + size > inner.Count) continue;
            yield return new Packet(inner[0], inner[1], inner.GetRange(6, size).ToArray());
        }
    }
}
