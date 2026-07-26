using TavernLib.Backend.Server.Configs;
using TavernLib.Services;

namespace TavernAnti.Config;

public class AntiCheatConfigFile(string filePath) : ServerConfigFile<AntiCheatConfig>(filePath), IService;
