using System.Text;

namespace RedisLiteServer;

public static class Helper
{
    public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
    {
        DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(unixTimeStamp);
    }

    public static string ReadStringFromBinaryFile(string filePath)
    {
        byte[] byteArray;
        using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read))
        {
            byteArray = new byte[fs.Length];
            fs.Read(byteArray, 0, byteArray.Length);
        }
        string data = Encoding.UTF8.GetString(byteArray);
        return data;
    }
    public static void SaveData(string path, string serilizedData)
    {
        byte[] byteArray = Encoding.UTF8.GetBytes(serilizedData);
        using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
        fs.Write(byteArray, 0, byteArray.Length);
    }

}
