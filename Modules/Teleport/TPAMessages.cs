using Vintagestory.API.Server;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EssentialsX.Modules.Teleport
{
    public class TPAMessages
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        public string TpaHeader { get; set; } = "[<font color='#5384EE' weight='bold'>Teleportaion</font>]";
        public string TpaFooter { get; set; } = " ";

        // Sent
        public string TpaSentSender { get; set; } = "<font color='#4CE0BB'>You sent {playername} teleportation request.</font>";
        public string TpaSentReceiver { get; set; } = "<font color='#4CE0BB'>You have received teleportation request from {playername}.</font>";
        public string TpaClickableReceiver { get; set; } =
         "You have received teleport request from: {playername} <a href='command:///tpaccept'>[ACCEPT]</a> <a href='command:///tpdeny'>[DENY]</a>";

        // Begin
        public string TpaBeginSender { get; set; } = "<font color='#4CE0E0'>You will be teleporting to {playername}. Do not move for {warmup}s!</font>";
        public string TpaBeginReceiver { get; set; } = "<font color='#4CE0E0'>{playername} is teleporting to you in {warmup}s.</font>";

        // Cancel / move
        public string TpaCanceledSender { get; set; } = "<font color='C91212'>You canceled the TPA request.</font>";
        public string TpaCanceledReceiver { get; set; } = "<font color='#C91212'>{playername} is canceled their TPA.</font>";
        public string TpaCancelUsage { get; set; } = "Cancel your outgoing TPA or an in-progress warmup.";
        public string TpaNothingToCancel { get; set; } = "You have no pending TPA or warmup to cancel.";


        // Denied
        public string TpaDeniedSender { get; set; } = "<font color='#C91212'>{playername} Has denied your TPA request.</font>";
        public string TpaDeniedReceiver { get; set; } = "<font color='#C91212'>You denied {playername}'s TPA request.</font>";

        // Success
        public string TpaSuccessSender { get; set; } = "<font color='#96E04C'>You successfuly teleported to {playername}.</font>";
        public string TpaSuccessReceiver { get; set; } = "<font color='#96E04C'>{playername} succesfully teleported to you.</font>";

        // Errors / usage
        public string TpaUsage { get; set; } = "<font color='#E04C4C'>Usage: /tpa 'Player Name'</font>";
        public string TpaNoPending { get; set; } = "<font color='#E04C4C'>You have no TPA request to Accept / Deny.</font>";
        public string TpaSelf { get; set; } = "<font color='#E04C4C'>You cannot teleport to your self.</font>";
        public string TpaNotFound { get; set; } = "<font color='#E04C4C'>Player not found or Offline.</font>";
        public string CooldownActive { get; set; } = "<font color='#E04C4C'>You must wait {seconds}s before using this again.</font>";
        public string TpaAlreadyPendingSender { get; set; } ="<font color='#E04C4C'>You already have a pending TPA request.</font>";


        public static string ConfigDir(ICoreServerAPI sapi)
        {
            var root = sapi.GetOrCreateDataPath("ModConfig");
            return Path.Combine(root, "EssentialsX", "Teleportation");
        }

        public static string ConfigPath(ICoreServerAPI sapi) => Path.Combine(ConfigDir(sapi), "TPAMessages.json");

        public static TPAMessages LoadOrCreate(ICoreServerAPI sapi)
        {
            try
            {
                var dir = ConfigDir(sapi);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var path = ConfigPath(sapi);
                if (!File.Exists(path))
                {
                    var def = new TPAMessages();
                    File.WriteAllText(path, JsonSerializer.Serialize(def, JsonOptions));
                    return def;
                }

                var text = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<TPAMessages>(text);
                return cfg ?? new TPAMessages();
            }
            catch
            {
                return new TPAMessages();
            }
        }
    }
}