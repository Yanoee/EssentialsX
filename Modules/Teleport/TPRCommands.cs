using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Teleport
{
    public class TPRCommands
    {
        private readonly ICoreServerAPI sapi;
        private TprConfig cfg = null!;
        private readonly Dictionary<string, long> nextUseTs = [];              // uid -> unix ms
        private readonly Dictionary<string, PendingRequest> pending = [];      // receiverUid -> request
        private readonly Dictionary<string, string> activeWarmupBySender = []; // senderUid -> receiverUid
        private readonly Dictionary<string, long> warmupCb = [];               // senderUid -> cb id
        private readonly Dictionary<string, long> movePollCb = [];             // senderUid -> tick id
        private readonly HashSet<string> canceledWarmup = [];                  // senderUid set

        private struct PendingRequest
        {
            public string SenderUid;
            public string ReceiverUid;
            public long ExpireAtMs;
        }

        public TPRCommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                cfg = TprConfig.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                cfg = new TprConfig();
                sapi.World.Logger.Error("[EssentialsX] Failed to load TPR module: {0}", ex);
            }
        }

        public void Register()
        {
            if (cfg == null)
            {
                sapi.World.Logger.Error("[EssentialsX] TPR config not initialized.");
                return;
            }
            if (!cfg.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: TPR");
                return;
            }

            sapi.ChatCommands
                .Create("tpr")
                .WithDescription(cfg.Messages.DescTpr)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .WithArgs(new StringArgParser("player", false))
                .HandleWith(OnTpr);

            sapi.ChatCommands
                .Create("tpraccept")
                .WithDescription(cfg.Messages.DescTpAccept)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpAccept);

            sapi.ChatCommands
                .Create("tprdeny")
                .WithDescription(cfg.Messages.DescTpDeny)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpDeny);

            sapi.ChatCommands
                .Create("tprcancel")
                .WithDescription(cfg.Messages.DescTpCancel)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpCancel);

            sapi.World.Logger.Event("[EssentialsX] Loaded module: TPR");

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

        //Commands

        private TextCommandResult OnTpr(TextCommandCallingArgs args)
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
                Send(caller, cfg.Messages.UsageTpr);
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
                if (canceledWarmup.Remove(req.SenderUid))
                {
                    activeWarmupBySender.Remove(req.SenderUid);
                    return;
                }

                CancelWarmup(req.SenderUid);
                DoTeleport(sender, receiver, applyCooldown: !bypass);

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

            var sender = PlayerByUid(req.SenderUid);
            pending.Remove(receiver.PlayerUID);

            if (sender != null)
            {
                Send(sender, cfg.Messages.DeniedSender.Replace("{playername}", receiver.PlayerName));
                Send(receiver, cfg.Messages.DeniedReceiver.Replace("{playername}", sender.PlayerName));
            }
            else
            {
                Send(receiver, cfg.Messages.NoPending);
            }

            return TextCommandResult.Success();
        }

        private TextCommandResult OnTpCancel(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error(cfg.Messages.PlayerOnly);

            PurgeExpiredPending(sapi.World.ElapsedMilliseconds);

            string? targetUid = null;
            foreach (var kv in pending)
            {
                if (kv.Value.SenderUid == caller.PlayerUID)
                {
                    targetUid = kv.Key;
                    break;
                }
            }

            if (targetUid == null && !activeWarmupBySender.ContainsKey(caller.PlayerUID))
            {
                Send(caller, cfg.Messages.NothingToCancel);
                return TextCommandResult.Success();
            }

            if (targetUid != null)
            {
                var req = pending[targetUid];
                pending.Remove(targetUid);

                var receiver = PlayerByUid(req.ReceiverUid);
                if (receiver != null)
                {
                    Send(receiver, cfg.Messages.CanceledReceiver.Replace("{playername}", caller.PlayerName));
                }
            }
            CancelWarmup(caller.PlayerUID);
            canceledWarmup.Add(caller.PlayerUID);
            activeWarmupBySender.Remove(caller.PlayerUID);

            Send(caller, cfg.Messages.CanceledSender);
            return TextCommandResult.Success();
        }

        // Internals & Handlers

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

        private void CancelWarmup(string senderUid)
        {
            if (movePollCb.TryGetValue(senderUid, out var pid))
            {
                sapi.World.UnregisterGameTickListener(pid);
                movePollCb.Remove(senderUid);
            }
            if (warmupCb.TryGetValue(senderUid, out var cbid))
            {
                sapi.World.UnregisterCallback(cbid);
                warmupCb.Remove(senderUid);
            }
        }

        private void DoTeleport(IServerPlayer sender, IServerPlayer receiver, bool applyCooldown)
        {
            var ent = receiver.Entity;
            var target = sender.Entity;
            if (ent == null || target == null)
            {
                Send(sender, cfg.Messages.TeleportFailed);
                Send(receiver, cfg.Messages.TeleportFailed);
                return;
            }

            try
            {
                var bp = target.ServerPos.AsBlockPos;
                sapi.WorldManager.LoadChunkColumnPriority(bp.X / GlobalConstants.ChunkSize, bp.Z / GlobalConstants.ChunkSize);

                double tx = target.ServerPos.X;
                double ty = target.ServerPos.Y;
                double tz = target.ServerPos.Z;

                ent.TeleportToDouble(tx, ty, tz);

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
                Send(receiver, cfg.Messages.TeleportFailed);
            }
        }

        private bool PlayerBypasses(IServerPlayer p)
        {
            if (cfg.BypassPlayers != null && cfg.BypassPlayers.Contains(p.PlayerName)) return true;
            if (cfg.BypassRoles != null)
            {
                foreach (var role in cfg.BypassRoles)
                    if (p.HasPrivilege(role)) return true;
            }
            return false;
        }

        private IServerPlayer? PlayerByUid(string uid)
        {
            return sapi.Server.Players.FirstOrDefault(sp => sp.PlayerUID == uid);
        }

        private IServerPlayer? FindPlayerByName(string namePart)
        {
            foreach (var sp in sapi.Server.Players)
                if (sp.PlayerName.Equals(namePart, StringComparison.OrdinalIgnoreCase))
                    return sp;

            foreach (var sp in sapi.Server.Players)
                if (sp.PlayerName.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                    return sp;

            return null;
        }

        private void Send(IServerPlayer p, string body)
        {
            var wrapped = $"{cfg.Messages.Prefix}: {body}";
            sapi.SendMessage(p, 0, wrapped, EnumChatType.CommandSuccess);
        }
    }
}
