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
        private static readonly string[] PlannedModules =
        {
            "Homes","InfoHelp","Rules","TPA","TPR","Spawn","Back","RTP","Kits","Moderation","JoinLeave","TabList"
        };
        public InfoHelpCommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                settings = InfoHelpSettings.LoadOrCreate(sapi);
                msgs = InfoHelpMessages.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                settings = new InfoHelpSettings();
                msgs = new InfoHelpMessages();
                sapi.World.Logger.Error("[EssentialsX] Failed to init InfoHelp module: {0}", ex);
            }
        }

        public void Register()
        {
            sapi.World.Logger.Event("[EssentialsX] Loaded module: {0}", ModuleName);

            if (settings != null && settings.Enabled)
            {
                sapi.ChatCommands
                   .Create("essentialsx")
                   .WithDescription(msgs.RootDesc)
                   .RequiresPrivilege("chat")
                   .HandleWith(OnHelpCommand)   // Root → show help if no subcommand
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
            else
            {
                sapi.World.Logger.Event("[EssentialsX] {0} disabled via settings.", ModuleName);
                return;
            }
        }

        private TextCommandResult OnInfoCommand(TextCommandCallingArgs args)
        {
            if (args.Caller?.Player is not IServerPlayer player)
                return TextCommandResult.Error(msgs.PlayerOnly);

            var mod = sapi.ModLoader.GetMod(settings.ModId);
            string version = mod?.Info?.Version ?? "Unknown";
            var loaded = EssentialsXRegistry.GetLoaded().OrderBy(s => s).ToList();
            int loadedPlanned = loaded.Intersect(PlannedModules).Count();
            int totalPlanned = PlannedModules.Length;
            string status = $"{loadedPlanned}/{totalPlanned} • {string.Join(", ", loaded)}";

            string body = msgs.InfoBody
                .Replace("{description}", msgs.Description)
                .Replace("{status}", status)
                .Replace("{version}", version);

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
                .Concat(player.HasPrivilege("admin") ? msgs.HelpAdmin : Array.Empty<string>());

            var safeLines = lines.Select(EscapeVtml);

            var colored = safeLines.Select(s => string.IsNullOrWhiteSpace(s) ? s : $"{msgs.HelpLinePrefix}{s}{msgs.HelpLineSuffix}");

            var body = new StringBuilder()
                .AppendLine(msgs.HelpTitle)
                .AppendLine(string.Join("\n", colored))
                .ToString();

            SendBlock(player, body);
            return TextCommandResult.Success();
        }

        private TextCommandResult OnReloadCommand(TextCommandCallingArgs args)
        {
            if (args.Caller?.Player is not IServerPlayer player)
                return TextCommandResult.Error(msgs.PlayerOnly);

            SendBlock(player, msgs.Reloading);

            // Reload settings + messages from disk
            settings = InfoHelpSettings.LoadOrCreate(sapi);
            msgs = InfoHelpMessages.LoadOrCreate(sapi);

            SendBlock(player, msgs.Reloaded);
            sapi.World.Logger.Event("[EssentialsX] Configs reloaded by {0}", player.PlayerName);
            return TextCommandResult.Success();
        }

        // --- helpers ---
        private void SendBlock(IServerPlayer plr, string body)
        {
            plr.SendMessage(GlobalConstants.GeneralChatGroup, $"{msgs.Header}\n{msgs.Prefix}\n{body}\n{msgs.Footer}", EnumChatType.Notification);
        }
        private static string EscapeVtml(string s)
            => s.Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
