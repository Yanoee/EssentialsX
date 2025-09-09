using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace EssentialsX.Modules.Teleport
{
    public class SpawnCommands
    {
        private readonly ICoreServerAPI sapi;
        private SpawnConfig cfg = null!;
        private readonly Dictionary<string, long> nextUseTimestampMs = new();
        private readonly Dictionary<string, long> warmupCallback = new();
        private readonly Dictionary<string, long> movePollListener = new();
        private readonly HashSet<string> canceledWarmup = new();

        public SpawnCommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                cfg = SpawnConfig.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                cfg = new SpawnConfig();
                sapi.World.Logger.Error("[EssentialsX] Failed to init Spawn module: {0}", ex);
            }
        }

        public void Register()
        {
            if (cfg == null)
            {
                sapi.World.Logger.Error("[EssentialsX] Spawn config not initialized.");
                return;
            }
            if (!cfg.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: Spawn");
                return;
            }
            sapi.ChatCommands
                .Create("spawn")
                .WithDescription(cfg.Messages.DescSpawn)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnSpawn);

            sapi.ChatCommands
                .Create("setspawn")
                .WithDescription(cfg.Messages.DescSetspawn)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnSetSpawn);

            sapi.World.Logger.Event("[EssentialsX] Loaded module: Spawn");
        }

        //Command Handlers

        private TextCommandResult OnSpawn(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);
            if (!cfg.Enabled)
            {
                Send(caller, cfg.Messages.Disabled);
                return TextCommandResult.Success();
            }
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
            BlockPos? target = ResolveSpawnTarget();
            if (target == null)
            {
                Send(caller, cfg.Messages.TeleportFailed);
                return TextCommandResult.Success();
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

        private TextCommandResult OnSetSpawn(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            if (!HasPermission(caller, cfg.SetSpawnAllowedRoles, cfg.SetSpawnAllowedPlayers))
            {
                Send(caller, cfg.Messages.NoPermissionSetspawn);
                return TextCommandResult.Success();
            }

            var ent = caller.Entity;
            if (ent == null)
            {
                Send(caller, cfg.Messages.TeleportFailed);
                return TextCommandResult.Success();
            }

            var pos = ent.ServerPos;
            cfg.HasCustomSpawn = true;
            cfg.SpawnPosition = new SpawnPosition
            {
                X = (int)Math.Floor(pos.X),
                Y = (int)Math.Floor(pos.Y),
                Z = (int)Math.Floor(pos.Z),
                Yaw = pos.Yaw,
                Pitch = pos.Pitch
            };

            try
            {
                SpawnConfig.Save(sapi, cfg);
                Send(caller, cfg.Messages.SetspawnSuccess);

                sapi.World.Logger.Event(
                    "[EssentialsX] Spawn location updated to {0},{1},{2} (yaw {3:0.00}, pitch {4:0.00}) by {5}",
                    cfg.SpawnPosition.X, cfg.SpawnPosition.Y, cfg.SpawnPosition.Z,
                    cfg.SpawnPosition.Yaw, cfg.SpawnPosition.Pitch, caller.PlayerName
                );
            }
            catch
            {
                Send(caller, cfg.Messages.SetspawnFailed);
            }

            return TextCommandResult.Success();
        }

        // Warmup 

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
            float lastHp = ent.WatchedAttributes?.GetFloat("health", 20f) ?? 20f;

            Send(caller, cfg.Messages.WarmupStart.Replace("{seconds}", warmupSeconds.ToString()));

            long pollId = sapi.World.RegisterGameTickListener(_ =>
            {
                var cur = ent.ServerPos.XYZ;

                if (cfg.CancelOnMove && cur.DistanceTo(startXyz) > 0.05)
                {
                    CancelWarmup(uid);
                    canceledWarmup.Add(uid);
                    Send(caller, cfg.Messages.WarmupCancelMove);
                }

                if (cfg.CancelOnDamage)
                {
                    float curHp = ent.WatchedAttributes?.GetFloat("health", lastHp) ?? lastHp;
                    if (curHp < lastHp - 0.01f)
                    {
                        CancelWarmup(uid);
                        canceledWarmup.Add(uid);
                        Send(caller, cfg.Messages.WarmupCancelDamage);
                    }
                    lastHp = curHp;
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

        // Teleport  Helpers 

        private void DoTeleport(IServerPlayer player, BlockPos? target, bool applyCooldown)
        {
            if (target == null)
            {
                Send(player, cfg.Messages.TeleportFailed);
                return;
            }

            try
            {
                sapi.WorldManager.LoadChunkColumnPriority(target.X / GlobalConstants.ChunkSize, target.Z / GlobalConstants.ChunkSize);

                double tx = target.X + 0.5;
                double ty = target.Y;
                double tz = target.Z + 0.5;
                player.Entity.TeleportToDouble(tx, ty, tz);

                if (applyCooldown && cfg.CooldownSeconds > 0)
                {
                    nextUseTimestampMs[player.PlayerUID] = sapi.World.ElapsedMilliseconds + (long)cfg.CooldownSeconds * 1000;
                }

                Send(player, cfg.Messages.TeleportSuccess);
            }
            catch
            {
                Send(player, cfg.Messages.TeleportFailed);
            }
        }

        private BlockPos? ResolveSpawnTarget()
        {
            // Custom spawn from JSON (if set)
            if (cfg.HasCustomSpawn && cfg.SpawnPosition != null)
            {
                var s = cfg.SpawnPosition;
                var bp = new BlockPos(s.X, s.Y, s.Z);
                return cfg.UseSafeTeleport ? FindSafeGround(bp) ?? bp : bp;
            }
            var wspawn = sapi.World.DefaultSpawnPosition;
            if (wspawn == null) return null;

            var def = new BlockPos((int)Math.Floor(wspawn.X), (int)Math.Floor(wspawn.Y), (int)Math.Floor(wspawn.Z));
            return cfg.UseSafeTeleport ? FindSafeGround(def) ?? def : def;
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
        private bool HasPermission(IServerPlayer p, List<string> roles, List<string> players)
        {
            var roleCode = p.Role?.Code ?? "";
            if (roles != null)
            {
                foreach (var r in roles)
                {
                    if (roleCode.Equals(r, StringComparison.OrdinalIgnoreCase)) return true;
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
        private void Send(IServerPlayer p, string body)
        {
            var wrapped = $"{cfg.Messages.Prefix}: {body}";
            sapi.SendMessage(p, 0, wrapped, EnumChatType.CommandSuccess);
        }
    }

    // Config

    public class SpawnConfig
    {
        public bool Enabled { get; set; } = true;
        public int WarmupSeconds { get; set; } = 10;
        public int CooldownSeconds { get; set; } = 120;
        public bool CancelOnMove { get; set; } = true;
        public bool CancelOnDamage { get; set; } = true;
        public bool UseSafeTeleport { get; set; } = true;
        public bool HasCustomSpawn { get; set; } = false;
        public SpawnPosition? SpawnPosition { get; set; } = null;
        public List<string> BypassRoles { get; set; } = new() { "admin", "sumod", "crmod" };
        public List<string> SetSpawnAllowedRoles { get; set; } = new() { "admin", "sumod", "crmod" };
        public List<string> BypassPlayers { get; set; } = new() { "Notch" };
        public List<string> SetSpawnAllowedPlayers { get; set; } = new() { "Notch" };

        public SpawnMessages Messages { get; set; } = new SpawnMessages();
        private const string RelFolder = "ModConfig/EssentialsX/Teleportation";
        private const string FileName = "SpawnConfig.json";

        public static SpawnConfig LoadOrCreate(ICoreServerAPI sapi)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);

            if (!File.Exists(path))
            {
                var def = new SpawnConfig();
                Save(sapi, def);
                return def;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<SpawnConfig>(json) ?? new SpawnConfig();
                cfg.Messages ??= new SpawnMessages();
                cfg.BypassRoles ??= new List<string>();
                cfg.SetSpawnAllowedRoles ??= new List<string>();
                cfg.BypassPlayers ??= new List<string>();   
                cfg.SetSpawnAllowedPlayers ??= new List<string>(); 
                return cfg;
            }
            catch
            {
                var def = new SpawnConfig();
                Save(sapi, def);
                return def;
            }
        }

        public static void Save(ICoreServerAPI sapi, SpawnConfig cfg)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);
            Directory.CreateDirectory(folder);

            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, opts), Encoding.UTF8);
        }
    }

    public class SpawnPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; }
    }

    public class SpawnMessages
    {
        public string Prefix { get; set; } = "<strong><font color='#FFFFFF'>[</font><font color='#5384EE' weight='bold'>Spawn</font><font color='#FFFFFF'>]</font></strong>";
        public string DescSpawn { get; set; } = "<font color='#00FF80'>/spawn — teleport to server spawn</font>";
        public string DescSetspawn { get; set; } = "<font color='#00FF80'>/setspawn — set global server spawn</font> <font color='#FF8080'>(Admin only)</font>";
        public string PlayerOnly { get; set; } = "<font color='#FF0000'>Only players can use this command.</font>";
        public string Disabled { get; set; } = "<font color='#FF8080'>Spawn is disabled by server settings.</font>";
        public string NoPermissionSetspawn { get; set; } = "<font color='#FF8080'>You don't have permission to use /setspawn.</font>";
        public string WarmupStart { get; set; } = "<font color='#EED053'>Teleporting in {seconds}s. Don't move!</font>";
        public string WarmupCancelMove { get; set; } = "<font color='#FF8080'>Teleport to </font><font color='#00FF80'> Spawn</font> <font color='#FF8080'>cancelled, you moved.</font>";
        public string WarmupCancelDamage { get; set; } = "<font color='#FF8080'>Teleport to </font><font color='#00FF80'> Spawn</font> <font color='#FF8080'>cancelled, you took damage.</font>";
        public string CooldownActive { get; set; } = "<font color='#FFA0A0'>You must wait {seconds}s before using /spawn again.</font>";
        public string TeleportSuccess { get; set; } = "<font color='#00FF80'>Teleported to spawn.</font>";
        public string TeleportFailed { get; set; } = "<font color='#FF8080'>Teleport failed.</font>";
        public string SetspawnSuccess { get; set; } = "<font color='#00FF80'>Server spawn updated.</font>";
        public string SetspawnFailed { get; set; } = "<font color='#FF8080'>Failed to update server spawn.</font>";
    }
}
