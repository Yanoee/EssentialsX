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
        private InfoHelpSettings cfg = null!;
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
                cfg = InfoHelpSettings.LoadOrCreate(sapi);
                msgs = InfoHelpMessages.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                cfg = new InfoHelpSettings();
                msgs = new InfoHelpMessages();
                sapi.World.Logger.Error("[EssentialsX] Failed to init {0} module: {1}", ModuleName, ex);
            }
        }
        public void Register()
        {
            try
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
                   .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("module"))
                   .HandleWith(OnHelpCommand)
                   .EndSubCommand()
                   .BeginSubCommand("reload")
                   .WithDescription(msgs.ReloadDesc)
                   .HandleWith(OnReloadCommand) /*Implement Proper PERM solution; not-band-aid*/
                   .EndSubCommand();
                sapi.World.Logger.Event("[EssentialsX] Loaded module: {0}", ModuleName);
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module: {1}", ModuleName, ex);
            }
        }

        // Commands
        private TextCommandResult OnInfoCommand(TextCommandCallingArgs args)
        {
            if (args.Caller?.Player is not IServerPlayer player)
                return TextCommandResult.Error(msgs.PlayerOnly);

            var mod = sapi.ModLoader.GetMod(cfg.ModId);
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

            string? requested = null;
            try { requested = args.RawArgs?.PopWord(); } catch { }
            var enabled = GetEnabledModules();

            if (!string.IsNullOrWhiteSpace(requested))
            {
                var page = BuildModuleHelpPage(requested!, enabled, player);
                if (page == null)
                {
                    SendBlock(player, BuildHelpIndex(enabled, player));
                    return TextCommandResult.Success();
                }

                SendBlock(player, page);
                return TextCommandResult.Success();
            }
            SendBlock(player, BuildHelpIndex(enabled, player));
            return TextCommandResult.Success();
        }
        private TextCommandResult OnReloadCommand(TextCommandCallingArgs args)
        {
            bool isConsole = args.Caller?.Player == null;
            IServerPlayer? player = args.Caller?.Player as IServerPlayer;

            if (!isConsole && player == null)
                return TextCommandResult.Error(msgs.PlayerOnly);

            if (!isConsole)
            {
                if (!player!.HasPrivilege("admin") && !IsBypassed(player))
                {
                    SendBlock(player, msgs.NoPermission);
                    return TextCommandResult.Success();
                }
                SendBlock(player, msgs.Reloading);
            }
            else
            {
                sapi.World.Logger.Event("[EssentialsX] /essentialsx reload invoked by SERVER CONSOLE");
                sapi.World.Logger.Event("[EssentialsX] Reloading configs...");
            }

            int count = 0;
            try
            {
                // Reload our single JSON (counts as 1)
                cfg = InfoHelpSettings.LoadOrCreate(sapi); count++;
                msgs = InfoHelpMessages.LoadOrCreate(sapi); // same file

                // Reload other modules so Enabled/help reflect immediately
                if (Try(() => HomeConfig.LoadOrCreate(sapi))) count++;
                if (Try(() => RulesConfig.LoadOrCreate(sapi))) count++;
                if (Try(() => TpaConfig.LoadOrCreate(sapi))) count++;
                if (Try(() => TprConfig.LoadOrCreate(sapi))) count++;
                if (Try(() => BackConfig.LoadOrCreate(sapi))) count++;
                if (Try(() => SpawnConfig.LoadOrCreate(sapi))) count++;
                if (Try(() => RtpConfig.LoadOrCreate(sapi))) count++;
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Reload error in {0}: {1}", ModuleName, ex);
                if (!isConsole) SendBlock(player!, msgs.ReloadFailed);
                return TextCommandResult.Success();
            }

            if (isConsole)
            {
                sapi.World.Logger.Event("[EssentialsX] Reloaded {0} JSON files.", count);
            }
            else
            {
                SendBlock(player!, msgs.ReloadedFormat.Replace("{count}", count.ToString()));
                sapi.World.Logger.Event("[EssentialsX] /essentialsx reload refreshed JSONs by {0}", player!.PlayerName);
            }
            return TextCommandResult.Success();
        }

        // Helpers
        private bool Try(Action act)
        {
            try { act(); return true; } catch { return false; }
        }
        private bool IsBypassed(IServerPlayer player)
        {
            var name = player.PlayerName ?? "";
            var uid = player.PlayerUID ?? "";
            if (cfg.BypassPlayers.Any(b =>
                string.Equals(b, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(b, uid, StringComparison.OrdinalIgnoreCase)))
                return true;
            var roleCode = player.Role?.Code ?? "";
            foreach (var token in cfg.BypassRoles)
            {
                if (string.Equals(token, roleCode, StringComparison.OrdinalIgnoreCase)) return true;
                if (player.HasPrivilege(token)) return true;
            }
            return false;
        }
        private void SendBlock(IServerPlayer plr, string body)
        {
            plr.SendMessage(GlobalConstants.GeneralChatGroup, $"{msgs.Prefix}: {body}", EnumChatType.Notification);
        }
        private static string EscapeVtml(string s)
            => s.Replace("<", "&lt;").Replace(">", "&gt;");
        private Dictionary<string, bool> GetEnabledModules()
        {
            bool homes = SafeEnabled(() => HomeConfig.LoadOrCreate(sapi).Enabled);
            bool rules = SafeEnabled(() => RulesConfig.LoadOrCreate(sapi).Enabled);
            bool tpa = SafeEnabled(() => TpaConfig.LoadOrCreate(sapi).Enabled);
            bool tpr = SafeEnabled(() => TprConfig.LoadOrCreate(sapi).Enabled);
            bool spawn = SafeEnabled(() => SpawnConfig.LoadOrCreate(sapi).Enabled);
            bool back = SafeEnabled(() => BackConfig.LoadOrCreate(sapi).Enabled);
            bool rtp = SafeEnabled(() => RtpConfig.LoadOrCreate(sapi).Enabled);

            return new Dictionary<string, bool>
            {
                ["Teleport"] = tpa || tpr, 
                ["Homes"] = homes,
                ["Rules"] = rules,
                ["Spawn"] = spawn,
                ["Back"] = back,
                ["RTP"] = rtp
            };
        }
        private bool SafeEnabled(Func<bool> getter)
        {
            try { return getter(); } catch { return false; }
        }
        private string BuildHelpIndex(Dictionary<string, bool> enabled, IServerPlayer player)
        {
            var sections = new List<string> { msgs.HelpTitle };

            void add(List<string> lines)
            {
                var safe = lines.Select(EscapeVtml);
                sections.Add(string.Join("\n", safe.Select(L => string.IsNullOrWhiteSpace(L) ? L : $"{msgs.HelpLinePrefix}{L}{msgs.HelpLineSuffix}")));
            }

            if (enabled.GetValueOrDefault("Teleport")) add(msgs.HelpTeleport);
            if (enabled.GetValueOrDefault("Homes")) add(msgs.HelpHomes);
            if (enabled.GetValueOrDefault("Rules")) add(msgs.HelpRules);
            if (enabled.GetValueOrDefault("Spawn")) add(msgs.HelpSpawn);
            if (enabled.GetValueOrDefault("Back")) add(msgs.HelpBack);
            if (enabled.GetValueOrDefault("RTP")) add(msgs.HelpRtp);

            if (player.HasPrivilege("admin") || IsBypassed(player)) /*TODO; Admin only page/commands*/
                add(msgs.HelpAdmin);

            return string.Join("\n", sections);
        }
        private string? BuildModuleHelpPage(string moduleArg, Dictionary<string, bool> enabled, IServerPlayer player)
        {
            var key = moduleArg.Trim().ToLowerInvariant();
            List<string>? page = null;
            bool allowed = true;

            switch (key)
            {
                case "teleport":
                case "tp":
                case "tpa":
                case "tpr":
                    if (!enabled.GetValueOrDefault("Teleport")) return null;
                    page = msgs.HelpTeleport; break;

                case "homes":
                case "home":
                    if (!enabled.GetValueOrDefault("Homes")) return null;
                    page = msgs.HelpHomes; break;

                case "rules":
                    if (!enabled.GetValueOrDefault("Rules")) return null;
                    page = msgs.HelpRules; break;

                case "spawn":
                    if (!enabled.GetValueOrDefault("Spawn")) return null;
                    page = msgs.HelpSpawn; break;

                case "back":
                    if (!enabled.GetValueOrDefault("Back")) return null;
                    page = msgs.HelpBack; break;

                case "rtp":
                case "randomteleport":
                    if (!enabled.GetValueOrDefault("RTP")) return null;
                    page = msgs.HelpRtp; break;

                case "admin":
                    allowed = player.HasPrivilege("admin") || IsBypassed(player);
                    page = allowed ? msgs.HelpAdmin : null;
                    break;

                default:
                    return null;
            }

            if (page == null) return null;

            var safe = page.Select(EscapeVtml);
            var colored = safe.Select(s => string.IsNullOrWhiteSpace(s) ? s : $"{msgs.HelpLinePrefix}{s}{msgs.HelpLineSuffix}");
            var body = new StringBuilder()
                .AppendLine(msgs.HelpTitle)
                .AppendLine(string.Join("\n", colored))
                .ToString();

            return body;
        }
    }
}
