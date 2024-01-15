using System.Diagnostics;
using System.Text;

namespace RedisLiteServer;

public class CommandProcessor(string path)
{
    private readonly KeyValueStore keyValueStore = new();
    private readonly Serializer.GeneralSerializer serializer = new();
    private readonly string persistenceFilePath = path ?? throw new ArgumentNullException();
    private const string nil = "(nil)";
    private const string ok = "OK";
    public string ProcessCommand(string command)
    {
        if (serializer.Deserialize(command) is not List<object> deserializedCommand)
        {
            return $"Unknown command format: {command}";
        }

        if (deserializedCommand.Count == 0)
        {
            return "Empty command received";
        }

        string commandType = deserializedCommand.FirstOrDefault() as string ?? string.Empty;

        switch (commandType.ToUpper())
        {
            case "COMMAND" when deserializedCommand.Count == 2:
                return SerializeResult(SerializeResult(null));
            case "INFO":
                return SerializeResult(ProcessInfoCommand(deserializedCommand));
            case "SET" when deserializedCommand.Count >= 3:
                return SerializeResult(ProcessSetCommand(deserializedCommand));
            case "GET" when deserializedCommand.Count == 2:
                return SerializeResult(ProcessGetCommand(deserializedCommand));
            case "EXISTS" when deserializedCommand.Count == 2:
                return SerializeResult(ProcessExistsCommand(deserializedCommand));
            case "DEL" when deserializedCommand.Count >= 2:
                return SerializeResult(ProcessDelCommand(deserializedCommand));
            case "INCR" when deserializedCommand.Count == 2:
                return SerializeResult(ProcessIncrCommand(deserializedCommand));
            case "DECR" when deserializedCommand.Count == 2:
                return SerializeResult(ProcessDecrCommand(deserializedCommand));
            case "LPUSH" when deserializedCommand.Count >= 3:
                return SerializeResult(ProcessLpushCommand(deserializedCommand));
            case "RPUSH" when deserializedCommand.Count >= 3:
                return SerializeResult(ProcessRpushCommand(deserializedCommand));
            case "SAVE" when deserializedCommand.Count == 1:
                ProcessSaveCommand(persistenceFilePath);
                return SerializeResult(ok);
            case "LOAD" when deserializedCommand.Count == 1:
                ProcessLoadCommand(persistenceFilePath);
                return SerializeResult(ok);
            default:
                return SerializeResult(null);
        }
    }

    private string SerializeResult(object result)
    {
        return serializer.Serialize(result);
    }

    private object ProcessInfoCommand(List<object> infoCommand)
    {
        var process = Process.GetCurrentProcess();
        TimeSpan uptime = DateTime.Now - process.StartTime;
        var memoryUsage = process.WorkingSet64;

        var info = new StringBuilder();
        info.AppendLine("# Server");
        info.AppendLine("redis_version:1.0.0"); // Replace with actual version
        info.AppendLine($"uptime_in_seconds:{(int)uptime.TotalSeconds}");
        info.AppendLine($"used_memory:{memoryUsage}");
        info.AppendLine($"connected_clients:{1}");
        info.AppendLine("# System");
        info.AppendLine($"os:{Environment.OSVersion}");
        info.AppendLine($".net_version:{Environment.Version}");

        return serializer.Serialize(info.ToString());
    }


    private object ProcessSetCommand(List<object> setCommand)
    {
        if (setCommand.Count < 3)
        {
            return "Error: Too few arguments for 'SET' command";
        }

        if (!(setCommand[1] is string key && setCommand[2] is object value))
        {
            return "Error: Invalid arguments for 'SET' command";
        }

        keyValueStore.Set(key, value, setCommand.GetRange(3, setCommand.Count - 3));

        return ok;
    }

    private object ProcessGetCommand(List<object> getCommand)
    {
        if (getCommand.Count < 2 || getCommand[1] is not string getKey)
        {
            return "(error) Invalid command or key type";
        }

        var value = keyValueStore.Get(getKey);
        if (value == default)
        {
            return nil;
        }

        return value;

    }

    private object ProcessExistsCommand(List<object> existsCommand)
    {
        if (existsCommand.Count < 2 || existsCommand[1] is not string existsKey)
        {
            return "(error) Invalid command or key type";
        }

        bool keyExists = keyValueStore.Exists(existsKey);
        return keyExists ? 1 : 0;
    }

    private object ProcessDelCommand(List<object> delCommand)
    {
        var keys = delCommand.Skip(1)?.Select(o => o.ToString())?.ToList();

        if (keys == null || keys.Count > 0)
            return keyValueStore.Del(keys);

        return 0;
    }

    private object ProcessIncrCommand(List<object> incrCommand)
    {
        if (incrCommand.Count < 2 || incrCommand[1] is not string incrKey)
        {
            return "(error) Invalid command or key type";
        }

        var value = keyValueStore.Incr(incrKey);
        if (value == null)
        {
            return "(error) ERR value is not an integer or out of range";
        }

        return value;
    }

    private object ProcessDecrCommand(List<object> decrCommand)
    {
        if (decrCommand.Count < 2 || decrCommand[1] is not string decrKey)
        {
            return "(error) Invalid command or key type";
        }

        var value = keyValueStore.Decr(decrKey);
        if (value == null)
        {
            return "(error) ERR value is not an integer or out of range";
        }

        return value;
    }

    private object ProcessLpushCommand(List<object> command)
    {
        if (command.Count < 2 || command[1] is not string pushKey)
        {
            return "(error) Invalid command or key type";
        }

        return keyValueStore.Lpush(pushKey, command.Skip(2));
    }

    private object ProcessRpushCommand(List<object> command)
    {
        if (command.Count < 2 || command[1] is not string pushKey)
        {
            return "(error) Invalid command or key type";
        }

        return keyValueStore.Rpush(pushKey, command.Skip(2));
    }

    public void ProcessLoadCommand(string path)
    {
        try
        {
            string data = Helper.ReadStringFromBinaryFile(path);
            if (!string.IsNullOrEmpty(data))
            {
                var storeData = serializer.Deserialize<DataStorage>(data);

                if (storeData != null)
                {
                    keyValueStore.MapFrom(storeData.GetData());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading database state: {ex.Message}");
        }
    }

    private void ProcessSaveCommand(string path)
    {
        var keyValueStoreSnapsho = keyValueStore.GetSnapshot();

        var serilizedData = serializer.Serialize(
            new DataStorage(keyValueStoreSnapsho.Item1, keyValueStoreSnapsho.Item2));
        Helper.SaveData(path, serilizedData);
    }

}
