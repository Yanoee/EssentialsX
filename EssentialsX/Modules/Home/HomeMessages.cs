namespace EssentialsX.Modules.Home
{
    public class HomeMessages
    {
        public string HomePrefix { get; set; } =
            "<strong><font color='#FFFFFF'>[</font><font color='#7E2DAD' weight='bold'>Home</font><font color='#FFFFFF'>]</font></strong>";

        // Descriptions
        public string DescSetHome { get; set; } = "<font color='#00FF80'>/sethome &lt;name&gt; — save your current position</font>";
        public string DescDelHome { get; set; } = "<font color='#00FF80'>/delhome &lt;name&gt; — delete a saved home</font>";
        public string DescHome { get; set; } = "<font color='#00FF80'>/home [name] — teleport to a saved home</font>";
        public string DescHomes { get; set; } = "<font color='#00FF80'>/homes — list your homes</font>";

        // Usage / errors
        public string PlayerOnly { get; set; } = "<font color='#FF0000'>Only players can use this command.</font>";
        public string NoPerm { get; set; } = "<font color='#B52828'>You don't have permission to set homes.</font>";
        public string UsageSetHome { get; set; } = "<font color='#3E2DAD'>Usage: /sethome name</font>";
        public string UsageDelHome { get; set; } = "<font color='#3E2DAD'>Usage: /delhome name</font>";
        public string NoHome { get; set; } = "<font color='#FFFFFF'>You don’t have any homes yet. Use </font><strong><font color='#C41B1B'>/sethome name</font></strong><font color='#FFFFFF'> first.</font>";
        public string LimitReached { get; set; } = "<font color='#C41B1B'>You reached your home limit ({count}). Delete a home first.</font>";
        public string NameTooLong { get; set; } = "<font color='#C41B1B'>Home name is too long (max {max} chars).</font>";
        public string NameInvalid { get; set; } = "<font color='#C41B1B'>Home name contains invalid characters.</font>";
        public string DuplicateName { get; set; } = "<font color='#C41B1B'>A home with that name already exists.</font>";
        public string NoSuchHome { get; set; } = "<font color='#C41B1B'>No such home.</font>";
        public string AlreadyTeleporting { get; set; } = "<font color='#C91212'>You already have a home teleport in progress.</font>";

        // Set-home constraints
        public string NoGround { get; set; } = "<font color='#C41B1B'>Stand on solid ground to set a home.</font>";
        public string InLiquid { get; set; } = "<font color='#C41B1B'>Cannot set home while in liquid.</font>";
        public string CombatTaggedSetBlocked { get; set; } = "<font color='#C41B1B'>You were hurt recently. You can’t set home for {seconds}s.</font>";
        public string CombatTaggedUseBlocked { get; set; } = "<font color='#C41B1B'>You were hurt recently. Wait {seconds}s before teleporting.</font>";

        // Warmup/cooldown
        public string Cooldown { get; set; } = "<font color='#C41B1B'>You must wait {seconds}s before using /home again.</font>";
        public string Begin { get; set; } = "<font color='#1BC470'>Teleporting to '{name}' in {seconds}s. Don't move!</font>";
        public string Moved { get; set; } = "<font color='#C91212'>You moved; teleportation canceled.</font>";
        public string DamageCancel { get; set; } = "<font color='#C91212'>You took damage; teleportation canceled.</font>";

        // Success
        public string Saved { get; set; } = "<font color='#FFFFFF'>Home '</font><font color='#73FF00' weight='bold'>{name}</font><font color='#FFFFFF'>' saved.</font>";
        public string Deleted { get; set; } = "<font color='#FFFFFF'>Home '</font><font color='#C41B1B'>{name}</font><font color='#FFFFFF'>' deleted.</font>";
        public string Teleported { get; set; } = "<font color='#1B70C4'>Teleported to '{name}'.</font>";

        // List
        public string ListNone { get; set; } = "<font color='#FFFFFF'>You have no homes. Use </font><strong><font color='#C41B1B'>/sethome</font></strong>";
        public string ListSome { get; set; } = "<font color='#0019FF'>Homes ({count}): {list}</font>";
    }
}
