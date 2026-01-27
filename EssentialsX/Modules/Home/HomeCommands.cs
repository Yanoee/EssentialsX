using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Home
{
    public class HomeCommands
    {
        private readonly ICoreServerAPI sapi;
        private HomeConfig cfg = null!;
        private readonly HomesStore store;
        private readonly Dictionary<string, long> nextUseTs = [];
        private readonly Dictionary<string, long> warmupCb = [];
        private readonly Dictionary<string, long> movePollCb = [];
        private readonly HashSet<string> canceledWarmup = [];
        private readonly Dictionary<string, long> lastDamageMs = [];

        public HomeCommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                cfg = HomeConfig.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                cfg = new HomeConfig();
                sapi.World.Logger.Error("[EssentialsX] Failed to init Homes module: {0}", ex);
            }

            store = new HomesStore(sapi);
        }

        public void Register()
        {
            if (cfg == null)
            {
                sapi.World.Logger.Error("[EssentialsX] Homes config not initialized.");
                return;
            }
            if (!cfg.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: Homes");
                return;
            }

            sapi.ChatCommands.Create("sethome")
                .WithDescription(cfg.Messages.DescSetHome)
                .RequiresPlayer().RequiresPrivilege("chat")
                .WithArgs(new StringArgParser("name", false))
                .HandleWith(SetHome);

            sapi.ChatCommands.Create("delhome")
                .WithDescription(cfg.Messages.DescDelHome)
                .RequiresPlayer().RequiresPrivilege("chat")
                .WithArgs(new StringArgParser("name", false))
                .HandleWith(DelHome);

            sapi.ChatCommands.Create("home")
                .WithDescription(cfg.Messages.DescHome)
                .RequiresPlayer().RequiresPrivilege("chat")
                .WithArgs(new StringArgParser("name", false))
                .HandleWith(Home);

            sapi.ChatCommands.Create("homes")
                .WithDescription(cfg.Messages.DescHomes)
                .RequiresPlayer().RequiresPrivilege("chat")
                .HandleWith(ListHomes);

            sapi.World.Logger.Event("[EssentialsX] Loaded module: Homes");

            sapi.Event.PlayerLeave += (IServerPlayer p) =>
            {
                var uid = p.PlayerUID;

                if (movePollCb.TryGetValue(uid, out var pid))
                {
                    sapi.World.UnregisterGameTickListener(pid);
                    movePollCb.Remove(uid);
                }
                if (warmupCb.TryGetValue(uid, out var cb))
                {
                    sapi.World.UnregisterCallback(cb);
                    warmupCb.Remove(uid);
                }
                canceledWarmup.Remove(uid);
            };
        }

        //Commands
        private TextCommandResult SetHome(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            if (cfg.CombatTagBlocksSetHome && IsCombatTagged(player, cfg.DenyIfCombatTaggedSeconds))
            {
                Send(player, cfg.Messages.CombatTaggedSetBlocked.Replace("{seconds}", cfg.DenyIfCombatTaggedSeconds.ToString()));
                return TextCommandResult.Success();
            }

            string? raw = GetWordArg(args);
            if (string.IsNullOrWhiteSpace(raw))
            {
                Send(player, cfg.Messages.UsageSetHome);
                return TextCommandResult.Success();
            }

            string name = NormalizeName(raw);
            if (name.Length > cfg.MaxNameLength)
            {
                Send(player, cfg.Messages.NameTooLong.Replace("{max}", cfg.MaxNameLength.ToString()));
                return TextCommandResult.Success();
            }
            if (!Regex.IsMatch(name, cfg.AllowedCharsRegex))
            {
                Send(player, cfg.Messages.NameInvalid);
                return TextCommandResult.Success();
            }

            string uid = player.PlayerUID;
            string role = player.Role?.Code ?? "suplayer";

            var data = store.Load(uid);
            int cap = GetLimit(uid, role);
            bool exists = TryFindKey(data.Homes, name, out string? existingKey);

            if (!exists && data.Homes.Count >= cap)
            {
                Send(player, cfg.Messages.LimitReached.Replace("{count}", cap.ToString()));
                return TextCommandResult.Success();
            }
            if (exists)
            {
                Send(player, cfg.Messages.DuplicateName);
                return TextCommandResult.Success();
            }

            if (cfg.NoSetHomeInAir && !OnSolidGround(player)) { Send(player, cfg.Messages.NoGround); return TextCommandResult.Success(); }
            if (cfg.DenyIfInLiquid && InLiquid(player)) { Send(player, cfg.Messages.InLiquid); return TextCommandResult.Success(); }

            var ep = player.Entity.ServerPos;
            var hp = new HomePoint { X = ep.X, Y = ep.Y, Z = ep.Z, Yaw = ep.Yaw, Pitch = ep.Pitch };

            string key = existingKey ?? name;
            data.Homes[key] = hp;
            data.LastUsed = key;
            store.Save(uid, data);

            Send(player, cfg.Messages.Saved.Replace("{name}", key));
            sapi.World.Logger.Audit("[EssentialsX] {0} saved home '{1}' at {2:F1}/{3:F1}/{4:F1}", player.PlayerName, key, hp.X, hp.Y, hp.Z);
            return TextCommandResult.Success();
        }

        private TextCommandResult DelHome(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);
            string uid = player.PlayerUID;

            string? raw = GetWordArg(args);
            if (string.IsNullOrWhiteSpace(raw)) { Send(player, cfg.Messages.UsageDelHome); return TextCommandResult.Success(); }

            string name = NormalizeName(raw);
            var data = store.Load(uid);

            if (!TryFindKey(data.Homes, name, out var existingKey) || existingKey == null)
            {
                Send(player, cfg.Messages.NoSuchHome);
                return TextCommandResult.Success();
            }

            data.Homes.Remove(existingKey);
            if (data.LastUsed == existingKey) data.LastUsed = null;
            store.Save(uid, data);

            Send(player, cfg.Messages.Deleted.Replace("{name}", existingKey));
            return TextCommandResult.Success();
        }

        private TextCommandResult ListHomes(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);
            string uid = player.PlayerUID;

            var data = store.Load(uid);
            if (data.Homes.Count == 0) { Send(player, cfg.Messages.ListNone); return TextCommandResult.Success(); }

            var names = new List<string>();
            foreach (var key in data.Homes.Keys)
            {
                var disp = key;
                names.Add($"<a href='command:///home {disp}'>{disp}</a>");
            }

            Send(player, cfg.Messages.ListSome
                .Replace("{count}", data.Homes.Count.ToString())
                .Replace("{list}", string.Join(", ", names)));
            return TextCommandResult.Success();
        }

        private TextCommandResult Home(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);
            string uid = player.PlayerUID;

            if (warmupCb.ContainsKey(uid) || movePollCb.ContainsKey(uid))
            {
                Send(player, cfg.Messages.AlreadyTeleporting);
                return TextCommandResult.Success();
            }

            if (IsCombatTagged(player, cfg.DenyIfCombatTaggedSeconds))
            {
                Send(player, cfg.Messages.CombatTaggedUseBlocked.Replace("{seconds}", cfg.DenyIfCombatTaggedSeconds.ToString()));
                return TextCommandResult.Success();
            }

            string? raw = GetWordArg(args);
            string? requested = string.IsNullOrWhiteSpace(raw) ? null : NormalizeName(raw);

            long now = sapi.World.ElapsedMilliseconds;
            if (!PlayerBypasses(player) && nextUseTs.TryGetValue(uid, out var ready) && now < ready)
            {
                int left = (int)Math.Ceiling((ready - now) / 1000.0);
                Send(player, cfg.Messages.Cooldown.Replace("{seconds}", left.ToString()));
                return TextCommandResult.Success();
            }

            var data = store.Load(uid);
            string? key = null;

            if (!string.IsNullOrWhiteSpace(requested))
            {
                if (!TryFindKey(data.Homes, requested, out key) || key == null)
                {
                    Send(player, cfg.Messages.NoSuchHome);
                    return TextCommandResult.Success();
                }
            }
            else
            {
                key = data.LastUsed;
                if (string.IsNullOrWhiteSpace(key) || !data.Homes.ContainsKey(key))
                {
                    Send(player, cfg.Messages.NoHome);
                    return TextCommandResult.Success();
                }
            }

            var target = data.Homes[key!];

            bool bypass = PlayerBypasses(player);
            int warm = bypass ? 0 : Math.Max(0, cfg.WarmupSeconds);

            if (warm <= 0)
            {
                DoTeleport(player, key!, target, applyCooldown: !bypass);
                data.LastUsed = key;
                store.Save(uid, data);
                return TextCommandResult.Success();
            }

            Send(player, cfg.Messages.Begin.Replace("{name}", key!).Replace("{seconds}", warm.ToString()));

            var ent = player.Entity;
            if (ent == null) { return TextCommandResult.Success(); }

            var startPos = ent.ServerPos.XYZ.Clone();
            float lastHp = GetHealth(ent);

            long pollId = sapi.World.RegisterGameTickListener(_ =>
            {
                var pos = ent.ServerPos.XYZ;

                if (cfg.CancelOnMove && pos.DistanceTo(startPos) > 0.05f)
                {
                    CancelWarmup(uid);
                    Send(player, cfg.Messages.Moved);
                    return;
                }

                if (cfg.CancelOnDamage)
                {
                    float hp = GetHealth(ent);
                    if (hp < lastHp - 0.01f)
                    {
                        lastDamageMs[uid] = sapi.World.ElapsedMilliseconds;

                        CancelWarmup(uid);
                        Send(player, cfg.Messages.DamageCancel);
                        return;
                    }
                    lastHp = hp;
                }
            }, 100);
            movePollCb[uid] = pollId;

            long cbId = sapi.World.RegisterCallback(_ =>
            {
                if (canceledWarmup.Remove(uid)) return;

                if (movePollCb.TryGetValue(uid, out var pid))
                {
                    sapi.World.UnregisterGameTickListener(pid);
                    movePollCb.Remove(uid);
                }
                warmupCb.Remove(uid);

                DoTeleport(player, key!, target, applyCooldown: !bypass);

                data.LastUsed = key;
                store.Save(uid, data);

            }, warm * 1000);
            warmupCb[uid] = cbId;

            return TextCommandResult.Success();
        }

        //Internals
        private void DoTeleport(IServerPlayer player, string homeName, HomePoint target, bool applyCooldown)
        {
            try
            {
                sapi.WorldManager.LoadChunkColumnPriority(
                    (int)(target.X / GlobalConstants.ChunkSize),
                    (int)(target.Z / GlobalConstants.ChunkSize)
                );

                player.Entity?.TeleportToDouble(target.X, target.Y, target.Z);

                var ent = player.Entity;
                if (ent != null)
                {
                    ent.ServerPos.Yaw = target.Yaw;
                    ent.ServerPos.Pitch = target.Pitch;
                }

                if (applyCooldown && cfg.CooldownSeconds > 0)
                {
                    nextUseTs[player.PlayerUID] = sapi.World.ElapsedMilliseconds + (long)cfg.CooldownSeconds * 1000;
                }

                Send(player, cfg.Messages.Teleported.Replace("{name}", homeName));
            }
            catch { }
        }

        private void CancelWarmup(string uid)
        {
            if (movePollCb.TryGetValue(uid, out var pid))
            {
                sapi.World.UnregisterGameTickListener(pid);
                movePollCb.Remove(uid);
            }
            if (warmupCb.TryGetValue(uid, out var cb))
            {
                sapi.World.UnregisterCallback(cb);
                warmupCb.Remove(uid);
            }
            canceledWarmup.Add(uid);
        }

        private bool PlayerBypasses(IServerPlayer p)
        {
            if (cfg.BypassPlayers != null)
                foreach (var n in cfg.BypassPlayers)
                    if (p.PlayerName.Equals(n, StringComparison.OrdinalIgnoreCase))
                        return true;

            if (cfg.BypassRoles != null)
            {
                var role = p.Role?.Code ?? "";
                foreach (var r in cfg.BypassRoles)
                    if (role.Equals(r, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            return false;
        }

        private static float GetHealth(Entity ent) =>
            ent?.WatchedAttributes?.GetFloat("health", 20f) ?? 20f;

        private int GetLimit(string uid, string role)
        {
            if (cfg.PlayerOverrides.TryGetValue(uid, out int ovByUid))
                return ovByUid;

            foreach (var kv in cfg.PlayerOverrides)
            {
                if (string.Equals(kv.Key, uid, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }

            return cfg.GetMaxHomesForRole(role);
        }

        private string NormalizeName(string raw)
        {
            string s = cfg.TrimNames ? raw.Trim() : raw;
            if ((s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") && s.EndsWith("'")))
                s = s.Substring(1, s.Length - 2);
            if (cfg.CaseInsensitiveNames) s = s.ToLowerInvariant();
            return s;
        }

        private bool TryFindKey(Dictionary<string, HomePoint> dict, string normalized, out string? existingKey)
        {
            if (!cfg.CaseInsensitiveNames)
            {
                existingKey = dict.ContainsKey(normalized) ? normalized : null;
                return existingKey != null;
            }

            foreach (var k in dict.Keys)
                if (string.Equals(k, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    existingKey = k;
                    return true;
                }
            existingKey = null;
            return false;
        }

        private static string? GetWordArg(TextCommandCallingArgs args)
        {
            return args.Parsers.
                FirstOrDefault()?
                .GetValue()?
                .ToString();
        }

        //environment checks
        private bool OnSolidGround(IServerPlayer p)
        {
            var pos = p.Entity.ServerPos.AsBlockPos;
            var below = pos.DownCopy();
            var block = sapi.World.BlockAccessor.GetBlock(below);
            return block != null && block.BlockId != 0;
        }

        private bool InLiquid(IServerPlayer p)
        {
            var pos = p.Entity.ServerPos.AsBlockPos;
            var block = sapi.World.BlockAccessor.GetBlock(pos);
            var code = block?.Code?.Path ?? "";
            return !string.IsNullOrEmpty(code) &&
                   (code.Contains("water", StringComparison.OrdinalIgnoreCase) ||
                    code.Contains("lava", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsCombatTagged(IServerPlayer p, int seconds)
        {
            if (seconds <= 0 || p == null) return false;
            var uid = p.PlayerUID;
            if (!lastDamageMs.TryGetValue(uid, out var lastMs)) return false;
            long now = sapi.World.ElapsedMilliseconds;
            return (now - lastMs) < seconds * 1000L;
        }

        private void Send(IServerPlayer p, string body)
        {
            var wrapped = $"{cfg.Messages.HomePrefix}: {body}";
            sapi.SendMessage(p, 0, wrapped, EnumChatType.CommandSuccess);
        }
    }
}
