using System;
using System.IO;

namespace TavernAnti;

public static class TavernAntiDirectories
{
    public static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    // Shared with TavernLib - TavernAnti reads/writes the same trust store files on disk
    // (no build dependency, just an agreed-upon file layout) so bans take effect immediately
    // via TavernLib's existing AuthManager blacklist check.
    public static string ModdingTavern => Path.Combine(AppData, "TheModdingTavern");
    public static string Users => Path.Combine(ModdingTavern, "users.json");
    public static string Blacklist => Path.Combine(ModdingTavern, "blacklist.json");
    public static string Whitelist => Path.Combine(ModdingTavern, "whitelist.json");

    // TavernAnti's own config, kept in a subfolder so the two plugins never race on the same file.
    public static string TavernAntiRoot => Path.Combine(ModdingTavern, "TavernAnti");
    public static string AntiCheatConfig => Path.Combine(TavernAntiRoot, "anticheat_config.json");
}
