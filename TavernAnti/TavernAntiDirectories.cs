using System.IO;
using TavernLib;

namespace TavernAnti;

public static class TavernAntiDirectories
{
    // TavernAnti's own config, kept in a subfolder of TavernLib's ModdingTavern folder so the
    // two plugins never race on the same file.
    public static string TavernAntiRoot => Path.Combine(TavernDirectories.ModdingTavern, "TavernAnti");
    public static string AntiCheatConfig => Path.Combine(TavernAntiRoot, "anticheat_config.json");
}
