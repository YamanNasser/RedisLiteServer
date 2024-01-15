namespace RedisLiteServer;
public class DataStorage
{
    public Dictionary<string, object> KeyData { get; set; } = [];
    public Dictionary<string, DateTime> KeyExpiry { get; set; } = [];

    public DataStorage()
    {
    }
    public DataStorage(Dictionary<string, object> keyData, Dictionary<string, DateTime> keyExpiry)
    {
        KeyData = keyData;
        KeyExpiry = keyExpiry;
    }

    public (Dictionary<string, object>, Dictionary<string, DateTime>) GetData()
    {
        return (KeyData, KeyExpiry);
    }
}
