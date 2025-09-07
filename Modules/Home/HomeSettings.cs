using Vintagestory.API.Common;

namespace EssentialsX.Modules.Home
{
    public class HomeSettings
    {
        public bool Enabled { get; set; } = true; // master on/off for the whole TPA module
        public double WarmupSeconds { get; set; } = 5;
        public double CooldownSeconds { get; set; } = 60;

        // --- Safety / rules (placeholders for future enforcement) ---
        public bool SafeTeleport { get; set; } = true;
        public int SafeScanRadius { get; set; } = 4;
        public int SafeScanMaxYDiff { get; set; } = 3;
        public bool NoSetHomeInAir { get; set; } = true;
        public int DenyIfCombatTaggedSeconds { get; set; } = 5;

        // roleCode -> max homes
        public Dictionary<string, int> MaxHomesByRole { get; set; } = new Dictionary<string, int>
        {
            ["suvisitor"] = 0,
            ["crvisitor"] = 0,
            ["suplayer"] = 1,
            ["crplayer"] = 2,
            ["sumod"] = 5,
            ["crmod"] = 5,
            ["admin"] = 999
        };

        public int GetMaxHomesForRole(string roleCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode)) return 0;
            if (MaxHomesByRole.TryGetValue(roleCode, out int n)) return n;
            // fallback for unknown roles
            return 1;
        }
        public static HomeSettings LoadOrCreate(ICoreAPI api)
        {
            // Relative path; VS will place it under VintagestoryData/ModConfig automatically.
            return ConfigIO.ReadOrCreate(api, "EssentialsX/Homes/HomeSettings.json", new HomeSettings());
        }
    }
}