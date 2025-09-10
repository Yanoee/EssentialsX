namespace EssentialsX.Modules.Teleport
{
    public class TpaMessages
    {
        // Prefix
        public string TPAPrefix { get; set; } = "<strong><font color='#FFFFFF'>[</font><font color='#5384EE' weight='bold'>TPA</font><font color='#FFFFFF'>]</font></strong>";

        // Descriptions
        public string DescTpa { get; set; } = "<font color='#00FF80'>/tpa &lt;player&gt; — request to teleport to a player</font>";
        public string DescTpAccept { get; set; } = "<font color='#00FF80'>/tpaccept — accept a pending TPA request</font>";
        public string DescTpDeny { get; set; } = "<font color='#00FF80'>/tpdeny — deny a pending TPA request</font>";
        public string DescTpCancel { get; set; } = "<font color='#00FF80'>/tpcancel — cancel your outgoing TPA or warmup</font>";

        // Usage / errors
        public string UsageTpa { get; set; } = "<font color='#E04C4C'>Usage: /tpa 'Player Name'</font>";
        public string PlayerOnly { get; set; } = "<font color='#FF0000'>Only players can use this command.</font>";
        public string NotFound { get; set; } = "<font color='#E04C4C'>Player not found or offline.</font>";
        public string SelfTarget { get; set; } = "<font color='#E04C4C'>You cannot teleport to yourself.</font>";
        public string CooldownActive { get; set; } = "<font color='#FFA0A0'>You must wait {seconds}s before using this again.</font>";
        public string AlreadyPendingSender { get; set; } = "<font color='#E04C4C'>You already have a pending TPA request.</font>";
        public string NoPending { get; set; } = "<font color='#E04C4C'>You have no TPA request to accept/deny.</font>";
        public string NothingToCancel { get; set; } = "You have no pending TPA or warmup to cancel.";
        public string TeleportFailed { get; set; } = "<font color='#FF8080'>Teleport failed.</font>";
        public string AlreadyTeleporting { get; set; } = "<font color='#C91212'>You already have a TPA teleport in progress.</font>";


        // Sent/receive
        public string SentSender { get; set; } = "<font color='#4CE0BB'>You sent {playername} a teleportation request.</font>";
        public string SentReceiver { get; set; } = "<font color='#4CE0BB'>You have received a teleportation request from {playername}.</font>";
        public string ClickableReceiver { get; set; } = "<a href='command:///tpaccept'>[ACCEPT]</a>   <a href='command:///tpdeny'>[DENY]</a>";

        // Warmup start cancels
        public string BeginSender { get; set; } = "<font color='#4CE0E0'>You will be teleporting to {playername}. Do not move for {warmup}s!</font>";
        public string BeginReceiver { get; set; } = "<font color='#4CE0E0'>{playername} is teleporting to you in {warmup}s.</font>";
        public string MovedSender { get; set; } = "<font color='#C91212'>You moved; teleportation canceled.</font>";
        public string MovedReceiver { get; set; } = "<font color='#C91212'>{playername} moved; teleportation canceled.</font>";
        public string DamageCancelSender { get; set; } = "<font color='#C91212'>You took damage; teleportation canceled.</font>";
        public string DamageCancelReceiver { get; set; } = "<font color='#C91212'>{playername} took damage; teleportation canceled.</font>";

        // Accept / deny / cancel / success
        public string DeniedSender { get; set; } = "<font color='#C91212'>{playername} denied your TPA request.</font>";
        public string DeniedReceiver { get; set; } = "<font color='#C91212'>You denied {playername}'s TPA request.</font>";
        public string CanceledSender { get; set; } = "<font color='#C91212'>You canceled the TPA request.</font>";
        public string CanceledReceiver { get; set; } = "<font color='#C91212'>{playername} canceled their TPA.</font>";
        public string SuccessSender { get; set; } = "<font color='#96E04C'>You successfully teleported to {playername}.</font>";
        public string SuccessReceiver { get; set; } = "<font color='#96E04C'>{playername} successfully teleported to you.</font>";
    }
}
