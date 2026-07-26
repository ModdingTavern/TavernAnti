using System;
using System.IO;
using Newtonsoft.Json;
using TavernAnti.Services;

namespace TavernAnti.Config;

public abstract class AntiCheatConfigFileBase<T>(string filePath) where T : class, new()
{
    private string FilePath { get; set; } = filePath;
    public T LastRead { get; private set; } = new();

    public virtual void ReadFromFile()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            if (!File.Exists(FilePath))
            {
                LastRead = new T();

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(LastRead, Formatting.Indented));

                return;
            }

            var config = File.ReadAllText(FilePath);
            var result = JsonConvert.DeserializeObject<T>(config);
            LastRead = result ?? new T();
        }
        catch (Exception e)
        {
            TavernAntiLogger.Error($"Error when managing file responsible for type {nameof(T)}! {e}");
            throw;
        }
    }

    public virtual void WriteToFile()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(LastRead, Formatting.Indented));
        }
        catch (Exception e)
        {
            TavernAntiLogger.Error($"Error when managing file responsible for type {nameof(T)}! {e}");
            throw;
        }
    }
}

public class AntiCheatConfigFile(string filePath) : AntiCheatConfigFileBase<AntiCheatConfig>(filePath), IService;
