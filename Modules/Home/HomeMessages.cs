using Vintagestory.API.Common;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EssentialsX.Modules.Home
{
    public class HomeMessages
    {
        public HomeGroup Home { get; set; } = new HomeGroup();
        public DescriptionsGroup Descriptions { get; set; } = new DescriptionsGroup();
        public string HomeHeader { get; set; } = " ";
        public string HomePrefix { get; set; } = "<strong><font color='#FFFFFF'>[</font><font color='#7E2DAD'>Home</font><font color='#FFFFFF'>]</font></strong>";
        public string HomeFooter { get; set; } = " ";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };


        public class HomeGroup
        {

            public string NoPerm { get; set; } = "<font color='#B52828'>You don't have permission to set homes.</font>";
            public string UsageSetHome { get; set; } = "<font color='#3E2DAD'>Usage: /sethome 'name'</font>";
            public string UsageDelHome { get; set; } = "<font color='#3E2DAD'>Usage: /delhome 'name'</font>";

            public string Saved { get; set; } = "<font color='#FFFFFF'>Home '</font><font color='#73FF00' weight='bold'>{name}</font><font color='#FFFFFF'>' saved.</font>";
            public string Deleted { get; set; } = "<font color='#FFFFFF'>Home '</font><font color='#C41B1B'>{name}</font><font color='#FFFFFF'>' deleted.</font>";
            public string NoHome { get; set; } = "<font color='#FFFFFF'>You have no current home.</font>";

            public string ListNone { get; set; } = "<font color='#FFFFFF'>You have no homes. To set one </font><strong><font color='#C41B1B'>/sethome</font></strong>";
            public string ListSome { get; set; } = "<font color='#0019FF'>Homes ({count}): {list}</font>";

            public string Teleporting { get; set; } = "<font color='#1BC470'>Teleporting to '{name}' in {seconds}s. Don't move!</font>";
            public string Teleported { get; set; } = "<font color='#1B70C4'>Teleported to '{name}'.</font>";

            public string CancelMove { get; set; } = "<font color='#C41B1B'>Home teleport cancelled you moved!</font>";
            public string CancelDamage { get; set; } = "<font color='#C41B1B'>Home teleport cancelled you took damage!</font>";
            public string Cooldown { get; set; } = "<font color='#C41B1B'>You must wait {seconds}s before using /home again.</font>";
            public string LimitReached { get; set; } = "<font color='#C41B1B'>You reached your home limit ({count}). Delete a home first.</font>";

            // Safety / validation
            public string NoGround { get; set; } = "<font color='#C41B1B'>Stand on solid ground to set a home.</font>";
            public string UnsafeDestination { get; set; } = "<font color='#C41B1B'>Could not find a safe spot near '{name}'.</font>";
            public string CombatTagged { get; set; } = "<font color='#C41B1B'>You were hurt recently. Wait {seconds}s before teleporting.</font>";
        }

        public class DescriptionsGroup
        {
            public string sethome { get; set; } = "<font color='#1B70C4'>Save your current position as a home.</font>";
            public string delhome { get; set; } = "<font color='#C41B1B'>Delete a saved home.</font>";
            public string home { get; set; } = "<font color='#1B70C4'>Teleport to a saved home (default: last used).</font>";
            public string homes { get; set; } = "<font color='#1B70C4'>List your homes.</font>";
        }

        public static HomeMessages LoadOrCreate(ICoreAPI api)
        {
            var path = Path.Combine(api.GetOrCreateDataPath("ModConfig"), "EssentialsX", "Homes", "HomeMessages.json");
            if (!File.Exists(path))
            {
                var def = new HomeMessages();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(def, JsonOptions));
                return def;
            }
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<HomeMessages>(json) ?? new HomeMessages();
        }
    }
}
