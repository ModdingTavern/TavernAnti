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
        public int ConsecutiveAscentTicks;
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
        entry.ConsecutiveAscentTicks = 0;
    }

    /// <summary>
    /// Evaluates an incoming position against the last accepted one without applying it.
    /// Returns null if the move is plausible, otherwise the violation to report.
    /// </summary>
    /// <param name="isGrounded">
    /// Server-computed grounded state for <paramref name="incoming"/> (e.g. a downward raycast) -
    /// never the client-reported bit, since a flying client would just fake that too.
    /// </param>
    public static ViolationType? Evaluate(
        IPlayer player,
        Vector3 incoming,
        bool isGrounded,
        float maxTeleportDistance,
        float maxSpeedMps,
        int speedViolationConsecutiveTicks,
        float minAscendSpeedMps,
        int flyViolationConsecutiveTicks)
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
            entry.ConsecutiveAscentTicks = 0;
            return ViolationType.Teleport;
        }

        // Tracked independently of the speed check below so one doesn't mask the other -
        // a flying client can ascend well under the horizontal speed cap.
        var verticalSpeed = (incoming.y - entry.Position.y) / deltaTime;
        if (!isGrounded && verticalSpeed > minAscendSpeedMps)
        {
            entry.ConsecutiveAscentTicks++;
        }
        else
        {
            entry.ConsecutiveAscentTicks = 0;
        }

        var speed = distance / deltaTime;
        if (speed > maxSpeedMps)
        {
            entry.ConsecutiveOverspeedTicks++;
        }
        else
        {
            entry.ConsecutiveOverspeedTicks = 0;
        }

        // Checked ahead of the speed hack below: a jump's ascent is grounded=false for only a
        // couple of ticks before gravity turns it around, so a sustained run of them (unlike a
        // single tick) isn't explainable by a normal jump.
        if (entry.ConsecutiveAscentTicks >= flyViolationConsecutiveTicks)
        {
            entry.ConsecutiveAscentTicks = 0;
            return ViolationType.Flying;
        }

        if (entry.ConsecutiveOverspeedTicks >= speedViolationConsecutiveTicks)
        {
            entry.ConsecutiveOverspeedTicks = 0;
            return ViolationType.SpeedHack;
        }

        return null;
    }

    public static void Clear(IPlayer player)
    {
        if (player != null) States.TryRemove(player, out _);
    }
}
