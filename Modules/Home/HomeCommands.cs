using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace EssentialsX.Modules.Home
{
    public class HomeCommands
    {
        private readonly ICoreServerAPI sapi;
        private readonly HomeSettings settings;
        private readonly HomeMessages messages;
        private readonly HomesStore store;
        private readonly Dictionary<string, long> nextUseTs = new();
        private readonly Dictionary<string, long> warmupCbIds = new();
        private readonly Dictionary<string, long> movePollIds = new();

        private const string ModuleName = "Home";

        public HomeCommands(ICoreServerAPI sapi, HomeSettings? homeSettings, HomeMessages? homeMessages)
        {
            this.sapi = sapi;
            try
            {
                settings = homeSettings ?? HomeSettings.LoadOrCreate(sapi);
                messages = homeMessages ?? HomeMessages.LoadOrCreate(sapi);
            }
            catch (Exception ex)
            {
                sapi.World.Logger.Error("[EssentialsX] Failed to init Homes module: {0}", ex);
                settings = new HomeSettings();
                messages = new HomeMessages();
            }

            store = new HomesStore(sapi);
        }

        // Registration
        public void Register()
        {
            sapi.World.Logger.Event("[EssentialsX] Loaded module: {0}", ModuleName);

            if (settings != null && settings.Enabled)
            {
                var nameOpt = new WordArgParser("name", false, null); // (I dont even know what does keep or remove????)
                //var nameOpt = TextCommandParsers.OptionalWord("name");  // use this maybe??

                sapi.ChatCommands.Create("sethome")
                    .RequiresPlayer().RequiresPrivilege("chat")
                    .WithDescription(messages.Descriptions.sethome)
                    .WithArgs(nameOpt)
                    .HandleWith(SetHome);

                sapi.ChatCommands.Create("delhome")
                    .RequiresPlayer().RequiresPrivilege("chat")
                    .WithDescription(messages.Descriptions.delhome)
                    .WithArgs(nameOpt)
                    .HandleWith(DelHome);

                sapi.ChatCommands.Create("home")
                    .RequiresPlayer().RequiresPrivilege("chat")
                    .WithDescription(messages.Descriptions.home)
                    .WithArgs(nameOpt)
                    .HandleWith(Home);

                sapi.ChatCommands.Create("homes")
                    .RequiresPlayer().RequiresPrivilege("chat")
                    .WithDescription(messages.Descriptions.homes)
                    .HandleWith(ListHomes);
            }
            else
            {
                sapi.World.Logger.Event("[EssentialsX] Module disabled by settings: {0}", ModuleName);
            }
        }


        // Handlers 
        private TextCommandResult SetHome(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            string uid = player.PlayerUID;
            string role = player.Role?.Code ?? "suplayer";

            string? name = GetNameArg(args);
            int limit = settings.GetMaxHomesForRole(role);
            var data = store.Load(uid);

            if (limit <= 0) { ChatMsg(player, messages.Home.NoPerm); return TextCommandResult.Success(); }
            if (string.IsNullOrWhiteSpace(name)) { ChatMsg(player, messages.Home.UsageSetHome); return TextCommandResult.Success(); }
            if (!data.Homes.ContainsKey(name) && data.Homes.Count >= limit)
            { ChatMsg(player, messages.Home.LimitReached, ("count", limit.ToString())); return TextCommandResult.Success(); }

            var ep = player.Entity.ServerPos;
            var home = new HomePoint { X = ep.X, Y = ep.Y, Z = ep.Z, Yaw = ep.Yaw, Pitch = ep.Pitch };

            data.Homes[name] = home;
            data.LastUsed = name;
            store.Save(uid, data);

            ChatMsg(player, messages.Home.Saved, ("name", name));
            sapi.World.Logger.Audit("{0} {1} saved home '{2}' at {3:F1}/{4:F1}/{5:F1}",
                messages, player.PlayerName, name, home.X, home.Y, home.Z);
            return TextCommandResult.Success();
        }

        private TextCommandResult DelHome(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            string uid = player.PlayerUID;

            string? name = GetNameArg(args);
            if (string.IsNullOrWhiteSpace(name)) { ChatMsg(player, messages.Home.UsageDelHome); return TextCommandResult.Success(); }

            var data = store.Load(uid);
            if (!data.Homes.Remove(name)) { ChatMsg(player, messages.Home.NoHome); return TextCommandResult.Success(); }

            if (data.LastUsed == name) data.LastUsed = null;
            store.Save(uid, data);

            ChatMsg(player, messages.Home.Deleted, ("name", name));
            return TextCommandResult.Success();
        }

        private TextCommandResult ListHomes(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            string uid = player.PlayerUID;
            var data = store.Load(uid);

            if (data.Homes.Count == 0) { ChatMsg(player, messages.Home.ListNone); return TextCommandResult.Success(); }

            ChatMsg(player, messages.Home.ListSome,
                ("count", data.Homes.Count.ToString()),
                ("list", string.Join(", ", data.Homes.Keys)));
            return TextCommandResult.Success();
        }

        private TextCommandResult Home(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            string uid = player.PlayerUID;

            string? requested = GetNameArg(args);

            // cooldown gate
            long nowMs = sapi.World.ElapsedMilliseconds;
            if (nextUseTs.TryGetValue(uid, out long readyTs) && nowMs < readyTs)
            {
                int left = (int)Math.Ceiling((readyTs - nowMs) / 1000.0);
                ChatMsg(player, messages.Home.Cooldown, ("seconds", left.ToString()));
                return TextCommandResult.Success();
            }

            var data = store.Load(uid);
            string? name = !string.IsNullOrWhiteSpace(requested) ? requested : data.LastUsed;

            if (string.IsNullOrWhiteSpace(name) || !data.Homes.TryGetValue(name, out var target))
            {
                ChatMsg(player, messages.Home.NoHome);
                return TextCommandResult.Success();
            }

            // warmup (with move + damage cancel via simple polling)
            double warmup = Math.Max(0, settings.WarmupSeconds);
            if (warmup <= 0) { Teleport(player, target, name); return TextCommandResult.Success(); }

            // cancel any prior warmup
            if (warmupCbIds.TryGetValue(uid, out long existingCb)) { sapi.Event.UnregisterCallback(existingCb); warmupCbIds.Remove(uid); }
            if (movePollIds.TryGetValue(uid, out long existingPoll)) { sapi.Event.UnregisterGameTickListener(existingPoll); movePollIds.Remove(uid); }

            var startPos = player.Entity.ServerPos.XYZ.Clone();
            float startHp = GetHealth(player.Entity);
            ChatMsg(player, messages.Home.Teleporting, ("name", name), ("seconds", settings.WarmupSeconds.ToString()));

            long pollId = sapi.Event.RegisterGameTickListener((dt) =>
            {
                var nowPos = player.Entity.ServerPos.XYZ;
                if (nowPos.DistanceTo(startPos) > 0.05f)
                {
                    CancelWarmup(player, messages.Home.CancelMove);
                    return;
                }
                if (GetHealth(player.Entity) < startHp - 0.001f)
                {
                    CancelWarmup(player, messages.Home.CancelDamage);
                    return;
                }
            }, 50);
            movePollIds[uid] = pollId;

            long cbId = sapi.Event.RegisterCallback((ms) =>
            {
                if (movePollIds.TryGetValue(uid, out long pid)) sapi.Event.UnregisterGameTickListener(pid);
                movePollIds.Remove(uid);
                warmupCbIds.Remove(uid);

                var nowPos2 = player.Entity.ServerPos.XYZ;
                if (nowPos2.DistanceTo(startPos) > 0.05f) { ChatMsg(player, messages.Home.CancelMove); return; }
                if (GetHealth(player.Entity) < startHp - 0.001f) { ChatMsg(player, messages.Home.CancelDamage); return; }

                Teleport(player, target, name);
            }, (int)(settings.WarmupSeconds * 1000));
            warmupCbIds[uid] = cbId;

            return TextCommandResult.Success();
        }

        // Helpers
        private static float GetHealth(Entity entity)
        {
            return entity.WatchedAttributes.GetFloat("health", 20f);
        }

        private void Teleport(IServerPlayer player, HomePoint target, string name)
        {
            nextUseTs[player.PlayerUID] = sapi.World.ElapsedMilliseconds + (long)(settings.CooldownSeconds * 1000);

            var tp = new EntityPos(target.X, target.Y, target.Z) { Yaw = target.Yaw, Pitch = target.Pitch };
            player.Entity.TeleportTo(tp);

            ChatMsg(player, messages.Home.Teleported, ("name", name));
            sapi.World.Logger.Audit("{0} {1} teleported to home '{2}' at {3:F1}/{4:F1}/{5:F1}",
                messages, player.PlayerName, name, target.X, target.Y, target.Z);
        }

        private void ChatMsg(IServerPlayer player, string template, params (string key, string value)[] vars)
        {
            string msg = template ?? "";
            if (vars != null)
            {
                foreach (var (key, value) in vars)
                {
                    if (key != null) msg = msg.Replace("{" + key + "}", Esc(value));
                }
            }
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                $"{messages.HomeHeader}\n{messages.HomePrefix}: {msg}\n{messages.HomeFooter}",
                EnumChatType.Notification);
        }
        private static string Esc(string? s) =>
            (s ?? "").Replace("<", "&lt;").Replace(">", "&gt;");


        private void CancelWarmup(IServerPlayer player, string reasonTemplate)
        {
            string uid = player.PlayerUID;

            if (movePollIds.TryGetValue(uid, out long pid))
            {
                sapi.Event.UnregisterGameTickListener(pid);
                movePollIds.Remove(uid);
            }
            if (warmupCbIds.TryGetValue(uid, out long cbid))
            {
                sapi.Event.UnregisterCallback(cbid);
                warmupCbIds.Remove(uid);
            }

            ChatMsg(player, reasonTemplate);
        }

        // Robust arg getter: supports quoted names and single word
        private static string? GetNameArg(TextCommandCallingArgs args)
        {
            try
            {
                var v = args[0];
                if (v != null)
                {
                    var s = v as string ?? v.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            catch { /* ignore */ }

            try
            {
                var w = args.RawArgs.PopWord();
                return string.IsNullOrWhiteSpace(w) ? null : w;
            }
            catch { return null; }
        }
    }
}