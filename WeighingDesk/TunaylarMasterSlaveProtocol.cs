using System.Globalization;
using System.Text;

namespace WeighingDesk;

/// <summary>Encodes a one-shot Load Line 2 master/slave request and decodes its 29-byte reply.</summary>
internal sealed class TunaylarMasterSlaveProtocol
{
    private const int FrameLength = 29;
    private readonly List<byte> _buffer = new();

    public static byte[] CreateRequest(byte address) => [0x02, address, 0x0D];

    public IReadOnlyList<decimal> Push(ReadOnlySpan<byte> incoming, byte expectedAddress, int decimalPlaces)
    {
        for (var i = 0; i < incoming.Length; i++) _buffer.Add(incoming[i]);
        var readings = new List<decimal>();
        while (true)
        {
            var start = _buffer.IndexOf(0x02);
            if (start < 0) { _buffer.Clear(); break; }
            if (start > 0) _buffer.RemoveRange(0, start);
            if (_buffer.Count < FrameLength) break;

            var frame = _buffer.GetRange(0, FrameLength).ToArray();
            if (TryDecode(frame, expectedAddress, decimalPlaces, out var weight))
            {
                readings.Add(weight);
                _buffer.RemoveRange(0, FrameLength);
            }
            else _buffer.RemoveAt(0);
        }
        if (_buffer.Count > FrameLength * 2) _buffer.RemoveRange(0, _buffer.Count - FrameLength);
        return readings;
    }

    public void Reset() => _buffer.Clear();

    private static bool TryDecode(byte[] frame, byte expectedAddress, int decimalPlaces, out decimal weight)
    {
        weight = 0m;
        if (frame.Length != FrameLength || frame[0] != 0x02 || frame[1] != expectedAddress || frame[^1] != 0x0D)
            return false;
        if (frame[10] != (byte)'/' || frame[13] != (byte)'/' || frame[18] != (byte)':') return false;

        var checksum = 0;
        for (var i = 1; i < FrameLength - 2; i++) checksum = (checksum + frame[i]) & 0xFF;
        if (frame[^2] != checksum) return false;

        var digits = Encoding.ASCII.GetString(frame, 21, 6).Trim();
        if (digits.Length == 0 || digits.Any(character => character is < '0' or > '9')) return false;
        if (!decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out weight)) return false;
        for (var i = 0; i < Math.Clamp(decimalPlaces, 0, 4); i++) weight /= 10m;
        return true;
    }
}
