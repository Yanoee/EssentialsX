using Vintagestory.API.Server;

namespace EssentialsX.Modules.Misc
{
    public class InfoHelpMessages
    {
        //Frame
        public string Prefix { get; set; } = "<strong><font color='#FFFFFF'>[</font><font color='#00FF80'>EssentialsX</font><font color='#FFFFFF'>]</font></strong>";
        public string RootDesc { get; set; } = "EssentialsX root command";
        public string InfoDesc { get; set; } = "Show EssentialsX mod info";
        public string HelpDesc { get; set; } = "Show EssentialsX help";
        public string ReloadDesc { get; set; } = "Reload EssentialsX configs";
        public string PlayerOnly { get; set; } = "Player only.";
        public string Description { get; set; } = "<font color='#00FF80'>EssentialsX</font><font color='#FFFFFF'> | Server QoL & Moderation | Author: </font><font color='#0080FF'>Yanoee</font>";
        public string InfoBody { get; set; } = "Version: {version}\nStatus: {status}\n{description}";
        public string HelpTitle { get; set; } = "<font color='#84EE53'>EssentialsX Help:</font>";
        public string HelpLinePrefix { get; set; } = "<font color='#FFFFFF'>";
        public string HelpLineSuffix { get; set; } = "</font>";
        public string NoPermission { get; set; } = "<font color='#E04C4C'>You lack permission.</font>";

        //Lists
        public List<string> HelpTeleport { get; set; } =
        [
            "TPA:",
            "- /tpa <player> : Request to teleport to a player",
            "- /tpcancel : Cancel your pending teleport request",
            "- /tpaccept : Accept teleport request",
            "- /tpdeny : Deny teleport request",

            "TPR:",
            "- /tpr <player> : Request a player to teleport to you",
            "- /tprcancel : Cancel your pending pull request",
            "- /tpraccept : Accept pull request",
            "- /tprdeny : Deny pull request"
        ];
        public List<string> HelpHomes { get; set; } =
        [
            "Homes:",
            "- /home <name> : Teleport to your home",
            "- /sethome <name> : Set a new home",
            "- /delhome <name> : Delete a home",
            "- /homes : List all homes"
        ];
        public List<string> HelpRules { get; set; } =
        [
            "Rules:",
            "- /rules : Show server rules"
        ];
        public List<string> HelpSpawn { get; set; } =
        [
            "Spawn:",
            "- /spawn : Teleport to spawn"
        ];
        public List<string> HelpBack { get; set; } =
        [
            "Back:",
            "- /back : Return to your last location"
        ];
        public List<string> HelpRtp { get; set; } =
        [
            "RTP:",
            "- /rtp : Random teleport"
        ];
        public List<string> HelpAdmin { get; set; } =
        [
            "Admin:",
            "- /essentialsx reload : Reload all EssentialsX configs"
        ];

        //Reload
        public string Reloading { get; set; } = "Reloading configs...";
        public string ReloadFailed { get; set; } = "<font color='#FF0000'>Reload failed. See server log.</font>";
        public string ReloadedFormat { get; set; } = "<font color='#00FF80'>Reloaded {count} JSON files.</font>";
        public static InfoHelpMessages LoadOrCreate(ICoreServerAPI sapi)
        {
            var model = InfoHelpConfigModel.LoadOrCreate(sapi);
            return model.Messages ?? new InfoHelpMessages();
        }

        internal void Save(ICoreServerAPI sapi, InfoHelpSettings settings)
        {
            var model = new InfoHelpConfigModel { Messages = this, Settings = settings };
            model.Save(sapi);
        }
    }
}
