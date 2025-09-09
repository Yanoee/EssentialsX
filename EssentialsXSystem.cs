using EssentialsX.Modules.Home;
using EssentialsX.Modules.Misc;
using EssentialsX.Modules.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace EssentialsX
{
    public class EssentialsXSystem : ModSystem
    {
        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            // --- Banner ---
            string version = Mod.Info.Version ?? "Error - ModInfo missing (?)";
            sapi.World.Logger.Event($@"

                ***********************************************************
                *    _____                   _   _       _     __   __    *
                *   |  ___|                 | | (_)     | |    \ \ / /    *
                *   | |__ ___ ___  ___ _ __ | |_ _  __ _| |___  \ V /     *
                *   |  __/ __/ __|/ _ \ '_ \| __| |/ _` | / __| /   \     *
                *   | |__\__ \__ \  __/ | | | |_| | (_| | \__ \/ /^\ \    *
                *   \____/___/___/\___|_| |_|\__|_|\__,_|_|___/\/   \/    *
                *                                                         *
                *      EssentialsX by Yanoee - Version: {version}         *
                ***********************************************************
            ");

            // === Homes ===
            try
            {
                var homeSettings = HomeSettings.LoadOrCreate(sapi);
                var homeMessages = HomeMessages.LoadOrCreate(sapi);

                if (homeSettings.Enabled)
                {
                    new HomeCommands(sapi, homeSettings, homeMessages).Register();
                    EssentialsXRegistry.Register("Homes");
                }
                else
                {
                    sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: Homes");
                }
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load Homes module: {0}", ex);
            }

            // === Misc / InfoHelp ===
            try
            {
                var infoSettings = InfoHelpSettings.LoadOrCreate(sapi);
                if (infoSettings.Enabled)
                {
                    new InfoHelpCommands(sapi).Register();
                    EssentialsXRegistry.Register("InfoHelp");
                }
                else
                {
                    sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: InfoHelp");
                }
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load InfoHelp module: {0}", ex);
            }

            // === Rules ===
            try
            {
                var rulesSettings = Rules.RulesSettings.LoadOrCreate(sapi);
                if (rulesSettings.Enabled)
                {
                    new Rules(sapi).Register();
                    EssentialsXRegistry.Register("Rules");
                }
                else
                {
                    sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: Rules");
                }
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load Rules module: {0}", ex);
            }

            // === Teleport (TPA) ===
            try
            {
                new TPACommands(sapi).Register();
                EssentialsXRegistry.Register("TPA");
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load TPA module: {0}", ex);
            }

            // === Teleport (TPR) ===
            try
            {
                new TPRCommands(sapi).Register();
                EssentialsXRegistry.Register("TPR");
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load TPR module: {0}", ex);
            }
            // === Teleport (Back) ===
            try
            {
                new BackCommands(sapi).Register();
                EssentialsXRegistry.Register("Back");
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load Back module: {0}", ex);
            }
            // === Teleport (Spawn) ===
            try
            {
                new SpawnCommands(sapi).Register();
                EssentialsXRegistry.Register("Spawn");
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load Spawn module: {0}", ex);
            }
        }
    }

    // essentialsx info 
    public static class EssentialsXRegistry
    {
        private static readonly HashSet<string> loaded = new();

        public static void Register(string moduleName)
        {
            if (!string.IsNullOrWhiteSpace(moduleName))
            {
                loaded.Add(moduleName);
            }
        }

        public static IReadOnlyCollection<string> GetLoaded() => loaded;
    }
}
