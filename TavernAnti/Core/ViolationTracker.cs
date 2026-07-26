using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Alta.Networking.Scripts.Player;
using TavernAnti.Config;
using TavernAnti.Services;

namespace TavernAnti.Core;

/// <summary>
/// Per-player sliding-window suspicion accumulator. Every detector patch reports into this
/// instead of taking enforcement action directly, so thresholds/escalation live in one place.
/// </summary>
public class ViolationTracker(AntiCheatConfigFile config) : IService
{
    private class Record
    {
        public readonly List<(DateTime Time, int Weight)> Events = new();
    }

    private readonly ConcurrentDictionary<IPlayer, Record> _records = new();

    public void Report(IPlayer player, ViolationType type, string detail)
    {
        if (player == null) return;

        try
        {
            var cfg = config.LastRead;
            var weight = cfg.Weights.TryGetValue(type, out var w) ? w : 1;
            var record = _records.GetOrAdd(player, _ => new Record());

            int score;
            lock (record)
            {
                var now = DateTime.UtcNow;
                record.Events.Add((now, weight));

                var windowStart = now - TimeSpan.FromSeconds(cfg.ViolationWindowSeconds);
                record.Events.RemoveAll(e => e.Time < windowStart);

                score = record.Events.Sum(e => e.Weight);
            }

            var username = player.UserInfo?.Username ?? "<unknown>";
            var dryRunTag = cfg.DryRun ? " [DRY RUN]" : "";
            TavernAntiLogger.Warn($"{username} violation={type} detail=\"{detail}\" weight={weight} score={score}{dryRunTag}");

            if (score >= cfg.BanThreshold)
            {
                Escalate(player, username, score, cfg.DryRun, isBan: true);
                lock (record) record.Events.Clear();
            }
            else if (score >= cfg.KickThreshold)
            {
                Escalate(player, username, score, cfg.DryRun, isBan: false);
            }
        }
        catch (Exception e)
        {
            TavernAntiLogger.Error($"Error while recording violation: {e}");
        }
    }

    private static void Escalate(IPlayer player, string username, int score, bool dryRun, bool isBan)
    {
        var reason = $"suspicious activity score {score}";

        if (dryRun)
        {
            TavernAntiLogger.Warn($"[DRY RUN] Would {(isBan ? "ban" : "kick")} {username} ({reason})");
            return;
        }

        if (isBan)
        {
            var userStore = TavernAntiServices.GetService<TrustedUserStore>();
            EnforcementActions.Ban(player, reason, userStore);
        }
        else
        {
            EnforcementActions.Kick(player, reason);
        }
    }

    public void ResetOnDisconnect(IPlayer player)
    {
        if (player == null) return;

        _records.TryRemove(player, out _);
        PlayerMovementState.Clear(player);
    }
}
