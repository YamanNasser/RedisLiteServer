using System.Globalization;

namespace RedisLiteServer.Serializer;
public class RespSerializer
{
    public virtual string? Serialize(object? message)
    {
        return message switch
        {
            null => "$-1\r\n",
            string str => SerializeBulkString(str),
            int integer => $":{integer}\r\n",
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
            '+' => message[1..^2], // Simple string
            ':' => int.Parse(message[1..^2], CultureInfo.InvariantCulture), // Integer
            '$' => DeserializeBulkString(message), // Bulk string
            '*' => DeserializeArray(message), // Array
            '-' => message[1..^2], // Error message
            _ => throw new NotSupportedException($"Unsupported RESP type: {message[0]}")
        };
    }

    private string SerializeBulkString(string str) => $"${str.Length}\r\n{str}\r\n";
    private string SerializeArray(IEnumerable<object> array) =>
        $"*{array.Count()}\r\n{string.Join("", array.Select(Serialize))}";

    private string DeserializeBulkString(string message)
    {
        ReadOnlySpan<char> messageSpan = message.AsSpan();

        int lengthIndex = messageSpan.IndexOf('\r');
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
        int countEndIndex = inputSpan.IndexOf('\r');
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
        int currentIndex = inputSpan.Slice(countEndIndex).IndexOf('\n') + countEndIndex + 1;

        for (int i = 0; i < count && currentIndex < input.Length; i++)
        {
            char currentChar = inputSpan[currentIndex];

            switch (currentChar)
            {
                case '$':
                    currentIndex = HandleBulkString(input, currentIndex, elements);
                    break;
                case ':':
                    currentIndex = HandleInteger(input, currentIndex, elements);
                    break;
                case '*':
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
        int lengthIndex = inputSpan.Slice(currentIndex).IndexOf('\r');
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
        int endOfInt = inputSpan.Slice(currentIndex).IndexOf('\r');
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
        int endOfLength = input.IndexOf('\r', start);
        int numberOfElements = int.Parse(input.Substring(start + 1, endOfLength - start - 1));
        int currentIndex = endOfLength + 2; // Move past "\r\n"
        int elementsProcessed = 0;

        while (elementsProcessed < numberOfElements && currentIndex < input.Length)
        {
            if (input[currentIndex] == '$')
            {
                currentIndex = SkipBulkString(input, currentIndex);
            }
            else if (input[currentIndex] == ':')
            {
                currentIndex = input.IndexOf('\n', currentIndex) + 1;
            }
            else if (input[currentIndex] == '*')
            {
                currentIndex = FindEndOfArray(input, currentIndex);
            }

            elementsProcessed++;
        }

        return currentIndex;
    }

    private int SkipBulkString(string input, int currentIndex)
    {
        int lengthIndex = input.IndexOf('\r', currentIndex);
        int length = int.Parse(input.Substring(currentIndex + 1, lengthIndex - currentIndex - 1));

        if (length == -1)
        {
            return input.IndexOf('\n', lengthIndex) + 1; // Move past null bulk string
        }

        return lengthIndex + 2 + length + 2; // Move past the bulk string
    }
}