using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace BotArena.Sdk;

/// <summary>
/// Small dependency-free JSON DOM/parser for the bounded canonical actor
/// contract. Object order and duplicate properties are deliberately retained
/// so the typed reader can enforce the host's exact canonical shape.
/// </summary>
internal static class ActorCanonicalJson
{
    public static Node Parse(
        string json,
        int maxDepth,
        int maxCollectionCount)
    {
        var parser = new Parser(
            json,
            maxDepth,
            maxCollectionCount,
            GenericActorContractVersions.MaxCanonicalContractNodes);
        return parser.ParseDocument();
    }

    internal enum Kind
    {
        Object,
        Array,
        String,
        Number,
        True,
        False,
        Null,
    }

    internal readonly record struct Property(string Name, Node Value)
    {
        internal Property(
            string name,
            Node value,
            int rawStart,
            int rawEnd)
            : this(name, value)
        {
            RawStart = rawStart;
            RawEnd = rawEnd;
        }

        internal int RawStart { get; }
        internal int RawEnd { get; }

        public bool NameEquals(string value) =>
            string.Equals(Name, value, StringComparison.Ordinal);
    }

    internal sealed class Node
    {
        private readonly string? _text;
        private readonly string _source;
        private readonly int _rawStart;
        private readonly int _rawEnd;
        private readonly ImmutableArray<Property> _properties;
        private readonly ImmutableArray<Node> _items;

        private Node(
            Kind kind,
            string source,
            int rawStart,
            int rawEnd,
            string? text,
            ImmutableArray<Property> properties,
            ImmutableArray<Node> items)
        {
            ValueKind = kind;
            _source = source;
            _rawStart = rawStart;
            _rawEnd = rawEnd;
            _text = text;
            _properties = properties;
            _items = items;
        }

        public Kind ValueKind { get; }

        public static Node Object(
            string source,
            int rawStart,
            int rawEnd,
            ImmutableArray<Property> properties) =>
            new(
                Kind.Object,
                source,
                rawStart,
                rawEnd,
                null,
                properties,
                default);

        public static Node Array(
            string source,
            int rawStart,
            int rawEnd,
            ImmutableArray<Node> items) =>
            new(
                Kind.Array,
                source,
                rawStart,
                rawEnd,
                null,
                default,
                items);

        public static Node String(
            string source,
            int rawStart,
            int rawEnd,
            string value) =>
            new(
                Kind.String,
                source,
                rawStart,
                rawEnd,
                value,
                default,
                default);

        public static Node Number(
            string source,
            int rawStart,
            int rawEnd) =>
            new(
                Kind.Number,
                source,
                rawStart,
                rawEnd,
                null,
                default,
                default);

        public static Node Literal(
            Kind kind,
            string source,
            int rawStart,
            int rawEnd) =>
            new(
                kind,
                source,
                rawStart,
                rawEnd,
                null,
                default,
                default);

        public string? GetString()
        {
            if (ValueKind != Kind.String)
                throw WrongKind(Kind.String);
            return _text;
        }

        public string GetRawText()
        {
            if (ValueKind != Kind.Number)
                throw WrongKind(Kind.Number);
            return _source[_rawStart.._rawEnd];
        }

        public bool TryGetInt32(out int value)
        {
            if (ValueKind != Kind.Number)
            {
                value = default;
                return false;
            }
            return int.TryParse(
                _source.AsSpan(_rawStart, _rawEnd - _rawStart),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);
        }

        public bool TryGetProperty(string name, out Node value)
        {
            if (ValueKind != Kind.Object)
                throw WrongKind(Kind.Object);
            for (int index = _properties.Length - 1; index >= 0; index--)
            {
                if (_properties[index].NameEquals(name))
                {
                    value = _properties[index].Value;
                    return true;
                }
            }
            value = null!;
            return false;
        }

        public int GetArrayLength()
        {
            if (ValueKind != Kind.Array)
                throw WrongKind(Kind.Array);
            return _items.Length;
        }

        public ObjectEnumerator EnumerateObject()
        {
            if (ValueKind != Kind.Object)
                throw WrongKind(Kind.Object);
            return new ObjectEnumerator(_properties);
        }

        public ArrayEnumerator EnumerateArray()
        {
            if (ValueKind != Kind.Array)
                throw WrongKind(Kind.Array);
            return new ArrayEnumerator(_items);
        }

        public string CanonicalObjectExcluding(
            params string[] excludedPropertyNames)
        {
            if (ValueKind != Kind.Object)
                throw WrongKind(Kind.Object);
            var builder = new StringBuilder(_rawEnd - _rawStart);
            builder.Append('{');
            bool wroteProperty = false;
            foreach (Property property in _properties)
            {
                if (excludedPropertyNames.Contains(
                        property.Name,
                        StringComparer.Ordinal))
                {
                    continue;
                }
                if (wroteProperty)
                    builder.Append(',');
                builder.Append(
                    _source,
                    property.RawStart,
                    property.RawEnd - property.RawStart);
                wroteProperty = true;
            }
            builder.Append('}');
            return builder.ToString();
        }

        private InvalidOperationException WrongKind(Kind expected) =>
            new(
                $"JSON node is {ValueKind}; operation requires {expected}.");

        internal struct ObjectEnumerator
        {
            private readonly ImmutableArray<Property> _values;
            private int _index;

            public ObjectEnumerator(ImmutableArray<Property> values)
            {
                _values = values;
                _index = -1;
            }

            public Property Current => _values[_index];

            public ObjectEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                int next = _index + 1;
                if (next >= _values.Length)
                    return false;
                _index = next;
                return true;
            }
        }

        internal struct ArrayEnumerator
        {
            private readonly ImmutableArray<Node> _values;
            private int _index;

            public ArrayEnumerator(ImmutableArray<Node> values)
            {
                _values = values;
                _index = -1;
            }

            public Node Current => _values[_index];

            public ArrayEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                int next = _index + 1;
                if (next >= _values.Length)
                    return false;
                _index = next;
                return true;
            }
        }
    }

    private sealed class Parser
    {
        private readonly string _json;
        private readonly int _maxDepth;
        private readonly int _maxCollectionCount;
        private readonly int _maxNodeCount;
        private int _index;
        private int _nodeCount;

        public Parser(
            string json,
            int maxDepth,
            int maxCollectionCount,
            int maxNodeCount)
        {
            _json = json;
            _maxDepth = maxDepth;
            _maxCollectionCount = maxCollectionCount;
            _maxNodeCount = maxNodeCount;
        }

        public Node ParseDocument()
        {
            SkipWhitespace();
            Node root = ParseValue(depth: 0);
            SkipWhitespace();
            if (_index != _json.Length)
                throw Error("JSON contains trailing data.");
            return root;
        }

        private Node ParseValue(int depth)
        {
            if (depth > _maxDepth)
                throw Error("JSON nesting limit exceeded.");
            if (_index >= _json.Length)
                throw Error("Unexpected end of JSON.");
            if (++_nodeCount > _maxNodeCount)
                throw Error("JSON node limit exceeded.");

            return _json[_index] switch
            {
                '{' => ParseObject(depth),
                '[' => ParseArray(depth),
                '"' => ParseStringNode(),
                't' => ParseLiteral("true", Kind.True),
                'f' => ParseLiteral("false", Kind.False),
                'n' => ParseLiteral("null", Kind.Null),
                '-' or >= '0' and <= '9' => ParseNumber(),
                _ => throw Error("Unexpected JSON token."),
            };
        }

        private Node ParseObject(int depth)
        {
            int start = _index;
            _index++;
            SkipWhitespace();
            var properties = ImmutableArray.CreateBuilder<Property>();
            if (Consume('}'))
            {
                return Node.Object(
                    _json,
                    start,
                    _index,
                    properties.ToImmutable());
            }

            while (true)
            {
                if (_index >= _json.Length || _json[_index] != '"')
                    throw Error("JSON object property name must be a string.");
                int propertyStart = _index;
                string name = ParseString();
                SkipWhitespace();
                Require(':');
                SkipWhitespace();
                Node value = ParseValue(depth + 1);
                if (properties.Count >= _maxCollectionCount)
                    throw Error("JSON object property limit exceeded.");
                properties.Add(
                    new Property(name, value, propertyStart, _index));
                SkipWhitespace();
                if (Consume('}'))
                {
                    return Node.Object(
                        _json,
                        start,
                        _index,
                        properties.ToImmutable());
                }
                Require(',');
                SkipWhitespace();
                if (_index < _json.Length && _json[_index] == '}')
                    throw Error("JSON object has a trailing comma.");
            }
        }

        private Node ParseArray(int depth)
        {
            int start = _index;
            _index++;
            SkipWhitespace();
            var items = ImmutableArray.CreateBuilder<Node>();
            if (Consume(']'))
            {
                return Node.Array(
                    _json,
                    start,
                    _index,
                    items.ToImmutable());
            }

            while (true)
            {
                if (items.Count >= _maxCollectionCount)
                    throw Error("JSON array item limit exceeded.");
                items.Add(ParseValue(depth + 1));
                SkipWhitespace();
                if (Consume(']'))
                {
                    return Node.Array(
                        _json,
                        start,
                        _index,
                        items.ToImmutable());
                }
                Require(',');
                SkipWhitespace();
                if (_index < _json.Length && _json[_index] == ']')
                    throw Error("JSON array has a trailing comma.");
            }
        }

        private string ParseString()
        {
            Require('"');
            var builder = new StringBuilder();
            while (_index < _json.Length)
            {
                char character = _json[_index++];
                if (character == '"')
                    return builder.ToString();
                if (character < 0x20)
                    throw Error("JSON string contains a control character.");
                if (character == '\\')
                {
                    ParseEscape(builder);
                    continue;
                }
                if (char.IsHighSurrogate(character))
                {
                    if (_index >= _json.Length
                        || !char.IsLowSurrogate(_json[_index]))
                    {
                        throw Error("JSON string has an unpaired surrogate.");
                    }
                    builder.Append(character);
                    builder.Append(_json[_index++]);
                    continue;
                }
                if (char.IsLowSurrogate(character))
                    throw Error("JSON string has an unpaired surrogate.");
                builder.Append(character);
            }
            throw Error("Unterminated JSON string.");
        }

        private Node ParseStringNode()
        {
            int start = _index;
            string value = ParseString();
            return Node.String(_json, start, _index, value);
        }

        private void ParseEscape(StringBuilder builder)
        {
            if (_index >= _json.Length)
                throw Error("Unterminated JSON escape.");
            char escaped = _json[_index++];
            switch (escaped)
            {
                case '"':
                case '\\':
                case '/':
                    builder.Append(escaped);
                    return;
                case 'b':
                    builder.Append('\b');
                    return;
                case 'f':
                    builder.Append('\f');
                    return;
                case 'n':
                    builder.Append('\n');
                    return;
                case 'r':
                    builder.Append('\r');
                    return;
                case 't':
                    builder.Append('\t');
                    return;
                case 'u':
                    AppendUnicodeEscape(builder);
                    return;
                default:
                    throw Error("JSON string contains an invalid escape.");
            }
        }

        private void AppendUnicodeEscape(StringBuilder builder)
        {
            char first = (char)ReadHexQuad();
            if (char.IsLowSurrogate(first))
                throw Error("JSON escape has a lone low surrogate.");
            if (!char.IsHighSurrogate(first))
            {
                builder.Append(first);
                return;
            }

            if (_json.Length - _index < 6
                || _json[_index] != '\\'
                || _json[_index + 1] != 'u')
            {
                throw Error("JSON escape has an unpaired high surrogate.");
            }
            _index += 2;
            char second = (char)ReadHexQuad();
            if (!char.IsLowSurrogate(second))
                throw Error("JSON escape has an invalid surrogate pair.");
            builder.Append(first);
            builder.Append(second);
        }

        private int ReadHexQuad()
        {
            if (_json.Length - _index < 4)
                throw Error("Truncated JSON Unicode escape.");
            int value = 0;
            for (int offset = 0; offset < 4; offset++)
            {
                char character = _json[_index++];
                int digit = character switch
                {
                    >= '0' and <= '9' => character - '0',
                    >= 'a' and <= 'f' => character - 'a' + 10,
                    >= 'A' and <= 'F' => character - 'A' + 10,
                    _ => -1,
                };
                if (digit < 0)
                    throw Error("JSON Unicode escape is not hexadecimal.");
                value = value * 16 + digit;
            }
            return value;
        }

        private Node ParseNumber()
        {
            int start = _index;
            Consume('-');
            if (_index >= _json.Length)
                throw Error("Truncated JSON number.");

            if (Consume('0'))
            {
                if (_index < _json.Length
                    && _json[_index] is >= '0' and <= '9')
                {
                    throw Error("JSON number has a leading zero.");
                }
            }
            else
            {
                RequireDigit(nonZeroFirst: true);
                while (_index < _json.Length
                       && _json[_index] is >= '0' and <= '9')
                {
                    _index++;
                }
            }

            if (Consume('.'))
            {
                RequireDigit(nonZeroFirst: false);
                while (_index < _json.Length
                       && _json[_index] is >= '0' and <= '9')
                {
                    _index++;
                }
            }
            if (_index < _json.Length
                && _json[_index] is 'e' or 'E')
            {
                _index++;
                if (_index < _json.Length
                    && _json[_index] is '+' or '-')
                {
                    _index++;
                }
                RequireDigit(nonZeroFirst: false);
                while (_index < _json.Length
                       && _json[_index] is >= '0' and <= '9')
                {
                    _index++;
                }
            }
            return Node.Number(_json, start, _index);
        }

        private Node ParseLiteral(string text, Kind kind)
        {
            int start = _index;
            if (_json.Length - _index < text.Length
                || !_json.AsSpan(_index, text.Length)
                    .SequenceEqual(text))
            {
                throw Error($"Invalid JSON literal; expected '{text}'.");
            }
            _index += text.Length;
            return Node.Literal(kind, _json, start, _index);
        }

        private void RequireDigit(bool nonZeroFirst)
        {
            if (_index >= _json.Length
                || _json[_index] < (nonZeroFirst ? '1' : '0')
                || _json[_index] > '9')
            {
                throw Error("JSON number requires a digit.");
            }
            _index++;
        }

        private void Require(char expected)
        {
            if (!Consume(expected))
                throw Error($"Expected JSON character '{expected}'.");
        }

        private bool Consume(char expected)
        {
            if (_index >= _json.Length || _json[_index] != expected)
                return false;
            _index++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_index < _json.Length
                   && _json[_index] is ' ' or '\t' or '\r' or '\n')
            {
                _index++;
            }
        }

        private FormatException Error(string message) =>
            new($"{message} (character {_index}).");
    }
}
