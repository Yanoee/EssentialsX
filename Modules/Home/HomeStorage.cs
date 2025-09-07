using System.Text.Json;
using System.Text.Json.Serialization;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Home
{
    public class HomePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; }
    }

    public class PlayerHomes
    {
        public Dictionary<string, HomePoint> Homes { get; set; } = new();
        public string? LastUsed { get; set; }
    }
    public class HomesStore
    {
        private readonly ICoreServerAPI sapi;
        private readonly string dir;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public HomesStore(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            // Proper path under VintagestoryData/ModData
            dir = Path.Combine(sapi.GetOrCreateDataPath("ModData"), "EssentialsX", "playerdata");
            Directory.CreateDirectory(dir);
        }

        private string FileFor(string uid) => Path.Combine(dir, uid + ".json");

        public PlayerHomes Load(string uid)
        {
            try
            {
                string path = FileFor(uid);
                if (!File.Exists(path)) return new PlayerHomes();
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PlayerHomes>(json, JsonOpts) ?? new PlayerHomes();
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Warning("[EssentialsX] Could not read homes for {0}: {1}", uid, ex.Message);
                return new PlayerHomes();
            }
        }

        public void Save(string uid, PlayerHomes data)
        {
            try
            {
                File.WriteAllText(FileFor(uid), JsonSerializer.Serialize(data, JsonOpts));
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Warning("[EssentialsX] Could not save homes for {0}: {1}", uid, ex.Message);
            }
        }
    }
}