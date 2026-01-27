using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace EssentialsX.Modules.Teleport
{
    public class RandomTeleport
    {
        private readonly ICoreServerAPI sapi;
        private RtpConfig cfg = null!;

        private readonly Dictionary<string, long> nextUseTimestampMs = [];
        private readonly Dictionary<string, long> warmupCallback = [];
        private readonly Dictionary<string, long> movePollListener = [];
        private readonly HashSet<string> canceledWarmup = [];
        private readonly Dictionary<string, bool> isSearching = [];

        public RandomTeleport(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                cfg = RtpConfig.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                cfg = new RtpConfig();
                sapi.World.Logger.Error("[EssentialsX] Failed to init Rtp module: {0}", ex);
            }
        }

        public void Register()
        {
            if (cfg == null)
            {
                sapi.World.Logger.Error("[EssentialsX] Rtp config not initialized.");
                return;
            }
            if (!cfg.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: Rtp");
                return;
            }

            sapi.ChatCommands
                .Create("rtp")
                .WithDescription(cfg.Messages.DescRtp)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnRtp);

            sapi.World.Logger.Event("[EssentialsX] Loaded module: Rtp");

            sapi.Event.PlayerLeave += (IServerPlayer p) =>
            {
                var uid = p.PlayerUID;
                canceledWarmup.Remove(uid);
                isSearching.Remove(uid);

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

        private TextCommandResult OnRtp(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            if (!cfg.Enabled)
            {
                Send(caller, cfg.Messages.Disabled);
                return TextCommandResult.Success();
            }

            var uid = caller.PlayerUID;

            if (isSearching.ContainsKey(uid) || warmupCallback.ContainsKey(uid) || movePollListener.ContainsKey(uid))
            {
                Send(caller, cfg.Messages.AlreadyTeleporting);
                return TextCommandResult.Success();
            }

            bool bypass = HasPermission(caller, cfg.BypassRoles, cfg.BypassPlayers);

            if (!bypass && cfg.CooldownSeconds > 0)
            {
                var now = sapi.World.ElapsedMilliseconds;
                if (nextUseTimestampMs.TryGetValue(uid, out var readyMs) && now < readyMs)
                {
                    var left = (int)Math.Ceiling((readyMs - now) / 1000.0);
                    Send(caller, cfg.Messages.CooldownActive.Replace("{seconds}", left.ToString()));
                    return TextCommandResult.Success();
                }
            }

            isSearching[uid] = true;
            Send(caller, cfg.Messages.Searching);

            var target = FindRandomSafeLocation(caller);
            isSearching.Remove(uid);

            if (target == null)
            {
                Send(caller, cfg.Messages.NoSafeLocation);
                return TextCommandResult.Success();
            }

            var warmup = bypass ? 0 : Math.Max(0, cfg.WarmupSeconds);
            if (warmup <= 0)
            {
                DoTeleport(caller, target, applyCooldown: !bypass);
                return TextCommandResult.Success();
            }

            BeginWarmup(caller, target, warmup, applyCooldown: !bypass);
            return TextCommandResult.Success();
        }


        private void BeginWarmup(IServerPlayer caller, BlockPos target, int warmupSeconds, bool applyCooldown)
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
                DoTeleport(caller, target, applyCooldown);

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

        // Helpers

        private void DoTeleport(IServerPlayer player, BlockPos target, bool applyCooldown)
        {
            try
            {
                double tx = target.X + 0.5;
                double ty = target.Y;
                double tz = target.Z + 0.5;
                player.Entity.TeleportToDouble(tx, ty, tz);

                if (applyCooldown && cfg.CooldownSeconds > 0)
                {
                    nextUseTimestampMs[player.PlayerUID] = sapi.World.ElapsedMilliseconds + (long)cfg.CooldownSeconds * 1000;
                }

                var msg = cfg.Messages.TeleportSuccess
                    .Replace("{x}", target.X.ToString())
                    .Replace("{y}", target.Y.ToString())
                    .Replace("{z}", target.Z.ToString());

                Send(player, msg);
            }
            catch
            {
                Send(player, cfg.Messages.TeleportFailed);
            }
        }

        private BlockPos? FindRandomSafeLocation(IServerPlayer caller)
        {
            const int attempts = 25;
            var center = GetCenterForRtp(caller);
            var rand = sapi.World.Rand;

            for (int i = 0; i < attempts; i++)
            {
                int minR = Math.Max(0, cfg.MinDistance);
                int maxR = Math.Max(minR, cfg.MaxDistance);
                int radius = (maxR == minR) ? minR : rand.Next(minR, maxR + 1);

                double angle = rand.NextDouble() * Math.PI * 2.0;
                int x = center.X + (int)Math.Round(radius * Math.Cos(angle));
                int z = center.Z + (int)Math.Round(radius * Math.Sin(angle));
                int cx = x / GlobalConstants.ChunkSize;
                int cz = z / GlobalConstants.ChunkSize;

                // Only probe already-loaded columns; never generate/preload
                var chunk = sapi.WorldManager.GetChunk(cx, 0, cz);
                if (chunk == null) continue;

                var y = GetSurfaceY(x, z);
                if (y == null) continue;

                var candidate = new BlockPos(x, y.Value, z);
                if (!IsSafe(candidate)) continue;

                if (!HasHeadroom(candidate)) continue;

                return candidate;
            }

            return null;
        }

        private BlockPos GetCenterForRtp(IServerPlayer caller)
        {
            var pos = caller.Entity?.ServerPos;
            if (pos != null)
            {
                return new BlockPos((int)Math.Floor(pos.X), (int)Math.Floor(pos.Y), (int)Math.Floor(pos.Z));
            }
            var wspawn = sapi.World.DefaultSpawnPosition;
            if (wspawn != null)
            {
                return new BlockPos((int)Math.Floor(wspawn.X), (int)Math.Floor(wspawn.Y), (int)Math.Floor(wspawn.Z));
            }
            int? sy = GetSurfaceY(0, 0);
            return new BlockPos(0, sy ?? 128, 0);
        }

        private int? GetSurfaceY(int x, int z)
        {
            var ba = sapi.World.BlockAccessor;
            int top = ba.MapSizeY - 2;
            if (top < 3) top = 3;

            for (int y = top; y >= 2; y--)
            {
                var here = ba.GetBlock(new BlockPos(x, y, z));
                var above = ba.GetBlock(new BlockPos(x, y + 1, z));
                var below = ba.GetBlock(new BlockPos(x, y - 1, z));

                if (IsAir(here) && IsAir(above) && IsSolidTop(below))
                {
                    if (cfg.AvoidWater && (IsWater(here) || IsWater(below))) continue;
                    if (cfg.AvoidLava && (IsLava(here) || IsLava(below))) continue;
                    if (cfg.AvoidLeaves && (IsLeaves(here) || IsLeaves(below))) continue;

                    return y;
                }
            }

            return null;
        }

        private bool IsSafe(BlockPos pos)
        {
            var ba = sapi.World.BlockAccessor;
            var feet = ba.GetBlock(pos);
            var below = ba.GetBlock(pos.DownCopy());

            if (!IsAir(feet)) return false;
            if (!IsSolidTop(below)) return false;

            if (cfg.AvoidWater && (IsWater(feet) || IsWater(below))) return false;
            if (cfg.AvoidLava && (IsLava(feet) || IsLava(below))) return false;
            if (cfg.AvoidLeaves && (IsLeaves(feet) || IsLeaves(below))) return false;

            return true;
        }

        private bool HasHeadroom(BlockPos pos)
        {
            var ba = sapi.World.BlockAccessor;
            var head = ba.GetBlock(pos.UpCopy());
            return IsAir(head);
        }

        // --- Checks
        private static bool IsAir(Block b) => b == null || b.BlockId == 0 || b.Replaceable > 0 && !b.SideSolid[BlockFacing.UP.Index];
        private static bool CodeHas(Block b, string needle) => b?.Code?.Path?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool IsWater(Block b) => CodeHas(b, "water");
        private static bool IsLava(Block b) => CodeHas(b, "lava");
        private static bool IsLeaves(Block b) => CodeHas(b, "leaves") || CodeHas(b, "leaf");
        private static bool IsSolidTop(Block b) => b != null && b.SideSolid[BlockFacing.UP.Index];

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

    public class RtpConfig
    {
        public bool Enabled { get; set; } = true;

        public int WarmupSeconds { get; set; } = 10;
        public int CooldownSeconds { get; set; } = 1200;

        public bool CancelOnMove { get; set; } = true;
        public bool CancelOnDamage { get; set; } = true;

        public int MinDistance { get; set; } = 5000;
        public int MaxDistance { get; set; } = 5000;

        public bool AvoidWater { get; set; } = true;
        public bool AvoidLava { get; set; } = true;
        public bool AvoidLeaves { get; set; } = true;

        public List<string> BypassRoles { get; set; } = ["admin", "sumod", "crmod"];
        public List<string> BypassPlayers { get; set; } = [];

        public RtpMessages Messages { get; set; } = new RtpMessages();

        private const string RelFolder = "ModConfig/EssentialsX/Teleportation";
        private const string FileName = "RtpConfig.json";

        public static RtpConfig LoadOrCreate(ICoreServerAPI sapi)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);

            if (!File.Exists(path))
            {
                var def = new RtpConfig();
                Save(sapi, def);
                return def;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<RtpConfig>(json) ?? new RtpConfig();
                cfg.Messages ??= new RtpMessages();
                cfg.BypassRoles ??= [];
                cfg.BypassPlayers ??= [];
                return cfg;
            }
            catch
            {
                var def = new RtpConfig();
                Save(sapi, def);
                return def;
            }
        }

        public static void Save(ICoreServerAPI sapi, RtpConfig cfg)
        {
            var folder = sapi.GetOrCreateDataPath(RelFolder);
            var path = Path.Combine(folder, FileName);
            Directory.CreateDirectory(folder);

            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, opts), Encoding.UTF8);
        }
    }

    public class RtpMessages
    {
        public string Prefix { get; set; } = "<strong><font color='#FFFFFF'>[</font><font color='#5384EE' weight='bold'>RTP</font><font color='#FFFFFF'>]</font></strong>";
        public string DescRtp { get; set; } = "<font color='#00FF80'>/rtp — random teleport around your current area</font>";
        public string PlayerOnly { get; set; } = "<font color='#FF0000'>Only players can use this command.</font>";
        public string Disabled { get; set; } = "<font color='#FF8080'>RTP is disabled by server settings.</font>";
        public string Searching { get; set; } = "<font color='#EED053'>Searching for a safe location…</font>";
        public string WarmupStart { get; set; } = "<font color='#EED053'>Teleporting in {seconds}s. Don't move!</font>";
        public string WarmupCancelMove { get; set; } = "<font color='#FF8080'>Random teleport cancelled, you moved.</font>";
        public string WarmupCancelDamage { get; set; } = "<font color='#FF8080'>Random teleport cancelled, you took damage.</font>";
        public string CooldownActive { get; set; } = "<font color='#FFA0A0'>You must wait {seconds}s before using /rtp again.</font>";
        public string TeleportSuccess { get; set; } = "<font color='#00FF80'>Teleported to </font><font color='#FFFFFF'>{x} {y} {z}</font>.";
        public string TeleportFailed { get; set; } = "<font color='#FF8080'>Teleport failed.</font>";
        public string NoSafeLocation { get; set; } = "<font color='#FF8080'>No safe location found. Please try again.</font>";
        public string AlreadyTeleporting { get; set; } = "<font color='#C91212'>You already have an /rtp in progress.</font>";
    }
}
