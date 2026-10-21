using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WeighingDesk;

/// <summary>Reads the human-readable weight payload emitted by the indicator's manual Send key.</summary>
internal sealed class TunaylarManualTransmitProtocol
{
    private static readonly Regex WeightBeforeUnit = new(
        @"([+-]?[0-9]+(?:[.,][0-9]+)?)\s*(?:\0\s*)?(?:kg|KG)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly StringBuilder _buffer = new();

    public IReadOnlyList<decimal> Push(ReadOnlySpan<byte> incoming)
    {
        foreach (var value in incoming)
        {
            if (value is >= 0x20 and <= 0x7E || value is 0x00 or 0x09 or 0x0A or 0x0D)
                _buffer.Append((char)value);
            else if (value is 0x02 or 0x03 or 0x04)
                _buffer.Append(' ');
        }

        if (_buffer.Length > 2048) _buffer.Remove(0, _buffer.Length - 1024);

        var text = _buffer.ToString();
        var matches = WeightBeforeUnit.Matches(text);
        if (matches.Count == 0) return Array.Empty<decimal>();

        // Manual shape 2 can contain gross, tare and net; the final value is the net value.
        var match = matches[^1];
        var normalized = match.Groups[1].Value.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var weight))
            return Array.Empty<decimal>();

        _buffer.Clear();
        return [weight];
    }

    public void Reset() => _buffer.Clear();
}
