namespace EssentialsX.Modules.Teleport
{
    public class TprMessages
    {
        // Prefix
        public string Prefix { get; set; } = "<strong><font color='#FFFFFF'>[</font><font color='#5384EE' weight='bold'>TPR</font><font color='#FFFFFF'>]</font></strong>";

        // Descriptions 
        public string DescTpr { get; set; } = "<font color='#00FF80'>/tpr &lt;player&gt; — request a player to teleport to you</font>";
        public string DescTpAccept { get; set; } = "<font color='#00FF80'>/tpraccept — accept a pending TPR request</font>";
        public string DescTpDeny { get; set; } = "<font color='#00FF80'>/tprdeny — deny a pending TPR request</font>";
        public string DescTpCancel { get; set; } = "<font color='#00FF80'>/tprcancel — cancel your outgoing TPR or warmup</font>";

        // Usage / errors
        public string UsageTpr { get; set; } = "<font color='#E04C4C'>Usage: /tpr 'Player Name'</font>";
        public string PlayerOnly { get; set; } = "<font color='#FF0000'>Only players can use this command.</font>";
        public string NotFound { get; set; } = "<font color='#E04C4C'>Player not found or offline.</font>";
        public string SelfTarget { get; set; } = "<font color='#E04C4C'>You cannot request yourself.</font>";
        public string CooldownActive { get; set; } = "<font color='#FFA0A0'>You must wait {seconds}s before using this again.</font>";
        public string AlreadyPendingSender { get; set; } = "<font color='#E04C4C'>You already have a pending TPR request.</font>";
        public string NoPending { get; set; } = "<font color='#E04C4C'>You have no TPR request to accept/deny.</font>";
        public string NothingToCancel { get; set; } = "You have no pending TPR or warmup to cancel.";
        public string TeleportFailed { get; set; } = "<font color='#FF8080'>Teleport failed.</font>";
        public string AlreadyTeleporting { get; set; } = "<font color='#C91212'>You already have a TPR teleport in progress.</font>";


        // Sent/receive
        public string SentSender { get; set; } = "<font color='#4CE0BB'>You asked {playername} to teleport to you.</font>";
        public string SentReceiver { get; set; } = "<font color='#4CE0BB'>You have received a teleport-to-you request from {playername}.</font>";
        public string ClickableReceiver { get; set; } = "<a href='command:///tpraccept'>[ACCEPT]</a>   <a href='command:///tprdeny'>[DENY]</a>";

        // Warmup begin / movement & damage cancels
        public string BeginSender { get; set; } = "<font color='#4CE0E0'>{playername} is teleporting to you in {warmup}s. Do not move!</font>";
        public string BeginReceiver { get; set; } = "<font color='#4CE0E0'>You will be teleported to {playername} in {warmup}s.</font>";
        public string MovedSender { get; set; } = "<font color='#C91212'>You moved; teleportation canceled.</font>";
        public string MovedReceiver { get; set; } = "<font color='#C91212'>{playername} moved; teleportation canceled.</font>";
        public string DamageCancelSender { get; set; } = "<font color='#C91212'>You took damage; teleportation canceled.</font>";
        public string DamageCancelReceiver { get; set; } = "<font color='#C91212'>{playername} took damage; teleportation canceled.</font>";

        // Deny / cancel / success
        public string DeniedSender { get; set; } = "<font color='#E04C4C'>{playername} denied your TPR request.</font>";
        public string DeniedReceiver { get; set; } = "<font color='#E04C4C'>You denied {playername}'s TPR request.</font>";
        public string CanceledSender { get; set; } = "<font color='#E04C4C'>Teleportation canceled.</font>";
        public string CanceledReceiver { get; set; } = "<font color='#E04C4C'>Teleportation canceled by {playername}.</font>";
        public string SuccessSender { get; set; } = "<font color='#84EE53'>{playername} teleported to you.</font>";
        public string SuccessReceiver { get; set; } = "<font color='#84EE53'>Teleported to {playername}.</font>";
    }
}
