using System.Globalization;
using System.Text;

namespace WeighingDesk;

internal sealed record TunaylarLoadLine2Reading(
    decimal DisplayedWeight,
    decimal TareWeight,
    int DecimalPlaces,
    bool IsNet,
    bool HasPresetTare,
    bool IsZero,
    bool IsManualPrint);

/// <summary>Decodes the fixed 21-byte continuous transmission described in the Load Line 2 manual.</summary>
internal sealed class TunaylarLoadLine2Protocol
{
    private const int FrameLength = 21;
    private readonly List<byte> _buffer = new();

    public IReadOnlyList<TunaylarLoadLine2Reading> Push(ReadOnlySpan<byte> incoming)
    {
        for (var i = 0; i < incoming.Length; i++) _buffer.Add(incoming[i]);
        var readings = new List<TunaylarLoadLine2Reading>();

        while (true)
        {
            var start = _buffer.IndexOf(0x02);
            if (start < 0)
            {
                _buffer.Clear();
                break;
            }
            if (start > 0) _buffer.RemoveRange(0, start);
            if (_buffer.Count < FrameLength) break;

            var frame = _buffer.GetRange(0, FrameLength).ToArray();
            if (TryDecode(frame, out var reading))
            {
                readings.Add(reading);
                _buffer.RemoveRange(0, FrameLength);
            }
            else
            {
                // Advance one byte and resynchronize at the next start marker.
                _buffer.RemoveAt(0);
            }
        }

        if (_buffer.Count > FrameLength * 4) _buffer.RemoveRange(0, _buffer.Count - FrameLength);
        return readings;
    }

    public void Reset() => _buffer.Clear();

    private static bool TryDecode(byte[] frame, out TunaylarLoadLine2Reading reading)
    {
        reading = default!;
        if (frame.Length != FrameLength || frame[0] != 0x02 || frame[18] != 0x03 || frame[20] != 0x0D)
            return false;
        var decimalPlaces = frame[3] <= 4 ? frame[3] : frame[3] is >= (byte)'0' and <= (byte)'4' ? frame[3] - (byte)'0' : byte.MaxValue;
        if (frame[4] != 0x20 || frame[11] != 0x20 || decimalPlaces > 4)
            return false;

        // A.0 and A.5 are documented as fixed at 1. C is the decimal-place count.
        if ((frame[1] & 0x21) != 0x21) return false;
        var checksum = 0;
        for (var i = 1; i < 18; i++) checksum = (checksum + frame[i]) & 0xFF;
        if (frame[19] != checksum) return false;

        if (!TryParseWeight(frame, 5, decimalPlaces, (frame[1] & 0x40) != 0, out var displayed)) return false;
        if (!TryParseWeight(frame, 12, decimalPlaces, false, out var tare)) return false;

        reading = new TunaylarLoadLine2Reading(
            displayed,
            tare,
            decimalPlaces,
            (frame[1] & 0x02) != 0,
            (frame[1] & 0x04) != 0,
            (frame[1] & 0x08) != 0,
            (frame[2] & 0x02) != 0);
        return true;
    }

    private static bool TryParseWeight(byte[] frame, int offset, int decimalPlaces, bool negative, out decimal weight)
    {
        weight = 0m;
        var digits = Encoding.ASCII.GetString(frame, offset, 6).Trim();
        if (digits.Length == 0 || digits.Any(character => character is < '0' or > '9')) return false;
        if (!decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out weight)) return false;
        for (var i = 0; i < decimalPlaces; i++) weight /= 10m;
        if (negative) weight = -weight;
        return true;
    }
}
