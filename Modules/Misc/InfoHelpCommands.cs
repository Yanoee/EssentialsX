using EssentialsX.Modules.Home;
using EssentialsX.Modules.Teleport;
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
            "Homes",
            "InfoHelp",
            "Rules",
            "TPA",
            "TPR",
            "Spawn",
            "Back",
            "RTP",
            "Kits",
            "Moderation",
            "JoinLeave",
            "TabList"
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

            if (settings?.Enabled == true)
            {
                sapi.ChatCommands
                   .Create("essentialsx")
                   .WithDescription(msgs.RootDesc)
                   .RequiresPrivilege("chat")
                   .HandleWith(OnHelpCommand)
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

            int count = 0;
            try
            {
                settings = InfoHelpSettings.LoadOrCreate(sapi); count++;
                msgs = InfoHelpMessages.LoadOrCreate(sapi); count++;
                HomeConfig.LoadOrCreate(sapi); count++;
                RulesConfig.LoadOrCreate(sapi); count++;
                TpaConfig.LoadOrCreate(sapi); count++;
                TprConfig.LoadOrCreate(sapi); count++;
                BackConfig.LoadOrCreate(sapi); count++;
                SpawnConfig.LoadOrCreate(sapi); count++;
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Reload error: {0}", ex);
                SendBlock(player, "<font color='#FF0000'>Reload failed. See server log.</font>");
                return TextCommandResult.Success();
            }
            SendBlock(player, $"<font color='#00FF80'>Reloaded {count} JSON files.</font>");
            sapi.World.Logger.Event("[EssentialsX] /essentialsx reload refreshed all module JSON files by {0}", player.PlayerName);
            return TextCommandResult.Success();
        }
        private void SendBlock(IServerPlayer plr, string body)
        {
            plr.SendMessage(GlobalConstants.GeneralChatGroup, $"{msgs.Prefix}: {body}", EnumChatType.Notification);
        }
        private static string EscapeVtml(string s)
            => s.Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
