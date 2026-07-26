# TavernAnti

Server-authoritative anti-cheat for *A Township Tale*. Ships as a MelonLoader `MelonPlugin`
(same category as [TavernLib](https://github.com/ModdingTavern/TavernLib)) that Tavern-hosted
dedicated servers run to independently validate player movement, interactions, and console
commands - regardless of what the connecting client has installed. A client-side anti-cheat
can't stop a cheater who simply doesn't install it; this only does anything on the server.

TavernAnti is a separate repo, but **does have a build dependency on TavernLib**, and now a
runtime one too: it references a built `TavernLib.dll` (dropped into `Dependencies\` like every
other dependency, not a cross-repo MSBuild `ProjectReference`) and reuses TavernLib's public
building blocks directly - `TavernLib.Services.IService`/`TavernServices` (the service locator),
`TavernLib.Backend.Server.Configs.ServerConfigFile<T>` (the generic JSON config base), and now
`TavernLib.Backend.Api.TavernApiManager.UserConfig` itself (TavernLib's live, in-memory user
store - see `TrustedUserStore`) - instead of maintaining its own copies. TavernLib added `Roles`
(per-user) and `UserIds` (on `blacklist`) to its `UserConfig` model specifically so TavernAnti
could depend on it directly rather than parsing `users.json` independently. **TavernLib is now a
hard runtime requirement**, not just a same-file cooperation: without it installed and running in
server mode, `TrustedUserStore` fails closed (no operator trust, bans log but don't persist). The
one thing TavernAnti still keeps its own copy of is its logger (`TavernAntiLogger`), since
TavernLib's is `internal` and, more importantly, tied to TavernLib's own MelonLoader console
identity - reusing it would make TavernAnti's log lines appear attributed to TavernLib.

## Why

Research into the decompiled game confirmed it ships with **no anti-cheat layer at all**, and
critically, **player movement is client-authoritative with zero server-side sanity checks
anywhere**. Working exploit mods (not included in this repo) demonstrate item-vacuuming,
speed-hacks, fly/noclip, arbitrary console-command execution, and more, all of which work
specifically because the server trusts whatever a connected client claims.

## What's implemented (Tier 1)

| Patch | Closes |
|---|---|
| `MovementPlausibilityPatch` | Speed-hacks, teleport-hacks, fly/noclip - the single biggest confirmed gap. Evaluates every server-side position update against the last accepted one; snaps the transform back if implausible. |
| `InteractionGuardPatch` | Item-vacuum, long-range raycast-grab, auto-steal. Rejects `Interact`/`InteractEnd` messages beyond plausible hand-reach or above a per-second rate limit. |
| `UnauthorizedWriteEscalationPatch` | Turns the game's own existing (but log-only) `StreamAuthorityHelper.LogUnauthorizedMessage` detections into tracked, escalating violations. No new detection logic. |
| `CommandPermissionPatch` | Arbitrary console-command execution (the `RunCommandOnServer` reflection exploit). See the in-code doc comment on this file for the full trace through `CommandSync`/`CommandService.Handle` - this is the one patch that **requires live verification** before trusting in enforcement mode; see below. |
| `IdentityTokenClaimGuardPatch` | Root-cause fix for forged `"Policy":"dev"` identity-token claims. `JWTUtility.CreateFromString` - the single decode path every identity token in the codebase goes through - never validates a signature at all, for any caller; any player can hand-craft a token with any claims. This rewrites the raw token string to strip an unverifiable `"Policy"` claim (unless the claiming user has `"owner"` in their `users.json` roles) *before* `JWTUtility.CreateFromString` runs, so every consumer downstream - including `ServerPlayerConnectionHandlerOld.CheckIfPlayerIsAllowed`'s "skip allowed check for dev join token" fast path, which otherwise bypasses almost every other join check - sees a token that never had the claim. Fails open (leaves the token untouched) on any parsing surprise. |
| `DeveloperClaimGuardPatch` | Defense-in-depth alongside `IdentityTokenClaimGuardPatch`: downgrades `IsDeveloper` back to `false` on `UserRolesUtility.GetRolesFromIdentityToken`'s result unless the claiming user has `"owner"` in their `users.json` roles, in case something ever constructs a `JwtSecurityToken` without going through `JWTUtility`. Rarely has anything left to do once the patch above is in place. |

All patches gate on `NetworkSceneManager.IsServer && !NetworkSceneManager.IsLocalTest`, so the
client-side copy of the plugin (which must still be installed, since MelonLoader loads
everything in `Plugins\`) is a silent no-op.

## Not implemented / explicit non-goals

- **`StakeRateLimitPatch` (land-claim automation rate limiting)** - not implemented yet. The
  exploit tool's own method names (`OutlineChunkWithStakes`/`DropStakeAt`) don't correspond to
  any class found in the decompiled game source; the exploit DLL itself needs to be decompiled
  first to find the actual target. Sequenced last per the plan.
- **`PlatformClaimAuditPatch` (PC-as-VR platform spoofing)** - not implemented. Would only ever
  be a low-weight, log-only telemetry signal (never a kick/ban trigger on its own), since it
  can't be definitively verified server-side.
- **ESP / wallhacks / minimap radar / full map reveal** - out of scope. The client already
  legitimately receives this data over the wire (no interest-management/visibility culling
  exists in the replication model), so a server-side patch can't stop a client from reading data
  it was already sent. Needs a much larger architecture change.
- **External C2 (outbound WebSocket/REST from a cheat client to a companion service)** -
  happens entirely within the cheater's own process; nothing server-side to hook.
- **Trade/vendor exploits** - a patch on `Alta.Trading.TradeVendor` was observed in the wild but
  its exact effect is unconfirmed. Deferred until this plugin's own logging can characterize it.

No remaining known gaps in the developer-role/identity-token-claim exploit family as of
`IdentityTokenClaimGuardPatch` - see that file's doc comment for the full trace of what it
closes and why the fix is applied at the shared decode choke point rather than per-consumer.

## Setup

1. Populate `Dependencies\`: copy the game's managed DLLs (`Newtonsoft.Json.dll`, `NLog.dll`,
   `UnityEngine*.dll`, the full `Alta.*.dll` set, `kcp2k.dll`) from
   `<GameDir>\A Township Tale_Data\Managed\`, `0Harmony.dll`/`MelonLoader.dll`/`MonoMod.*.dll`
   from a MelonLoader install (`<GameDir>\MelonLoader\net472\`), and `TavernLib.dll` from a
   TavernLib build (`TavernLib\TavernLib\bin\<Config>\TavernLib.dll` - build that repo first;
   it needs the same `Dependencies\` population plus a publicized
   `Generated\Root.Township-publicized.dll`, see TavernLib's own setup). **`TavernLib.dll` is a
   plain copied file, not a project reference** - if TavernLib changes (e.g. its `UserConfig`
   shape again), rebuild TavernLib and re-copy the DLL, or TavernAnti will keep building against
   a stale copy with no warning.
2. **No publicizer needed for TavernAnti itself** - `Root.Township` is referenced directly from
   `<GameDir>\A Township Tale_Data\Managed\Root.Township.dll` (see the `HintPath` in
   `TavernAnti.csproj`), not a publicized copy in `Generated\`. This is simpler to set up but
   means the compiler enforces real C# accessibility: any Harmony patch target or field access
   on a `private`/`internal` game member won't compile as normal dot-syntax or `nameof(...)`.
   The fix, already applied everywhere this comes up, is consistent:
   - **Private method as a Harmony patch target**: use a plain string instead of `nameof`, e.g.
     `[HarmonyPatch(typeof(NetworkEntity), "SerializeMove")]` - Harmony resolves the name via
     reflection at runtime regardless of compile-time visibility. See
     `MovementPlausibilityPatch`/`CommandPermissionPatch`.
   - **Private field access**: read it via `AccessTools.Field(typeof(X), "fieldName")` (cached
     as a `static readonly FieldInfo`) instead of `instance.fieldName`. See
     `InteractionGuardPatch.EntityField` / `EnforcementActions.IpAddressField`.
   - If a fresh build error names another private member, apply the same recipe rather than
     widening scope elsewhere.
3. Restore NuGet packages for `TavernAnti\packages.config` (JWT/IdentityModel packages, used by
   `DeveloperClaimGuardPatch`/`IdentityTokenClaimGuardPatch`'s `JwtSecurityToken` handling) -
   `nuget.exe restore TavernAnti.sln` works even without `dotnet restore` support for
   `packages.config`-style projects.
4. Build `TavernAnti.sln`. Output is `TavernAnti\bin\<Config>\TavernAnti.dll`.
5. Drop the built DLL into the dedicated server's `Plugins\` folder, alongside `MelonLoader`
   and `TavernLib`.

## Configuration

`%AppData%\TheModdingTavern\TavernAnti\anticheat_config.json`, created with defaults on first
run. **Ships with `"dry_run": true`** - every patch still evaluates and logs violations, but
takes no enforcement action (no position snap-back, no dropped interactions, no kicks/bans).
Run in dry-run against real server traffic first to tune `max_player_speed_mps`,
`max_interact_reach`, and the violation thresholds before flipping it off.

**Trust** reuses TavernLib's own live `UserConfig` (backed by
`%AppData%\TheModdingTavern\users.json`) rather than a separate TavernAnti-owned allow-list: a
user is trusted for anything TavernAnti can't otherwise verify (running server console commands
via the networked path in `CommandPermissionPatch`, claiming an elevated identity-token role like
`"Policy":"dev"` in `IdentityTokenClaimGuardPatch`/`DeveloperClaimGuardPatch`) if their entry
under `users` has `"owner"` in its `"roles"` array:

```json
"users": {
  "ftwimcody": {
    "user_id": 2000000001,
    "token": "...",
    "registered_from": "...",
    "roles": ["owner"]
  }
}
```

No `"roles"` entry (or no `"owner"` in it) means fully denied for that user - there's one place
server owners manage who's trusted, not two. `TrustedUserStore` doesn't read/write the file
itself at all: it fetches `TavernApiManager.UserConfig` from `TavernServices` and reads/mutates
that live object directly, the exact same instance TavernLib's own `AuthManager` uses. That
avoids the lost-update risk of two independent readers/writers touching the same file, and means
a ban or role change is visible to TavernLib immediately, without a round trip through disk.

## Verification status

**Builds cleanly** against a real local game install + a locally-built `TavernLib.dll` (only the
harmless AMD64/MSIL `MSB3270` warning, same as TavernLib itself). Not yet runtime-verified.
Before relying on this in production, confirm the two private-member Harmony targets
(`NetworkEntity.SerializeMove`, `CommandSync.SyncCommand`) and the overload-disambiguated target
(`UserRolesUtility.GetRolesFromIdentityToken(JwtSecurityToken)`) actually bind at runtime
(MelonLoader logs a warning if a `[HarmonyPatch]` target fails to resolve) - these bind by name
(and, for the last one, argument types) and will silently fail to patch if a method signature has
shifted since this was written. Then:

1. Local dedicated server + normal client, `dry_run: true`, confirm baseline join/play is
   unaffected and nothing logs spuriously during normal play.
2. A second client running a copy of a known exploit mod against the local test server:
   trigger fly/speed-hack, item-vacuum/long-range grab, and the `RunCommandOnServer`
   reflection call as a non-operator account. Confirm each is flagged, and with `dry_run:
   false`, actually blocked.
3. Confirm a ban actually writes to `users.json`'s `blacklist` node and a follow-up join
   attempt is denied by TavernLib's `AuthManager`.
4. Craft (or patch a client to send) a join token with a `"Policy":"dev"` claim as a
   non-operator, non-VR (e.g. desktop) client. Confirm the join is still denied with "You will
   need a VR headset to play" rather than sailing through the dev fast path. Then add `"owner"`
   to that user's `roles` in `users.json` and confirm the same join now succeeds - this is the
   one check that verifies `IdentityTokenClaimGuardPatch`'s string rewrite round-trips correctly
   through the original `JWTUtility.CreateFromString` (padding/encoding mismatches would surface
   here as a join failure for *everyone*, not just forged tokens, so this step matters even for
   legitimate players).

Never run exploit-mod binaries against a real production Tavern server - use a throwaway local
test server only.
