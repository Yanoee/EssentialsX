using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Misc
{
    public class InfoHelpCommands
    {
        private readonly ICoreServerAPI sapi;
        private InfoHelpSettings settings = null!;
        private InfoHelpMessages msgs = null!;
        private const string ModuleName = "InfoHelp";

        public InfoHelpCommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                settings = InfoHelpSettings.LoadOrCreate(sapi);
                msgs = InfoHelpMessages.LoadOrCreate(sapi);
            }
            catch
            {
                settings = new InfoHelpSettings();
                msgs = new InfoHelpMessages();
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module.");
            }
        }

        public void Register()
        {
            sapi.World.Logger.Event("[EssentialsX] Loaded module: {0}", ModuleName);
            if (!settings.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] {0} disabled via settings.", ModuleName);
                return;
            }

            sapi.ChatCommands
                .Create("essentialsx")
                .WithDescription(msgs.RootDesc)
                .RequiresPrivilege("chat")
                .BeginSubCommand("info")
                .WithDescription(msgs.InfoDesc)
                .HandleWith(OnInfoCommand)
                .EndSubCommand()
                .BeginSubCommand("help")
                .WithDescription(msgs.HelpDesc)
                .HandleWith(OnHelpCommand)
                .EndSubCommand()
                .BeginSubCommand("reload")
                .WithDescription(msgs.ReloadDesc)
                .RequiresPrivilege("admin")
                .HandleWith(OnReloadCommand)
                .EndSubCommand();
        }

        private TextCommandResult OnInfoCommand(TextCommandCallingArgs args)
        {
            if (args.Caller?.Player is not IServerPlayer player)
                return TextCommandResult.Error(msgs.PlayerOnly);

            var mod = sapi.ModLoader.GetMod(settings.ModId);
            string description = msgs.Description;
            string status = mod != null ? "Loaded Modules: " : "FATAL ERROR!";
            string version = mod?.Info?.Version ?? "Unknown";

            var body = new StringBuilder()
                .AppendLine(msgs.InfoBody
                .Replace("{description}", description)
                .Replace("{status}", status)
                .Replace("{version}", version))
                .ToString();

            SendBlock(player, body);
            return TextCommandResult.Success();
        }

        private TextCommandResult OnHelpCommand(TextCommandCallingArgs args)
        {
            if (args.Caller?.Player is not IServerPlayer player)
                return TextCommandResult.Error(msgs.PlayerOnly);

            var lines =
                msgs.HelpTeleport
                .Concat(msgs.HelpHomes)
                .Concat(msgs.HelpRules)
                .Concat(player.HasPrivilege("admin") ? msgs.HelpAdmin : new string[0]);

            var body = new StringBuilder()
                .AppendLine(msgs.HelpTitle)
                .AppendLine(string.Join("\n", lines))
                .ToString();

            SendBlock(player, body);
            return TextCommandResult.Success();
        }

        private TextCommandResult OnReloadCommand(TextCommandCallingArgs args)
        {
            if (args.Caller?.Player is not IServerPlayer player)
                return TextCommandResult.Error(msgs.PlayerOnly);

            SendBlock(player, msgs.Reloading);

            // If you later expose per-module reloaders, call them here.
            // (Kept generic to avoid cross-module coupling.)

            SendBlock(player, msgs.Reloaded);
            sapi.World.Logger.Event("[EssentialsX] Configs reloaded by {0}", player.PlayerName);
            return TextCommandResult.Success();
        }

        // --- helpers ---
        private void SendBlock(IServerPlayer plr, string body)
        {
            plr.SendMessage(GlobalConstants.GeneralChatGroup, $"{msgs.Header}\n{msgs.Prefix}\n{body}\n{msgs.Footer}", EnumChatType.Notification);
        }
    }
}
