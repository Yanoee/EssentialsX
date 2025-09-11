using System.Text.Encodings.Web;
using System.Text.Json;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Misc
{
    public class InfoHelpSettings
    {
        public string ModId { get; set; } = "essentialsx";
        public List<string> BypassRoles { get; set; } = ["admin"];
        public List<string> BypassPlayers { get; set; } = ["Notch"];
        
        private const string RelFolder = "ModConfing/EssentialsX/Misc";
        private const string FileName = "HelpConfig.json";
        internal static string PathOf(ICoreServerAPI sapi) =>
            Path.Combine(sapi.GetOrCreateDataPath(""), RelFolder, FileName);

        public static InfoHelpSettings LoadOrCreate(ICoreServerAPI sapi)
        {
            var model = InfoHelpConfigModel.LoadOrCreate(sapi);
            return model.Settings ?? new InfoHelpSettings();
        }
    }

    internal class InfoHelpConfigModel
    {
        public InfoHelpMessages? Messages { get; set; }
        public InfoHelpSettings? Settings { get; set; }

        private static readonly JsonSerializerOptions Pretty = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        public static InfoHelpConfigModel LoadOrCreate(ICoreServerAPI sapi)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(InfoHelpSettings.PathOf(sapi))!);
            var path = InfoHelpSettings.PathOf(sapi);

            if (!File.Exists(path))
            {
                var fresh = new InfoHelpConfigModel
                {
                    Messages = new InfoHelpMessages(),
                    Settings = new InfoHelpSettings()
                };
                File.WriteAllText(path, JsonSerializer.Serialize(fresh, Pretty));
                return fresh;
            }

            try
            {
                var json = File.ReadAllText(path);
                var model = JsonSerializer.Deserialize<InfoHelpConfigModel>(json) ?? new InfoHelpConfigModel();
                model.Messages ??= new InfoHelpMessages();
                model.Settings ??= new InfoHelpSettings();
                return model;
            }
            catch
            {
                var fresh = new InfoHelpConfigModel
                {
                    Messages = new InfoHelpMessages(),
                    Settings = new InfoHelpSettings()
                };
                File.WriteAllText(path, JsonSerializer.Serialize(fresh, Pretty));
                return fresh;
            }
        }

        public void Save(ICoreServerAPI sapi)
        {
            var path = InfoHelpSettings.PathOf(sapi);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Pretty));
        }
    }
}
