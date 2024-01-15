using System.Globalization;

namespace RedisLiteServer.Serializer;
public class RespSerializer
{
    private const char SimpleString = '+';
    private const char Integer = ':';
    private const char BulkString = '$';
    private const char Array = '*';
    private const char ErrorMessage = '-';
    private const string nil = "$-1\r\n";
    private const char cr = '\r';
    private const char lf = '\n';
    private const string crlf = "\r\n";


    public virtual string? Serialize(object? message)
    {
        return message switch
        {
            null => nil,
            string str => SerializeBulkString(str),
            int integer => $"{Integer}{integer}{crlf}",
            IEnumerable<object> array => SerializeArray(array),
            _ => default,
        };
    }

    public virtual object Deserialize(string message)
    {
        if (string.IsNullOrEmpty(message))
            throw new ArgumentException("Message cannot be null or empty");

        return message[0] switch
        {
            SimpleString => message[1..^2],
            Integer => int.Parse(message[1..^2], CultureInfo.InvariantCulture),
            BulkString => DeserializeBulkString(message),
            Array => DeserializeArray(message),
            ErrorMessage => message[1..^2],
            _ => throw new NotSupportedException($"Unsupported RESP type: {message[0]}")
        };
    }

    private string SerializeBulkString(string str) => $"{BulkString}{str.Length}{crlf}{str}{crlf}";
    private string SerializeArray(IEnumerable<object> array) =>
        $"{Array}{array.Count()}{crlf}{string.Join("", array.Select(Serialize))}";

    private string DeserializeBulkString(string message)
    {
        ReadOnlySpan<char> messageSpan = message.AsSpan();

        int lengthIndex = messageSpan.IndexOf(cr);
        if (lengthIndex == -1)
        {
            throw new FormatException("Invalid RESP bulk string format: '\\r' not found.");
        }

        ReadOnlySpan<char> lengthSpan = messageSpan[1..lengthIndex];

        if (int.TryParse(lengthSpan, out var length))
        {
            if (length == -1) return null;

            var startIndex = lengthIndex + 2; // Move past "\r\n"
            if (startIndex + length > message.Length)
            {
                throw new FormatException("Invalid RESP bulk string format: Length exceeds message size.");
            }

            ReadOnlySpan<char> valSpan = messageSpan.Slice(startIndex, length);
            return new string(valSpan); // Converts the span back to a string
        }
        else
        {
            throw new FormatException("Invalid RESP bulk string format: Length is not a valid integer.");
        }
    }

    private List<object> DeserializeArray(string input)
    {
        ReadOnlySpan<char> inputSpan = input.AsSpan();
        int countEndIndex = inputSpan.IndexOf(cr);
        if (countEndIndex == -1)
        {
            throw new FormatException("Invalid array format: '\\r' not found.");
        }

        ReadOnlySpan<char> countSpan = inputSpan.Slice(1, countEndIndex - 1);
        if (!int.TryParse(countSpan, out int count))
        {
            throw new FormatException("Invalid array format: Count is not a valid integer.");
        }

        var elements = new List<object>(count);
        int currentIndex = inputSpan.Slice(countEndIndex).IndexOf(lf) + countEndIndex + 1;

        for (int i = 0; i < count && currentIndex < input.Length; i++)
        {
            char currentChar = inputSpan[currentIndex];

            switch (currentChar)
            {
                case BulkString:
                    currentIndex = HandleBulkString(input, currentIndex, elements);
                    break;
                case Integer:
                    currentIndex = HandleInteger(input, currentIndex, elements);
                    break;
                case Array:
                    currentIndex = HandleNestedArray(input, currentIndex, elements);
                    break;
                default:
                    throw new FormatException("Invalid array format: Unexpected character.");
            }
        }

        return elements;
    }



    private int HandleBulkString(string input, int currentIndex, List<object> elements)
    {
        ReadOnlySpan<char> inputSpan = input.AsSpan();
        int lengthIndex = inputSpan.Slice(currentIndex).IndexOf(cr);
        if (lengthIndex == -1)
        {
            throw new FormatException("Invalid RESP bulk string format: '\\r' not found.");
        }

        lengthIndex += currentIndex; // Adjust lengthIndex to the absolute position in input
        ReadOnlySpan<char> lengthSpan = inputSpan.Slice(currentIndex + 1, lengthIndex - currentIndex - 1);

        if (!int.TryParse(lengthSpan, out int length))
        {
            throw new FormatException("Invalid RESP bulk string format: Length is not a valid integer.");
        }

        currentIndex = lengthIndex + 2; // Move past "\r\n"

        if (length == -1)
        {
            elements.Add(null); // Null bulk string
        }
        else
        {
            if (currentIndex + length > input.Length)
            {
                throw new FormatException("Invalid RESP bulk string format: Length exceeds message size.");
            }

            ReadOnlySpan<char> bulkStringSpan = inputSpan.Slice(currentIndex, length);
            elements.Add(bulkStringSpan.ToString()); // Convert the span to a string
            currentIndex += length + 2; // Move past the bulk string and "\r\n"
        }

        return currentIndex;
    }


    private int HandleInteger(string input, int currentIndex, List<object> elements)
    {
        ReadOnlySpan<char> inputSpan = input.AsSpan();
        int endOfInt = inputSpan.Slice(currentIndex).IndexOf(cr);
        if (endOfInt == -1)
        {
            throw new FormatException("Invalid integer format: '\\r' not found.");
        }

        endOfInt += currentIndex; // Adjust endOfInt to the absolute position in input
        ReadOnlySpan<char> intSpan = inputSpan.Slice(currentIndex + 1, endOfInt - currentIndex - 1);

        if (!int.TryParse(intSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedInt))
        {
            throw new FormatException("Invalid integer format: Unable to parse integer.");
        }

        elements.Add(parsedInt);
        return endOfInt + 2; // Move past "\r\n"
    }

    private int HandleNestedArray(string input, int currentIndex, List<object> elements)
    {
        ReadOnlySpan<char> inputSpan = input.AsSpan();
        int endOfNestedArray = FindEndOfArray(input, currentIndex);

        if (endOfNestedArray == -1 || endOfNestedArray > input.Length)
        {
            throw new FormatException("Invalid nested array format.");
        }

        ReadOnlySpan<char> nestedArraySpan = inputSpan.Slice(currentIndex, endOfNestedArray - currentIndex);

        // Assuming DeserializeArray is refactored to accept 
        elements.Add(DeserializeArray(nestedArraySpan.ToString()));

        return endOfNestedArray;
    }

    private int FindEndOfArray(string input, int start)
    {
        int endOfLength = input.IndexOf(cr, start);
        int numberOfElements = int.Parse(input.Substring(start + 1, endOfLength - start - 1));
        int currentIndex = endOfLength + 2; // Move past "\r\n"
        int elementsProcessed = 0;

        while (elementsProcessed < numberOfElements && currentIndex < input.Length)
        {
            if (input[currentIndex] == BulkString)
            {
                currentIndex = SkipBulkString(input, currentIndex);
            }
            else if (input[currentIndex] == Integer)
            {
                currentIndex = input.IndexOf(lf, currentIndex) + 1;
            }
            else if (input[currentIndex] == Array)
            {
                currentIndex = FindEndOfArray(input, currentIndex);
            }

            elementsProcessed++;
        }

        return currentIndex;
    }

    private int SkipBulkString(string input, int currentIndex)
    {
        int lengthIndex = input.IndexOf(cr, currentIndex);
        int length = int.Parse(input.Substring(currentIndex + 1, lengthIndex - currentIndex - 1));

        if (length == -1)
        {
            return input.IndexOf(lf, lengthIndex) + 1; // Move past null bulk string
        }

        return lengthIndex + 2 + length + 2; // Move past the bulk string
    }
}