using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Logging;
using TavernAnti.Config;
using TavernAnti.Core;
using TavernAnti.Services;

[assembly: MelonInfo(typeof(TavernAnti.TavernAntiPlugin), "TavernAnti", "0.1.0", "Tavern Team", "https://github.com/ModdingTavern/TavernAnti")]
namespace TavernAnti;

public class TavernAntiPlugin : MelonPlugin
{
    internal static MelonLogger.Instance Logger { get; private set; }

    public override void OnEarlyInitializeMelon()
    {
        Logger = LoggerInstance;

        SetupServices();
    }

    public override void OnInitializeMelon()
    {
        try
        {
            // Called explicitly rather than relying on MelonLoader's implicit per-assembly
            // patching, since TavernLib does the latter without an explicit call anywhere
            // and that behavior isn't documented/guaranteed.
            new Harmony("com.moddingtavern.tavernanti").PatchAll(Assembly.GetExecutingAssembly());
            TavernAntiLogger.Msg("Harmony patches applied");
        }
        catch (Exception e)
        {
            Logger.BigError($"Error when applying TavernAnti Harmony patches!!!!! {e}");
            throw;
        }
    }

    private void SetupServices()
    {
        try
        {
            // Every enforcement patch gates on NetworkSceneManager.IsServer, so on a client
            // process these services simply never receive any violation reports - but they
            // are only constructed at all on the dedicated server, same pattern TavernLib
            // uses for its own server-only services.
            if (!CommandLineArguments.Contains(CommandLineArguments.StartServerArgument)) return;

            TavernAntiLogger.Msg("Booting TavernAnti in server mode");

            var config = new AntiCheatConfigFile(TavernAntiDirectories.AntiCheatConfig);
            config.ReadFromFile();
            TavernAntiServices.AddService(config);

            TavernAntiServices.AddService(new TrustedUserStore());
            TavernAntiServices.AddService(new ViolationTracker(config));
        }
        catch (Exception e)
        {
            Logger.BigError($"Error when setting up TavernAnti services!!!!! {e}");
            throw;
        }
    }
}
