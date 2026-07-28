using Alta.Global;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Serialization;
using HarmonyLib;
using TavernAnti.Config;
using TavernAnti.Core;
using TavernLib.Services;
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
// SerializeMove is private, so it can't be referenced via nameof() against the raw
// (non-publicized) Root.Township.dll - Harmony resolves this string by reflection at
// runtime instead, regardless of compile-time accessibility.
[HarmonyPatch(typeof(NetworkEntity), "SerializeMove")]
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

        var config = TavernServices.GetService<AntiCheatConfigFile>()?.LastRead;
        var tracker = TavernServices.GetService<ViolationTracker>();
        if (config == null || tracker == null) return;

        var isGrounded = IsGrounded(newPosition, config.FlyGroundCheckDistance);

        var violation = PlayerMovementState.Evaluate(
            player,
            newPosition,
            isGrounded,
            config.MaxTeleportDistance,
            config.MaxPlayerSpeedMps,
            config.SpeedViolationConsecutiveTicks,
            config.FlyMinAscendSpeedMps,
            config.FlyViolationConsecutiveTicks);

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

    /// <summary>
    /// Independent server-side ground check (downward raycast against the same StandableMask
    /// the client's own locomotion uses), deliberately not the client-reported grounded bit -
    /// LocomotionController.IsGrounded is never serialized to the server, and a client capable
    /// of flying could set it to whatever it wants anyway.
    /// </summary>
    private static bool IsGrounded(Vector3 position, float groundCheckDistance)
    {
        var settings = GlobalSettings<SmoothLocomotionSettings>.Instance;
        if (settings == null) return true; // fail open - don't flag before world/settings are ready

        return Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance, settings.StandableMask);
    }
}
