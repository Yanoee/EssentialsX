using System.Text.Json;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Teleport
{
    public class TPASettings
    {
        public bool Enabled { get; set; } = true;   // master on/off for the whole TPA module
        public int WarmupSeconds { get; set; } = 5;
        public int CooldownSeconds { get; set; } = 60;
        public int RequestExpireSeconds { get; set; } = 30;
        public List<string> BypassRoles { get; set; } = new() { "admin" };

        public static string ConfigDir(ICoreServerAPI sapi)
        {
            var root = sapi.GetOrCreateDataPath("ModConfig");
            return Path.Combine(root, "EssentialsX", "Teleportation");
        }

        public static string ConfigPath(ICoreServerAPI sapi) => Path.Combine(ConfigDir(sapi), "TPASettings.json");

        public static TPASettings LoadOrCreate(ICoreServerAPI sapi)
        {
            try
            {
                var dir = ConfigDir(sapi);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var path = ConfigPath(sapi);
                if (!File.Exists(path))
                {
                    var def = new TPASettings();
                    var json = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                    return def;
                }

                var text = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<TPASettings>(text);
                return cfg ?? new TPASettings();
            }
            catch
            {
                return new TPASettings();
            }
        }
    }
}