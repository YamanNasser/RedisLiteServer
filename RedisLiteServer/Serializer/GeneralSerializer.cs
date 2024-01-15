using System.Collections;
using System.Globalization;

namespace RedisLiteServer.Serializer;

public class GeneralSerializer : RespSerializer
{
    private const string DefaultDateFormat = "yyyy-MM-dd HH:mm:ss";

    public override string? Serialize(object? message)
    {
        var retult = base.Serialize(message);
        if (retult == default)
        {
            return message switch
            {
                Dictionary<string, object> dictionary => SerializeDictionary(dictionary),
                Dictionary<string, DateTime> dictionary => SerializeDictionary(dictionary),
                Dictionary<string, int> dictionary => SerializeDictionary(dictionary),
                Dictionary<string, double> dictionary => SerializeDictionary(dictionary),
                _ => SerializeObject(message)
            };
        }
        return retult;
    }

    public T Deserialize<T>(string message) where T : new()
    {
        if (string.IsNullOrEmpty(message))
            throw new ArgumentException("Message cannot be null or empty");

        return DeserializeObject<T>(message);
    }


    private Dictionary<string, object> DeserializeDictionary(string respString)
    {
        var deserializedObject = Deserialize(respString);
        if (deserializedObject == null) return null;
        if (deserializedObject is IEnumerable<object> serializedPairs)
        {
            var keyValueStore = new Dictionary<string, object>();

            foreach (var serializedItem in serializedPairs.OfType<IEnumerable<object>>())
            {
                var (keyObject, valueObject) = (serializedItem.ElementAtOrDefault(0), serializedItem.ElementAtOrDefault(1));

                if (keyObject is string keyString && valueObject != null)
                {
                    var key = Deserialize(keyString);
                    var value = Deserialize(valueObject.ToString());

                    if (key is string keyStringCast)
                    {
                        var stringType = CheckStringType(value.ToString());
                        if (stringType.GetType() == typeof(DateTime))
                        {
                            keyValueStore[keyStringCast] = DateTime.Parse(value.ToString());
                        }
                        else if (stringType.GetType() == typeof(int))
                        {
                            keyValueStore[keyStringCast] = int.Parse(value.ToString());
                        }
                        else if (stringType.GetType() == typeof(double))
                        {
                            keyValueStore[keyStringCast] = double.Parse(value.ToString());
                        }
                        else
                        {
                            keyValueStore[keyStringCast] = value;
                        }

                    }
                }
            }

            return keyValueStore;
        }

        throw new ArgumentException("Invalid RESP format for dictionary deserialization.");
    }
    private T DeserializeObject<T>(string respString) where T : new()
    {
        if (string.IsNullOrEmpty(respString))
            throw new ArgumentException("respString cannot be null or empty");

        var deserializedData = Deserialize(respString) as List<object> ?? throw new InvalidOperationException("Deserialized data is not in the expected format.");
        var obj = new T();
        var properties = obj.GetType().GetProperties();

        if (deserializedData.Count != properties.Length)
            throw new InvalidOperationException("Mismatch between number of properties in object and data.");

        for (int i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var propertyValue = ConvertToPropertyType(deserializedData[i], property.PropertyType);
            property.SetValue(obj, propertyValue);
        }

        return obj;
    }
    private object ConvertToPropertyType(object value, Type propertyType)
    {
        if (value == null) return null;

        if (propertyType == typeof(DateTime))
        {
            return DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture);
        }

        if (propertyType == typeof(int))
        {
            return int.Parse(value.ToString(), CultureInfo.InvariantCulture);
        }

        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var inputDictionary = DeserializeDictionary(value.ToString());
            if (inputDictionary != null)
            {
                var type = propertyType.GetGenericArguments()[1];
                if (type != typeof(object))
                {

                    var resultDictionary = Activator
                                          .CreateInstance(typeof(Dictionary<,>)
                                          .MakeGenericType(typeof(string), type)) as IDictionary;

                    foreach (var entry in inputDictionary)
                    {
                        object convertedValue = Convert.ChangeType(entry.Value, type);
                        resultDictionary[entry.Key] = convertedValue;
                    }

                    return resultDictionary;
                }
                else
                    return inputDictionary;
            }
        }

        return default;
    }
    private string SerializeDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        if (dictionary.Count == 0)
            return Serialize(null);

        var serializedPairs = dictionary.Select(kvp => new object[] { Serialize(kvp.Key), Serialize(kvp.Value) }).ToArray();
        return Serialize(serializedPairs);
    }
    private string? SerializeObject(object? obj)
    {
        if (obj == null)
            return Serialize(null);

        var type = obj.GetType();

        if (type == typeof(int))
        {
            return Serialize(obj);
        }
        else if (type == typeof(double))
        {
            return Serialize(obj.ToString());
        }
        else if (type == typeof(DateTime))
        {
            var dateTime = (DateTime)obj;
            return Serialize(dateTime.ToString(DefaultDateFormat));
        }
        else
        {
            var properties = type.GetProperties();
            var serializedProperties = properties.Select(property => Serialize(property.GetValue(obj))).ToArray();
            return Serialize(serializedProperties);
        }
    }
    private object CheckStringType(string inputStr)
    {

        if (double.TryParse(inputStr, out double numericResult))
        {
            return numericResult;
        }

        if (DateTime.TryParse(inputStr, out DateTime dateTimeResult))
        {
            return dateTimeResult;
        }
        return inputStr;
    }
}
