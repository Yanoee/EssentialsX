using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Teleport
{
    public class TPRCommands
    {
        private readonly ICoreServerAPI sapi;
        private readonly TPRSettings settings;
        private readonly TPRMessages msgs;

        private readonly Dictionary<string, long> nextUseTs = new();              // uid -> unix ms
        private readonly Dictionary<string, PendingRequest> pending = new();      // receiverUid -> request
        private readonly Dictionary<string, long> warmupCb = new();               // senderUid -> cb id
        private readonly Dictionary<string, long> movePollCb = new();             // senderUid -> tick id
        private readonly HashSet<string> canceledWarmup = new();                  // senderUid set
        private readonly Dictionary<string, string> activeWarmupBySender = new(); // senderUid -> receiverUid

        private const string ModuleName = "TPR";

        private class PendingRequest
        {
            public string SenderUid { get; set; } = "";
            public string ReceiverUid { get; set; } = "";
            public long ExpireAtMs { get; set; }
        }

        public TPRCommands(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            try
            {
                settings = TPRSettings.LoadOrCreate(sapi);
                msgs = TPRMessages.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module.", ex);
                settings = new TPRSettings();
                msgs = new TPRMessages();
            }
        }

        // Registration
        public void Register()
        {
            sapi.World.Logger.Event("[EssentialsX] Loaded module: {0}", ModuleName);

            if (settings != null && !settings.Enabled)
            {
                sapi.World.Logger.Event("[EssentialsX] {0} disabled via settings.", ModuleName);
                return;
            }

            var parsers = sapi.ChatCommands.Parsers;
            var playerOpt = parsers.OptionalWord("player");

            sapi.ChatCommands.Create("tpr")
                .WithDescription(msgs.TprUsage)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .WithArgs(playerOpt) // optional target
                .HandleWith(OnTpr);

            sapi.ChatCommands.Create("tpraccept")
                .WithDescription("Accept a pending TPR request")
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTprAccept);

            sapi.ChatCommands.Create("tprdeny")
                .WithDescription("Deny a pending TPR request")
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTprDeny);

            sapi.ChatCommands.Create("tprcancel")
                .WithDescription(msgs.TprCancelUsage)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTprCancel);
        }

        private TextCommandResult OnTpr(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("Player only.");

            // Cooldown gate: only enforced after success, so we just display if still active
            long readyTs;
            var nowMs = sapi.World.ElapsedMilliseconds;
            if (!PlayerBypasses(caller) && nextUseTs.TryGetValue(caller.PlayerUID, out readyTs) && nowMs < readyTs)
            {
                var left = (int)Math.Ceiling((readyTs - nowMs) / 1000.0);
                SendBlock(caller, msgs.CooldownActive.Replace("{seconds}", left.ToString()));
                return TextCommandResult.Success();
            }

            string? targetName = null;
            try { targetName = args[0] as string; } catch { /* optional */ }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                SendPrefix(caller, msgs.TprUsage);
                return TextCommandResult.Success();
            }

            var target = FindPlayerByName(targetName) ?? PlayerByUid(targetName);
            if (target == null)
            {
                SendBlock(caller, msgs.TprNoTarget);
                return TextCommandResult.Success();
            }

            if (target.PlayerUID == caller.PlayerUID)
            {
                SendBlock(caller, msgs.TprSelf);
                return TextCommandResult.Success();
            }

            // Block duplicate pending from the same sender (to anyone)
            foreach (var kv in pending)
            {
                var req = kv.Value;
                if (req.SenderUid == caller.PlayerUID && nowMs < req.ExpireAtMs)
                {
                    SendBlock(caller, msgs.TprAlreadyPendingSender);
                    return TextCommandResult.Success();
                }
            }

            // Create/replace pending request for receiver (latest wins)
            pending[target.PlayerUID] = new PendingRequest
            {
                SenderUid = caller.PlayerUID,
                ReceiverUid = target.PlayerUID,
                ExpireAtMs = nowMs + settings.RequestExpireSeconds * 1000
            };

            // Notify sender
            SendBlock(caller, msgs.TprSentSender.Replace("{playername}", target.PlayerName));

            // Notify receiver (normal + clickable VTML)
            SendBlock(target, msgs.TprSentReceiver.Replace("{playername}", caller.PlayerName));
            SendBlock(target, msgs.TprClickableReceiver.Replace("{playername}", caller.PlayerName));

            return TextCommandResult.Success();
        }

        private TextCommandResult OnTprAccept(TextCommandCallingArgs args)
        {
            var receiver = args.Caller.Player as IServerPlayer;
            if (receiver == null) return TextCommandResult.Error("Player only.");

            if (!pending.TryGetValue(receiver.PlayerUID, out var req))
            {
                SendBlock(receiver, msgs.TprNoPending);
                return TextCommandResult.Success();
            }

            // Expiry
            var now = sapi.World.ElapsedMilliseconds;
            if (now > req.ExpireAtMs)
            {
                pending.Remove(receiver.PlayerUID);
                SendBlock(receiver, msgs.TprNoPending);
                return TextCommandResult.Success();
            }

            var sender = PlayerByUid(req.SenderUid);
            if (sender == null || sender.ConnectionState != EnumClientState.Playing)
            {
                pending.Remove(receiver.PlayerUID);
                SendBlock(receiver, msgs.TprNoPending);
                return TextCommandResult.Success();
            }

            pending.Remove(receiver.PlayerUID);

            bool bypass = PlayerBypasses(sender);
            int warm = bypass ? 0 : settings.WarmupSeconds;

            // Inform both
            SendBlock(sender, msgs.TprBeginSender.Replace("{playername}", receiver.PlayerName).Replace("{warmup}", warm.ToString()));
            SendBlock(receiver, msgs.TprBeginReceiver.Replace("{playername}", sender.PlayerName).Replace("{warmup}", warm.ToString()));

            if (warm <= 0)
            {
                DoTeleport(sender, receiver);
                return TextCommandResult.Success();
            }

            // Movement cancel poll (watch sender move)
            var startPos = sender.Entity.ServerPos.XYZ.Clone();
            long pollId = 0;
            pollId = sapi.World.RegisterGameTickListener(dt =>
            {
                var pos = sender.Entity.ServerPos.XYZ;
                if (pos.DistanceTo(startPos) > 0.05)
                {
                    if (movePollCb.TryGetValue(sender.PlayerUID, out var pid))
                    {
                        sapi.World.UnregisterGameTickListener(pid);
                        movePollCb.Remove(sender.PlayerUID);
                    }

                    SendBlock(sender, msgs.TprCanceledSender);
                    SendBlock(receiver, msgs.TprCanceledReceiver.Replace("{playername}", sender.PlayerName));

                    if (warmupCb.TryGetValue(sender.PlayerUID, out var warmCbId))
                    {
                        sapi.World.UnregisterCallback(warmCbId);
                        warmupCb.Remove(sender.PlayerUID);
                    }
                    canceledWarmup.Add(sender.PlayerUID);
                    activeWarmupBySender.Remove(sender.PlayerUID);
                }
            }, 100);

            movePollCb[sender.PlayerUID] = pollId;

            // Warmup timer
            long cbId = sapi.World.RegisterCallback(_ =>
            {
                if (canceledWarmup.Remove(req.SenderUid))
                {
                    activeWarmupBySender.Remove(req.SenderUid);
                    return;
                }

                if (movePollCb.TryGetValue(sender.PlayerUID, out var pid))
                {
                    sapi.World.UnregisterGameTickListener(pid);
                    movePollCb.Remove(sender.PlayerUID);
                }

                warmupCb.Remove(sender.PlayerUID);

                var snd = PlayerByUid(req.SenderUid);
                var rcv = PlayerByUid(req.ReceiverUid);
                if (snd == null || rcv == null)
                {
                    activeWarmupBySender.Remove(req.SenderUid);
                    return;
                }

                DoTeleport(snd, rcv);
                activeWarmupBySender.Remove(req.SenderUid);
            }, warm * 1000);

            warmupCb[sender.PlayerUID] = cbId;
            activeWarmupBySender[sender.PlayerUID] = receiver.PlayerUID;

            return TextCommandResult.Success();
        }

        private TextCommandResult OnTprDeny(TextCommandCallingArgs args)
        {
            var receiver = args.Caller.Player as IServerPlayer;
            if (receiver == null) return TextCommandResult.Error("Player only.");

            if (!pending.TryGetValue(receiver.PlayerUID, out var req))
            {
                SendBlock(receiver, msgs.TprNoPending);
                return TextCommandResult.Success();
            }

            pending.Remove(receiver.PlayerUID);

            var sender = PlayerByUid(req.SenderUid);
            if (sender != null)
            {
                SendBlock(sender, msgs.TprCanceledSender);
            }
            SendBlock(receiver, msgs.TprCanceledReceiver.Replace("{playername}", sender?.PlayerName ?? "unknown"));

            return TextCommandResult.Success();
        }

        private TextCommandResult OnTprCancel(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("Player only.");
            string uid = caller.PlayerUID;
            bool didCancel = false;

            // Cancel in-progress warmup
            if (warmupCb.TryGetValue(uid, out var warmId))
            {
                sapi.World.UnregisterCallback(warmId);
                warmupCb.Remove(uid);

                if (movePollCb.TryGetValue(uid, out var pollId))
                {
                    sapi.World.UnregisterGameTickListener(pollId);
                    movePollCb.Remove(uid);
                }

                canceledWarmup.Add(uid);

                if (activeWarmupBySender.TryGetValue(uid, out var recvUid))
                {
                    var recv = PlayerByUid(recvUid);
                    if (recv != null)
                        SendBlock(recv, msgs.TprCanceledReceiver.Replace("{playername}", caller.PlayerName));
                }
                activeWarmupBySender.Remove(uid);

                SendBlock(caller, msgs.TprCanceledSender);
                didCancel = true;
            }

            // Cancel a pending (not yet accepted) request
            var now = sapi.World.ElapsedMilliseconds;
            string? hitKey = null;

            foreach (var kv in pending)
            {
                var req = kv.Value;
                if (req.SenderUid == uid && now < req.ExpireAtMs)
                {
                    hitKey = kv.Key; // receiver UID
                    break;
                }
            }

            if (hitKey != null)
            {
                pending.Remove(hitKey);

                var recv = PlayerByUid(hitKey);
                if (recv != null)
                    SendBlock(recv, msgs.TprCanceledReceiver.Replace("{playername}", caller.PlayerName));

                SendBlock(caller, msgs.TprCanceledSender);
                didCancel = true;
            }

            if (!didCancel)
            {
                SendBlock(caller, msgs.TprNothingToCancel);
            }

            return TextCommandResult.Success();
        }

        private void DoTeleport(IServerPlayer sender, IServerPlayer receiver)
        {
            // Apply cooldown ONLY on success
            if (!PlayerBypasses(sender))
            {
                var now = sapi.World.ElapsedMilliseconds;
                nextUseTs[sender.PlayerUID] = now + settings.CooldownSeconds * 1000;
            }

            // For TPR (pull): receiver moves to sender
            var to = sender.Entity.ServerPos.AsBlockPos;
            receiver.Entity.TeleportTo(to.X, to.Y, to.Z);

            SendBlock(sender, msgs.TprSuccessSender.Replace("{playername}", receiver.PlayerName));
            SendBlock(receiver, msgs.TprSuccessReceiver.Replace("{playername}", sender.PlayerName));
        }

        private bool PlayerBypasses(IServerPlayer? plr)
        {
            try
            {
                var role = plr?.Role?.Code ?? "suplayer";
                foreach (var r in settings.BypassRoles)
                {
                    if (string.Equals(r, role, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            return false;
        }

        private IServerPlayer? PlayerByUid(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p.PlayerUID == uid) return p as IServerPlayer;
            }
            return null;
        }

        private IServerPlayer? FindPlayerByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            // Exact match
            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase)) return p as IServerPlayer;
            }
            // Startswith
            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p.PlayerName.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return p as IServerPlayer;
            }
            return null;
        }

        private void SendPrefix(IServerPlayer plr, string msg)
        {
            plr.SendMessage(GlobalConstants.GeneralChatGroup, $"{msg}", EnumChatType.Notification);
        }

        private void SendBlock(IServerPlayer plr, string body)
        {
            plr.SendMessage(GlobalConstants.GeneralChatGroup, $"{msgs.TprHeader}\n{body}\n{msgs.TprFooter}", EnumChatType.Notification);
        }
    }
}
