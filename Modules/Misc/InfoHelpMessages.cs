using System.Text.Encodings.Web;
using System.Text.Json;
using Vintagestory.API.Server;


namespace EssentialsX.Modules.Misc
{
    public class InfoHelpMessages
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        // Frame
        public string Header { get; set; } = " ";
        public string Prefix { get; set; } = "<strong><font color=#FFFFFF>[</font><font color='#00FF80'>EssentialsX</font><font color=#FFFFFF>]</font></strong>";
        public string Footer { get; set; } = " ";

        // Descriptions for command registration
        public string Description { get; set; } = "<font color='#00FF80'>EssentialsX</font><font color='#FFFFFF'> | Server QoL & Moderation | Author: </font><font color='#0080FF'>Yanoee</font>";
        public string RootDesc { get; set; } = "EssentialsX root command";
        public string InfoDesc { get; set; } = "Show EssentialsX mod info";
        public string HelpDesc { get; set; } = "Show EssentialsX help";
        public string ReloadDesc { get; set; } = "Reload EssentialsX configs (admin only)";

        // Common
        public string PlayerOnly { get; set; } = "Player only.";

        // Info
        public string InfoTitle { get; set; } = "<font color='#84EE53'>EssentialsX</font>";
        public string InfoBody { get; set; } = "Version: {version}\nStatus: {status}\n{description}";

        // Help
        public string HelpTitle { get; set; } = "<font color='#84EE53'>EssentialsX Help</font>";
        public List<string> HelpTeleport { get; set; } = new()
        {
            "Teleport:",
            "- /tpa <player> : Request to teleport to a player",
            "- /tpr <player> : Request a player to teleport to you",
            "- /tpaccept : Accept teleport request",
            "- /tpdeny : Deny teleport request"
        };
        public List<string> HelpHomes { get; set; } = new()
        {
            "",
            "Homes:",
            "- /home <name> : Teleport to your home",
            "- /sethome <name> : Set a new home",
            "- /delhome <name> : Delete a home",
            "- /homes : List all homes"
        };
        public List<string> HelpRules { get; set; } = new()
        {
            "",
            "Rules:",
            "- /rules : Show server rules"
        };
        public List<string> HelpAdmin { get; set; } = new()
        {
            "",
            "Admin:",
            "- /essentialsx reload : Reload all EssentialsX configs"
        };

        // Reload
        public string Reloading { get; set; } = "Reloading configs...";
        public string Reloaded { get; set; } = "Configs reloaded.";

        private static string Dir(ICoreServerAPI sapi)
        {
            var root = sapi.GetOrCreateDataPath("ModConfig");
            return Path.Combine(root, "EssentialsX", "Misc");
        }
        private static string PathOf(ICoreServerAPI sapi) => Path.Combine(Dir(sapi), "InfoHelpMessages.json");

        public static InfoHelpMessages LoadOrCreate(ICoreServerAPI sapi)
        {
            try
            {
                Directory.CreateDirectory(Dir(sapi));
                var path = PathOf(sapi);
                if (!File.Exists(path))
                {
                    var def = new InfoHelpMessages();
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, JsonSerializer.Serialize(def, JsonOptions));
                    return def;
                }
                return JsonSerializer.Deserialize<InfoHelpMessages>(File.ReadAllText(path)) ?? new InfoHelpMessages();
            }
            catch
            {
                return new InfoHelpMessages();
            }
        }
    }
}
