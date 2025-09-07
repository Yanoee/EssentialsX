using Vintagestory.API.Common;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EssentialsX.Modules.Home
{
    public class HomeMessages
    {
        public HomeGroup Home { get; set; } = new HomeGroup();
        public DescriptionsGroup Descriptions { get; set; } = new DescriptionsGroup();
        public string HomeHeader { get; set; } = "[<font color='#7E2DAD' weight='bold'>Home</font>]";
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

            public string Saved { get; set; } = "<font color='#5FFF00'>Home '{name}' saved.</font>";
            public string Deleted { get; set; } = "<font color='#00EAFF'>Home '{name}' deleted.</font>";
            public string NoHome { get; set; } = "<font color='#00EAFF'>No such home. Use /homes to list, or /sethome name.</font>";

            public string ListNone { get; set; } = "<font color='#0019FF'>You have no homes.</font>";
            public string ListSome { get; set; } = "<font color='#0019FF'>Homes ({count}): {list}</font>";

            public string Teleporting { get; set; } = "<font color='#00FFE5'>Teleporting to '{name}' in {seconds}s. Don't move!</font>";
            public string Teleported { get; set; } = "<font color='#00FFE5'>Teleported to '{name}'.</font>";

            public string CancelMove { get; set; } = "<font color='#B52828'>Teleport cancelled: you moved.</font>";
            public string CancelDamage { get; set; } = "<font color='#B52828'>Teleport cancelled: you took damage.</font>";
            public string Cooldown { get; set; } = "<font color='#B52828'>You must wait {seconds}s before using /home again.</font>";
            public string LimitReached { get; set; } = "<font color='#B52828'>You reached your home limit ({count}). Delete a home first.</font>";

            // Safety / validation
            public string NoGround { get; set; } = "<font color='#B52828'>Stand on solid ground to set a home.</font>";
            public string UnsafeDestination { get; set; } = "<font color='#B52828'>Could not find a safe spot near '{name}'.</font>";
            public string CombatTagged { get; set; } = "<font color='#B52828'>You were hurt recently. Wait {seconds}s before teleporting.</font>";
        }

        public class DescriptionsGroup
        {
            public string sethome { get; set; } = "Save your current position as a home.";
            public string delhome { get; set; } = "Delete a saved home.";
            public string home { get; set; } = "Teleport to a saved home (default: last used).";
            public string homes { get; set; } = "List your homes.";
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
