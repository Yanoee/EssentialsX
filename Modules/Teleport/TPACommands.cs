using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Teleport
{
    public class TPACommands
    {
        private readonly ICoreServerAPI sapi;
        private TpaConfig cfg = null!;
        private readonly Dictionary<string, long> nextUseTs = [];
        private readonly Dictionary<string, PendingRequest> pending = [];
        private readonly Dictionary<string, string> activeWarmupBySender = [];
        private readonly Dictionary<string, long> warmupCb = [];
        private readonly Dictionary<string, long> movePollCb = [];
        private readonly HashSet<string> canceledWarmup = [];
        private struct PendingRequest
        {
            public string SenderUid;
            public string ReceiverUid;
            public long ExpireAtMs;
        }

        public TPACommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                cfg = TpaConfig.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                cfg = new TpaConfig();
                sapi.World.Logger.Error("[EssentialsX] Failed to load TPA module: {0}", ex);
            }
        }

        public void Register()
        {
            if (cfg == null)
            {
                sapi.World.Logger.Error("[EssentialsX] TPA config not initialized.");
                return;
            }
            if (!cfg.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: TPA");
                return;
            }

            sapi.ChatCommands
                .Create("tpa")
                .WithDescription(cfg.Messages.DescTpa)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .WithArgs(new StringArgParser("player", false))
                .HandleWith(OnTpa);

            sapi.ChatCommands
                .Create("tpaccept")
                .WithDescription(cfg.Messages.DescTpAccept)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpAccept);

            sapi.ChatCommands
                .Create("tpdeny")
                .WithDescription(cfg.Messages.DescTpDeny)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpDeny);

            sapi.ChatCommands
                .Create("tpcancel")
                .WithDescription(cfg.Messages.DescTpCancel)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpCancel);

            sapi.World.Logger.Event("[EssentialsX] Loaded module: TPA");
            sapi.Event.PlayerLeave += (IServerPlayer p) =>
            {
                var uid = p.PlayerUID;
                canceledWarmup.Remove(uid);
                activeWarmupBySender.Remove(uid);

                if (movePollCb.TryGetValue(uid, out var pid))
                {
                    sapi.World.UnregisterGameTickListener(pid);
                    movePollCb.Remove(uid);
                }
                if (warmupCb.TryGetValue(uid, out var cbid))
                {
                    sapi.World.UnregisterCallback(cbid);
                    warmupCb.Remove(uid);
                }
                var toRemove = new List<string>();
                foreach (var kv in pending)
                    if (kv.Value.SenderUid == uid || kv.Value.ReceiverUid == uid)
                        toRemove.Add(kv.Key);
                foreach (var k in toRemove) pending.Remove(k);
            };
        }

        // Command handlers
        private TextCommandResult OnTpa(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            PurgeExpiredPending(sapi.World.ElapsedMilliseconds);

            var nowMs = sapi.World.ElapsedMilliseconds;

            if (!PlayerBypasses(caller) && nextUseTs.TryGetValue(caller.PlayerUID, out var ready) && nowMs < ready)
            {
                var left = (int)Math.Ceiling((ready - nowMs) / 1000.0);
                Send(caller, cfg.Messages.CooldownActive.Replace("{seconds}", left.ToString()));
                return TextCommandResult.Success();
            }
            string? targetName = null;
            try { targetName = args[0] as string; } catch { }
            targetName = targetName?.Trim();

            if (!string.IsNullOrEmpty(targetName) && targetName!.Length >= 2)
            {
                char first = targetName[0], last = targetName[^1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    targetName = targetName.Substring(1, targetName.Length - 2).Trim();
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                Send(caller, cfg.Messages.UsageTpa);
                return TextCommandResult.Success();
            }

            var target = FindPlayerByName(targetName);
            if (target == null)
            {
                Send(caller, cfg.Messages.NotFound);
                return TextCommandResult.Success();
            }
            if (target.PlayerUID == caller.PlayerUID)
            {
                Send(caller, cfg.Messages.SelfTarget);
                return TextCommandResult.Success();
            }

            foreach (var kv in pending)
            {
                var req = kv.Value;
                if (req.SenderUid == caller.PlayerUID && nowMs < req.ExpireAtMs)
                {
                    Send(caller, cfg.Messages.AlreadyPendingSender);
                    return TextCommandResult.Success();
                }
            }

            pending[target.PlayerUID] = new PendingRequest
            {
                SenderUid = caller.PlayerUID,
                ReceiverUid = target.PlayerUID,
                ExpireAtMs = nowMs + cfg.RequestExpireSeconds * 1000
            };

            Send(caller, cfg.Messages.SentSender.Replace("{playername}", target.PlayerName));
            Send(target, cfg.Messages.SentReceiver.Replace("{playername}", caller.PlayerName));
            Send(target, cfg.Messages.ClickableReceiver.Replace("{playername}", caller.PlayerName));

            return TextCommandResult.Success();
        }
        private TextCommandResult OnTpAccept(TextCommandCallingArgs args)
        {
            var receiver = args.Caller.Player as IServerPlayer;
            if (receiver == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            PurgeExpiredPending(sapi.World.ElapsedMilliseconds);

            if (!pending.TryGetValue(receiver.PlayerUID, out var req))
            {
                Send(receiver, cfg.Messages.NoPending);
                return TextCommandResult.Success();
            }

            var now = sapi.World.ElapsedMilliseconds;
            if (now > req.ExpireAtMs)
            {
                pending.Remove(receiver.PlayerUID);
                Send(receiver, cfg.Messages.NoPending);
                return TextCommandResult.Success();
            }

            var sender = PlayerByUid(req.SenderUid);
            if (sender == null || sender.ConnectionState != EnumClientState.Playing)
            {
                pending.Remove(receiver.PlayerUID);
                Send(receiver, cfg.Messages.NoPending);
                return TextCommandResult.Success();
            }

            if (warmupCb.ContainsKey(sender.PlayerUID) 
                || movePollCb.ContainsKey(sender.PlayerUID) 
                || activeWarmupBySender.ContainsKey(sender.PlayerUID))
            {
                Send(sender, cfg.Messages.AlreadyTeleporting);
                return TextCommandResult.Success();
            }

            pending.Remove(receiver.PlayerUID);

            bool bypass = PlayerBypasses(sender);
            int warm = bypass ? 0 : Math.Max(0, cfg.WarmupSeconds);

            Send(sender, cfg.Messages.BeginSender.Replace("{playername}", receiver.PlayerName).Replace("{warmup}", warm.ToString()));
            Send(receiver, cfg.Messages.BeginReceiver.Replace("{playername}", sender.PlayerName).Replace("{warmup}", warm.ToString()));

            if (warm <= 0)
            {
                DoTeleport(sender, receiver, applyCooldown: !bypass);
                return TextCommandResult.Success();
            }

            activeWarmupBySender[sender.PlayerUID] = receiver.PlayerUID;

            var ent = sender.Entity;
            if (ent == null)
            {
                Send(sender, cfg.Messages.TeleportFailed);
                return TextCommandResult.Success();
            }

            var startPos = ent.ServerPos.XYZ.Clone();
            float lastHp = ent.WatchedAttributes?.GetFloat("health", 20f) ?? 20f;

            long pollId = sapi.World.RegisterGameTickListener(_ =>
            {
                var pos = ent.ServerPos.XYZ;

                if (cfg.CancelOnMove && pos.DistanceTo(startPos) > 0.05)
                {
                    CancelWarmup(sender.PlayerUID);
                    Send(sender, cfg.Messages.MovedSender);
                    Send(receiver, cfg.Messages.MovedReceiver.Replace("{playername}", sender.PlayerName));
                    return;
                }

                if (cfg.CancelOnDamage)
                {
                    float curHp = ent.WatchedAttributes?.GetFloat("health", lastHp) ?? lastHp;
                    if (curHp < lastHp - 0.01f)
                    {
                        CancelWarmup(sender.PlayerUID);
                        Send(sender, cfg.Messages.DamageCancelSender);
                        Send(receiver, cfg.Messages.DamageCancelReceiver.Replace("{playername}", sender.PlayerName));
                        return;
                    }
                    lastHp = curHp;
                }
            }, 100);

            movePollCb[sender.PlayerUID] = pollId;

            long cbId = sapi.World.RegisterCallback(_ =>
            {
                if (canceledWarmup.Remove(sender.PlayerUID)) return;

                if (movePollCb.TryGetValue(sender.PlayerUID, out var pid))
                {
                    sapi.World.UnregisterGameTickListener(pid);
                    movePollCb.Remove(sender.PlayerUID);
                }
                warmupCb.Remove(sender.PlayerUID);

                var snd = PlayerByUid(req.SenderUid);
                var rcv = PlayerByUid(req.ReceiverUid);
                if (snd != null && rcv != null)
                {
                    DoTeleport(snd, rcv, applyCooldown: !bypass);
                }
                activeWarmupBySender.Remove(sender.PlayerUID);

            }, warm * 1000);

            warmupCb[sender.PlayerUID] = cbId;
            return TextCommandResult.Success();
        }
        private TextCommandResult OnTpDeny(TextCommandCallingArgs args)
        {
            var receiver = args.Caller.Player as IServerPlayer;
            if (receiver == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            PurgeExpiredPending(sapi.World.ElapsedMilliseconds);

            if (!pending.TryGetValue(receiver.PlayerUID, out var req))
            {
                Send(receiver, cfg.Messages.NoPending);
                return TextCommandResult.Success();
            }

            pending.Remove(receiver.PlayerUID);

            var sender = PlayerByUid(req.SenderUid);
            if (sender != null)
            {
                Send(sender, cfg.Messages.DeniedSender.Replace("{playername}", receiver.PlayerName));
                Send(receiver, cfg.Messages.DeniedReceiver.Replace("{playername}", sender.PlayerName));
            }
            return TextCommandResult.Success();
        }
        private TextCommandResult OnTpCancel(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            // Cancel active warmup by caller
            if (activeWarmupBySender.TryGetValue(caller.PlayerUID, out var rcvUid))
            {
                CancelWarmup(caller.PlayerUID);

                var rcv = PlayerByUid(rcvUid);
                if (rcv != null)
                {
                    Send(caller, cfg.Messages.CanceledSender);
                    Send(rcv, cfg.Messages.CanceledReceiver.Replace("{playername}", caller.PlayerName));
                }
                return TextCommandResult.Success();
            }

            // Cancel pending request safely 
            string? keyToRemove = null;
            IServerPlayer? rcvToNotify = null;

            foreach (var kv in pending)
            {
                var recvUid = kv.Key;
                var req = kv.Value;
                if (req.SenderUid == caller.PlayerUID)
                {
                    keyToRemove = recvUid;
                    rcvToNotify = PlayerByUid(recvUid);
                    break;
                }
            }
            
            if (keyToRemove != null)
            {
                pending.Remove(keyToRemove);

                if (rcvToNotify != null)
                {
                    Send(caller, cfg.Messages.CanceledSender);
                    Send(rcvToNotify, cfg.Messages.CanceledReceiver.Replace("{playername}", caller.PlayerName));
                }
                return TextCommandResult.Success();
            }

            Send(caller, cfg.Messages.NothingToCancel);
            return TextCommandResult.Success();
        }

        // Internals
        private void DoTeleport(IServerPlayer sender, IServerPlayer receiver, bool applyCooldown)
        {
            try
            {
                var ent = receiver.Entity;
                if (ent == null)
                {
                    Send(sender, cfg.Messages.TeleportFailed);
                    return;
                }

                double tx = ent.ServerPos.X + 0.5;
                double ty = ent.ServerPos.Y;
                double tz = ent.ServerPos.Z + 0.5;

                // Preload destination chunk column
                sapi.WorldManager.LoadChunkColumnPriority(
                    (int)(tx / GlobalConstants.ChunkSize),
                    (int)(tz / GlobalConstants.ChunkSize)
                );

                sender.Entity?.TeleportToDouble(tx, ty, tz);

                // success-only cooldown
                if (applyCooldown && cfg.CooldownSeconds > 0)
                {
                    nextUseTs[sender.PlayerUID] = sapi.World.ElapsedMilliseconds + (long)cfg.CooldownSeconds * 1000;
                }

                Send(sender, cfg.Messages.SuccessSender.Replace("{playername}", receiver.PlayerName));
                Send(receiver, cfg.Messages.SuccessReceiver.Replace("{playername}", sender.PlayerName));
            }
            catch
            {
                Send(sender, cfg.Messages.TeleportFailed);
            }
        }
        private void CancelWarmup(string senderUid)
        {
            if (movePollCb.TryGetValue(senderUid, out var pid))
            {
                sapi.World.UnregisterGameTickListener(pid);
                movePollCb.Remove(senderUid);
            }
            if (warmupCb.TryGetValue(senderUid, out var cb))
            {
                sapi.World.UnregisterCallback(cb);
                warmupCb.Remove(senderUid);
            }
            canceledWarmup.Add(senderUid);
            activeWarmupBySender.Remove(senderUid);
        }
        private void PurgeExpiredPending(long nowMs)
        {
            if (pending.Count == 0) return;
            var toRemove = new List<string>();
            foreach (var kv in pending)
            {
                if (nowMs > kv.Value.ExpireAtMs) toRemove.Add(kv.Key);
            }
            foreach (var k in toRemove) pending.Remove(k);
        }
        private bool PlayerBypasses(IServerPlayer p)
        {
            var role = p.Role?.Code ?? "";

            if (cfg.BypassRoles != null)
            {
                foreach (var r in cfg.BypassRoles)
                    if (role.Equals(r, StringComparison.OrdinalIgnoreCase))
                        return true;
            }

            if (cfg.BypassPlayers != null)
            {
                foreach (var name in cfg.BypassPlayers)
                    if (p.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return true;
            }

            return false;
        }
        private IServerPlayer? PlayerByUid(string uid)
        {
            foreach (var plr in sapi.World.AllOnlinePlayers)
                if (plr is IServerPlayer sp && sp.PlayerUID == uid) return sp;
            return null;
        }
        private IServerPlayer? FindPlayerByName(string name)
        {
            IServerPlayer? exact = null;
            IServerPlayer? starts = null;
            name = name.Trim();

            foreach (var plr in sapi.World.AllOnlinePlayers)
            {
                if (plr is not IServerPlayer sp) continue;
                if (sp.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase)) { exact = sp; break; }
                if (starts == null && sp.PlayerName.StartsWith(name, StringComparison.OrdinalIgnoreCase)) starts = sp;
            }
            return exact ?? starts;
        }
        private void Send(IServerPlayer p, string body)
        {
            var wrapped = $"{cfg.Messages.TPAPrefix}: {body}";
            sapi.SendMessage(p, 0, wrapped, EnumChatType.CommandSuccess);
        }
    }
}