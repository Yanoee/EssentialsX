using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Teleport
{
    public class TprConfig
    {
        public bool Enabled { get; set; } = true;
        public int WarmupSeconds { get; set; } = 5;
        public int CooldownSeconds { get; set; } = 60;
        public int RequestExpireSeconds { get; set; } = 30;
        public bool CancelOnMove { get; set; } = true;
        public bool CancelOnDamage { get; set; } = true;
        public List<string>? BypassRoles { get; set; } = ["admin", "sumod", "crmod"];
        public List<string>? BypassPlayers { get; set; } = ["Notch"];
        public TprMessages Messages { get; set; } = new TprMessages();

        private const string RelFolder = "ModConfig/EssentialsX/Teleportation";
        private const string FileName = "TprConfig.json";

        public static TprConfig LoadOrCreate(ICoreServerAPI sapi)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);

            if (!File.Exists(path))
            {
                var def = new TprConfig();
                Save(sapi, def);
                return def;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<TprConfig>(json) ?? new TprConfig();
                cfg.Messages ??= new TprMessages();
                cfg.BypassRoles ??= [];
                cfg.BypassPlayers ??= [];
                return cfg;
            }
            catch
            {
                var def = new TprConfig();
                Save(sapi, def);
                return def;
            }
        }

        public static void Save(ICoreServerAPI sapi, TprConfig cfg)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);
            Directory.CreateDirectory(folder);

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, opts), Encoding.UTF8);
        }
    }
}
