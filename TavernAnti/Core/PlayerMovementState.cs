using System;
using System.Collections.Concurrent;
using Alta.Networking.Scripts.Player;
using UnityEngine;

namespace TavernAnti.Core;

/// <summary>
/// Tracks each player's last server-accepted position, shared between
/// MovementPlausibilityPatch (which writes it) and InteractionGuardPatch
/// (which reads it to compute reach distance) so both patches agree on
/// "where the player actually is" without re-deriving it independently.
/// </summary>
public static class PlayerMovementState
{
    private class Entry
    {
        public Vector3 Position;
        public DateTime Timestamp;
        public int ConsecutiveOverspeedTicks;
    }

    private static readonly ConcurrentDictionary<IPlayer, Entry> States = new();

    public static Vector3? GetLastKnownPosition(IPlayer player)
    {
        return player != null && States.TryGetValue(player, out var entry) ? entry.Position : null;
    }

    public static void Accept(IPlayer player, Vector3 position)
    {
        if (player == null) return;

        var entry = States.GetOrAdd(player, _ => new Entry { Position = position, Timestamp = DateTime.UtcNow });
        entry.Position = position;
        entry.Timestamp = DateTime.UtcNow;
        entry.ConsecutiveOverspeedTicks = 0;
    }

    /// <summary>
    /// Evaluates an incoming position against the last accepted one without applying it.
    /// Returns null if the move is plausible, otherwise the violation to report.
    /// </summary>
    public static ViolationType? Evaluate(IPlayer player, Vector3 incoming, float maxTeleportDistance, float maxSpeedMps, int speedViolationConsecutiveTicks)
    {
        if (player == null) return null;

        var now = DateTime.UtcNow;

        if (!States.TryGetValue(player, out var entry))
        {
            // First move we've seen for this player this session - nothing to compare against yet.
            States[player] = new Entry { Position = incoming, Timestamp = now };
            return null;
        }

        var distance = Vector3.Distance(entry.Position, incoming);
        var deltaTime = Math.Max((float)(now - entry.Timestamp).TotalSeconds, 0.001f);

        if (distance > maxTeleportDistance)
        {
            entry.ConsecutiveOverspeedTicks = 0;
            return ViolationType.Teleport;
        }

        var speed = distance / deltaTime;
        if (speed > maxSpeedMps)
        {
            entry.ConsecutiveOverspeedTicks++;
            if (entry.ConsecutiveOverspeedTicks >= speedViolationConsecutiveTicks)
            {
                entry.ConsecutiveOverspeedTicks = 0;
                return ViolationType.SpeedHack;
            }

            // Not enough consecutive over-limit ticks yet - could be a lag spike, don't flag yet.
            return null;
        }

        entry.ConsecutiveOverspeedTicks = 0;
        return null;
    }

    public static void Clear(IPlayer player)
    {
        if (player != null) States.TryRemove(player, out _);
    }
}
