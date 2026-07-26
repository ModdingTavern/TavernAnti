using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Serialization;
using HarmonyLib;
using TavernAnti.Config;
using TavernAnti.Core;
using TavernAnti.Services;
using UnityEngine;

namespace TavernAnti.Patches;

/// <summary>
/// Server-side movement plausibility check on NetworkEntity.SerializeMove - the confirmed
/// single biggest gap in the game (player movement is client-authoritative with zero
/// server-side validation anywhere). Countermeasure for fly/noclip/speed-hack/teleport-hack.
///
/// SerializeMove reads/writes position+rotation as part of a larger per-entity serialization
/// pass sharing one Stream, so (unlike InteractionGuardPatch) a prefix cannot safely block the
/// call outright without leaving the stream cursor out of sync for whatever is serialized
/// after it. Instead this lets the original run (so the stream stays framed correctly), then
/// evaluates the resulting transform position in a postfix and snaps it back to the last
/// server-accepted position if implausible.
/// </summary>
[HarmonyPatch(typeof(NetworkEntity), nameof(NetworkEntity.SerializeMove))]
public static class MovementPlausibilityPatch
{
    [HarmonyPrefix]
    public static void Prefix(NetworkEntity __instance, out Vector3 __state)
    {
        __state = __instance.transform.position;
    }

    [HarmonyPostfix]
    public static void Postfix(NetworkEntity __instance, IPlayer player, Stream stream, Vector3 __state)
    {
        if (!NetworkSceneManager.IsServer || NetworkSceneManager.IsLocalTest) return;
        if (!stream.IsReading || player == null) return;

        var newPosition = __instance.transform.position;
        if (newPosition == __state) return; // vanilla didn't apply a move from this call

        var config = TavernAntiServices.GetService<AntiCheatConfigFile>()?.LastRead;
        var tracker = TavernAntiServices.GetService<ViolationTracker>();
        if (config == null || tracker == null) return;

        var violation = PlayerMovementState.Evaluate(
            player,
            newPosition,
            config.MaxTeleportDistance,
            config.MaxPlayerSpeedMps,
            config.SpeedViolationConsecutiveTicks);

        if (violation == null)
        {
            PlayerMovementState.Accept(player, newPosition);
            return;
        }

        tracker.Report(player, violation.Value, $"pos={newPosition} prev={__state}");

        if (config.DryRun) return;

        var lastGood = PlayerMovementState.GetLastKnownPosition(player) ?? __state;
        __instance.transform.position = lastGood;
    }
}
