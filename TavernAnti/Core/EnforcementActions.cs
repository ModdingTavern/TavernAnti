using Alta.Networking.Scripts.Player;
using TavernAnti.Config;

namespace TavernAnti.Core;

internal static class EnforcementActions
{
    public static void Kick(IPlayer player, string reason)
    {
        try
        {
            var connection = player?.ConnectionToRemotePlayer;
            if (connection == null)
            {
                TavernAntiLogger.Warn($"Wanted to kick {player?.UserInfo?.Username} but no connection was available");
                return;
            }

            TavernAntiLogger.Warn($"Kicking {player.UserInfo?.Username}: {reason}");
            connection.Disconnect($"TavernAnti: {reason}");
        }
        catch (System.Exception e)
        {
            TavernAntiLogger.Error($"Error while kicking player: {e}");
        }
    }

    public static void Ban(IPlayer player, string reason, TrustedUserStore userStore)
    {
        try
        {
            var username = player?.UserInfo?.Username;
            var ip = player?.ConnectionToRemotePlayer?.IpAddress;

            TavernAntiLogger.Warn($"Banning {username} ({ip}): {reason}");

            userStore.AppendBan(username, ip);

            Kick(player, reason);
        }
        catch (System.Exception e)
        {
            TavernAntiLogger.Error($"Error while banning player: {e}");
        }
    }
}
