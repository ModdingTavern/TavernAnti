using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TavernLib;
using TavernLib.Services;

namespace TavernAnti.Config;

/// <summary>
/// Reads/writes the same on-disk trust store TavernLib already owns and enforces
/// (%AppData%\TheModdingTavern\users.json), via TavernLib.TavernDirectories for the path (a
/// real build dependency now - see TavernAnti.csproj's reference to TavernLib.dll).
///
/// IMPORTANT: TavernLib's UserConfigFile stores users/whitelist/blacklist together in a single
/// users.json (confirmed by reading TavernApiManager.cs - the separate
/// TavernDirectories.Blacklist/.Whitelist path constants in TavernLib are not actually used for
/// the enforced file). Bans MUST be appended to users.json's embedded "blacklist" node, or
/// TavernLib's AuthManager will never see them.
///
/// Reads/writes go through a loose JObject rather than TavernLib's own typed UserConfigFile/
/// UserConfig - a deliberate choice even though TavernLib.dll is now a real reference. The live
/// schema has already grown fields (a "roles" array per user, a "user_ids" list on blacklist)
/// that aren't in TavernLib's own checked-in UserConfig class as of this writing, meaning that
/// class is behind whatever's actually deployed. Round-tripping through it would silently drop
/// any field it doesn't know about on every write-back (exactly what an earlier version of this
/// file did with its own equivalent mirror). Parsing loosely means TavernAnti only ever touches
/// the specific nodes it cares about and passes everything else through untouched, so it can't
/// corrupt data TavernLib owns no matter how that schema keeps evolving - independent of whether
/// TavernLib's own C# model of the file has caught up.
/// </summary>
public class TrustedUserStore : IService
{
    // The role string in a user's "roles" array that grants TavernAnti's elevated trust
    // (running networked console commands, claiming a developer identity-token role).
    private const string TrustedRole = "owner";

    public void AppendBan(string username, string ip)
    {
        try
        {
            var root = ReadUsersFile();
            var blacklist = root["blacklist"] as JObject;
            if (blacklist == null)
            {
                blacklist = new JObject();
                root["blacklist"] = blacklist;
            }

            AppendIfMissing(blacklist, "usernames", username);
            AppendIfMissing(blacklist, "ips", ip);

            WriteUsersFile(root);

            TavernAntiLogger.Warn($"Appended ban for username='{username}' ip='{ip}' to shared blacklist");
        }
        catch (Exception e)
        {
            TavernAntiLogger.Error($"Failed to append ban for username='{username}' ip='{ip}': {e}");
        }
    }

    /// <summary>
    /// Permission gate for networked commands (CommandPermissionPatch) and claimed
    /// identity-token roles (IdentityTokenClaimGuardPatch/DeveloperClaimGuardPatch): a user is
    /// trusted if users.json's users[username].roles contains TrustedRole ("owner"). Reusing
    /// TavernLib's own trust store instead of a separate TavernAnti-owned allow-list means
    /// there's one place server owners manage who's trusted, not two. Fail-closed: any lookup
    /// failure (missing user, missing/malformed roles array, missing file) returns false.
    /// </summary>
    public bool IsOperator(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        try
        {
            var root = ReadUsersFile();
            var users = root["users"] as JObject;

            // TavernLib keys the users map by lowercased username (see AuthManager.cs -
            // payload.Username.ToLower()) - match that convention for the lookup.
            var entry = users?[username.ToLowerInvariant()] as JObject;
            var roles = entry?["roles"] as JArray;

            return roles != null && roles.Any(r => string.Equals(r.Value<string>(), TrustedRole, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception e)
        {
            TavernAntiLogger.Error($"Failed to check trusted role for username='{username}': {e}");
            return false;
        }
    }

    private static void AppendIfMissing(JObject blacklist, string arrayPropertyName, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var array = blacklist[arrayPropertyName] as JArray;
        if (array == null)
        {
            array = new JArray();
            blacklist[arrayPropertyName] = array;
        }

        if (!array.Any(v => string.Equals(v.Value<string>(), value, StringComparison.OrdinalIgnoreCase)))
            array.Add(value);
    }

    private JObject ReadUsersFile()
    {
        var path = TavernDirectories.Users;
        Directory.CreateDirectory(TavernDirectories.ModdingTavern);

        if (!File.Exists(path)) return new JObject();

        var json = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
    }

    private void WriteUsersFile(JObject root)
    {
        var path = TavernDirectories.Users;
        Directory.CreateDirectory(TavernDirectories.ModdingTavern);

        File.WriteAllText(path, root.ToString(Formatting.Indented));
    }
}
