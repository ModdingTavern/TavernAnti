using System.Collections.Generic;
using Newtonsoft.Json;
using TavernAnti.Core;

namespace TavernAnti.Config;

public class AntiCheatConfig
{
    // Movement plausibility (MovementPlausibilityPatch)
    [JsonProperty("max_player_speed_mps")] public float MaxPlayerSpeedMps { get; set; } = 12f;
    [JsonProperty("max_teleport_distance")] public float MaxTeleportDistance { get; set; } = 15f;
    [JsonProperty("speed_violation_consecutive_ticks")] public int SpeedViolationConsecutiveTicks { get; set; } = 3;

    // Interaction range/rate (InteractionGuardPatch)
    [JsonProperty("max_interact_reach")] public float MaxInteractReach { get; set; } = 3.0f;
    [JsonProperty("max_interacts_per_second")] public int MaxInteractsPerSecond { get; set; } = 8;

    // Escalation
    [JsonProperty("violation_weights")]
    public Dictionary<ViolationType, int> Weights { get; set; } = new()
    {
        [ViolationType.SpeedHack] = 2,
        [ViolationType.Teleport] = 5,
        [ViolationType.InteractRange] = 3,
        [ViolationType.InteractRate] = 2,
        [ViolationType.UnauthorizedWrite] = 4,
        [ViolationType.CommandPermission] = 10,
    };

    [JsonProperty("violation_window_seconds")] public int ViolationWindowSeconds { get; set; } = 300;
    [JsonProperty("warn_threshold")] public int WarnThreshold { get; set; } = 5;
    [JsonProperty("kick_threshold")] public int KickThreshold { get; set; } = 15;
    [JsonProperty("ban_threshold")] public int BanThreshold { get; set; } = 30;

    // Log-only until thresholds are tuned against a real server's traffic.
    [JsonProperty("dry_run")] public bool DryRun { get; set; } = true;
}
