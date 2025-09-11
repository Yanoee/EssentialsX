using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Misc
{
    public class RulesCommands
    {
        private readonly ICoreServerAPI sapi;
        private RulesConfig cfg = null!;
        public RulesCommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                cfg = RulesConfig.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                cfg = new RulesConfig();
                sapi.World.Logger.Error("[EssentialsX] Failed to init Rules module: {0}", ex);
            }
        }

        public void Register()
        {
            if (cfg == null)
            {
                sapi.World.Logger.Error("[EssentialsX] Rules config not initialized.");
                return;
            }
            if (!cfg.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: Rules");
                return;
            }

            sapi.ChatCommands
                .Create("rules")
                .WithDescription(cfg.Messages.Usage)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnRules);

            sapi.World.Logger.Event("[EssentialsX] Loaded module: Rules");
        }
        private TextCommandResult OnRules(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("<font color='#FF0000'>Player only.</font>");

            int page = 1;
            if (args.ArgCount > 0 && args[0] is string s && !string.IsNullOrWhiteSpace(s))
            {
                if (!int.TryParse(s, out page) || page < 1) page = 1;
            }

            var lines = cfg.Messages.Lines ?? new List<string>();
            if (lines.Count == 0)
            {
                Send(caller, cfg.Messages.NoRules);
                return TextCommandResult.Success();
            }

            int pageSize = Math.Max(1, cfg.LinesPerPage);
            int totalPages = (int)Math.Ceiling(lines.Count / (double)pageSize);
            page = Math.Min(page, totalPages);

            int startIdx = (page - 1) * pageSize;
            int endExclusive = Math.Min(lines.Count, startIdx + pageSize);

            var parts = new List<string>();
            for (int i = startIdx; i < endExclusive; i++)
            {
                var body = cfg.Messages.ItemFormat
                    .Replace("{index}", (i + 1).ToString())
                    .Replace("{text}", $"<font color='#FFFFFF'>{lines[i] ?? string.Empty}</font>");
                parts.Add(body);
            }
            parts.Add(
                cfg.Messages.PageLabel
                    .Replace("{page}", page.ToString())
                    .Replace("{total}", totalPages.ToString())
            );

            string bodyOut = string.Join("\n", parts);
            Send(caller, bodyOut);
            return TextCommandResult.Success();
        }

        private void Send(IServerPlayer p, string body)
        {
            var wrapped = $"{cfg.Messages.Prefix}:\n{body}";
            sapi.SendMessage(p, GlobalConstants.GeneralChatGroup, wrapped, EnumChatType.CommandSuccess);
        }
    }
    public class RulesConfig
    {
        public bool Enabled { get; set; } = true;
        public int LinesPerPage { get; set; } = 8;

        public RulesMessages Messages { get; set; } = new RulesMessages();

        private const string RelFolder = "ModConfig/EssentialsX/Misc";
        private const string FileName = "RulesConfig.json";

        public static RulesConfig LoadOrCreate(ICoreServerAPI sapi)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);

            if (!File.Exists(path))
            {
                var def = new RulesConfig();
                Save(sapi, def);
                return def;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<RulesConfig>(json) ?? new RulesConfig();
                cfg.Messages ??= new RulesMessages();
                return cfg;
            }
            catch
            {
                var def = new RulesConfig();
                Save(sapi, def);
                return def;
            }
        }

        public static void Save(ICoreServerAPI sapi, RulesConfig cfg)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);
            Directory.CreateDirectory(folder);

            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, opts), Encoding.UTF8);
        }
    }

    public class RulesMessages
    {
        public string Prefix { get; set; } = "<strong><font color='#FFFFFF'>[</font><font color='#FFFF00' weight='bold'>Rules</font><font color='#FFFFFF'>]</font></strong>";
        public string Usage { get; set; } = "<font color='#00FF80'>/rules [page] — show server rules</font>";
        public string PageLabel { get; set; } = "<font color='#0080FF'>Page {page}/{total}</font>";
        public string ItemFormat { get; set; } = "{index}. {text}";
        public string NoRules { get; set; } = "<font color='#FF8080'>No rules configured.</font>";
        public List<string> Lines { get; set; } = new()
        {
            "Be respectful to other players.",
            "No cheating, exploiting, or griefing.",
            "Keep chat civil; no hate speech.",
            "No advertising without admin permission.",
            "Use appropriate channels for languages.",
            "Report bugs and issues to admins."
        };
    }
}
