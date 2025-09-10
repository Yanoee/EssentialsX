using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Teleport
{
    public class BackCommands
    {
        private readonly ICoreServerAPI sapi;
        private BackConfig cfg = null!;

        private readonly Dictionary<string, BlockPos> lastDeath = [];
        private readonly Dictionary<string, long> nextUseTimestampMs = [];
        private readonly Dictionary<string, long> warmupCallback = [];
        private readonly Dictionary<string, long> movePollListener = [];
        private readonly HashSet<string> canceledWarmup = [];

        public BackCommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                cfg = BackConfig.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                cfg = new BackConfig();
                sapi.World.Logger.Error("[EssentialsX] Failed to init Back module: {0}", ex);
            }
        }

        public void Register()
        {
            if (cfg == null)
            {
                sapi.World.Logger.Error("[EssentialsX] Back config not initialized.");
                return;
            }
            if (!cfg.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: Back");
                return;
            }

            sapi.ChatCommands
                .Create("back")
                .WithDescription(cfg.Messages.Usage)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnBack);

            sapi.World.Logger.Event("[EssentialsX] Loaded module: Back");
            sapi.Event.PlayerDeath += (IServerPlayer p, DamageSource _) =>
            {
                var pos = p.Entity?.ServerPos?.AsBlockPos;
                if (pos != null)
                {
                    lastDeath[p.PlayerUID] = pos.Copy();
                    Send(p, cfg.Messages.DeathSaved
                        .Replace("{x}", pos.X.ToString())
                        .Replace("{y}", pos.Y.ToString())
                        .Replace("{z}", pos.Z.ToString()));
                }
            };
            sapi.Event.PlayerLeave += (IServerPlayer p) =>
            {
                var uid = p.PlayerUID;
                canceledWarmup.Remove(uid);
                if (movePollListener.TryGetValue(uid, out var pid))
                {
                    sapi.World.UnregisterGameTickListener(pid);
                    movePollListener.Remove(uid);
                }
                if (warmupCallback.TryGetValue(uid, out var cbid))
                {
                    sapi.World.UnregisterCallback(cbid);
                    warmupCallback.Remove(uid);
                }
            };
        }

        private TextCommandResult OnBack(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            if (warmupCallback.ContainsKey(caller.PlayerUID) || movePollListener.ContainsKey(caller.PlayerUID))
            {
                Send(caller, cfg.Messages.AlreadyTeleporting);
                return TextCommandResult.Success();
            }

            if (!lastDeath.TryGetValue(caller.PlayerUID, out var death) || death == null)
            {
                Send(caller, cfg.Messages.NoLocationSaved);
                return TextCommandResult.Success();
            }
            BlockPos? target = death;

            bool bypass = HasPermission(caller, cfg.BypassRoles, cfg.BypassPlayers);
            if (!bypass && cfg.CooldownSeconds > 0)
            {
                var now = sapi.World.ElapsedMilliseconds;
                if (nextUseTimestampMs.TryGetValue(caller.PlayerUID, out var readyMs) && now < readyMs)
                {
                    var left = (int)Math.Ceiling((readyMs - now) / 1000.0);
                    Send(caller, cfg.Messages.CooldownActive.Replace("{seconds}", left.ToString()));
                    return TextCommandResult.Success();
                }
            }

            var warmup = bypass ? 0 : Math.Max(0, cfg.WarmupSeconds);
            if (warmup <= 0)
            {
                DoTeleport(caller, target, applyCooldown: !bypass);
                return TextCommandResult.Success();
            }

            BeginWarmup(caller, target, warmup);
            return TextCommandResult.Success();
        }

        private void BeginWarmup(IServerPlayer caller, BlockPos? target, int warmupSeconds)
        {
            var uid = caller.PlayerUID;
            CancelWarmup(uid);

            var ent = caller.Entity;
            if (ent == null)
            {
                Send(caller, cfg.Messages.TeleportFailed);
                return;
            }

            var startXyz = ent.ServerPos.XYZ.Clone();

            Send(caller, cfg.Messages.WarmupBegin.Replace("{warmup}", warmupSeconds.ToString()));

            long pollId = sapi.World.RegisterGameTickListener(_ =>
            {
                var cur = ent.ServerPos.XYZ;

                if (cfg.CancelOnMove && cur.DistanceTo(startXyz) > 0.05)
                {
                    CancelWarmup(uid);
                    canceledWarmup.Add(uid);
                    Send(caller, cfg.Messages.WarmupCanceled);
                }
            }, 100);
            movePollListener[uid] = pollId;

            long cb = sapi.World.RegisterCallback(_ =>
            {
                if (canceledWarmup.Remove(uid)) return;

                CancelWarmup(uid);
                DoTeleport(caller, target, applyCooldown: true);

            }, warmupSeconds * 1000);
            warmupCallback[uid] = cb;
        }

        private void CancelWarmup(string uid)
        {
            if (movePollListener.TryGetValue(uid, out var pid))
            {
                sapi.World.UnregisterGameTickListener(pid);
                movePollListener.Remove(uid);
            }
            if (warmupCallback.TryGetValue(uid, out var cbid))
            {
                sapi.World.UnregisterCallback(cbid);
                warmupCallback.Remove(uid);
            }
        }

        private void DoTeleport(IServerPlayer player, BlockPos? target, bool applyCooldown)
        {
            if (target == null)
            {
                Send(player, cfg.Messages.NoLocationSaved);
                return;
            }

            try
            {
                sapi.WorldManager.LoadChunkColumnPriority(target.X / GlobalConstants.ChunkSize, target.Z / GlobalConstants.ChunkSize);

                BlockPos final = target;
                if (cfg.UseSafeTeleport)
                {
                    final = FindSafeGround(target) ?? target;
                }

                double tx = final.X + 0.5;
                double ty = final.Y;
                double tz = final.Z + 0.5;

                var ent = player.Entity;
                if (ent == null)
                {
                    Send(player, cfg.Messages.TeleportFailed);
                    return;
                }

                ent.TeleportToDouble(tx, ty, tz);

                if (applyCooldown && cfg.CooldownSeconds > 0)
                {
                    nextUseTimestampMs[player.PlayerUID] = sapi.World.ElapsedMilliseconds + (long)cfg.CooldownSeconds * 1000;
                }

                Send(player, cfg.Messages.Teleported
                    .Replace("{x}", final.X.ToString())
                    .Replace("{y}", final.Y.ToString())
                    .Replace("{z}", final.Z.ToString()));
            }
            catch
            {
                Send(player, cfg.Messages.TeleportFailed);
            }
        }

        private bool HasPermission(IServerPlayer p, List<string> roles, List<string> players)
        {
            var code = p.Role?.Code ?? "";
            if (roles != null)
            {
                foreach (var r in roles)
                {
                    if (code.Equals(r, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            if (players != null)
            {
                foreach (var name in players)
                {
                    if (p.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        private BlockPos? FindSafeGround(BlockPos around)
        {
            var pos = around.Copy();
            for (int i = 0; i < 12; i++)
            {
                var here = sapi.World.BlockAccessor.GetBlock(pos);
                var below = sapi.World.BlockAccessor.GetBlock(pos.DownCopy());
                if (here.IsReplacableBy(sapi.World.GetBlock(0)) && below.SideSolid[BlockFacing.UP.Index])
                    return pos;
                pos.Y--;
            }
            return around;
        }

        private void Send(IServerPlayer p, string body)
        {
            var wrapped = $"{cfg.Messages.BackPrefix}: {body}";
            sapi.SendMessage(p, 0, wrapped, EnumChatType.CommandSuccess);
        }
    }

    //Config
    public class BackConfig
    {
        public bool Enabled { get; set; } = true;
        public int WarmupSeconds { get; set; } = 5;
        public int CooldownSeconds { get; set; } = 600;
        public bool CancelOnMove { get; set; } = true;
        public bool UseSafeTeleport { get; set; } = true;

        public List<string> BypassRoles { get; set; } = ["admin", "sumod", "crmod"];
        public List<string> BypassPlayers { get; set; } = ["Notch"];

        public BackMessages Messages { get; set; } = new BackMessages();

        private const string RelFolder = "ModConfig/EssentialsX/Teleportation";
        private const string FileName = "BackConfig.json";

        public static BackConfig LoadOrCreate(ICoreServerAPI sapi)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);

            if (!File.Exists(path))
            {
                var def = new BackConfig();
                Save(sapi, def);
                return def;
            }
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<BackConfig>(json) ?? new BackConfig();

                cfg.Messages ??= new BackMessages();
                cfg.BypassRoles ??= [];
                cfg.BypassPlayers ??= [];

                return cfg;
            }
            catch
            {
                var def = new BackConfig();
                Save(sapi, def);
                return def;
            }
        }
        public static void Save(ICoreServerAPI sapi, BackConfig cfg)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);
            Directory.CreateDirectory(folder);

            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, opts), Encoding.UTF8);
        }
    }

    public class BackMessages
    {
        public string BackPrefix { get; set; } = "<strong><font color='#FFFFFF'>[</font><font color='#FFFF00' weight='bold'>Back</font><font color='#FFFFFF'>]</font></strong>";
        public string Usage { get; set; } = "<font color='#E0A84C'>Return to your last death or teleport position</font>";
        public string PlayerOnly { get; set; } = "<font color='#FF0000'>Only players can use /back.</font>";
        public string NoLocationSaved { get; set; } = "<font color='#FF0000'>No saved death location.</font>";
        public string WarmupBegin { get; set; } = "<font color='#0080FF'>Teleporting to your death point in {warmup}s. Don't move!</font>";
        public string WarmupCanceled { get; set; } = "<font color='#FF0000'>Teleport canceled, you moved!</font>";
        public string CooldownActive { get; set; } = "<font color='#FF0000'>You must wait {seconds}s before using /back again.</font>";
        public string Teleported { get; set; } = "<font color='#00FF80'>Teleported back to {x}, {y}, {z}.</font>";
        public string DeathSaved { get; set; } = "<font color='#00FF80'>Death point saved at {x}, {y}, {z}.</font>";
        public string TeleportFailed { get; set; } = "<font color='#FF8080'>Teleport failed.</font>";
        public string AlreadyTeleporting { get; set; } = "<font color='#C91212'>You already have a /back teleport in progress.</font>";
    }
}
