using System.Globalization;

namespace GT7LivMan.Core.Svg;

/// <summary>Stateful reader over an SVG "d" attribute string, per the grammar in the SVG spec.</summary>
internal sealed class PathCursor(string source)
{
    private const string CommandLetters = "MmLlHhVvCcSsQqTtAaZz";

    private readonly string _source = source;
    private int _index;

    public bool AtEnd
    {
        get
        {
            SkipSeparators();
            return _index >= _source.Length;
        }
    }

    public void SkipSeparators()
    {
        while (_index < _source.Length && (char.IsWhiteSpace(_source[_index]) || _source[_index] == ','))
        {
            _index++;
        }
    }

    public char? PeekCommand()
    {
        SkipSeparators();
        return _index < _source.Length && CommandLetters.IndexOf(_source[_index]) >= 0
            ? _source[_index]
            : null;
    }

    public char ReadCommand()
    {
        SkipSeparators();
        char c = _source[_index];
        _index++;
        return c;
    }

    public double ReadNumber()
    {
        SkipSeparators();
        int start = _index;
        if (_index < _source.Length && (_source[_index] == '+' || _source[_index] == '-'))
        {
            _index++;
        }

        bool sawDigits = false;
        while (_index < _source.Length && char.IsAsciiDigit(_source[_index]))
        {
            _index++;
            sawDigits = true;
        }

        if (_index < _source.Length && _source[_index] == '.')
        {
            _index++;
            while (_index < _source.Length && char.IsAsciiDigit(_source[_index]))
            {
                _index++;
                sawDigits = true;
            }
        }

        if (_index < _source.Length && (_source[_index] == 'e' || _source[_index] == 'E'))
        {
            int expStart = _index;
            _index++;
            if (_index < _source.Length && (_source[_index] == '+' || _source[_index] == '-'))
            {
                _index++;
            }

            if (_index < _source.Length && char.IsAsciiDigit(_source[_index]))
            {
                while (_index < _source.Length && char.IsAsciiDigit(_source[_index]))
                {
                    _index++;
                }
            }
            else
            {
                _index = expStart;
            }
        }

        if (!sawDigits)
        {
            throw new FormatException($"Expected a number at position {start} in path data '{_source}'.");
        }

        return double.Parse(_source.AsSpan(start, _index - start), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>Reads a single SVG arc flag: exactly one '0' or '1' digit, which may abut the next token with no separator.</summary>
    public bool ReadFlag()
    {
        SkipSeparators();
        if (_index >= _source.Length || (_source[_index] != '0' && _source[_index] != '1'))
        {
            throw new FormatException($"Expected an arc flag ('0' or '1') at position {_index} in path data '{_source}'.");
        }

        bool result = _source[_index] == '1';
        _index++;
        return result;
    }
}
