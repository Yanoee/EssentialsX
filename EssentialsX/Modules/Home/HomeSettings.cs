using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Home
{
    public class HomeConfig
    {
        public bool Enabled { get; set; } = true;
        public int WarmupSeconds { get; set; } = 5;
        public int CooldownSeconds { get; set; } = 60;
        public bool CancelOnMove { get; set; } = true;
        public bool CancelOnDamage { get; set; } = true;
        public int DenyIfCombatTaggedSeconds { get; set; } = 5;
        public bool CombatTagBlocksSetHome { get; set; } = true;
        public bool SafeTeleport { get; set; } = true;
        public bool NoSetHomeInAir { get; set; } = true;
        public bool DenyIfInLiquid { get; set; } = false;
        public Dictionary<string, int> MaxHomesByRole { get; set; } = new()
        {
            ["suvisitor"] = 0,
            ["crvisitor"] = 0,
            ["suplayer"] = 1,
            ["crplayer"] = 2,
            ["sumod"] = 5,
            ["crmod"] = 5,
            ["admin"] = 999
        };
        public Dictionary<string, int> PlayerOverrides { get; set; } = [];
        public int MaxNameLength { get; set; } = 24;
        public string AllowedCharsRegex { get; set; } = @"^[A-Za-z0-9 _\-]+$";
        public bool CaseInsensitiveNames { get; set; } = true;
        public bool TrimNames { get; set; } = true;
        public List<string>? BypassRoles { get; set; } = ["admin", "sumod", "crmod"];
        public List<string>? BypassPlayers { get; set; } = ["Notch"];
        public HomeMessages Messages { get; set; } = new HomeMessages();
        private const string RelFolder = "ModConfig/EssentialsX/Homes";
        private const string FileName = "HomesConfig.json";

        public static HomeConfig LoadOrCreate(ICoreServerAPI sapi)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, FileName);

            if (!File.Exists(path))
            {
                var def = new HomeConfig();
                Save(sapi, def);
                return def;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<HomeConfig>(json) ?? new HomeConfig();
                cfg.Messages ??= new HomeMessages();
                cfg.BypassRoles ??= [];
                cfg.BypassPlayers ??= [];
                cfg.MaxHomesByRole ??= [];
                cfg.PlayerOverrides ??= [];
                return cfg;
            }
            catch
            {
                var def = new HomeConfig();
                Save(sapi, def);
                return def;
            }
        }

        public static void Save(ICoreServerAPI sapi, HomeConfig cfg)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, FileName);

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, opts), Encoding.UTF8);
        }

        public int GetMaxHomesForRole(string roleCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode)) return 0;
            if (MaxHomesByRole.TryGetValue(roleCode, out int n)) return n;
            return 1;
        }
    }
}
