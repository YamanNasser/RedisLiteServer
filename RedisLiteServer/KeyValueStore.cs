namespace RedisLiteServer;
public class KeyValueStore
{
    protected Dictionary<string, object> KeyData = [];
    protected Dictionary<string, DateTime> KeyExpiry = [];

    public (Dictionary<string, object>, Dictionary<string, DateTime>) GetSnapshot() => (KeyData, KeyExpiry);

    public int Count() => KeyData.Count;

    public void MapFrom((Dictionary<string, object>, Dictionary<string, DateTime>) data)
    {
        ArgumentNullException.ThrowIfNull(data);

        KeyData.Clear();
        KeyExpiry.Clear();

        var snapshot = data;
        if (snapshot.Item1 != null)
        {
            foreach (var (key, value) in snapshot.Item1)
            {
                KeyData.Add(key, value);
            }
        }
        if (snapshot.Item2 != null)
        {
            if (KeyData.Count > 0)
                foreach (var (key, value) in snapshot.Item2)
                {
                    KeyExpiry.Add(key, value);
                }
        }
    }

    public void Set<TValue>(string key, TValue value, List<object> options = null)
    {
        KeyData[key] = value;

        if (options != null)
            ProcessExpirationOptions(key, options);
    }

    public object? Get(string key)
    {
        if (KeyData.TryGetValue(key, out var storedValue))
        {
            if (HasExpired(key))
            {
                RemoveExpiredKey(key);
                return default;
            }

            return storedValue;
        }
        return default;
    }

    public bool Exists(string key)
    {
        return KeyData.ContainsKey(key);
    }

    public int Del(List<string> keys)
    {
        int deletedCount = 0;

        foreach (var keyToDelete in keys)
        {
            if (KeyData.Remove(keyToDelete))
            {
                RemoveExpiredKey(keyToDelete);
                deletedCount++;
            }
        }

        return deletedCount;
    }

    public int? Incr(string key)
    {
        if (KeyData.TryGetValue(key, out var currentValue))
        {
            if (currentValue is int currentIntValue)
            {
                int incrementedValue = checked(currentIntValue + 1);
                KeyData[key] = incrementedValue;
                return incrementedValue;
            }
        }
        return null;
    }

    public int? Decr(string key)
    {
        if (KeyData.TryGetValue(key, out var currentValue))
        {
            if (currentValue is int currentIntValue)
            {
                int incrementedValue = checked(currentIntValue - 1);
                KeyData[key] = incrementedValue;
                return incrementedValue;
            }
        }
        return null;
    }

    public object Lpush(string key, IEnumerable<object> values)
    {
        KeyData.TryGetValue(key, out var listValue);

        if (listValue == default || listValue is not List<object> list)
        {
            list = [];
            KeyData[key] = list;
        }

        list.InsertRange(0, values);
        return list.Count;
    }

    public int Rpush(string key, IEnumerable<object> values)
    {
        KeyData.TryGetValue(key, out var listValue);
        if (listValue == default || listValue is not List<object> list)
        {
            list = [];
            KeyData[key] = list;
        }

        list.AddRange(values);
        return list.Count;
    }


    private void ProcessExpirationOptions(string key, List<object> options)
    {
        int expiryTimeSeconds = 0;

        for (int i = 0; i < options.Count; i += 2)
        {
            if (options[i] as string == "EX")
            {
                expiryTimeSeconds = Convert.ToInt32(options[i + 1]);
            }
            else if (options[i] as string == "PX")
            {
                expiryTimeSeconds = Convert.ToInt32(options[i + 1]) / 1000;
            }
            else if (options[i] as string == "EXAT")
            {

                KeyExpiry[key] = Helper.UnixTimeStampToDateTime(Convert.ToDouble(options[i + 1]));

            }
            else if (options[i] as string == "PXAT")
            {
                KeyExpiry[key] = Helper.UnixTimeStampToDateTime(Convert.ToDouble(options[i + 1]) / 1000);
            }
        }

        if (expiryTimeSeconds > 0)
        {
            KeyExpiry[key] = DateTime.Now.AddSeconds(expiryTimeSeconds);

        }
    }

    private bool HasExpired(string key)
    {
        if (KeyExpiry.TryGetValue(key, out var value))
        {
            if (value != default)
            {
                return DateTime.Now > value;
            }
        }
        return false;
    }

    private void RemoveExpiredKey(string key)
    {
        KeyData.Remove(key);
        KeyExpiry.Remove(key);
    }


}
