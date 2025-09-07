using System.Text.Json;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Misc
{
    public class InfoHelpSettings
    {
        public bool Enabled { get; set; } = true;
        public string ModId { get; set; } = "essentialsx";

        private static string Dir(ICoreServerAPI sapi)
        {
            var root = sapi.GetOrCreateDataPath("ModConfig");
            return Path.Combine(root, "EssentialsX", "Misc");
        }

        private static string PathOf(ICoreServerAPI sapi) => Path.Combine(Dir(sapi), "InfoHelpSettings.json");

        public static InfoHelpSettings LoadOrCreate(ICoreServerAPI sapi)
        {
            try
            {
                Directory.CreateDirectory(Dir(sapi));
                var path = PathOf(sapi);
                if (!File.Exists(path))
                {
                    var def = new InfoHelpSettings();
                    File.WriteAllText(path, JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true }));
                    return def;
                }
                return JsonSerializer.Deserialize<InfoHelpSettings>(File.ReadAllText(path)) ?? new InfoHelpSettings();
            }
            catch
            {
                return new InfoHelpSettings();
            }
        }
    }
}
