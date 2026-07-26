using Alta.Networking;
using Alta.Networking.Scripts.Player;
using HarmonyLib;
using TavernAnti.Core;
using TavernAnti.Services;

namespace TavernAnti.Patches;

/// <summary>
/// StreamAuthorityHelper.LogUnauthorizedMessage is called from ~20 sites across the game
/// (Interactor, Pickup, PlayerProgressionManager, LiquidContainer, etc.) whenever a client
/// writes network data for something it doesn't have authority over - but today it only logs.
/// This turns every one of those existing, already-correct detections into a tracked,
/// escalating violation instead of a log line that scrolls past. No new detection logic.
/// </summary>
[HarmonyPatch(typeof(StreamAuthorityHelper), nameof(StreamAuthorityHelper.LogUnauthorizedMessage))]
public static class UnauthorizedWriteEscalationPatch
{
    [HarmonyPostfix]
    public static void Postfix(IPlayer player)
    {
        if (!NetworkSceneManager.IsServer || NetworkSceneManager.IsLocalTest) return;

        TavernAntiServices.GetService<ViolationTracker>()
            ?.Report(player, ViolationType.UnauthorizedWrite, "StreamAuthorityHelper rejected an unauthorized network write");
    }
}
