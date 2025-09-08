using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System.Text.Encodings.Web;

namespace EssentialsX.Modules.Misc
{
    public class Rules
    {
        // Register
        private readonly ICoreServerAPI sapi;
        private RulesSettings settings = null!;
        private RulesMessages messages = null!;
        private const string ModuleName = "Rules";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        public void Register()
        {
            sapi.World.Logger.Event("[EssentialsX] Loaded module: {0}", ModuleName);

            if (settings != null && settings.Enabled)
            {
                sapi.ChatCommands.Create("rules")
                    .WithDescription(messages.Usage)
                    .RequiresPlayer()
                    .RequiresPrivilege("chat")
                    .HandleWith(OnRules);
            }
            else
            {
                sapi.World.Logger.Event("[EssentialsX] {0} disabled via settings.", ModuleName);
                return;
            }

        }

        // --- Settings ---
        public class RulesSettings
        {
            public bool Enabled { get; set; } = true;                // master on/off
            public int LinesPerPage { get; set; } = 8;               // paging for /rules

            public static string ConfigDir(ICoreServerAPI sapi)
            {
                var root = sapi.GetOrCreateDataPath("ModConfig");
                return Path.Combine(root, "EssentialsX", "Misc");
            }

            public static string PathOf(ICoreServerAPI sapi) => System.IO.Path.Combine(ConfigDir(sapi), "RulesSettings.json");

            public static RulesSettings LoadOrCreate(ICoreServerAPI sapi)
            {
                try
                {
                    var dir = ConfigDir(sapi);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    var path = PathOf(sapi);
                    if (!File.Exists(path))
                    {
                        var def = new RulesSettings();
                        File.WriteAllText(path, JsonSerializer.Serialize(def, JsonOptions));
                        return def;
                    }
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<RulesSettings>(json) ?? new RulesSettings();
                }
                catch
                {
                    return new RulesSettings();
                }
            }
        }

        // --- Messages ---
        public class RulesMessages
        {
            private static readonly JsonSerializerOptions JsonOptions = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            public string RulesHeader { get; set; } = " ";
            public string RulesPrefix { get; set; } = "<strong><font color=#FFFFFF>[</font><font color='#FFFF00'>Rules</font><font color=#FFFFFF>]</font></strong>";
            public string RulesFooter { get; set; } = " ";
            public string Usage { get; set; } = "/rules [page] — show server rules";
            public string PageLabel { get; set; } = "<font color='#0080FF'>Page {page}/{total}</font>";
            public string ItemFormat { get; set; } = "{index}. {text}";
            public string NoRules { get; set; } = "No rules configured.";
            public List<string> Lines { get; set; } = new()
            {
                "Be respectful to other players.",
                "No cheating, exploiting, or griefing.",
                "Keep chat civil; no hate speech.",
                "No advertising without admin permission.",
                "English in global chat; other languages in groups.",
                "Report bugs and issues to admins.",
            };

            public static string ConfigDir(ICoreServerAPI sapi)
            {
                var root = sapi.GetOrCreateDataPath("ModConfig");
                return Path.Combine(root, "EssentialsX", "Misc");
            }

            public static string PathOf(ICoreServerAPI sapi) => System.IO.Path.Combine(ConfigDir(sapi), "RulesMessages.json");

            public static RulesMessages LoadOrCreate(ICoreServerAPI sapi)
            {
                try
                {
                    var dir = ConfigDir(sapi);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    var path = PathOf(sapi);
                    if (!File.Exists(path))
                    {
                        var def = new RulesMessages();
                        File.WriteAllText(path, JsonSerializer.Serialize(def, JsonOptions));
                        return def;
                    }
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<RulesMessages>(json) ?? new RulesMessages();
                }
                catch
                {
                    return new RulesMessages();
                }
            }
        }

        // --- Helpers / Handlers ---

        public Rules(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                settings = RulesSettings.LoadOrCreate(sapi);
                messages = RulesMessages.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Rules settings/messages failed to init: {0}", ex);
                settings = new RulesSettings();
                messages = new RulesMessages();
            }
        }

        private TextCommandResult OnRules(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("Player only.");

            // parse optional page number (1-based). fallback to 1
            int page = 1;
            try
            {
                if (args.ArgCount > 0 && args[0] is string s && !string.IsNullOrWhiteSpace(s))
                {
                    if (!int.TryParse(s, out page) || page < 1) page = 1;
                }
            }
            catch { page = 1; }

            var lines = messages.Lines ?? new List<string>();
            if (lines.Count == 0)
            {
                SendBlock(caller, messages.NoRules);
                return TextCommandResult.Success();
            }

            int pageSize = Math.Max(1, settings.LinesPerPage);
            int totalPages = (int)Math.Ceiling(lines.Count / (double)pageSize);
            page = Math.Min(page, totalPages);

            int startIdx = (page - 1) * pageSize;
            int endExclusive = Math.Min(lines.Count, startIdx + pageSize);

            // Build the body using ItemFormat and PageLabel
            var parts = new List<string>();

            for (int i = startIdx; i < endExclusive; i++)
            {
                var body = messages.ItemFormat
                    .Replace("{index}", (i + 1).ToString())
                    .Replace("{text}", $"<font color='#FFFFFF'>{lines[i] ?? string.Empty}</font>");
                parts.Add(body);
            }


            parts.Add(messages.PageLabel.Replace("{page}", page.ToString()).Replace("{total}", totalPages.ToString()));


            string bodyOut = string.Join("\n", parts);
            SendBlock(caller, bodyOut);
            return TextCommandResult.Success();
        }

        private void SendBlock(IServerPlayer plr, string body)
        {
            plr.SendMessage(GlobalConstants.GeneralChatGroup, $"{messages.RulesHeader}\n{messages.RulesPrefix}\n{body}\n{messages.RulesFooter}", EnumChatType.Notification);
        }
    }
}
