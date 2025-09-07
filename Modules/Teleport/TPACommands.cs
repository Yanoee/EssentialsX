using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Teleport
{
    public class TPACommands
    {
        private readonly ICoreServerAPI sapi;
        private readonly TPASettings settings;
        private readonly TPAMessages msgs;

        private readonly Dictionary<string, long> nextUseTs = new();              // uid -> unix ms
        private readonly Dictionary<string, PendingRequest> pending = new();      // receiverUid -> request
        private readonly Dictionary<string, long> warmupCb = new();               // senderUid -> cb id
        private readonly Dictionary<string, long> movePollCb = new();             // senderUid -> cb id
        private readonly Dictionary<string, string> activeWarmupBySender = new(); // senderUid -> receiverUid for an accepted request during warmup
        private readonly HashSet<string> canceledWarmup = new();

        private const string ModuleName = "TPA";

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
                settings = TPASettings.LoadOrCreate(sapi);
                msgs = TPAMessages.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to load {0} module.", ex);
                // Fallbacks so server keeps running
                settings = new TPASettings();
                msgs = new TPAMessages();
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
            var playerOpt = parsers.OptionalWord("player");  // <-- I am suffering

            sapi.ChatCommands.Create("tpa")
                .WithDescription(msgs.TpaUsage)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .WithArgs(playerOpt)  // IDK why I did this
                .HandleWith(OnTpa);

            sapi.ChatCommands.Create("tpaccept")
                .WithDescription("Accept a pending TPA request")
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpAccept);

            sapi.ChatCommands.Create("tpdeny")
                .WithDescription("Deny a pending TPA request")
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpDeny);

            sapi.ChatCommands.Create("tpcancel")
                .WithDescription(msgs.TpaCancelUsage)
                .RequiresPlayer()
                .RequiresPrivilege("chat")
                .HandleWith(OnTpCancel);

        }

        private TextCommandResult OnTpa(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("Player only.");
            
            // --- Cooldown gate (applies only after a successful TP) ---
            var nowMs = sapi.World.ElapsedMilliseconds;
            if (!PlayerBypasses(caller) && nextUseTs.TryGetValue(caller.PlayerUID, out var ready) && nowMs < ready)
            {
                var left = (int)Math.Ceiling((ready - nowMs) / 1000.0);
                SendBlock(caller, msgs.CooldownActive.Replace("{seconds}", left.ToString()));
                return TextCommandResult.Success();
            }
            string? targetName = null;
            try
            {
                targetName = args[0] as string;
            }
            catch (Exception)
            {
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                SendPrefix(caller, msgs.TpaUsage);
                return TextCommandResult.Success();
            }

            var target = FindPlayerByName(targetName);
            if (target == null)
            {
                SendBlock(caller, msgs.TpaNotFound);
                return TextCommandResult.Success();
            }

            if (target.PlayerUID == caller.PlayerUID)
            {
                SendBlock(caller, msgs.TpaSelf);
                return TextCommandResult.Success();
            }
            // --- Block duplicate pending from the same sender (to anyone) ---
            {
                var nowCheck = sapi.World.ElapsedMilliseconds;
                foreach (var kv in pending)
                {
                    var req = kv.Value;
                    if (req.SenderUid == caller.PlayerUID && nowCheck < req.ExpireAtMs)
                    {
                        SendBlock(caller, msgs.TpaAlreadyPendingSender);
                        return TextCommandResult.Success();
                    }
                }
            }

            // Create/replace pending request for receiver (latest wins)
            var now = sapi.World.ElapsedMilliseconds;
            pending[target.PlayerUID] = new PendingRequest
            {
                SenderUid = caller.PlayerUID,
                ReceiverUid = target.PlayerUID,
                ExpireAtMs = now + settings.RequestExpireSeconds * 1000
            };

            // Notify sender
            SendBlock(caller, msgs.TpaSentSender.Replace("{playername}", target.PlayerName));

            // Notify receiver (normal + clickable VTML)
            SendBlock(target, msgs.TpaSentReceiver.Replace("{playername}", caller.PlayerName));
            SendBlock(target, msgs.TpaClickableReceiver.Replace("{playername}", caller.PlayerName));

            return TextCommandResult.Success();
        }

        private TextCommandResult OnTpAccept(TextCommandCallingArgs args)
        {
            var receiver = args.Caller.Player as IServerPlayer;
            if (receiver == null) return TextCommandResult.Error("Player only.");
            if (!pending.TryGetValue(receiver.PlayerUID, out var req))
            {
                SendBlock(receiver, msgs.TpaNoPending);
                return TextCommandResult.Success();
            }

            // Expiry
            var now = sapi.World.ElapsedMilliseconds;
            if (now > req.ExpireAtMs)
            {
                pending.Remove(receiver.PlayerUID);
                SendBlock(receiver, msgs.TpaNoPending);
                return TextCommandResult.Success();
            }

            var sender = PlayerByUid(req.SenderUid);
            if (sender == null || sender.ConnectionState != EnumClientState.Playing)

            {
                pending.Remove(receiver.PlayerUID);
                SendBlock(receiver, msgs.TpaNoPending);
                return TextCommandResult.Success();
            }

            pending.Remove(receiver.PlayerUID);

            bool bypass = PlayerBypasses(sender);
            int warm = bypass ? 0 : settings.WarmupSeconds;

            // Inform both
            SendBlock(sender, msgs.TpaBeginSender.Replace("{playername}", receiver.PlayerName).Replace("{warmup}", warm.ToString()));
            SendBlock(receiver, msgs.TpaBeginReceiver.Replace("{playername}", sender.PlayerName).Replace("{warmup}", warm.ToString()));

            if (warm <= 0)
            {
                DoTeleport(sender, receiver);
                return TextCommandResult.Success();
            }

            // /tpcancel can kill it and notify receiver
            activeWarmupBySender[sender.PlayerUID] = receiver.PlayerUID;

            // Movement cancel poll
            var startPos = sender.Entity.ServerPos.XYZ.Clone();
            long pollId = 0;
            pollId = sapi.World.RegisterGameTickListener(dt =>
            {
                var pos = sender.Entity.ServerPos.XYZ;
                if (pos.DistanceTo(startPos) > 0.05)
                {
                    if (movePollCb.TryGetValue(sender.PlayerUID, out var pollId))
                    {
                        sapi.World.UnregisterGameTickListener(pollId);
                        movePollCb.Remove(sender.PlayerUID);
                    }
                    SendBlock(sender, msgs.TpaCanceledSender);
                    SendBlock(receiver, msgs.TpaCanceledReceiver.Replace("{playername}", sender.PlayerName));
                    
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
                if (canceledWarmup.Remove(req.SenderUid)) return;

                if (movePollCb.TryGetValue(sender.PlayerUID, out var pollId))
                {
                    sapi.World.UnregisterGameTickListener(pollId);
                    movePollCb.Remove(sender.PlayerUID);
                }

                warmupCb.Remove(sender.PlayerUID);

                var snd = PlayerByUid(req.SenderUid);
                var rcv = PlayerByUid(req.ReceiverUid);
                if (snd != null && rcv != null)
                {
                    DoTeleport(snd, rcv);
                    activeWarmupBySender.Remove(req.SenderUid);

                }
            }, warm * 1000);
            warmupCb[sender.PlayerUID] = cbId;

            return TextCommandResult.Success();

        }

        private TextCommandResult OnTpDeny(TextCommandCallingArgs args)
        {
            var receiver = args.Caller.Player as IServerPlayer;
            if (receiver == null) return TextCommandResult.Error("Player only.");

            if (!pending.TryGetValue(receiver.PlayerUID, out var req))
            {
                SendBlock(receiver, msgs.TpaNoPending);
                return TextCommandResult.Success();
            }

            pending.Remove(receiver.PlayerUID);

            var sender = PlayerByUid(req.SenderUid);
            if (sender != null)
            {
                SendBlock(sender, msgs.TpaDeniedSender.Replace("{playername}", receiver.PlayerName));
            }
            SendBlock(receiver, msgs.TpaDeniedReceiver.Replace("{playername}", sender?.PlayerName ?? "unknown"));
            return TextCommandResult.Success();
        }

        private TextCommandResult OnTpCancel(TextCommandCallingArgs args)
        {
            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("Player only.");
            string uid = caller.PlayerUID;
            bool didCancel = false;

            // ---- Cancel an in-progress warmup (accepted request) ----
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
                        SendBlock(recv, msgs.TpaCanceledReceiver.Replace("{playername}", caller.PlayerName));
                }
                activeWarmupBySender.Remove(uid);

                SendBlock(caller, msgs.TpaCanceledSender);
                didCancel = true;
            }

            // ---- Cancel a pending (not yet accepted) request you sent ----
            var now = sapi.World.ElapsedMilliseconds;
            string? hitKey = null;
            PendingRequest? hitReq = null;

            foreach (var kv in pending)
            {
                var req = kv.Value;
                if (req.SenderUid == uid && now < req.ExpireAtMs)
                {
                    hitKey = kv.Key;      // key is receiver UID
                    hitReq = req;
                    break;
                }
            }

            if (hitKey != null)
            {
                pending.Remove(hitKey);

                // hitKey IS the receiver UID (the dictionary key)
                var recv = PlayerByUid(hitKey);
                if (recv != null)
                    SendBlock(recv, msgs.TpaCanceledReceiver.Replace("{playername}", caller.PlayerName));

                SendBlock(caller, msgs.TpaCanceledSender);
                didCancel = true;
            }

            if (!didCancel)
            {
                SendBlock(caller, msgs.TpaNothingToCancel);
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

            var to = receiver.Entity.ServerPos.AsBlockPos;
            sender.Entity.TeleportTo(to.X, to.Y, to.Z);

            SendBlock(sender, msgs.TpaSuccessSender.Replace("{playername}", receiver.PlayerName));
            SendBlock(receiver, msgs.TpaSuccessReceiver.Replace("{playername}", sender.PlayerName));
        }

        private bool PlayerBypasses(IServerPlayer plr)
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
            foreach (var p in sapi.Server.Players)
                if (p.PlayerUID == uid) return p;
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
            // StartsWith fallback
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
            plr.SendMessage(GlobalConstants.GeneralChatGroup, $"{msgs.TpaHeader}\n{body}\n{msgs.TpaFooter}", EnumChatType.Notification);
        }
    }
}