using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TavernAnti.Services;

namespace TavernAnti.Config;

/// <summary>
/// Reads/writes the same on-disk trust-store files TavernLib already owns and enforces
/// (%AppData%\TheModdingTavern\users.json), plus TavernAnti's own operator allow-list.
/// This is a live file-shape dependency, not a build dependency - there is no reference
/// to TavernLib.dll anywhere in this project.
///
/// IMPORTANT: TavernLib's UserConfigFile stores users/whitelist/blacklist together in a
/// single users.json (confirmed by reading TavernApiManager.cs - the separate
/// TavernDirectories.Blacklist/.Whitelist path constants in TavernLib are not actually
/// used for the enforced file). Bans MUST be appended to users.json's embedded "blacklist"
/// node, not a standalone blacklist.json, or TavernLib's AuthManager will never see them.
/// </summary>
public class TrustedUserStore : IService
{
    private const string OperatorsFileName = "operators.json";

    public void AppendBan(string username, string ip)
    {
        try
        {
            var store = ReadUserStore();

            if (!string.IsNullOrWhiteSpace(username) && !store.Blacklist.Usernames.Contains(username))
                store.Blacklist.Usernames.Add(username);

            if (!string.IsNullOrWhiteSpace(ip) && !store.Blacklist.Ips.Contains(ip))
                store.Blacklist.Ips.Add(ip);

            WriteUserStore(store);

            TavernAntiLogger.Warn($"Appended ban for username='{username}' ip='{ip}' to shared blacklist");
        }
        catch (Exception e)
        {
            TavernAntiLogger.Error($"Failed to append ban for username='{username}' ip='{ip}': {e}");
        }
    }

    /// <summary>
    /// v1 permission gate for networked commands (see CommandPermissionPatch): the game's
    /// real command permissions come from a Policy claim on the joining player's identity
    /// token, which isn't retained anywhere accessible after join completes. Rather than
    /// guess at reconstructing a UserInfoWithPermissions/Policies list, TavernAnti ships
    /// its own explicit operator allow-list that server owners populate. This is coarser
    /// than the game's real per-user policy scoping, but it is fail-closed: anyone not on
    /// the list is denied, which is what actually closes the "any player can run admin
    /// commands via RunCommandOnServer reflection" exploit.
    /// </summary>
    public bool IsOperator(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        var operators = ReadOperators();
        return operators.Operators.Any(op => string.Equals(op, username, StringComparison.OrdinalIgnoreCase));
    }

    private UserStore ReadUserStore()
    {
        var path = TavernAntiDirectories.Users;
        Directory.CreateDirectory(TavernAntiDirectories.ModdingTavern);

        if (!File.Exists(path)) return new UserStore();

        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<UserStore>(json) ?? new UserStore();
    }

    private void WriteUserStore(UserStore store)
    {
        var path = TavernAntiDirectories.Users;
        Directory.CreateDirectory(TavernAntiDirectories.ModdingTavern);

        File.WriteAllText(path, JsonConvert.SerializeObject(store, Formatting.Indented));
    }

    private OperatorStore ReadOperators()
    {
        var path = Path.Combine(TavernAntiDirectories.TavernAntiRoot, OperatorsFileName);
        Directory.CreateDirectory(TavernAntiDirectories.TavernAntiRoot);

        if (!File.Exists(path))
        {
            var empty = new OperatorStore();
            File.WriteAllText(path, JsonConvert.SerializeObject(empty, Formatting.Indented));
            return empty;
        }

        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<OperatorStore>(json) ?? new OperatorStore();
    }

    // Mirrors TavernLib's UserConfig shape field-for-field so round-tripping this file
    // never drops the "users" entries TavernLib itself owns.
    private class UserStore
    {
        [JsonProperty("users")] public Dictionary<string, JsonUserEntry> Users { get; set; } = new();
        [JsonProperty("whitelist")] public ListConfig Whitelist { get; set; } = new();
        [JsonProperty("blacklist")] public ListConfig Blacklist { get; set; } = new();
    }

    private class JsonUserEntry
    {
        [JsonProperty("user_id")] public ulong UserId { get; set; }
        [JsonProperty("token")] public string Token { get; set; }
        [JsonProperty("registered_from")] public string RegisteredFrom { get; set; }
    }

    private class ListConfig
    {
        [JsonProperty("usernames")] public List<string> Usernames { get; set; } = [];
        [JsonProperty("ips")] public List<string> Ips { get; set; } = [];
    }

    private class OperatorStore
    {
        [JsonProperty("operators")] public List<string> Operators { get; set; } = [];
    }
}
