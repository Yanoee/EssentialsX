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
                ***********************************************************");

            EssentialsXRegistry.TryRegister<InfoHelpCommands>(sapi, () =>
            {
                new InfoHelpCommands(sapi).Register();
            }, "InfoHelp");
            EssentialsXRegistry.TryRegister<HomeCommands>(sapi, () =>
            {
                new HomeCommands(sapi).Register();
            }, "Homes");
            EssentialsXRegistry.TryRegister<RulesCommands>(sapi, () =>
            {
                new RulesCommands(sapi).Register();
            }, "Rules");
            EssentialsXRegistry.TryRegister<TPACommands>(sapi, () =>
            {
                new TPACommands(sapi).Register();
            }, "TPA");
            EssentialsXRegistry.TryRegister<TPRCommands>(sapi, () =>
            {
                new TPRCommands(sapi).Register();
            }, "TPR");
            EssentialsXRegistry.TryRegister<BackCommands>(sapi, () =>
            {
                new BackCommands(sapi).Register();
            }, "Back");
            EssentialsXRegistry.TryRegister<SpawnCommands>(sapi, () =>
            {
                new SpawnCommands(sapi).Register();
            }, "Spawn");
            EssentialsXRegistry.TryRegister<RandomTeleport>(sapi, () =>
            {
                new RandomTeleport(sapi).Register();
            }, "RTP");
        }
    }
    public static class EssentialsXRegistry
    {
        private static readonly HashSet<string> loaded = [];
        public static void TryRegister<TCommands>(ICoreServerAPI sapi, Action register, string? moduleNameOverride = null)
        {
            string inferred = typeof(TCommands).Name;
            if (inferred.EndsWith("Commands"))
                inferred = inferred.Substring(0, inferred.Length - "Commands".Length);

            string moduleName = string.IsNullOrWhiteSpace(moduleNameOverride) ? inferred : moduleNameOverride;

            try
            {
                register();
                Register(moduleName);
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module: {1}", moduleName, ex);
            }
        }

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
