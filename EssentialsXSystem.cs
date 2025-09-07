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
                new HomeCommands(sapi, homeSettings, homeMessages).Register();

            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module.", ex);
            }

            // === Misc / Help, Rules, Join-Leave ===
            //Help
            try
            {
                new InfoHelpCommands(sapi).Register();
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module.", ex);
            }
            // Rules
            try
            {
                new Rules(sapi).Register();
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load Rules module: {0}", ex);
            }
            // === Teleport (TPA baseline) ===
            try
            {
                new TPACommands(sapi).Register();
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module.", ex);
            }
            // === Teleport (TPR: pull player to you) ===
            try
            {
                new TPRCommands(sapi).Register(); 
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module.", ex);
            }

        }
    }
}
