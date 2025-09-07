using Vintagestory.API.Server;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EssentialsX.Modules.Teleport
{
    public class TPRMessages
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        public string TprHeader { get; set; } = "[<font color='#5384EE' weight='bold'>Teleportation</font>]";
        public string TprFooter { get; set; } = " ";

        // Sent
        public string TprSentSender { get; set; } = "<font color='#4CE0BB'>You sent {playername} pull request.</font>";
        public string TprSentReceiver { get; set; } = "<font color='#4CE0BB'>You have received a pull request from {playername}.</font>";
        public string TprClickableReceiver { get; set; } =
            "You have received pull request from: {playername} <a href='command:///tpraccept' color='#84EE53'>[ACCEPT]</a> <a href='command:///tprdeny' color='#EE5384'>[DENY]</a>";

        // Usage
        public string TprUsage { get; set; } = "/tpr <player> — request to pull the player to you";

        // Begin
        public string TprBeginSender { get; set; } = "<font color='#84EE53'>Pull accepted. Teleporting {playername} to you in {warmup}s...</font>";
        public string TprBeginReceiver { get; set; } = "<font color='#84EE53'>Pull accepted. You will be teleported to {playername} in {warmup}s...</font>";

        // Success
        public string TprSuccessSender { get; set; } = "<font color='#84EE53'>{playername} teleported to you.</font>";
        public string TprSuccessReceiver { get; set; } = "<font color='#84EE53'>Teleported to {playername}.</font>";

        // Canceled
        public string TprCanceledSender { get; set; } = "<font color='#E04C4C'>Teleportation canceled.</font>";
        public string TprCanceledReceiver { get; set; } = "<font color='#E04C4C'>Teleportation canceled by {playername}.</font>";

        // Errors / states
        public string TprNoPending { get; set; } = "<font color='#E0A84C'>There is no pending pull request.</font>";
        public string TprAlreadyPendingSender { get; set; } = "<font color='#E0A84C'>You already have a pending pull request.</font>";
        public string TprSelf { get; set; } = "<font color='#E04C4C'>You cannot target yourself.</font>";
        public string TprNoTarget { get; set; } = "<font color='#E04C4C'>Target player not found.</font>";
        public string CooldownActive { get; set; } = "<font color='#E0A84C'>You must wait {seconds}s before sending another pull.</font>";
        public string TprCancelUsage { get; set; } = "Cancel your outgoing TPR or an in-progress warmup.";
        public string TprNothingToCancel { get; set; } = "You have no pending TPR or warmup to cancel.";

        public static string ConfigDir(ICoreServerAPI sapi)
        {
            var root = sapi.GetOrCreateDataPath("ModConfig");
            return Path.Combine(root, "EssentialsX", "Teleportation");
        }

        public static string ConfigPath(ICoreServerAPI sapi) => Path.Combine(ConfigDir(sapi), "TPRMessages.json");

        public static TPRMessages LoadOrCreate(ICoreServerAPI sapi)
        {
            try
            {
                var dir = ConfigDir(sapi);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var path = ConfigPath(sapi);
                if (!File.Exists(path))
                {
                    var def = new TPRMessages();
                    File.WriteAllText(path, JsonSerializer.Serialize(def, JsonOptions));
                    return def;
                }

                var text = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<TPRMessages>(text);
                return cfg ?? new TPRMessages();
            }
            catch
            {
                return new TPRMessages();
            }
        }
    }
}
