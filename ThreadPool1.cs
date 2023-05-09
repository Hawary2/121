using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using GameServer.Game.MsgServer;
using System.Threading.Generic;
using GameServer.Game.MsgFloorItem;
using GameServer.Game.MsgServer.AttackHandler;
using GameServer.Game.MsgTournaments;
using GameServer.Client;
using GameServer.Role.Instance;
//using GameServer.Threading;

namespace GameServer
{
    public static class ThreadPool
    {
        public static int Online
        {
            get
            {
                int current = Pool.GamePoll.Count;
                return current;
            }
        }
        public static int MaxOnline;
       
        //private static DateTime ServerStamp = DateTime.Now;
        //private static DateTime ArenaStamp = DateTime.Now;

        //public static TimerRule<GameClient> MainCallBack;
        public static TimerRule<ServerSockets.SecuritySocket> ConnectionReceive, ConnectionSend, ConnectionReview;
        public static StaticPool GenericThreadPool;
        public static TimerRule<GameClient> Characters, AutoAttack, Monsters, Buffers, Floor;
        public static StaticPool ReceivePool, SendPool;
        public static void Create()
        {
           
            GenericThreadPool = new StaticPool(32).Run();
            ReceivePool = new StaticPool(32).Run();
            SendPool = new StaticPool(32).Run();

            Floor = new TimerRule<GameClient>(FloorCallback, 300, ThreadPriority.BelowNormal);
            Characters = new TimerRule<GameClient>(CharactersCallback, 1000);
            AutoAttack = new TimerRule<GameClient>(AutoAttackCallback, 1000, ThreadPriority.BelowNormal);
            Monsters = new TimerRule<GameClient>(MonstersCallback, 500, ThreadPriority.BelowNormal);
            Buffers = new TimerRule<GameClient>(BuffersCallback, 1000);
            ConnectionReceive = new TimerRule<ServerSockets.SecuritySocket>(connectionReceive, 1, ThreadPriority.Highest);
            // ConnectionSend = new TimerRule<ServerSockets.SecuritySocket>(connectionSend, 1);
            ConnectionSend = new TimerRule<ServerSockets.SecuritySocket>(connectionSend, 1, ThreadPriority.Highest);
            ConnectionReview = new TimerRule<ServerSockets.SecuritySocket>(_ConnectionReview, 60000, ThreadPriority.Lowest);

            Subscribe(ServerFunctions, 60000, ThreadPriority.BelowNormal);
            Subscribe(WorldTournaments, 1000);
            Subscribe(ArenaFunctions, 1000, ThreadPriority.BelowNormal);
            Subscribe(TeamArenaFunctions, 1000, ThreadPriority.BelowNormal);
        }
       
        
        public static bool Register(GameClient client)
        {
            if (client.TimerSubscriptions == null)
            {
                client.TimerSubscriptions = new IDisposable[]
                {
                   Subscribe(Characters, client),
                   Subscribe(AutoAttack, client),
                   Subscribe(Buffers, client),
                   Subscribe(Monsters, client),
                   Subscribe(Floor, client),
                   //Subscribe(MainCallBack, client)
                };
                return true;
            }
            return false;
        }
        public static void Unregister(GameClient client)
        {
            lock (client.TimerSyncRoot)
            {
                if (client.TimerSubscriptions != null)
                {
                    foreach (var Now in client.TimerSubscriptions)
                        Now.Dispose();
                    client.TimerSubscriptions = null;
                }
            }
        }
       
        private static void MonstersCallback(GameClient client, int time)
        {
            if (client == null || client.Player == null || client.Player.View == null) return;
            client.Player.View.CheckUpMonsters(client, time);
        }
        private static unsafe void CharactersCallback(Client.GameClient client, int time)
        {
            if (Program.ExitRequested || !client.FullLoading || client.Fake || !client.Player.CompleteLogin)
                return;
            try
            {
                DateTime Now = DateTime.Now;
                #region Atribute Points
                using (var rec = new ServerSockets.RecycledPacket())
                {
                    var stream = rec.GetStream();
                    if (client.Player.Level == 140)
                    {
                        if (client.Player.Agility + client.Player.Strength + client.Player.Vitality + client.Player.Spirit + client.Player.Atributes >= 903)
                        {
                            client.Player.Vitality = 1; client.Player.Agility = client.Player.Strength = client.Player.Spirit = 0; client.Player.Atributes = 901;
                            client.Player.SendUpdate(stream, client.Player.Strength, Game.MsgServer.MsgUpdate.DataType.Strength);
                            client.Player.SendUpdate(stream, client.Player.Agility, Game.MsgServer.MsgUpdate.DataType.Agility);
                            client.Player.SendUpdate(stream, client.Player.Spirit, Game.MsgServer.MsgUpdate.DataType.Spirit);
                            client.Player.SendUpdate(stream, client.Player.Vitality, Game.MsgServer.MsgUpdate.DataType.Vitality);
                            client.Player.SendUpdate(stream, client.Player.Atributes, Game.MsgServer.MsgUpdate.DataType.Atributes);
                        }
                    }

                }
                #endregion
                #region Jiang Hu
                if (client.Player.MyJiangHu != null)
                {
                    client.Player.MyJiangHu.CheckStatus(client);
                }
                #endregion

                #region Nobility System
                if (DateTime.Now >= client.Player.PayNobilitySystem.PeriodTime && client.Player.PayNobilitySystem.IsActive)
                {
                    client.Player.Nobility = new Role.Instance.Nobility(client);
                    client.Player.Nobility.Donation = client.Player.PayNobilitySystem.LastNobilityDonation;
                    Pool.NobilityRanking.UpdateRank(client.Player.Nobility);
                    client.Player.NobilityRank = client.Player.Nobility.Rank;
                    client.Player.PayNobilitySystem.SetDataAnyWay(0, 0, 0, 0, false);
                }
                #endregion
                #region Intensify Archer
                if (client.Player.InUseIntensify)
                {
                    if (Now > client.Player.IntensifyStamp.AddSeconds(5))
                    {
                        if (!client.Player.ContainFlag(MsgUpdate.Flags.Focused))
                        {
                            client.Player.InUseIntensify = false;
                            MsgSpellAnimation MsgSpell = new MsgSpellAnimation(client.Player.UID
                          , client.Player.UID, 0, 0, client.Player.FocusClientSpell.ID
                          , client.Player.FocusClientSpell.Level, client.Player.FocusClientSpell.UseSpellSoul);


                            MsgSpell.Targets.Enqueue(new MsgSpellAnimation.SpellObj(client.Player.UID, 0, MsgAttackPacket.AttackEffect.None));
                            using (var stream = new ServerSockets.RecycledPacket().GetStream())
                            {
                                MsgSpell.SetStream(stream);
                                MsgSpell.Send(client);
                            }
                            client.Player.AddFlag(MsgUpdate.Flags.Focused, 60, true);
                        }
                    }
                }
                #endregion
                #region Online Training Map
                if (client.Player.Map == 601)
                {
                    if (!client.Map.ValidLocation(client.Player.X, client.Player.Y))
                    {
                        client.Teleport(64, 56, 601);
                    }
                }
                #endregion
                #region Earth Map
                if (client.Player.Map == 44463)
                {
                    if (Now > client.Player.EarthStamp.AddSeconds(10))
                    {
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();
                            Game.MsgFloorItem.MsgItemPacket effect = Game.MsgFloorItem.MsgItemPacket.Create();
                            effect.m_UID = (uint)Game.MsgFloorItem.MsgItemPacket.EffectMonsters.EarthquakeLeftRight;
                            effect.DropType = MsgDropID.Earth;
                            effect.m_X = client.Player.X;
                            effect.m_Y = client.Player.Y;
                            client.Send(stream.ItemPacketCreate(effect));
                        }
                        client.Player.EarthStamp = DateTime.Now;
                    }
                }
                #endregion
                #region Map 1700 No Auto hunt
                if (client.Player.Map == 1700)
                {
                    if (client.Player.OnAutoHunt)
                    {
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();
                            client.Send(stream.AutoHuntCreate(3, 0));
                            client.Player.OnAutoHunt = false;
                        }
                    }
                }
                #endregion
                #region Map 10250 No Auto hunt
                if (client.Player.Map == 10250)
                {
                    if (client.Player.OnAutoHunt)
                    {
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();
                            client.Send(stream.AutoHuntCreate(3, 0));
                            client.Player.OnAutoHunt = false;
                            client.CreateBoxDialog("You cannot use Auto Hunt in here..");
                        }
                    }
                }
                #endregion
                #region Vote System
                Database.VoteSystem.CheckUp(client);
                #endregion
                #region Active Pick
                if (client.Player.ActivePick)
                {
                    if (client.PokerPlayer != null)
                        return;
                    if (Now > client.Player.PickStamp)
                    {
                        client.Player.ActivePick = false;

                        if (client.Player.MonkMiseryTransforming == 1)
                        {
                            client.Player.MonkMiseryTransforming = 0;
                            client.Teleport(client.Player.X, client.Player.Y, 3831);
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                Server.AddMapMonster(stream, client.Map, 7484, client.Player.X, client.Player.Y, 3, 3, 1, client.Player.DynamicID);

                            }
                        }
                        if (client.Player.QuestGUI.CheckQuest(1830, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                if (client.Player.Money >= 99999)
                                {
                                    client.Player.Money -= 99999;
                                    client.Player.SendUpdate(stream, client.Player.Money, MsgUpdate.DataType.Money);
                                    if (Role.Core.Rate(60))
                                    {
                                        client.Player.Money += 100;
                                        client.Player.SendUpdate(stream, client.Player.Money, MsgUpdate.DataType.Money);
                                        client.Inventory.Add(stream, 721878);
                                        client.SendSysMesage("You received 100 Silver!");
                                        client.Player.QuestGUI.FinishQuest(1830);
                                        client.SendSysMesage("Shark is satisfied with your bid and sold the Victory Portrait to you.");
                                        client.ActiveNpc = (uint)Game.MsgNpc.NpcID.Shark;
                                        Game.MsgNpc.NpcHandler.Shark(client, stream, 4, "", 0);
                                    }
                                    else
                                    {
                                        client.CreateDialog(stream, "Too low! Higher!", "I~see.");
                                    }
                                }
                                else
                                {
                                    client.CreateDialog(stream, "Sorry, but you don`t have enough Silver.", "I~see.");
                                }
                            }

                        }
                        if (client.Player.QuestGUI.CheckQuest(3647, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            if (client.ActiveNpc == (uint)Game.MsgNpc.NpcID.LavaFlower1 || client.ActiveNpc == (uint)Game.MsgNpc.NpcID.LavaFlower6
                                || client.ActiveNpc == (uint)Game.MsgNpc.NpcID.LavaFlower2 || client.ActiveNpc == (uint)Game.MsgNpc.NpcID.LavaFlower5
                                || client.ActiveNpc == (uint)Game.MsgNpc.NpcID.LavaFlower3 || client.ActiveNpc == (uint)Game.MsgNpc.NpcID.LavaFlower4
                                || client.ActiveNpc == (uint)Game.MsgNpc.NpcID.LavaFlower7)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    if (client.Inventory.HaveSpace(1))
                                    {
                                        client.Inventory.AddItemWitchStack(3008747, 0, 1, stream);
                                        client.SendSysMesage("You received LavaFlower!", MsgMessage.ChatMode.System);
                                        if (client.Inventory.Contain(3008747, 10))
                                            client.CreateBoxDialog("You`ve collected 10 Lava Flowers. Go and try to extract the Fire Force.");

                                    }
                                    else
                                        client.CreateBoxDialog("Please make 1 more space in your inventory.");

                                }
                            }

                        }
                        if (client.Player.QuestGUI.CheckQuest(3642, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            if (client.ActiveNpc >= (uint)Game.MsgNpc.NpcID.WhiteHerb1 && client.ActiveNpc <= (uint)Game.MsgNpc.NpcID.WhiteHerb6)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    if (client.Inventory.HaveSpace(1))
                                    {
                                        client.Inventory.AddItemWitchStack(3008741, 0, 1, stream);
                                        client.SendSysMesage("You received WhiteHerb!", MsgMessage.ChatMode.System);
                                    }
                                    else
                                        client.CreateBoxDialog("Please make 1 more space in your inventory.");

                                }
                            }

                        }
                        if (client.Player.QuestGUI.CheckQuest(1653, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            if (client.ActiveNpc >= 8551 && client.ActiveNpc <= 8555)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    if (client.Inventory.HaveSpace(1))
                                    {
                                        client.Inventory.AddItemWitchStack(711478, 0, 1, stream);
                                        client.SendSysMesage("You~received~a~Rainbow~Flower!", MsgMessage.ChatMode.System);
                                    }
                                    else
                                        client.CreateBoxDialog("Please make 1 more space in your inventory.");


                                    if (client.OnRemoveNpc != null)
                                    {
                                        client.OnRemoveNpc.Respawn = DateTime.Now.AddSeconds(10);
                                        client.Map.RemoveNpc(client.OnRemoveNpc, stream);
                                        client.Map.soldierRemains.TryAdd(client.OnRemoveNpc.UID, client.OnRemoveNpc);
                                    }
                                }
                            }
                        }
                        if (client.Player.QuestGUI.CheckQuest(6131, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            if (client.Inventory.Contain(720995, 1))
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    ActionQuery action = new ActionQuery()
                                    {
                                        ObjId = client.Player.UID,
                                        Type = ActionType.ClikerON,
                                        Fascing = 7,
                                        PositionX = client.Player.X,
                                        PositionY = client.Player.Y,
                                        dwParam = 0x0c

                                    };
                                    client.Send(stream.ActionCreate(action));
                                }
                            }
                            else if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.SaltedFish)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    if (client.Inventory.HaveSpace(1))
                                    {
                                        client.Inventory.Add(stream, 711479);
                                        client.SendSysMesage("You received a pack of Salted Fish!", MsgMessage.ChatMode.System);
                                    }
                                    else
                                        client.CreateBoxDialog("Please make 1 more space in your inventory.");
                                }
                            }

                        }
                        if (client.Player.QuestGUI.CheckQuest(1640, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.SaltedFish)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    if (client.Inventory.HaveSpace(1))
                                    {
                                        client.Inventory.Add(stream, 711472);
                                        client.SendSysMesage("You receive the Salted Fish!", MsgMessage.ChatMode.System);
                                    }
                                    else
                                        client.CreateBoxDialog("Please make 1 more space in your inventory.");
                                }
                            }
                            else if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.FishingNet)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    if (client.Inventory.HaveSpace(1))
                                    {
                                        client.Inventory.Add(stream, 711473);
                                        client.SendSysMesage("You received a Fishing Net!", MsgMessage.ChatMode.System);
                                    }
                                    else
                                        client.CreateBoxDialog("Please make 1 more space in your inventory.");
                                }

                            }
                        }
                        if (client.Player.QuestGUI.CheckQuest(1594, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.WhiteChrysanthemum)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Inventory.Add(stream, 711441);
                                    client.SendSysMesage("You've got a White Chrysanthemum!", MsgMessage.ChatMode.System);
                                }
                            }
                            else if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.Jasmine)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Inventory.Add(stream, 711442);
                                    client.SendSysMesage("You've got a Jasmine!", MsgMessage.ChatMode.System);
                                }
                            }
                            else if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.Lily)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Inventory.Add(stream, 711440);
                                    client.SendSysMesage("You've got a Lily!", MsgMessage.ChatMode.System);
                                }
                            }
                            else if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.WillowLeaf)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Inventory.Add(stream, 711443);
                                    client.SendSysMesage("You've got a Willow Leaf!", MsgMessage.ChatMode.System);
                                }
                            }


                        }
                        if (client.Player.QuestGUI.CheckQuest(1469, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.st1TreeSeed)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();

                                    client.Inventory.Add(stream, 720971);
                                    client.Player.QuestGUI.IncreaseQuestObjectives(stream, 1469, 1);
                                    if (client.Player.QuestGUI.CheckObjectives(1469, 1, 1, 1))
                                        client.CreateBoxDialog("You`ve~collected~enough~seeds.~Go~report~to~Wan~Ying,~right~away.");
                                    else
                                        client.CreateBoxDialog("You`ve~received~a~seed.");
                                }
                            }
                            if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.nd2TreeSeed)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Inventory.Add(stream, 720971);
                                    client.Player.QuestGUI.IncreaseQuestObjectives(stream, 1469, 0, 1);
                                    if (client.Player.QuestGUI.CheckObjectives(1469, 1, 1, 1))
                                        client.CreateBoxDialog("You`ve~collected~enough~seeds.~Go~report~to~Wan~Ying,~right~away.");
                                    else
                                        client.CreateBoxDialog("You`ve~received~a~seed.");
                                }
                            }
                            if (client.ActiveNpc == (ushort)Game.MsgNpc.NpcID.rd3TreeSeed)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Inventory.Add(stream, 720971);
                                    client.Player.QuestGUI.IncreaseQuestObjectives(stream, 1469, 0, 0, 1);
                                    if (client.Player.QuestGUI.CheckObjectives(1469, 1, 1, 1))
                                        client.CreateBoxDialog("You`ve~collected~enough~seeds.~Go~report~to~Wan~Ying,~right~away.");
                                    else
                                        client.CreateBoxDialog("You`ve~received~a~seed.");
                                }
                            }
                        }
                        if (client.Player.QuestGUI.CheckQuest(1330, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                client.Player.SendString(stream, MsgStringPacket.StringID.Effect, true, "allcure5");
                                switch (client.Player.QuestCaptureType)
                                {

                                    case 1:
                                        {
#if Arabic
                                             client.SendSysMesage("You captured a Thunder Ape.", MsgMessage.ChatMode.System);
#else
                                            client.SendSysMesage("You captured a Thunder Ape.", MsgMessage.ChatMode.System);
#endif

                                            client.Player.QuestGUI.IncreaseQuestObjectives(stream, 1330, 1);
                                        }
                                        break;
                                    case 2:
                                        {
#if Arabic
                                            client.SendSysMesage("You captured a Thunder Ape L58.", MsgMessage.ChatMode.System);
#else
                                            client.SendSysMesage("You captured a Thunder Ape L58.", MsgMessage.ChatMode.System);
#endif

                                            client.Player.QuestGUI.IncreaseQuestObjectives(stream, 1330, 0, 1);
                                        }
                                        break;

                                }
                            }
                        }
                        if (client.Player.QuestGUI.CheckQuest(1317, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            var ActiveQuest = Database.QuestInfo.GetFinishQuest((uint)Game.MsgNpc.NpcID.CarpenterJack, client.Player.Class, 1317);
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                client.Inventory.AddItemWitchStack(711356, 0, 1, stream);
                                client.Player.QuestGUI.IncreaseQuestObjectives(stream, 1317, 1);
                                client.Player.SendString(stream, MsgStringPacket.StringID.Effect, true, "allcure5");
#if Arabic
                                client.SendSysMesage("You received 1 Chiff Flower.", MsgMessage.ChatMode.System);
#else
                                client.SendSysMesage("You received 1 Chiff Flower.", MsgMessage.ChatMode.System);
#endif

                            }
                            if (client.Player.QuestGUI.CheckObjectives(1317, 20))
                            {
#if Arabic
                                     client.Player.QuestGUI.SendAutoPatcher("You have collected enough CliffFowers. Send it to Carpenter Jack.", ActiveQuest.FinishNpcId.Map, ActiveQuest.FinishNpcId.X, ActiveQuest.FinishNpcId.Y, ActiveQuest.FinishNpcId.ID);
                           
#else
                                client.Player.QuestGUI.SendAutoPatcher("You have collected enough CliffFowers. Send it to Carpenter Jack.", ActiveQuest.FinishNpcId.Map, ActiveQuest.FinishNpcId.X, ActiveQuest.FinishNpcId.Y, ActiveQuest.FinishNpcId.ID);

#endif
                            }
                        }

                        else if (client.Player.QuestGUI.CheckQuest(1011, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            if (client.Inventory.HaveSpace(1))
                            {
                                if (client.Inventory.Contain(711239, 5))
                                {
                                    var ActiveQuest4 = Database.QuestInfo.GetFinishQuest((uint)Game.MsgNpc.NpcID.XuLiang, client.Player.Class, 1011);
#if Arabic
                                    client.Player.QuestGUI.SendAutoPatcher("You`ve~picked~5~Peach~Blossoms!~Now~give~them~to~Xu~Liang.", ActiveQuest4.FinishNpcId.Map, ActiveQuest4.FinishNpcId.X, ActiveQuest4.FinishNpcId.Y, ActiveQuest4.FinishNpcId.ID);
#else
                                    client.Player.QuestGUI.SendAutoPatcher("You`ve~picked~5~Peach~Blossoms!~Now~give~them~to~Xu~Liang.", ActiveQuest4.FinishNpcId.Map, ActiveQuest4.FinishNpcId.X, ActiveQuest4.FinishNpcId.Y, ActiveQuest4.FinishNpcId.ID);
#endif

                                }
                                else
                                {
                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        client.Player.QuestGUI.IncreaseQuestObjectives(stream, 1011, 1);
                                        client.Inventory.AddItemWitchStack(711239, 0, 1, stream);
                                        client.Player.SendString(stream, MsgStringPacket.StringID.Effect, true, "allcure5");
#if Arabic
                                            client.SendSysMesage("You picked a Peach Blossom from the Peach Tree!", MsgMessage.ChatMode.System);
#else
                                        client.SendSysMesage("You picked a Peach Blossom from the Peach Tree!", MsgMessage.ChatMode.System);
#endif

                                    }

                                }
                            }
                            else
                            {
#if Arabic
                                client.SendSysMesage("Please make 1 more space in your inventory.", MsgMessage.ChatMode.System);
#else
                                client.SendSysMesage("Please make 1 more space in your inventory.", MsgMessage.ChatMode.System);
#endif

                            }
                        }
                        else if (client.Player.QuestGUI.CheckQuest(6049, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                client.Player.SendString(stream, MsgStringPacket.StringID.Effect, true, "accession1");
                                client.Player.QuestGUI.IncreaseQuestObjectives(stream, 6049, 1, 1);

                                if (client.OnRemoveNpc != null)
                                {
                                    Game.MsgServer.MsgStringPacket packet = new Game.MsgServer.MsgStringPacket();
                                    packet.ID = MsgStringPacket.StringID.Effect;
                                    packet.UID = client.OnRemoveNpc.UID;
                                    packet.Strings = new string[1] { "M_Fire1" };
                                    client.Player.View.SendView(stream.StringPacketCreate(packet), true);


                                    client.OnRemoveNpc.Respawn = DateTime.Now.AddSeconds(10);
                                    client.Map.RemoveNpc(client.OnRemoveNpc, stream);
                                    client.Map.soldierRemains.TryAdd(client.OnRemoveNpc.UID, client.OnRemoveNpc);
                                    //add effect here

                                    Game.MsgNpc.Dialog dialog = new Game.MsgNpc.Dialog(client, stream);
#if Arabic
                                      dialog.AddText("What? You said the Desert Guardian sent you here to find us? Well, I had to play dead to keep the bandits from seeing me. I will avenge my comrades, one day!")
                                    .AddText("~I`ll go back and report this to Desert Guardian! Thanks for coming to find us. I thought we would never be seen again.");
                                    dialog.AddOption("No~Problem.", 255);
                                    dialog.AddAvatar(101).FinalizeDialog();
#else
                                    dialog.AddText("What? You said the Desert Guardian sent you here to find us? Well, I had to play dead to keep the bandits from seeing me. I will avenge my comrades, one day!")
                                  .AddText("~I`ll go back and report this to Desert Guardian! Thanks for coming to find us. I thought we would never be seen again.");
                                    dialog.AddOption("No~Problem.", 255);
                                    dialog.AddAvatar(101).FinalizeDialog();
#endif

                                }

                                if (client.Player.QuestGUI.CheckObjectives(6049, 8))
                                {

                                    var ActiveQuest = Database.QuestInfo.GetFinishQuest((uint)Game.MsgNpc.NpcID.DesertGuardian, client.Player.Class, 6049);
#if Arabic
          client.Player.QuestGUI.SendAutoPatcher("You~are~too~far~away~from~the~Soldier`s~Remains!", ActiveQuest.FinishNpcId.Map, ActiveQuest.FinishNpcId.X, ActiveQuest.FinishNpcId.Y, ActiveQuest.FinishNpcId.ID);
                           
#else
                                    client.Player.QuestGUI.SendAutoPatcher("You~are~too~far~away~from~the~Soldier`s~Remains!", ActiveQuest.FinishNpcId.Map, ActiveQuest.FinishNpcId.X, ActiveQuest.FinishNpcId.Y, ActiveQuest.FinishNpcId.ID);

#endif
                                    client.Player.QuestGUI.SendAutoPatcher("You~are~too~far~away~from~the~Soldier`s~Remains!", ActiveQuest.FinishNpcId.Map, ActiveQuest.FinishNpcId.X, ActiveQuest.FinishNpcId.Y, ActiveQuest.FinishNpcId.ID);
                                }
                                else
                                {
#if Arabic
                                       client.CreateBoxDialog("This soldier has died. Release his soul!");
#else
                                    client.CreateBoxDialog("This soldier has died. Release his soul!");
#endif
                                }

                                //client.CreateBoxDialog("You~are~too~far~away~from~the~Soldier`s~Remains!");
                            }
                        }
                        else if (client.Player.QuestGUI.CheckQuest(6014, MsgQuestList.QuestListItem.QuestStatus.Accepted))
                        {

                            if (client.Inventory.Contain(client.Player.DailyMagnoliaItemId, 1))
                            {


                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();

                                    client.Map.AddMagnolia(stream, client.Player.DailyMagnoliaItemId);
                                    Game.MsgServer.MsgStringPacket packet = new Game.MsgServer.MsgStringPacket();
                                    packet.ID = MsgStringPacket.StringID.Effect;
                                    packet.UID = client.Map.Magnolia.UID;
                                    packet.Strings = new string[1] { "accession1" };
                                    client.Player.View.SendView(stream.StringPacketCreate(packet), true);
                                    client.Player.SendString(stream, MsgStringPacket.StringID.Effect, true, "eidolon");
                                    client.Player.QuestGUI.FinishQuest(6014);
                                    client.Inventory.Remove(client.Player.DailyMagnoliaItemId, 1, stream);
                                    switch (client.Player.DailyMagnoliaItemId)
                                    {
                                        case 729306:
                                            {
                                                client.Player.SubClass.AddStudyPoints(client, 10, stream);
                                                client.Inventory.AddItemWitchStack(729304, 0, 1, stream);
                                                client.GainExpBall(600, true, Role.Flags.ExperienceEffect.angelwing);
#if Arabic
                                                 client.CreateBoxDialog("Congratulations!~You~received~60 minutes of EXP, 10 Study Points and 1 Chi Token.!");
#else
                                                client.CreateBoxDialog("Congratulations!~You~received~60 minutes of EXP, 10 Study Points and 1 Chi Token.!");
#endif

                                                break;
                                            }
                                        case 729307:
                                            {
                                                client.Player.SubClass.AddStudyPoints(client, 20, stream);
                                                client.Inventory.AddItemWitchStack(729304, 0, 1, stream);
                                                client.GainExpBall(900, true, Role.Flags.ExperienceEffect.angelwing);
#if Arabic
                                                  client.CreateBoxDialog("Congratulations!~You~received~90 minutes of EXP, 20 Study Points, 1 Chi Token.!");
#else
                                                client.CreateBoxDialog("Congratulations!~You~received~90 minutes of EXP, 20 Study Points, 1 Chi Token.!");
#endif

                                                break;
                                            }
                                        case 729308:
                                            {
                                                client.Player.SubClass.AddStudyPoints(client, 50, stream);
                                                client.Inventory.AddItemWitchStack(729304, 0, 1, stream);
                                                client.GainExpBall(1200, true, Role.Flags.ExperienceEffect.angelwing);
#if Arabic
                                                   client.CreateBoxDialog("Congratulations!~You~received~120 minutes of EXP, 50 Study Points, 1 Chi Token!");
#else
                                                client.CreateBoxDialog("Congratulations!~You~received~120 minutes of EXP, 50 Study Points, 1 Chi Token!");
#endif

                                                break;
                                            }
                                        case 729309:
                                            {
                                                client.Player.SubClass.AddStudyPoints(client, 100, stream);
                                                client.Inventory.AddItemWitchStack(729304, 0, 1, stream);
                                                client.GainExpBall(1800, true, Role.Flags.ExperienceEffect.angelwing);
#if Arabic
                                                 client.CreateBoxDialog("Congratulations!~You~received~180 minutes of EXP, 100 Study Points, 1 Chi Token.!");
#else
                                                client.CreateBoxDialog("Congratulations!~You~received~180 minutes of EXP, 100 Study Points, 1 Chi Token.!");
#endif

                                                break;
                                            }
                                        case 7293010:
                                            {
                                                client.Player.SubClass.AddStudyPoints(client, 300, stream);
                                                client.Inventory.AddItemWitchStack(729304, 0, 1, stream);
                                                client.GainExpBall(3000, true, Role.Flags.ExperienceEffect.angelwing);
#if Arabic
                                                  client.CreateBoxDialog("Congratulations!~You~received~300 minutes of EXP, 300 Study Points, 1 Chi Token.!");
#else
                                                client.CreateBoxDialog("Congratulations!~You~received~300 minutes of EXP, 300 Study Points, 1 Chi Token.!");
#endif

                                                break;
                                            }
                                    }
                                }
                            }
                            else
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Player.RemovePick(stream);
                                }
                            }
                        }
                    }
                }
                #endregion
                #region Map 3071 End Teleport
                if (client.Player.Map == 3071)
                {
                    if ((Now.Hour == 13 || Now.Hour == 21) && Now.Minute == 30)
                    {
                        client.Teleport(300, 279, 1002);
                    }
                }
                #endregion
                #region Map 5342 Teleport
                if (Now.Minute >= 35)
                {
                    if (client.Player.Map == 5342 && Pool.ServerMaps[5342].Values.Where(i => i.Player.Alive).Count() == 1)
                    {

                        if (client.Player.Alive)
                        {
                            client.Player.ConquerPoints += 800000;
                            //client.Player.ClassExperience += 1000;
                            client.Teleport(300, 279, 1002);
                            Server.SendGlobalPacket(new MsgMessage("[The Grave] Event has been finished. Our hero is: " + client.Player.Name, MsgMessage.MsgColor.red, MsgMessage.ChatMode.TopLeft).GetArray(new ServerSockets.RecycledPacket().GetStream()));

                        }
                    }
                }
                #endregion
               
                #region Map 6011 Teleport
                if (Now.Minute >= 15 || Now.Minute < 10)
                {
                    if (client.Player.Map == 6011 && Pool.ServerMaps[6011].Values.Where(i => i.Player.Alive).Count() == 1)
                    {
                        client.Teleport(300, 279, 1002);

                        if (client.Player.Alive)
                        {
                            client.Player.ConquerPoints += 500000;
                            client.Player.ClassExperience += 500;
                            foreach (var player in Pool.GamePoll.Values)
                            {
                                player.Player.RemoveFlag(MsgUpdate.Flags.TopMrsConquer);
                                player.Player.RemoveFlag(MsgUpdate.Flags.TopMrConquer);
                            }
                            Program.ServerConfig.ConquerorWinner = client.Player.UID;
                            client.Player.AddFlag(Role.Core.IsBoy(client.Player.Body) ? MsgUpdate.Flags.TopMrConquer : MsgUpdate.Flags.TopMrsConquer, Role.StatusFlagsBigVector32.PermanentFlag, false);
                            Server.SendGlobalPacket(new MsgMessage("[Conqueror] Event has been finished. Our hero is: " + client.Player.Name, MsgMessage.MsgColor.red, MsgMessage.ChatMode.TopLeft).GetArray(new ServerSockets.RecycledPacket().GetStream()));

                        }
                    }
                }
                #endregion
                
                #region Map 3845 Teleport
                if ((Now.Hour == 17 && Now.Minute == 30 && Now.Second == 1 || Now.Hour == 23 && Now.Minute == 0 && Now.Second == 1))
                {
                    if (client.Player.Map == 3845)
                    {
                        client.Teleport(300, 279, 1002);
                    }
                }
                #endregion
                #region OnlineStamp
                if (Now >= client.Player.OnlineStamp.AddMinutes(1))
                {
                    client.Player.OnlineMinutes += 1;
                    client.Player.OnlineStamp = DateTime.Now;
                    //if (client.Player.Map == 5342 && client.Player.Alive)
                    //{
                    //    if (Now.Minute >= 5 && Now.Minute <= 9)
                    //    {
                    //        client.Player.ConquerPoints += 25000;
                    //    }
                    //}

                }
                #endregion
                #region Arena Map Messages
                if (client.Player.Map == 1005)
                {
                    if (!client.Player.ContainFlag(MsgUpdate.Flags.SoulShackle))
                    {
                        if (!client.Player.Alive)
                        {
                            if (Now >= client.Player.DeadStamp.AddSeconds(4))
                            {
                                ushort x = 0; ushort y = 0;
                                client.Map.GetRandCoord(ref x, ref y);
                                client.Teleport(x, y, 1005, 0);
                            }
                        }
                        if (Now >= client.Player.StampArenaScore.AddSeconds(3))
                        {
                            uint Rate = 0;
                            if (client.Player.MisShoot != 0)
                                Rate = (uint)(((float)client.Player.HitShoot / (float)client.Player.MisShoot) * 100f);

#if Arabic
                        client.SendSysMesage("[Arena Stats]", MsgMessage.ChatMode.FirstRightCorner, MsgMessage.MsgColor.yellow);
                        client.SendSysMesage("Shots: " + client.Player.MisShoot + " Hits: " + client.Player.HitShoot + " Rate: " + Rate.ToString() + " percent", MsgMessage.ChatMode.ContinueRightCorner, MsgMessage.MsgColor.yellow);
                        client.SendSysMesage("Kills: " + client.Player.ArenaKills + " Deaths: " + client.Player.ArenaDeads + " ", MsgMessage.ChatMode.ContinueRightCorner, MsgMessage.MsgColor.yellow);

#else
                            client.SendSysMesage("[Arena Stats - In Volcano > 3]", MsgMessage.ChatMode.FirstRightCorner, MsgMessage.MsgColor.yellow);
                            client.SendSysMesage("Shots: " + client.Player.MisShoot + " Hits: " + client.Player.HitShoot + " Rate: " + Rate.ToString() + " percent", MsgMessage.ChatMode.ContinueRightCorner, MsgMessage.MsgColor.yellow);
                            client.SendSysMesage("Kills: " + client.Player.ArenaKills + " Deaths: " + client.Player.ArenaDeads + " ", MsgMessage.ChatMode.ContinueRightCorner, MsgMessage.MsgColor.yellow);

#endif

                            client.Player.StampArenaScore = Now;


                        }
                    }
                }
                #endregion
                #region Taoist Power
                client.Player.UpdateTaoistPower(Now);
                #endregion
                #region Invalid X | Y
                //if (client.Player.X == 0 || client.Player.Y == 0)
                //{
                //    client.Teleport(410, 354, 1002);
                //}
                #endregion
                #region Two Hand Safety Checks
                if (Database.AtributesStatus.IsTaoist(client.Player.Class))
                {
                    if (client.Equipment.LeftWeapon != 0)
                    {
                        if (Database.ItemType.IsHossu(client.Equipment.LeftWeapon) == false)
                        {
                            if (client.Inventory.HaveSpace(1))
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Equipment.Remove(Role.Flags.ConquerItem.LeftWeapon, stream);
                                    client.Equipment.LeftWeapon = 0;
                                }
                            }
                        }
                    }
                }
                else if (Database.ItemType.IsTwoHand(client.Equipment.RightWeapon))
                {
                    if (client.Equipment.LeftWeapon != 0 && Database.ItemType.IsShield(client.Equipment.LeftWeapon) == false)
                    {
                        if (client.Inventory.HaveSpace(1))
                        {
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                if (client.Equipment.Remove(Role.Flags.ConquerItem.LeftWeapon, stream) == false)
                                    client.Equipment.Remove(Role.Flags.ConquerItem.AleternanteLeftWeapon, stream);
                                client.Equipment.LeftWeapon = 0;
                            }
                        }
                    }
                }
                #endregion


            }
            catch (Exception e)
            {
                MyConsole.WriteException(e);
            }
        }
        private static unsafe void FloorCallback(Client.GameClient client ,int time)
        {
            if (Program.ExitRequested)
                return;
            try
            {
                if (client == null || !client.FullLoading || client.Player == null || client.Fake)
                    return;
                DateTime Now = DateTime.Now;
                #region ManiacDance
                if (client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.ManiacDance))
                {
                    if (Now > client.Player.ManiacDanceStamp)
                    {
                        client.Player.ManiacDanceStamp = DateTime.Now.AddMilliseconds(1000);
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();
                            var ClientSpell = client.MySpells.ClientSpells[(ushort)Role.Flags.SpellID.ManiacDance];
                            var DBSpell = Pool.Magic[(ushort)Role.Flags.SpellID.ManiacDance][0];
                            MsgSpellAnimation MsgSpell = new MsgSpellAnimation(
                                client.Player.UID
                                  , 0, client.Player.X, client.Player.Y, ClientSpell.ID
                                  , ClientSpell.Level, ClientSpell.UseSpellSoul);
                            uint Experience = 0;

                            foreach (Role.IMapObj target in client.Player.View.Roles(Role.MapObjectType.Monster))
                            {
                                Game.MsgMonster.MonsterRole attacked = target as Game.MsgMonster.MonsterRole;
                                if (Game.MsgServer.AttackHandler.Calculate.Base.GetDistance(client.Player.X, client.Player.Y, attacked.X, attacked.Y) <= 5)
                                {
                                    if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, attacked, DBSpell))
                                    {
                                        MsgSpellAnimation.SpellObj AnimationObj;
                                        Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, attacked, DBSpell, out AnimationObj);
                                        AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                        Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, attacked);

                                        MsgSpell.Targets.Enqueue(AnimationObj);

                                    }
                                }
                            }
                            foreach (Role.IMapObj targer in client.Player.View.Roles(Role.MapObjectType.Player))
                            {
                                var attacked = targer as Role.Player;
                                if (Game.MsgServer.AttackHandler.Calculate.Base.GetDistance(client.Player.X, client.Player.Y, attacked.X, attacked.Y) <= 5)
                                {
                                    if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, attacked, DBSpell))
                                    {
                                        MsgSpellAnimation.SpellObj AnimationObj;
                                        Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, attacked, DBSpell, out AnimationObj);
                                        AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                        Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, attacked);

                                        MsgSpell.Targets.Enqueue(AnimationObj);
                                    }
                                }

                            }
                            foreach (Role.IMapObj targer in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                            {
                                var attacked = targer as Role.SobNpc;
                                if (Game.MsgServer.AttackHandler.Calculate.Base.GetDistance(client.Player.X, client.Player.Y, attacked.X, attacked.Y) <= 5)
                                {
                                    if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, attacked, DBSpell))
                                    {
                                        MsgSpellAnimation.SpellObj AnimationObj;
                                        Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, attacked, DBSpell, out AnimationObj);
                                        AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                        Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, attacked);
                                        MsgSpell.Targets.Enqueue(AnimationObj);
                                    }
                                }
                            }
                            Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                            MsgSpell.SetStream(stream);
                            MsgSpell.Send(client);
                        }
                    }
                }
                #endregion
                if (client.Player.FloorSpells.Count != 0)
                {
                    foreach (var ID in client.Player.FloorSpells)
                    {
                        switch (ID.Key)
                        {

                //////////  اسكلات الارشيف/////////
                            #region WaterShockwave
                            case (ushort)Role.Flags.SpellID.WaterShockwave:
                            case (ushort)Role.Flags.SpellID.WaterShockwavePassive:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();
                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                RemoveSpells.Enqueue(spell);
                                                Game.MsgServer.AttackHandler.Algoritms.InLineAlgorithm Line = new Game.MsgServer.AttackHandler.Algoritms.InLineAlgorithm(spell.FloorPacket.m_X, spell.FloorPacket.OwnerX, spell.FloorPacket.m_Y, spell.FloorPacket.OwnerY, client.Map, 15, 0);
                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 2;
                                                spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                                                spellclient.SpellPacket.X = (ushort)(spell.FloorPacket.OwnerX);
                                                spellclient.SpellPacket.Y = (ushort)(spell.FloorPacket.OwnerY);
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;
                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;
                                                            AnimationObj.MoveX = monster.X;
                                                            AnimationObj.MoveY = monster.Y;
                                                            Pool.ServerMaps[monster.Map].Pushback(ref AnimationObj.MoveX, ref AnimationObj.MoveY, client.Player.Angle, 2);

                                                            client.Map.View.MoveTo<Role.IMapObj>(monster, (ushort)AnimationObj.MoveX, (ushort)AnimationObj.MoveY);
                                                            monster.X = (ushort)AnimationObj.MoveX;
                                                            monster.Y = (ushort)AnimationObj.MoveY;
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;
                                                            AnimationObj.MoveX = target.X;
                                                            AnimationObj.MoveY = target.Y;
                                                            target.Owner.Map.Pushback(ref AnimationObj.MoveX, ref AnimationObj.MoveY, client.Player.Angle, 2);

                                                            if (!Game.MsgServer.AttackHandler.CheckAttack.CheckFloors.CheckGuildWar(target.Owner, (ushort)AnimationObj.MoveX, (ushort)AnimationObj.MoveY))
                                                            {
                                                                continue;
                                                            }
                                                            client.Map.View.MoveTo<Role.IMapObj>(target, (ushort)AnimationObj.MoveX, (ushort)AnimationObj.MoveY);
                                                            target.X = (ushort)AnimationObj.MoveX;
                                                            target.Y = (ushort)AnimationObj.MoveY;
                                                            target.View.Role(false, null);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.MoveX = obj.X;
                                                            AnimationObj.MoveY = obj.Y;
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);
                                            }
                                            else if (spellclient.CheckElseInvocke(Now, spell))
                                            {
                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 1;
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 5)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            uint Damage = (uint)spell.DBSkill.Damage2;
                                                            AnimationObj.Damage = Damage;
                                                            Role.Instance.Ninja.Item item;
                                                            if (client.MyNinja.TryGetValueEquip(Ninja.ItemType.DragonSigilBillow, out item))
                                                            {
                                                                AnimationObj.Damage += item.DBItem.Damage;
                                                            }
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;
                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 5)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);

                                                            AnimationObj.Damage = (uint)spell.DBSkill.DamageOnHuman;
                                                            Role.Instance.Ninja.Item item;
                                                            if (client.MyNinja.TryGetValueEquip(Ninja.ItemType.DragonSigilBillow, out item))
                                                            {
                                                                AnimationObj.Damage += item.DBItem.Damage;
                                                            }
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 5)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            uint Damage = (uint)spell.DBSkill.Damage2;
                                                            AnimationObj.Damage = (uint)Damage;
                                                            Role.Instance.Ninja.Item item;
                                                            if (client.MyNinja.TryGetValueEquip(Ninja.ItemType.DragonSigilBillow, out item))
                                                            {
                                                                AnimationObj.Damage += item.DBItem.Damage;
                                                            }
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                            #region WildFireball
                            case (ushort)Role.Flags.SpellID.WildFireball:
                            case (ushort)Role.Flags.SpellID.WildFireballPassive:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();
                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                RemoveSpells.Enqueue(spell);
                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 1;
                                                spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;
                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.OwnerX, spell.FloorPacket.OwnerY) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Damage += (uint)(AnimationObj.Damage * (50 - (spellclient.Spells.Count * 10)) / 100);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.OwnerX, spell.FloorPacket.OwnerY) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Damage += (uint)(AnimationObj.Damage * (50 - (spellclient.Spells.Count * 10)) / 100);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.OwnerX, spell.FloorPacket.OwnerY) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Damage += (uint)(AnimationObj.Damage * (50 - (spellclient.Spells.Count * 10)) / 100);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);
                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y, p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());
                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                            #region FlameofDestruction
                            case (ushort)Role.Flags.SpellID.FlameofDestruction:
                            case (ushort)Role.Flags.SpellID.FlameofDestructionPassive:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();
                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                RemoveSpells.Enqueue(spell);
                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;
                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y, p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                            else if (spellclient.CheckElseInvocke(Now, spell))
                                            {
                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 1;
                                                spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;
                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= spell.DBSkill.MaxTargets)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            uint Damage = (uint)spell.DBSkill.Damage2;
                                                            AnimationObj.Damage = (uint)Damage;
                                                            Role.Instance.Ninja.Item item;
                                                            if (client.MyNinja.TryGetValueEquip(Ninja.ItemType.FlameSigilRapid, out item))
                                                            {
                                                                AnimationObj.Damage += (uint)item.DBItem.Power;
                                                            }
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= spell.DBSkill.MaxTargets)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            if (AnimationObj.Damage >= spell.DBSkill.DamageOnHuman)
                                                                AnimationObj.Damage = (uint)spell.DBSkill.DamageOnHuman;
                                                            else
                                                            {
                                                                if (spell.DBSkill.DamageOnHuman / 2 > AnimationObj.Damage)
                                                                    AnimationObj.Damage = (uint)spell.DBSkill.DamageOnHuman / 2;
                                                                if (spell.DBSkill.DamageOnHuman / 4 > AnimationObj.Damage)
                                                                    AnimationObj.Damage = (uint)spell.DBSkill.DamageOnHuman / 4;
                                                            }
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= spell.DBSkill.MaxTargets)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            uint Damage = (uint)spell.DBSkill.Damage2;
                                                            if (AnimationObj.Damage >= Damage)
                                                                AnimationObj.Damage = (uint)Damage;
                                                            else
                                                            {
                                                                if (Damage / 2 > AnimationObj.Damage)
                                                                    AnimationObj.Damage = (uint)Damage / 2;
                                                                if (Damage / 4 > AnimationObj.Damage)
                                                                    AnimationObj.Damage = (uint)Damage / 4;
                                                            }
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());
                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                            #region DustDetachment
                            case (ushort)Role.Flags.SpellID.DustDetachment:
                            case (ushort)Role.Flags.SpellID.DustDetachmentPassive:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();
                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                RemoveSpells.Enqueue(spell);
                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;
                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y, p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                            else if (spellclient.CheckElseInvocke(Now, spell))
                                            {
                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 1;
                                                spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;
                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= spell.DBSkill.MaxTargets)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= spell.DBSkill.MaxTargets)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                            if (target.Alive)
                                                            {
                                                                Role.Instance.Ninja.Item item;
                                                                if (client.MyNinja.TryGetValueEquip(Ninja.ItemType.DustSigilStunn, out item))
                                                                {
                                                                    if (client.Player.BattlePower > target.BattlePower && Role.Core.Rate(item.DBItem.Power))
                                                                        target.AddFlag(MsgUpdate.Flags.Dizzy, 3, true);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= spell.DBSkill.MaxTargets)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());
                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                            #region SickleWind
                            //case (ushort)Role.Flags.SpellID.SickleWind:
                            //case (ushort)Role.Flags.SpellID.SickleWindPassive:
                            //    {
                            //        var spellclient = ID.Value;
                            //        Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();
                            //        using (var rec = new ServerSockets.RecycledPacket())
                            //        {
                            //            var stream = rec.GetStream();
                            //            var spells = spellclient.Spells.ToArray();
                            //            foreach (var spell in spells)
                            //            {
                            //                if (spellclient.CheckInvocke(Now, spell))
                            //                {
                            //                    RemoveSpells.Enqueue(spell);
                            //                    spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;
                            //                    foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y, p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                            //                        user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                            //                }
                            //                else if (spellclient.CheckElseInvocke(Now, spell))
                            //                {
                            //                    spellclient.CreateMsgSpell(0);
                            //                    spellclient.SpellPacket.bomb = 1;
                            //                    spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                            //                    foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                            //                    {
                            //                        var monster = obj as Game.MsgMonster.MonsterRole;
                            //                        if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 5)
                            //                        {
                            //                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                            //                            {
                            //                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                            //                                Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                            //                                Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                            //                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                            //                                spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                            //                            }
                            //                        }
                            //                    }
                            //                    foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                            //                    {
                            //                        var target = obj as Role.Player;

                            //                        if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 5)
                            //                        {
                            //                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                            //                            {
                            //                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                            //                                Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                            //                                Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                            //                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                            //                                spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                            //                            }
                            //                        }
                            //                    }
                            //                    foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                            //                    {
                            //                        var target = obj as Role.SobNpc;

                            //                        if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 5)
                            //                        {
                            //                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                            //                            {
                            //                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                            //                                Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                            //                                Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                            //                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                            //                                spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                            //                            }
                            //                        }
                            //                    }
                            //                    spellclient.SendView(stream, client);
                            //                }
                            //            }
                            //        }
                            //        while (RemoveSpells.Count > 0)
                            //            spellclient.RemoveItem(RemoveSpells.Dequeue());
                            //        if (spellclient.Spells.Count == 0)
                            //        {
                            //            Role.FloorSpell.ClientFloorSpells FloorSpell;
                            //            client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                            //        }
                            //        break;
                            //    }
                            #endregion
               ////////////////////////////////////


                            #region ShadowOfChaser
                            case (ushort)Role.Flags.SpellID.ShadowofChaser:
                                {

                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);


                                                spellclient.X = spell.FloorPacket.m_X;
                                                spellclient.Y = spell.FloorPacket.m_Y;

                                                spellclient.CreateMsgSpell(0);



                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Range.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Range.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            //AnimationObj.Damage = AnimationObj.Damage * 250 / 100;
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Range.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                            #region HorrorOfStomper
                            case (ushort)Role.Flags.SpellID.HorrorofStomper:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);
                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 1;
                                                spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                                                spellclient.SpellPacket.X = spell.FloorPacket.OwnerX;
                                                spellclient.SpellPacket.Y = spell.FloorPacket.OwnerY;
                                                spellclient.SpellPacket.SpellLevel = spell.DBSkill.Level;


                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    for (int i = 0; i < 2; i++)
                                                    {
                                                        var monster = obj as Game.MsgMonster.MonsterRole;
                                                        byte myAngle = (byte)spell.FloorPacket.Angle;

                                                        if (myAngle > 3)
                                                            myAngle -= 2;
                                                        else
                                                            myAngle += 2;
                                                        if (i != 0)
                                                        {
                                                            if (myAngle > 3)
                                                                myAngle -= 4;
                                                            else
                                                                myAngle += 4;
                                                        }
                                                        uint xxxx = spell.FloorPacket.m_X;
                                                        uint yyyy = spell.FloorPacket.m_Y;
                                                        client.Map.Pushback(ref xxxx, ref yyyy, (Role.Flags.ConquerAngle)myAngle, 7);
                                                        if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) < 9)
                                                        {
                                                            GameServer.Game.MsgServer.AttackHandler.Algoritms.Fan sector = new GameServer.Game.MsgServer.AttackHandler.Algoritms.Fan(spell.FloorPacket.m_X, spell.FloorPacket.m_Y, (ushort)xxxx, (ushort)yyyy, 2, 40);
                                                            if (sector.IsInFan(obj.X, obj.Y))
                                                            {
                                                                if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                                {
                                                                    Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                                    Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                                    Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                                    AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                                    spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                                }
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    for (int i = 0; i < 2; i++)
                                                    {
                                                        byte myAngle = (byte)spell.FloorPacket.Angle;

                                                        if (myAngle > 3)
                                                            myAngle -= 2;
                                                        else
                                                            myAngle += 2;
                                                        if (i != 0)
                                                        {
                                                            if (myAngle > 3)
                                                                myAngle -= 4;
                                                            else
                                                                myAngle += 4;
                                                        }
                                                        uint xxxx = spell.FloorPacket.m_X;
                                                        uint yyyy = spell.FloorPacket.m_Y;
                                                        client.Map.Pushback(ref xxxx, ref yyyy, (Role.Flags.ConquerAngle)myAngle, 7);
                                                        var target = obj as Role.SobNpc;

                                                        GameServer.Game.MsgServer.AttackHandler.Algoritms.Fan sector = new GameServer.Game.MsgServer.AttackHandler.Algoritms.Fan(spell.FloorPacket.m_X, spell.FloorPacket.m_Y, (ushort)xxxx, (ushort)yyyy, 2, 40);
                                                        if (sector.IsInFan(obj.X, obj.Y))
                                                        {
                                                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                            {
                                                                //     return;
                                                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                                Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                                Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                                AnimationObj.Hit = 1;//??
                                                                spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                            }
                                                            break;
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                {

                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                                }


                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                            #region PeaceOfStomper
                            case (ushort)Role.Flags.SpellID.PeaceofStomper:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);
                                                spellclient.UID = spell.FloorPacket.ItemOwnerUID;
                                                spellclient.X = spell.FloorPacket.m_X;
                                                spellclient.Y = spell.FloorPacket.m_Y;

                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 1;

                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            //    return;
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);


                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));


                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);

                                    }
                                    break;
                                }
                            #endregion
                            #region SeaBurial
                            case (ushort)Role.Flags.SpellID.SeaBurial:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);

                                                uint xX = spell.FloorPacket.m_X, yY = spell.FloorPacket.m_Y;
                                                client.Map.Pushback(ref xX, ref yY, Role.Core.GetAngle(spell.FloorPacket.m_X, spell.FloorPacket.m_Y, spell.FloorPacket.OwnerX, spell.FloorPacket.OwnerY), 18);


                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                                                spellclient.SpellPacket.X = (ushort)xX;
                                                spellclient.SpellPacket.Y = (ushort)yY;
                                                spellclient.SpellPacket.bomb = 1;
                                                Game.MsgServer.AttackHandler.Algoritms.InLineAlgorithm Line = new Game.MsgServer.AttackHandler.Algoritms.InLineAlgorithm(spell.FloorPacket.m_X, spell.FloorPacket.OwnerX, spell.FloorPacket.m_Y, spell.FloorPacket.OwnerY, client.Map, 18, 0);

                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            //AnimationObj.Damage = AnimationObj.Damage * 60 / 100;
                                                            // AnimationObj.Damage = AnimationObj.Damage * 95 / 100;
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);
                                                spell.FloorPacket.Timer = 0;
                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }

                                    break;
                                }
                            #endregion
                            #region TideTrap
                            case (ushort)Role.Flags.SpellID.TideTrap:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();


                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);
                                                //if (Attack)
                                                spellclient.X = spell.FloorPacket.OwnerX;
                                                spellclient.Y = spell.FloorPacket.OwnerY;

                                                spellclient.CreateMsgSpell(0);

                                                ushort X = spell.FloorPacket.m_X, Y = spell.FloorPacket.m_Y;
                                                var coord = Game.MsgServer.AttackHandler.Algoritms.MoveCoords.CheckBladeTeampsCoords(spellclient.X, spellclient.Y, X
                                                , Y, null, 12);

                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    bool hit = false;
                                                    for (int j = 0; j < coord.Count; j++)
                                                        if (Game.MsgServer.AttackHandler.Calculate.Base.GetDDistance(obj.X, obj.Y, (ushort)coord[j].X, (ushort)coord[j].Y) <= 2)
                                                            hit = true;
                                                    if (hit)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    bool hit = false;
                                                    for (int j = 0; j < coord.Count; j++)
                                                        if (Game.MsgServer.AttackHandler.Calculate.Base.GetDDistance(obj.X, obj.Y, (ushort)coord[j].X, (ushort)coord[j].Y) <= 2)
                                                            hit = true;
                                                    if (hit)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            AnimationObj.Damage = AnimationObj.Damage * 60 / 100;
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    bool hit = false;
                                                    for (int j = 0; j < coord.Count; j++)
                                                        if (Game.MsgServer.AttackHandler.Calculate.Base.GetDDistance(obj.X, obj.Y, (ushort)coord[j].X, (ushort)coord[j].Y) <= 2)
                                                            hit = true;
                                                    if (hit)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }

                                    break;
                                }
                            #endregion
                            #region RageOfWar
                            case (ushort)Role.Flags.SpellID.RageofWar:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);
                                                //if (Attack)
                                                spellclient.X = spell.FloorPacket.m_X;
                                                spellclient.Y = spell.FloorPacket.m_Y;

                                                spellclient.CreateMsgSpell(0);


                                                spellclient.SpellPacket.bomb = 1;
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            AnimationObj.Damage = AnimationObj.Damage * 60 / 100;
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }

                                    break;
                                }
                            #endregion
                            #region WrathOfTheEmperor & InfernalEcho
                            case (ushort)Role.Flags.SpellID.WrathoftheEmperor:
                            case (ushort)Role.Flags.SpellID.InfernalEcho:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);

                                                spellclient.X = spell.FloorPacket.m_X;
                                                spellclient.Y = spell.FloorPacket.m_Y;

                                                spellclient.CreateMsgSpell(0);


                                                spellclient.SpellPacket.bomb = 1;
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= (int)(ID.Key == (ushort)Role.Flags.SpellID.WrathoftheEmperor ? 2 : 3))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= (int)(ID.Key == (ushort)Role.Flags.SpellID.WrathoftheEmperor ? 2 : 3))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= (int)(ID.Key == (ushort)Role.Flags.SpellID.WrathoftheEmperor ? 2 : 3))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }

                                    break;
                                }
                            #endregion
                            #region TwilightDance
                            case (ushort)Role.Flags.SpellID.TwilightDance:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;

                                                RemoveSpells.Enqueue(spell);
                                                spellclient.CreateMsgSpell(client.Player.UID);

                                                int increased_attack = 0;
                                                if (spellclient.Spells.Count == 3)
                                                    increased_attack = 15;
                                                else if (spellclient.Spells.Count == 2)
                                                    increased_attack = 25;
                                                else if (spellclient.Spells.Count == 1)
                                                    increased_attack = 35;

                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj, true, increased_attack);
                                                            //int increased_attack = 0;
                                                            if (spellclient.Spells.Count == 3)
                                                                AnimationObj.Damage = AnimationObj.Damage * 100 / 100;
                                                            else if (spellclient.Spells.Count == 2)
                                                                AnimationObj.Damage = AnimationObj.Damage * 110 / 100;
                                                            else if (spellclient.Spells.Count == 1)
                                                                AnimationObj.Damage = AnimationObj.Damage * 120 / 100;
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                ActionQuery action = new ActionQuery()
                                                {
                                                    ObjId = spell.FloorPacket.ItemOwnerUID,
                                                    TargetPositionY = spell.FloorPacket.m_Y,
                                                    TargetPositionX = spell.FloorPacket.m_X,
                                                    PositionX = spell.FloorPacket.OwnerX,
                                                    PositionY = spell.FloorPacket.OwnerY,
                                                    Type = ActionType.RemoveTrap
                                                };

                                                //client.Player.View.SendView(stream.ActionCreate(action), false);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                 p => Role.Core.GetDistance(p.X, p.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 18))
                                                {
                                                    if (user.DynamicID == client.Player.DynamicID)
                                                    {
                                                        user.Send(stream.ActionCreate(action));

                                                        user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                                    }
                                                }

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                            }

                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                          
                        }
                    }
                }

                #region Check Items
                foreach (var item in client.Player.View.Roles(Role.MapObjectType.Item))
                {
                    if (!item.Alive)
                    {
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();
                            var PItem = item as Game.MsgFloorItem.MsgItem;
                            if (PItem.ItemBase != null) PItem.ItemBase.Color = 0;
                            PItem.SendAll(stream, item.IsTrap() ? Game.MsgFloorItem.MsgDropID.RemoveEffect : Game.MsgFloorItem.MsgDropID.Remove);
                            client.Map.View.LeaveMap<Role.IMapObj>(item);
                            continue;
                        }
                    }
                    if (item.IsTrap())
                    {
                        if (client.Player.Map == 4006 && client.Player.JoinTowerOfMysteryLayer == 7)
                        {
                            if (!(Role.Core.GetDistance(client.Player.X, client.Player.Y, 44, 62) <= 3))
                            {
                                if (Role.Core.GetDistance(client.Player.X, client.Player.Y, item.X, item.Y) <= 2)
                                {

                                    if (DateTime.Now > client.Player.TowerOfMysteryFrezeeStamp)
                                    {
                                        client.Player.AddFlag(MsgUpdate.Flags.Freeze, 3, true);
                                        client.Player.TowerOfMysteryFrezeeStamp = DateTime.Now.AddSeconds(5);
                                    }

                                }
                                foreach (var user in client.Player.View.Roles(Role.MapObjectType.Player))
                                {
                                    if (Role.Core.GetDistance(user.X, user.Y, item.X, item.Y) <= 2)
                                    {
                                        var _user = user as Role.Player;
                                        if (DateTime.Now > _user.TowerOfMysteryFrezeeStamp)
                                        {
                                            _user.AddFlag(MsgUpdate.Flags.Freeze, 3, true);
                                            _user.TowerOfMysteryFrezeeStamp = DateTime.Now.AddSeconds(5);
                                        }

                                    }
                                }
                            }
                        }
                        var FloorItem = item as Game.MsgFloorItem.MsgItem;
                        if (FloorItem.ItemBase == null)
                            continue;
                        if (FloorItem.ItemBase.ITEM_ID == Game.MsgFloorItem.MsgItemPacket.NormalDaggerStorm
                           || FloorItem.ItemBase.ITEM_ID == Game.MsgFloorItem.MsgItemPacket.SoulOneDaggerStorm
                           || FloorItem.ItemBase.ITEM_ID == Game.MsgFloorItem.MsgItemPacket.SoulTwoDaggerStorm)
                        {
                            if (Now >= FloorItem.AttackStamp.AddMilliseconds(900))
                            {
                                FloorItem.AttackStamp = Now;
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    MsgSpellAnimation MsgSpell = new MsgSpellAnimation(FloorItem.OwnerEffert.Player.UID
                                        , 0, FloorItem.X, FloorItem.Y, FloorItem.DBSkill.ID
                                        , FloorItem.DBSkill.Level, FloorItem.OwnerEffert.MySpells.ClientSpells[FloorItem.DBSkill.ID].UseSpellSoul);
                                    foreach (var _monster in FloorItem.GMap.View.Roles(Role.MapObjectType.Monster, FloorItem.X, FloorItem.Y
                                        , p => Role.Core.GetDistance(p.X, p.Y, FloorItem.MsgFloor.m_X, FloorItem.MsgFloor.m_Y) <= 3))
                                    {
                                        var monster = _monster as Game.MsgMonster.MonsterRole;
                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(FloorItem.OwnerEffert, monster, FloorItem.DBSkill))
                                        {
                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                            Game.MsgServer.AttackHandler.Calculate.Range.OnMonster(FloorItem.OwnerEffert.Player, monster, FloorItem.DBSkill, out AnimationObj);
                                            Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, FloorItem.OwnerEffert, monster);
                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, FloorItem.SpellSoul);


                                            MsgSpell.Targets.Enqueue(AnimationObj);

                                        }
                                    }
                                    foreach (var player in FloorItem.GMap.View.Roles(Role.MapObjectType.Player, FloorItem.X, FloorItem.Y
                                        , p => Game.MsgServer.AttackHandler.Calculate.Base.GetDistance(p.X, p.Y, FloorItem.MsgFloor.m_X, FloorItem.MsgFloor.m_Y) <= 3))
                                    {
                                        if (player.UID != FloorItem.OwnerEffert.Player.UID)
                                        {
                                            var atacked = player as Role.Player;
                                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(FloorItem.OwnerEffert, atacked, FloorItem.DBSkill))
                                            {

                                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                Game.MsgServer.AttackHandler.Calculate.Range.OnPlayer(FloorItem.OwnerEffert.Player, atacked, FloorItem.DBSkill, out AnimationObj);
                                                Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, FloorItem.OwnerEffert, atacked);
                                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, FloorItem.SpellSoul);

                                                MsgSpell.Targets.Enqueue(AnimationObj);

                                            }
                                        }

                                    }
                                    foreach (var player in FloorItem.GMap.View.Roles(Role.MapObjectType.SobNpc, FloorItem.X, FloorItem.Y
                                        , p => Game.MsgServer.AttackHandler.Calculate.Base.GetDistance(p.X, p.Y, FloorItem.MsgFloor.m_X, FloorItem.MsgFloor.m_Y) <= 3))
                                    {
                                        if (player.UID != FloorItem.OwnerEffert.Player.UID)
                                        {
                                            var atacked = player as Role.SobNpc;
                                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(FloorItem.OwnerEffert, atacked, FloorItem.DBSkill))
                                            {

                                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                Game.MsgServer.AttackHandler.Calculate.Range.OnNpcs(FloorItem.OwnerEffert.Player, atacked, FloorItem.DBSkill, out AnimationObj);
                                                Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, FloorItem.OwnerEffert, atacked);
                                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, FloorItem.SpellSoul);


                                                MsgSpell.Targets.Enqueue(AnimationObj);

                                            }
                                        }

                                    }

                                    MsgSpell.SetStream(stream);
                                    MsgSpell.Send(client);
                                }
                            }
                        }

                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                MyConsole.WriteException(e);
            }

        }
        private static unsafe void AutoAttackCallback(Client.GameClient client, int time)
        {
            if (Program.ExitRequested)
                return;
            try
            {
                if (client == null || !client.FullLoading || client.Player == null || client.Fake)
                    return;
                if (client.OnAutoAttack && (client.Player.Alive || client.Player.ContainFlag(MsgUpdate.Flags.NeptuneCurse)))
                {
                    if (client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.Dizzy))
                    {
                        client.OnAutoAttack = false;
                        return;
                    }
                    if (client.Player.ContainFlag(MsgUpdate.Flags.FatalStrike))
                    {
                        InteractQuery action = new InteractQuery();
                        action = InteractQuery.ShallowCopy(client.AutoAttack);
                        client.Player.RandomSpell = action.SpellID;
                        MsgAttackPacket.Process(client, action);
                        client.OnAutoAttack = true;
                        return;
                    }
                   
                    if (client.Player.Owner.Rune.IsEquipped("DeadlySight"))
                    {
                        InteractQuery action = new InteractQuery();
                        action = InteractQuery.ShallowCopy(client.AutoAttack);
                        client.Player.RandomSpell = action.SpellID;
                        MsgAttackPacket.Process(client, action);
                    }
                    if (DateTime.Now >= client.Player.AttackStamp.AddMilliseconds(900))
                    {
                        InteractQuery action = new InteractQuery();
                        action = InteractQuery.ShallowCopy(client.AutoAttack);
                        client.Player.RandomSpell = action.SpellID;
                        MsgAttackPacket.Process(client, action);
                    }


                }
            }
            catch (Exception e)
            {
                MyConsole.WriteException(e);
            }

        }

        private static unsafe void BuffersCallback(Client.GameClient client, int time)
        {
            if (Program.ExitRequested)
                return;
            try
            {
                if (client == null || !client.FullLoading || client.Player == null || client.Fake)
                    return;
              
                DateTime Now = DateTime.Now;
                #region PK Points
                if (client.Player.PKPoints > 0)
                {
                    if (Now > client.Player.PkPointsStamp.AddMinutes(6))
                    {
                        client.Player.PKPoints -= 1;
                        client.Player.PkPointsStamp = DateTime.Now;
                    }
                }
                #endregion
                #region XPList
                if (Now >= client.Player.XPListStamp.AddSeconds(4) && client.Player.Alive)
                {
                    client.Player.XPListStamp = Now.AddSeconds(4);
                    if (!client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.XPList))
                    {
                        client.Player.XPCount++;
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();

                            client.Player.SendUpdate(stream, client.Player.XPCount, MsgUpdate.DataType.XPCircle);
                            if (client.Player.XPCount >= 100)
                            {
                                client.Player.XPCount = 0;
                                client.Player.AddFlag(Game.MsgServer.MsgUpdate.Flags.XPList, 20, true);
                                client.Player.SendUpdate(stream, 1, MsgUpdate.DataType.XPList);

                                client.Player.SendString(stream, Game.MsgServer.MsgStringPacket.StringID.Effect, true, new string[1] { "xp" });
                            }
                        }
                    }
                }
                #endregion
                #region Undying Will
                if (client.Player.Alive && client.MySpells.ClientSpells.ContainsKey((ushort)Role.Flags.SpellID.UndyingWill))
                {
                    if (Time32.Now >= client.Player.UndyingWillStamp.AddSeconds(5))
                    {
                        client.Player.UndyingWillStamp = Time32.Now;
                        MsgSpell ClientSpell;
                        if (client.MySpells.ClientSpells.TryGetValue((ushort)Role.Flags.SpellID.UndyingWill, out ClientSpell))
                        {
                            if (client.Player.HitPoints < client.Status.MaxHitpoints || client.Player.Mana < client.Status.MaxMana)
                            {
                                var DBSpell = Pool.Magic[ClientSpell.ID][ClientSpell.Level];
                                client.Player.HitPoints += (int)(DBSpell.Damage * client.Status.MaxHitpoints / 100);
                                client.Player.Mana += (ushort)(DBSpell.DamageOnMonster * client.Status.MaxMana / 100);
                                if (client.Player.HitPoints > client.Status.MaxHitpoints)
                                    client.Player.HitPoints = (int)client.Status.MaxHitpoints;
                                if (client.Player.Mana > client.Status.MaxMana)
                                    client.Player.Mana = (ushort)client.Status.MaxMana;
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Player.SendString(stream, (MsgStringPacket.StringID)30, true, "hxdf_hf");

                                    ushort firstlevel = ClientSpell.Level;
                                    if (ClientSpell.Level < Pool.Magic[ClientSpell.ID].Count - 1)
                                    {
                                        if (client.Player.Level >= DBSpell.NeedLevel)
                                        {
                                            ClientSpell.Experience += (int)(client.Status.MaxHitpoints / 100 * Program.ServerConfig.ExpRateSpell);
                                            if (ClientSpell.Experience > DBSpell.Experience)
                                            {
                                                ClientSpell.Level++;
                                                ClientSpell.Experience = 0;
                                            }
                                            if (ClientSpell.PreviousLevel != 0 && ClientSpell.PreviousLevel >= ClientSpell.Level / 2)
                                            {
                                                ClientSpell.Level = ClientSpell.PreviousLevel;
                                            }
                                            try
                                            {
                                                if (ClientSpell.Level > firstlevel)
                                                    client.SendSysMesage("You increased the spell level!", MsgMessage.ChatMode.TopLeftSystem, MsgMessage.MsgColor.red, false);
                                            }
                                            catch (Exception e) { MyConsole.WriteLine(e.ToString()); }
                                            client.Send(stream.SpellCreate(ClientSpell));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion
                #region Stamina & Vigor
                if (client.Player.Alive && !client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.Fly))
                {
                    byte MaxStamina = (byte)(client.Player.HeavenBlessing > 0 ? 150 : 100);
                    if (client.Equipment.UseMonkEpicWeapon)
                    {
                        MsgSpell user_spell = null;
                        if (client.MySpells.ClientSpells.TryGetValue((ushort)Role.Flags.SpellID.GraceofHeaven, out user_spell))
                        {
                            Database.MagicType.Magic DBSpell = Pool.Magic[user_spell.ID][user_spell.Level];
                            MaxStamina += (byte)DBSpell.Damage;
                        }
                    }
                    // if (client.Player.Stamina < MaxStamina && !client.Player.ContainFlag(MsgUpdate.Flags.FrostArrows))
                    if (client.Player.Stamina < MaxStamina && !client.Player.ContainFlag(MsgUpdate.Flags.FrostArrows) && Time32.Now >= client.Player.DuelEndStamp.AddSeconds(20))
                    {
                        if (DateTime.Now >= client.Player.StaminaStamp.AddSeconds(client.Player.Action == Role.Flags.ConquerAction.Sit ? 1 : 3) || client.Player.ContainFlag(MsgUpdate.Flags.Rampage))
                        {
                            #region Rampage
                            if (!client.Player.ContainFlag(MsgUpdate.Flags.Rampage) && Database.AtributesStatus.IsTrojan(client.Player.Class))
                            {
                                MsgSpell user_spell = null;
                                if (client.MySpells.ClientSpells.TryGetValue((ushort)Role.Flags.SpellID.Rampage, out user_spell))
                                {
                                    Database.MagicType.Magic DBSpell = Pool.Magic[user_spell.ID][user_spell.Level];
                                    if (Time32.Now >= client.Player.RampageStamp.AddSeconds(DBSpell.CoolDown))
                                    {
                                        if (client.Player.Stamina < 10)
                                        {
                                            client.Player.RampageStamp = Time32.Now;
                                            client.Player.AddFlag(MsgUpdate.Flags.Rampage, (int)DBSpell.Duration, true);
                                            using (var stream = new ServerSockets.RecycledPacket().GetStream())
                                            {
                                                MsgUpdate upd = new MsgUpdate(stream, client.Player.UID, 1);
                                                upd.Append(stream, MsgUpdate.DataType.FineRain, (uint)MsgUpdate.Flags.Rampage, (uint)DBSpell.Duration, client.Status.MaxHitpoints, client.Status.MaxHitpoints);
                                                client.Send(upd.GetArray(stream));

                                                MsgSpellAnimation MsgSpell = new MsgSpellAnimation(client.Player.UID
                                            , client.Player.UID, 0, 0, DBSpell.ID
                                            , DBSpell.Level, 0);
                                                MsgSpell.SetStream(stream);
                                                MsgSpell.Send(client);
                                            }
                                            client.Equipment.QueryEquipment(client.Equipment.Alternante);
                                        }
                                    }
                                }
                            }
                            #endregion
                            client.Player.StaminaStamp = DateTime.Now;
                            ushort addstamin = 0;
                            if (Now > client.Player.FanRecoverStamin.AddSeconds(8))
                            {
                                if (client.Player.OnXPSkill() == MsgUpdate.Flags.DragonFlow)
                                    addstamin += 20;
                            }
                            if (client.Player.Action == Role.Flags.ConquerAction.Sit)
                                addstamin += 15;
                            else
                                addstamin += 5;

                            if (client.Player.OnXPSkill() == MsgUpdate.Flags.Omnipotence)
                                addstamin *= 2;

                            if (client.Player.ContainFlag(MsgUpdate.Flags.WindWalkerFan))
                            {
                                if (Now > client.Player.FanRecoverStamin.AddSeconds(5))
                                {
                                    addstamin += 10;
                                    client.Player.FanRecoverStamin = DateTime.Now;
                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        if (client.Player.Stamina + addstamin < MaxStamina)
                                            client.Player.SendString(stream, MsgStringPacket.StringID.Effect, true, "TSM_SXJ_HPhf");
                                    }
                                }

                            }

                            if (client.Player.ContainFlag(MsgUpdate.Flags.LightningShield))
                            {
                                if (Time32.Now >= client.Player.LightningShieldStamp.AddSeconds(8))
                                {
                                    addstamin += 20;
                                    client.Player.LightningShieldStamp = Time32.Now;
                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        if (client.Player.Stamina + addstamin < MaxStamina)
                                            client.Player.SendString(stream, MsgStringPacket.StringID.Effect, true, "thor_HPrec");
                                    }
                                }
                            }

                            client.Player.Stamina = (ushort)Math.Min((int)(client.Player.Stamina + addstamin), MaxStamina);
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                client.Player.SendUpdate(stream, client.Player.Stamina, Game.MsgServer.MsgUpdate.DataType.Stamina);
                            }
                        }
                    }

                    if (client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.Ride))
                    {
                        if (client.Player.CheckInvokeFlag(Game.MsgServer.MsgUpdate.Flags.Ride, Now))
                        {
                            if (client.Vigor < client.Status.MaxVigor)
                            {
                                client.Vigor = (ushort)Math.Min(client.Vigor + 2, client.Status.MaxVigor);

                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Send(stream.ServerInfoCreate(client.Vigor));
                                }
                            }
                        }

                    }
                }
                #endregion
                #region Healer
                if (client.Player.Alive && !client.Player.ContainFlag(MsgUpdate.Flags.HealingSnow))
                {
                    byte itemLevel = 0;
                    ushort points = 0;
                    if (client.Rune.IsEquipped("Healer", ref itemLevel))
                    {
                        if (Time32.Now >= client.Player.HealerStamp.AddSeconds(5))
                        {
                            client.Player.HealerStamp = Time32.Now;
                            switch (itemLevel)
                            {
                                case 1: points = 1000; break;
                                case 2: points = 3000; break;
                                case 3: points = 5000; break;
                                case 4: points = 7000; break;
                                case 5: points = 9000; break;
                                case 6: points = 11000; break;
                                case 7: points = 14000; break;
                                case 8: points = 17000; break;
                                case 9: points = 20000; break;
                            }
                            client.Player.HitPoints += points;
                            client.Player.Mana += points;
                            if (client.Player.HitPoints > client.Status.MaxHitpoints)
                                client.Player.HitPoints = (int)client.Status.MaxHitpoints;
                            if (client.Player.Mana > client.Status.MaxMana)
                                client.Player.Mana = (ushort)client.Status.MaxMana;
                        }
                    }
                }
                #endregion
                #region Defense Potion
                if (client.Player.OnDefensePotion)
                {
                    if (Now >= client.Player.OnDefensePotionStamp)
                    {
                        client.Player.OnDefensePotion = false;
                    }
                }
                #endregion
                #region Attack Potion
                if (client.Player.OnAttackPotion)
                {
                    if (Now >= client.Player.OnAttackPotionStamp)
                    {
                        client.Player.OnAttackPotion = false;
                    }
                }
                #endregion
                #region Heaven Blessing
                if (client.Player.HeavenBlessing > 0)
                {
                    if (client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.HeavenBlessing))
                    {
                        if (Now >= client.Player.HeavenBlessTime)
                        {
                            client.Player.RemoveFlag(Game.MsgServer.MsgUpdate.Flags.HeavenBlessing);
                            client.Player.HeavenBlessing = 0;
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                client.Player.SendUpdate(stream, 0, Game.MsgServer.MsgUpdate.DataType.HeavensBlessing);
                                client.Player.SendUpdate(stream, Game.MsgServer.MsgUpdate.OnlineTraining.Remove, Game.MsgServer.MsgUpdate.DataType.OnlineTraining);

                                client.Player.Stamina = (ushort)Math.Min((int)client.Player.Stamina, 100);
                                client.Player.SendUpdate(stream, client.Player.Stamina, Game.MsgServer.MsgUpdate.DataType.Stamina);
                            }
                        }
                        if (client.Player.Map != 601 && client.Player.Map != 1039)
                        {
                            if (Now >= client.Player.ReceivePointsOnlineTraining)
                            {
                                client.Player.ReceivePointsOnlineTraining = Now.AddMinutes(1);
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Player.SendUpdate(stream, Game.MsgServer.MsgUpdate.OnlineTraining.IncreasePoints, Game.MsgServer.MsgUpdate.DataType.OnlineTraining);//+10
                                }
                            }
                            if (Now >= client.Player.OnlineTrainingTime)
                            {
                                client.Player.OnlineTrainingPoints += 100000;
                                client.Player.OnlineTrainingTime = Now.AddMinutes(10);
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Player.SendUpdate(stream, Game.MsgServer.MsgUpdate.OnlineTraining.ReceiveExperience, Game.MsgServer.MsgUpdate.DataType.OnlineTraining);
                                }
                            }
                        }
                    }
                }
                #endregion
                #region




                #endregion
                #region Check PinCode
                /*  if (client.ActiveNpc != (uint)Game.MsgNpc.NpcID.CreatePinCode && !client.Player.TREPIN && Now >= client.Player.PinCodeCheck.AddSeconds(20))
                  {
                      client.Player.PinCodeCheck = DateTime.Now;
                      using (var rec = new ServerSockets.RecycledPacket())
                      {
                          var stream = rec.GetStream();
                          // client.ActiveNpc = 56456546;
                          client.ActiveNpc = (uint)Game.MsgNpc.NpcID.CreatePinCode;
                          Game.MsgNpc.NpcHandler.CreatePin(client, stream, 0, "", 0);
                      }

                  }*/
                #endregion
                #region Enlighten
                if (client.Player.EnlightenReceive > 0)
                {
                    if (Now >= client.Player.EnlightenTime.AddMinutes(20))
                    {
                        client.Player.EnlightenTime = DateTime.Now;
                        client.Player.EnlightenReceive -= 1;
                    }
                }
                #endregion
                #region Double Exp Time
                if (client.Player.DExpTime > 0)
                {
                    client.Player.DExpTime -= 1;
                    if (client.Player.DExpTime == 0)
                        client.Player.RateExp = 1;
                }
                #endregion
                #region Exp Protection
                if (client.Player.ExpProtection > 0)
                    client.Player.ExpProtection -= 1;
                #endregion
                #region Expire VIP
                if (Now >= client.Player.ExpireVip)
                {
                    if (client.Player.VipLevel > 1)
                    {
                        client.Player.VipLevel = 0;
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();
                            client.Player.SendUpdate(stream, client.Player.VipLevel, Game.MsgServer.MsgUpdate.DataType.VIPLevel);

                            client.Player.UpdateVip(stream);
                        }
                    }
                }
                #endregion
                #region Ghost flag
                if (!client.Player.Alive && client.Player.CompleteLogin)
                {
                    if (DateTime.Now > client.Player.GhostStamp)
                    {
                        if (!client.Player.ContainFlag(MsgUpdate.Flags.Ghost))
                        {
                            client.Player.AddFlag(Game.MsgServer.MsgUpdate.Flags.Ghost, Role.StatusFlagsBigVector32.PermanentFlag, true);
                            if (client.Player.Body % 10 == 5)
                                client.Player.TransformationID = 99;
                            else
                                client.Player.TransformationID = 98;
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                client.Send(stream.MapStatusCreate(client.Player.Map, client.Map.ID, client.Map.TypeStatus));
                            }
                        }
                    }
                }
                #endregion
                #region Activeness
                if (Now > client.Player.LoginTimer.AddHours(1))
                {
                    client.Player.LoginTimer = DateTime.Now;
                    client.Activeness.IncreaseTask(3);
                    client.Activeness.IncreaseTask(15);
                    client.Activeness.IncreaseTask(27);

                }
                #endregion
                #region Nobility
                if (client.Player.Nobility.PaidPeriod < DateTime.Now)
                {
                    if (client.Player.Nobility.PaidRank != Role.Instance.Nobility.NobilityRank.Serf)
                    {
                        client.Player.Nobility.Donation = client.Player.Nobility.DonationToBack;
                        client.Player.Nobility.DonationToBack = 0;
                        client.Player.Nobility.PaidRank = Role.Instance.Nobility.NobilityRank.Serf;
                        Pool.NobilityRanking.UpdateRank(client.Player.Nobility);
                        client.Player.NobilityRank = client.Player.Nobility.Rank;
                        client.Equipment.QueryEquipment(client.Equipment.Alternante, false);
                        using (var rect = new ServerSockets.RecycledPacket())
                        {
                            var stream = rect.GetStream();
                            client.Send(stream.NobilityIconCreate(client.Player.Nobility));
                        }
                    }
                }
                #endregion
                #region Blackspot
                if (client.Player.BlackSpot)
                {
                    if (Now >= client.Player.Stamp_BlackSpot)
                    {
                        client.Player.BlackSpot = false;
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();

                            client.Player.View.SendView(stream.BlackspotCreate(false, client.Player.UID), true);
                        }
                    }
                }
                #endregion
                #region EagleEye Countdown
                if (client.Player.EagleEyeCountDown)
                {
                    if (Now >= client.Player.EagleEyeStamp.AddSeconds(20))
                    {
                        client.Player.EagleEyeCountDown = false;
                    }
                }
                #endregion
                #region Overwhelm
                if (client.Rune.IsEquipped("Overwhelm"))
                    client.Player.AddFlag(MsgUpdate.Flags.Overwhelm, Role.StatusFlagsBigVector32.PermanentFlag, false);
                else client.Player.RemoveFlag(MsgUpdate.Flags.Overwhelm);
                #endregion
                #region Crackstar
                if (!client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.CrackStarNegative) && client.Player.Alive)
                {
                    foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                    {
                        if (obj.UID == client.Player.UID) continue;
                        if (Role.Core.GetDistance(client.Player.X, client.Player.Y, obj.X, obj.Y) <= 3)
                        {
                            var Target = obj as Role.Player;
                            if (Target.ContainFlag(Game.MsgServer.MsgUpdate.Flags.CrackStar))
                            {
                                if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(Target.Owner, client.Player, null))
                                {
                                    client.Player.CrackStarNegativeDealer = Target;
                                    client.Player.AddFlag(MsgUpdate.Flags.CrackStarNegative, 5, true);


                                    MsgSpellAnimation MsgSpell = new MsgSpellAnimation(client.Player.UID, 0, 0, 0, 10010, 0, 0);
                                    Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                    Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(Target, client.Player, null, out AnimationObj);
                                    AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, 0);
                                    Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, Target.Owner, client.Player);
                                    MsgSpell.Targets.Enqueue(AnimationObj);

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        MsgSpell.SetStream(stream);
                                        MsgSpell.Send(client);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                #endregion
                #region Undying~Imprinting
                if (client.MySpells.ClientSpells.ContainsKey((ushort)Role.Flags.SpellID.UndyingImprinting))
                {
                    uint limit = (uint)(client.Rune.IsEquipped("FightingWill") ? 120 : 60);
                    if (client.Player.ThunderStrikerUndyingImprinting < limit)
                    {
                        client.Player.ThunderStrikerUndyingImprinting++;
                        if (client.Player.ThunderStrikerUndyingImprinting % 60 == 0 && client.Player.ThunderStrikerUndyingImprinting > 0)
                        {
                            client.Player.Stamina += 20;
                            if (client.Player.Stamina > 100 + (client.Player.HeavenBlessing > 0 ? 50 : 0))
                                client.Player.Stamina = (byte)(100 + (client.Player.HeavenBlessing > 0 ? 50 : 0));
                        }
                    }
                    else if (client.Player.ThunderStrikerUndyingImprinting > limit)
                        client.Player.ThunderStrikerUndyingImprinting = limit;
                }
                #endregion
                #region Flags
                foreach (var flag in client.Player.BitVector.GetFlags())
                {
                    if (flag.Expire(Now))
                    {
                        if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.BloodTide || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.FineRain1 || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.FineRain1 || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.FineRain2 || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.IronGuard || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.RiseofTaoism || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.Rampage)
                        {
                            client.Player.RemoveFlag((Game.MsgServer.MsgUpdate.Flags)flag.Key);
                            client.Equipment.QueryEquipment(client.Equipment.Alternante);
                        }
                        else if (flag.Key >= (int)Game.MsgServer.MsgUpdate.Flags.TyrantAura && flag.Key <= (int)Game.MsgServer.MsgUpdate.Flags.EartAura)
                        {
                            client.Player.AddAura(client.Player.UseAura, null, 0);
                        }
                        else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.CrackStarNegative)
                        {
                            if (client.Player.CrackStarNegativeDealer != null)
                            {
                                MsgSpellAnimation MsgSpell = new MsgSpellAnimation(client.Player.UID, 0, 0, 0, 10010, 0, 0);
                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj = new MsgSpellAnimation.SpellObj() { Damage = (uint)((double)client.Player.CrackStarNegativeDealer.HitPoints * 20d / 100d), UID = client.Player.UID, Hit = 1 };
                                Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client.Player.CrackStarNegativeDealer.Owner, client.Player);
                                MsgSpell.Targets.Enqueue(AnimationObj);

                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    MsgSpell.SetStream(stream);
                                    MsgSpell.Send(client);
                                }
                                client.Player.CrackStarNegativeDealer = null;
                            }
                            client.Player.RemoveFlag((Game.MsgServer.MsgUpdate.Flags)flag.Key);
                        }
                        else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.ThunderRampage)
                        {
                            if (client.Player.ThunderStrikerUndyingImprinting >= 60)
                            {
                                client.Player.ThunderStrikerUndyingImprinting -= 60;
                                client.Player.Stamina += 20;
                                if (client.Player.Stamina > 100 + (client.Player.HeavenBlessing > 0 ? 50 : 0))
                                    client.Player.Stamina = (byte)(100 + (client.Player.HeavenBlessing > 0 ? 50 : 0));
                                #region ChainedStorm(Rune Skill)
                                if (client.Rune.IsEquipped("ChainedStorm") && client.MySpells.ClientSpells.ContainsKey((ushort)Role.Flags.SpellID.WindstormBattleaxe))
                                {
                                    var spell = Pool.Magic[(ushort)Role.Flags.SpellID.WindstormBattleaxe][client.MySpells.ClientSpells[(ushort)Role.Flags.SpellID.WindstormBattleaxe].Level];
                                    MsgSpellAnimation MsgSpell = new MsgSpellAnimation(client.Player.UID
                                           , 0, client.Player.X, client.Player.Y, spell.ID
                                           , spell.Level, 0);
                                    using (var stream = new ServerSockets.RecycledPacket().GetStream())
                                    {
                                        MsgSpell.SetStream(stream);
                                        MsgSpell.Send(client);
                                    }
                                    client.Player.AddSpellFlag(MsgUpdate.Flags.AttackUp, (int)(spell.Duration + 30), true);
                                }
                                #endregion
                            }
                            client.Player.RemoveFlag((Game.MsgServer.MsgUpdate.Flags)flag.Key);
                        }
                        else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.LightningShieldActivated)
                        {
                            client.Player.SendUpdate(new ServerSockets.RecycledPacket().GetStream(), MsgUpdate.Flags.LightningShieldActivated, 0, 0, 0, MsgUpdate.DataType.AzureShield);
                            client.Player.RemoveFlag((Game.MsgServer.MsgUpdate.Flags)flag.Key);
                        }
                        else
                        {

                            if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.Superman || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.Cyclone
                                || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.SuperCyclone)
                            {
                                Role.KOBoard.KOBoardRanking.AddItem(new Role.KOBoard.Entry() { UID = client.Player.UID, Name = client.Player.Name, Points = client.Player.KillCounter }, true);
                            }
                            client.Player.RemoveFlag((Game.MsgServer.MsgUpdate.Flags)flag.Key);
                        }
                    }
                    if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.ScarofEarth)
                    {
                        if (flag.CheckInvoke(Now))
                        {
                            if (client.Player.ScarofEarthl != null && client.Player.AttackerScarofEarthl != null)
                            {
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();

                                    var DBSpell = client.Player.ScarofEarthl;
                                    MsgSpellAnimation MsgSpell = new MsgSpellAnimation(
                                        client.Player.UID
                                          , 0, client.Player.X, client.Player.Y, DBSpell.ID
                                          , DBSpell.Level, 0, 1);

                                    MsgSpellAnimation.SpellObj AnimationObj = new MsgSpellAnimation.SpellObj()
                                    {
                                        UID = client.Player.UID,
                                        Damage = (uint)DBSpell.Damage2,
                                        Hit = 1
                                    };

                                    Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client.Player.AttackerScarofEarthl, client.Player);
                                    MsgSpell.SetStream(stream);
                                    MsgSpell.Targets.Enqueue(AnimationObj);
                                    MsgSpell.Send(client);
                                }
                            }
                        }
                    }

                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.DragonFlow)
                    {
                        if (flag.CheckInvoke(Now))
                        {
                            byte MaxStamina = (byte)(client.Player.HeavenBlessing > 0 ? 150 : 100);

                            if (client.Player.Stamina < MaxStamina)
                            {
                                client.Player.Stamina += 20;
                                client.Player.Stamina = (ushort)Math.Min((int)client.Player.Stamina, MaxStamina); using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    client.Player.SendUpdate(stream, client.Player.Stamina, Game.MsgServer.MsgUpdate.DataType.Stamina);
                                }
                            }
                        }
                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.HealingSnow)
                    {
                        if (flag.CheckInvoke(Now) && client.Player.Alive)
                        {
                            if (client.Player.HitPoints < client.Status.MaxHitpoints || client.Player.Mana < client.Status.MaxMana)
                            {
                                MsgSpell spell;
                                if (client.MySpells.ClientSpells.TryGetValue((ushort)Role.Flags.SpellID.HealingSnow, out spell))
                                {
                                    var arrayspells = Pool.Magic[(ushort)Role.Flags.SpellID.HealingSnow];
                                    var DbSpell = arrayspells[(ushort)Math.Min((int)spell.Level, arrayspells.Count - 1)];

                                    client.Player.HitPoints = (int)Math.Min(client.Status.MaxHitpoints, (int)(client.Player.HitPoints + DbSpell.Damage2));
                                    client.Player.Mana = (ushort)Math.Min(client.Status.MaxMana, (int)(client.Player.Mana + DbSpell.Damage3));
                                    client.Player.SendUpdateHP();
                                    client.Player.XPCount += 1;
                                }
                            }
                        }
                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.Bleed)
                    {
                        if (flag.CheckInvoke(Now))
                        {
                            if (client.Player.HitPoints < client.Player.BleedDamage)
                            {
                                client.Player.BleedDamage = 0;
                                goto jump;
                            }
                            client.Player.HitPoints = Math.Max(1, (int)(client.Player.HitPoints - client.Player.BleedDamage));
                        jump:

                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();

                                InteractQuery action = new InteractQuery()
                                {
                                    Damage = client.Player.BleedDamage,
                                    AtkType = (ushort)MsgAttackPacket.AttackID.Physical,
                                    X = client.Player.X,
                                    Y = client.Player.Y,
                                    OpponentUID = client.Player.UID
                                };
                                client.Player.View.SendView(stream.InteractionCreate(action), true);
                            }
                        }
                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.Poisoned)
                    {
                        if (flag.CheckInvoke(Now))
                        {
                            int damage = (int)Game.MsgServer.AttackHandler.Calculate.Base.CalculatePoisonDamage((uint)client.Player.HitPoints, client.Player.PoisonLevel);
                            if (damage > 1)
                            {
                                damage -= (int)(damage * Math.Min(100, client.PerfectionStatus.ToxinEraser)) / 100;

                            }
                            if (client.Player.HitPoints == 1)
                            {
                                damage = 0;
                                goto jump;
                            }
                            damage -= (int)((damage * Math.Min(client.Status.Detoxication, 90)) / 100);
                            client.Player.HitPoints = Math.Max(1, (int)(client.Player.HitPoints - damage));

                        jump:

                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();

                                InteractQuery action = new InteractQuery()
                                {
                                    Damage = damage,
                                    AtkType = (ushort)MsgAttackPacket.AttackID.Physical,
                                    X = client.Player.X,
                                    Y = client.Player.Y,
                                    OpponentUID = client.Player.UID
                                };
                                client.Player.View.SendView(stream.InteractionCreate(action), true);
                            }

                        }
                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.ShurikenVortex)
                    {
                        if (flag.CheckInvoke(Now))
                        {
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();

                                InteractQuery action = new InteractQuery()
                                {
                                    UID = client.Player.UID,
                                    X = client.Player.X,
                                    Y = client.Player.Y,
                                    SpellID = (ushort)Role.Flags.SpellID.ShurikenEffect,
                                    AtkType = (ushort)MsgAttackPacket.AttackID.Magic
                                };

                                MsgAttackPacket.ProcescMagic(client, stream.InteractionCreate(action), action);
                            }
                        }
                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.RedName || flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.BlackName)
                    {
                        if (flag.CheckInvoke(Now))
                        {
                            if (client.Player.PKPoints > 0)
                                client.Player.PKPoints -= 1;

                            client.Player.PkPointsStamp = DateTime.Now;
                        }
                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.Cursed)
                    {
                        if (flag.CheckInvoke(Now))
                        {
                            if (client.Player.CursedTimer > 0)
                                client.Player.CursedTimer -= 1;
                        }

                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.TidalWave)
                    {
                        if (client.Player.XPCount < 100)
                            client.Player.XPCount++;
                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.Quench)
                    {
                        if (client.Player.XPCount > 0)
                            client.Player.XPCount--;
                    }
                    else if (flag.Key == (int)Game.MsgServer.MsgUpdate.Flags.FineRain1)
                    {
                        if (client.Player.FineRainPower > 0)
                        {
                            uint value = 5000;
                            if (client.Player.FineRainPower - (int)value > 0)
                                client.Player.FineRainPower -= value;
                            else
                            {
                                client.Player.FineRainPower = 0;
                                value = 0;
                            }

                            using (var stream = new ServerSockets.RecycledPacket().GetStream())
                            {
                                MsgUpdate upd = new MsgUpdate(stream, client.Player.UID, 1);
                                upd.Append(stream, MsgUpdate.DataType.FineRain, (uint)MsgUpdate.Flags.FineRain1, (uint)(Time32.Now.AllSeconds() - flag.Timer.AddSeconds(flag.Secounds).AllSeconds()), client.Player.FineRainPower, client.Player.FineRainPower);
                                client.Send(upd.GetArray(stream));
                            }
                            if ((int)client.Status.MaxHitpoints - (int)value > 0)
                                client.Status.MaxHitpoints -= value;
                            else client.Status.MaxHitpoints = 1;
                            if (client.Player.HitPoints > client.Status.MaxHitpoints)
                                client.Player.HitPoints = (int)client.Status.MaxHitpoints;
                        }
                    }
                }
                #endregion
                #region Transform
                if (client.Player.OnTransform)
                {
                    if (client.Player.TransformInfo != null)
                    {
                        if (client.Player.TransformInfo.CheckUp(Now))
                            client.Player.TransformInfo = null;
                    }
                }
                #endregion
                #region Praying
                if (client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.Praying))
                {
                    if (client.Player.BlessTime < 7200000 - 30000)
                    {
                        if (Now > client.Player.CastPrayStamp.AddSeconds(30))
                        {
                            bool have = false;
                            foreach (var ownerpraying in client.Player.View.Roles(Role.MapObjectType.Player))
                            {
                                if (Role.Core.GetDistance(client.Player.X, client.Player.Y, ownerpraying.X, ownerpraying.Y) <= 2)
                                {
                                    var target = ownerpraying as Role.Player;
                                    if (target.ContainFlag(MsgUpdate.Flags.CastPray))
                                    {
                                        have = true;
                                        break;
                                    }
                                }
                            }
                            if (!have)
                                client.Player.RemoveFlag(MsgUpdate.Flags.Praying);
                            client.Player.CastPrayStamp = DateTime.Now;
                            client.Player.BlessTime += 30000;
                        }
                    }
                    else
                        client.Player.BlessTime = 3100000;
                }
                #endregion
                #region Castpray
                if (client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.CastPray))
                {
                    if (client.Player.BlessTime < 7200000 - 60000)
                    {
                        if (Now > client.Player.CastPrayStamp.AddSeconds(30))
                        {
                            client.Player.CastPrayStamp = DateTime.Now;
                            client.Player.BlessTime += 60000;
                        }
                    }
                    else
                        client.Player.BlessTime = 7200000;
                    if (Now > client.Player.CastPrayActionsStamp.AddSeconds(5))
                    {
                        client.Player.CastPrayActionsStamp = DateTime.Now;
                        foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                        {
                            if (Role.Core.GetDistance(client.Player.X, client.Player.Y, obj.X, obj.Y) <= 1)
                            {
                                var Target = obj as Role.Player;
                                if (Target.Reborn < 2)
                                {
                                    if (!Target.ContainFlag(Game.MsgServer.MsgUpdate.Flags.Praying))
                                    {
                                        Target.AddFlag(Game.MsgServer.MsgUpdate.Flags.Praying, Role.StatusFlagsBigVector32.PermanentFlag, true);

                                        using (var rec = new ServerSockets.RecycledPacket())
                                        {
                                            var stream = rec.GetStream();
                                            ActionQuery action = new ActionQuery()
                                            {
                                                ObjId = client.Player.UID,
                                                dwParam = (uint)client.Player.Action,
                                                Timestamp = obj.UID
                                            };
                                            client.Player.View.SendView(stream.ActionCreate(action), true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else if (client.Player.BlessTime > 0)
                {
                    if (!client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.CastPray) && !client.Player.ContainFlag(Game.MsgServer.MsgUpdate.Flags.Praying))
                    {

                        if (Now > client.Player.CastPrayStamp.AddSeconds(2))
                        {
                            if (client.Player.BlessTime > 2000)
                                client.Player.BlessTime -= 2000;
                            else
                                client.Player.BlessTime = 0;
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();
                                client.Player.SendUpdate(stream, client.Player.BlessTime, Game.MsgServer.MsgUpdate.DataType.LuckyTimeTimer);
                            }
                            client.Player.CastPrayStamp = DateTime.Now;
                        }
                    }
                }
                #endregion
                #region Team invite
                if (client.Team != null)
                {
                    if (client.Team.AutoInvite == true && client.Player.Map != 1036 && client.Team.CkeckToAdd())
                    {
                        if (Now > client.Team.InviteTimer.AddSeconds(10))
                        {
                            client.Team.InviteTimer = Now;
                            foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                            {
                                if (!client.Team.SendInvitation.Contains(obj.UID))
                                {
                                    client.Team.SendInvitation.Add(obj.UID);

                                    if ((obj as Role.Player).Owner.Team == null)
                                    {
                                        using (var rec = new ServerSockets.RecycledPacket())
                                        {
                                            var stream = rec.GetStream();

                                            obj.Send(stream.RelationCreate(client.Player, obj as Role.Player));

                                            stream.TeamCreate(MsgTeam.TeamTypes.InviteRequest, client.Player.UID);
                                            obj.Send(stream);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (client.Team.TeamLider(client))
                    {
                        if (Now > client.Team.UpdateLeaderLocationStamp.AddSeconds(4))
                        {
                            client.Team.UpdateLeaderLocationStamp = Now;
                            using (var rec = new ServerSockets.RecycledPacket())
                            {
                                var stream = rec.GetStream();

                                ActionQuery action = new ActionQuery()
                                {
                                    ObjId = client.Player.UID,
                                    dwParam = 1015,
                                    Type = ActionType.LocationTeamLieder,
                                    PositionX = client.Team.Leader.Player.X,
                                    PositionY = client.Team.Leader.Player.Y
                                };
                                client.Team.SendTeam(stream.ActionCreate(action), client.Player.UID, client.Player.Map);
                            }
                        }
                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                MyConsole.WriteException(e);
            }

        }
       
        private static void ArenaFunctions(int time)
        {
            if (Program.ExitRequested)
                return;
            Game.MsgTournaments.MsgSchedules.Arena.CheckGroups();
            Game.MsgTournaments.MsgSchedules.Arena.CreateMatches();
            Game.MsgTournaments.MsgSchedules.Arena.VerifyMatches();
        }
        private static void TeamArenaFunctions(int time)
        {
            if (Program.ExitRequested)
                return;
            Game.MsgTournaments.MsgSchedules.TeamArena.CheckGroups();
            Game.MsgTournaments.MsgSchedules.TeamArena.CreateMatches();
            Game.MsgTournaments.MsgSchedules.TeamArena.VerifyMatches();
        }
        private static void WorldTournaments(int time)
        {
          
            if (!Server.FullLoading) return;
            DateTime DateNow = DateTime.Now;
            #region Console Title
            if (Online > MaxOnline)
                MaxOnline = Online;
            MyConsole.Title = "[" + Program.ServerConfig.ServerName + "] - [Players Online]: " + Online + " - [Max Online]: " + MaxOnline + " - Game Clock: [" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + "]";
            //using DBFunctionality.MySqlCommand(DBFunctionality.MySqlCommandType.INSERT).Insert("playersonline").Insert("Online", Online).Execute();
            new DBFunctionality.MySqlCommand(DBFunctionality.MySqlCommandType.UPDATE).Select("playersonline").Set("Online", Pool.GamePoll.Count + 47).Execute();

            #endregion
            if (Program.ExitRequested)
                return;
            #region Anima Furnaces (Smelting)
            if (Server.FullLoading)
            {
                if (Pool.ServerMaps.ContainsKey(10428))
                {
                    var map = Pool.ServerMaps[10428];
                    if (map.View.GetAllMapRolesCount(Role.MapObjectType.Item, i => (i as MsgItem).MsgFloor.m_ID == 2284) > 0)
                    {
                        if (Time32.Now >= Pool.smeltFloorStamp.AddSeconds(30))
                        {
                            Pool.smeltFloorStamp = Time32.Now;
                            var floor = map.View.GetAllMapRoles(Role.MapObjectType.Item, i => (i as MsgItem).MsgFloor.m_ID == 2284).FirstOrDefault();
                            map.RemoveTrap(floor.X, floor.Y, floor);

                            foreach (var client in Pool.GamePoll.Values)
                            {
                                if (client.Player.Map == map.ID && (client.Player.DynamicID == 0 || client.Player.DynamicID == map.ID))
                                {
                                    client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = floor.X, Y = floor.Y, Strings = new string[1] { "DragonSoul_djs" } }));
                                }
                            }

                        }
                    }
                    else if (map.View.GetAllMapRolesCount(Role.MapObjectType.Item, i => (i as MsgItem).MsgFloor.m_ID == 2285) > 0)
                    {
                        if (Time32.Now >= Pool.smeltFloorStamp.AddSeconds(2))
                        {
                            Pool.smeltFloorStamp = Time32.Now;
                            var floor = map.View.GetAllMapRoles(Role.MapObjectType.Item, i => (i as MsgItem).MsgFloor.m_ID == 2285).FirstOrDefault() as MsgItem;
                            map.RemoveTrap(floor.X, floor.Y, floor);

                            floor.UID = MsgItem.UIDS.Next;
                            floor.MsgFloor.DropType = MsgDropID.Effect;
                            floor.MsgFloor.m_ID = 2284;
                            map.EnqueueItem(floor);
                            floor.SendAll(new ServerSockets.RecycledPacket().GetStream(), MsgDropID.Effect);

                            //Result
                            if (Role.Core.Rate(50))//Lunar
                            {
                                Pool.SmeltingSessions.Add(1);
                                foreach (var client in Pool.GamePoll.Values)
                                    if (client.Player.Map == map.ID && (client.Player.DynamicID == 0 || client.Player.DynamicID == map.ID))
                                    {
                                        client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = 42, Y = 55, Strings = new string[1] { "DragonSoul_ylsb" } }));
                                        client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = 50, Y = 47, Strings = new string[1] { "DragonSoul_ylcg" } }));

                                        client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = 60, Y = 57, Strings = new string[1] { "npc_liandanlu_1" } }));
                                        client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = 60, Y = 57, Strings = new string[1] { "glebesword" } }));

                                        if (client.Player.DragonFurnace > 0 && !client.Player.DragonFurnace.ToString().StartsWith("5"))
                                        {

                                            if (Role.Core.Rate(BaseFunc.AnimaUpgradeRate(client.Player.DragonFurnace)))
                                            {
                                                if ((client.Player.DragonFurnace + 1) % 100 >= 14)
                                                    Pool.SmeltingSuccesses.Add(new Pool.Smelt() { Furnace = 1, Name = client.Player.Name, Prize = Pool.ItemsBase[client.Player.DragonFurnace + 1].Name });
                                                client.Inventory.Add(new ServerSockets.RecycledPacket().GetStream(), client.Player.DragonFurnace + 1, 1);
                                                client.CreateBoxDialog("+------- Smelting Succeeded -------+\n" +
                                                       "              Congratulations! \n" +
                                                    "        You received a " + Pool.ItemsBase[client.Player.DragonFurnace + 1].Name + "!\n" +
                                     "+----------------------------------+");
                                            }
                                            else
                                            {
                                                if ((client.Player.DragonFurnace - 1) % 100 >= 14)
                                                    Pool.SmeltingSuccesses.Add(new Pool.Smelt() { Furnace = 1, Name = client.Player.Name, Prize = Pool.ItemsBase[client.Player.DragonFurnace - 1].Name });
                                                client.Inventory.Add(new ServerSockets.RecycledPacket().GetStream(), client.Player.DragonFurnace, 1);
                                                client.Inventory.Add(new ServerSockets.RecycledPacket().GetStream(), client.Player.DragonFurnace - 1, 1);
                                                client.CreateBoxDialog("+------- Smelting Succeeded -------+\n" +
                                                       "  The Anima is not upgraded,! \n" +
                                                    "        but you received an extra " + Pool.ItemsBase[client.Player.DragonFurnace - 1].Name + "!\n" +
                                     "+----------------------------------+");
                                            }
                                        }
                                        client.Player.DragonFurnace = 0;
                                    }
                            }
                            else
                            {
                                Pool.SmeltingSessions.Add(0);
                                foreach (var client in Pool.GamePoll.Values)
                                {
                                    if (client.Player.Map == map.ID && (client.Player.DynamicID == 0 || client.Player.DynamicID == map.ID))
                                    {
                                        client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = 50, Y = 47, Strings = new string[1] { "DragonSoul_ylsb" } }));
                                        client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = 42, Y = 55, Strings = new string[1] { "DragonSoul_ylcg" } }));

                                        client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = 52, Y = 65, Strings = new string[1] { "npc_liandanlu_1" } }));
                                        client.Send(new ServerSockets.RecycledPacket().GetStream().StringPacketCreate(new Game.MsgServer.MsgStringPacket() { ID = MsgStringPacket.StringID.LocationEffect, X = 52, Y = 65, Strings = new string[1] { "glebesword" } }));

                                        if (client.Player.DragonFurnace > 0 && client.Player.DragonFurnace.ToString().StartsWith("5"))
                                        {
                                            client.Player.DragonFurnace -= 1000000;
                                            if (Role.Core.Rate(BaseFunc.AnimaUpgradeRate(client.Player.DragonFurnace)))
                                            {
                                                if ((client.Player.DragonFurnace + 1) % 100 >= 14)
                                                    Pool.SmeltingSuccesses.Add(new Pool.Smelt() { Furnace = 0, Name = client.Player.Name, Prize = Pool.ItemsBase[client.Player.DragonFurnace + 1].Name });
                                                client.Inventory.Add(new ServerSockets.RecycledPacket().GetStream(), client.Player.DragonFurnace + 1, 1);
                                                client.CreateBoxDialog("+------- Smelting Succeeded -------+\n" +
                                                       "              Congratulations! \n" +
                                                    "        You received a " + Pool.ItemsBase[client.Player.DragonFurnace + 1].Name + "!\n" +
                                     "+----------------------------------+");
                                            }
                                            else
                                            {
                                                if ((client.Player.DragonFurnace - 1) % 100 >= 14)
                                                    Pool.SmeltingSuccesses.Add(new Pool.Smelt() { Furnace = 0, Name = client.Player.Name, Prize = Pool.ItemsBase[client.Player.DragonFurnace - 1].Name });
                                                client.Inventory.Add(new ServerSockets.RecycledPacket().GetStream(), client.Player.DragonFurnace, 1);
                                                client.Inventory.Add(new ServerSockets.RecycledPacket().GetStream(), client.Player.DragonFurnace - 1, 1);
                                                client.CreateBoxDialog("+------- Smelting Succeeded -------+\n" +
                                                       "  The Anima is not upgraded,! \n" +
                                                    "        but you received an extra " + Pool.ItemsBase[client.Player.DragonFurnace - 1].Name + "!\n" +
                                     "+----------------------------------+");
                                            }
                                        }
                                        client.Player.DragonFurnace = 0;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (Time32.Now >= Pool.smeltFloorStamp.AddSeconds(5))
                        {
                            Pool.smeltFloorStamp = Time32.Now;
                            var item = new MsgItem(null, 50, 55, MsgItem.ItemType.Effect, 0, 0, map.ID, 0, false, map, 60 * 60 * 1000);
                            item.MsgFloor.m_ID = 2285;
                            item.MsgFloor.m_Color = 1;
                            item.MsgFloor.DropType = MsgDropID.Effect;
                            map.EnqueueItem(item);
                            item.SendAll(new ServerSockets.RecycledPacket().GetStream(), MsgDropID.Effect);

                        }
                    }
                }
            }
            #endregion
            #region Tournaments
            Game.MsgTournaments.MsgSchedules.CheckUp();
            #endregion
            #region Desert Soldier Remains
            new Action<Role.GameMap>(p => p.CheckUpSoldierReamins()).Invoke(Pool.ServerMaps[1000]);
            #endregion
            #region Sector Traps [New Map for Perfection]
            new Action<Role.GameMap>(p => p.GenerateSectorTraps(50, 336, 1417)).Invoke(Pool.ServerMaps[3998]);
            new Action<Role.GameMap>(p => p.GenerateSectorTraps(59, 334, 1417)).Invoke(Pool.ServerMaps[3998]);
            new Action<Role.GameMap>(p => p.GenerateSectorTraps(32, 351, 1417)).Invoke(Pool.ServerMaps[3998]);
            new Action<Role.GameMap>(p => p.GenerateSectorTraps(30, 346, 1417)).Invoke(Pool.ServerMaps[3998]);
            new Action<Role.GameMap>(p => p.GenerateSectorTraps(12, 355, 1417)).Invoke(Pool.ServerMaps[3998]);
            new Action<Role.GameMap>(p => p.GenerateSectorTraps(22, 341, 1417)).Invoke(Pool.ServerMaps[3998]);
            #endregion
            #region Poker Tables
            foreach (var t in Database.Poker.Tables.Values)
                PokerHandler.PokerTablesCallback(t, 0);
            #endregion
            #region Roullette Tables
            foreach (var roullet in Database.Roulettes.RoulettesPoll.Values)
                roullet.work();
            #endregion
            #region Team Pk Now
            foreach (var teamGroup in Game.MsgTournaments.MsgTeamPkTournament.EliteGroups)
                teamGroup.timerCallback();
            #endregion
            #region Skill Team Pk Now
            foreach (var sTeamGroup in Game.MsgTournaments.MsgSkillTeamPkTournament.EliteGroups)
                sTeamGroup.timerCallback();
            #endregion
            #region Eite Pk Now
            foreach (var elitegroup in Game.MsgTournaments.MsgEliteTournament.EliteGroups)
                elitegroup.timerCallback();
            #endregion
            #region Server down
            if (DateNow.Hour == 09 && DateNow.Minute == 54 & DateNow.Second == 00)
            {
                MyConsole.WriteLine("The server will be brought down for maintenance in (5 Minutes). Please log off immediately to avoid data loss.");
                MsgMessage msg = new MsgMessage("The server will be brought down for maintenance in (5 Minutes). Please log off immediately to avoid data loss.", "ALLUSERS", "GM", MsgMessage.MsgColor.red, MsgMessage.ChatMode.Center);
                Server.SendGlobalPacket(msg.GetArray(new ServerSockets.RecycledPacket().GetStream()));
            }
            if (DateNow.Hour == 09 && DateNow.Minute == 55 & DateNow.Second == 00)
            {
                MyConsole.WriteLine("The server will be brought down for maintenance in (4 Minutes). Please log off immediately to avoid data loss.");
                MsgMessage msg = new MsgMessage("The server will be brought down for maintenance in (4 Minutes). Please log off immediately to avoid data loss.", "ALLUSERS", "GM", MsgMessage.MsgColor.red, MsgMessage.ChatMode.Center);
                Server.SendGlobalPacket(msg.GetArray(new ServerSockets.RecycledPacket().GetStream()));
            }
            if (DateNow.Hour == 09 && DateNow.Minute == 56 & DateNow.Second == 00)
            {
                MyConsole.WriteLine("The server will be brought down for maintenance in (3 Minutes). Please log off immediately to avoid data loss.");
                MsgMessage msg = new MsgMessage("The server will be brought down for maintenance in (3 Minutes). Please log off immediately to avoid data loss.", "ALLUSERS", "GM", MsgMessage.MsgColor.red, MsgMessage.ChatMode.Center);
                Server.SendGlobalPacket(msg.GetArray(new ServerSockets.RecycledPacket().GetStream()));
            }
            if (DateNow.Hour == 09 && DateNow.Minute == 57 & DateNow.Second == 00)
            {
                MyConsole.WriteLine("The server will be brought down for maintenance in (2 Minutes). Please log off immediately to avoid data loss.");
                MsgMessage msg = new MsgMessage("The server will be brought down for maintenance in (2 Minutes). Please log off immediately to avoid data loss.", "ALLUSERS", "GM", MsgMessage.MsgColor.red, MsgMessage.ChatMode.Center);
                Server.SendGlobalPacket(msg.GetArray(new ServerSockets.RecycledPacket().GetStream()));
            }
            if (DateNow.Hour == 09 && DateNow.Minute == 58 & DateNow.Second == 00)
            {
                MyConsole.WriteLine("The server will be brought down for maintenance in (1 Minutes). Please log off immediately to avoid data loss.");
                MsgMessage msg = new MsgMessage("The server will be brought down for maintenance in (1 Minutes). Please log off immediately to avoid data loss.", "ALLUSERS", "GM", MsgMessage.MsgColor.red, MsgMessage.ChatMode.Center);
                Server.SendGlobalPacket(msg.GetArray(new ServerSockets.RecycledPacket().GetStream()));
            }
            if (DateNow.Hour == 09 && DateNow.Minute == 59 & DateNow.Second == 00)
            {

                Server.SaveDatabase();
                if (Server.FullLoading && !Program.ServerConfig.IsInterServer)
                {
                    foreach (var user in Pool.GamePoll.Values)
                    {
                        if (user.OnInterServer)
                            continue;
                        if ((user.ClientFlag & Client.ServerFlag.LoginFull) == Client.ServerFlag.LoginFull)
                        {
                            user.ClientFlag |= Client.ServerFlag.QueuesSave;
                            Database.ServerDatabase.LoginQueue.TryEnqueue(user);
                        }
                    }

                    MyConsole.WriteLine("All online clients have been saved successfully.", ConsoleColor.Magenta);
                }
                if (Database.ServerDatabase.LoginQueue.Finish())
                {
                    System.Threading.Thread.Sleep(1000);
                }
                // break;
                MsgMessage msg = new MsgMessage("The server will be brought down for maintenance in (15 Seconds). Please log off immediately to avoid data loss.", "ALLUSERS", "GM", MsgMessage.MsgColor.red, MsgMessage.ChatMode.Center);
                Server.SendGlobalPacket(msg.GetArray(new ServerSockets.RecycledPacket().GetStream()));
            }
            #endregion
            #region Ganoderma
            if (DateNow.Minute == 15 && DateNow.Second == 0)//re-spawn ganoderma
            {

                var map = Pool.ServerMaps[1011];
                if (!map.ContainMobID(3130))
                {

                    using (var rec = new ServerSockets.RecycledPacket())
                    {
                        var stream = rec.GetStream();
                        Server.AddMapMonster(stream, map, 3130, 667, 753, 18, 18, 1);
#if Arabic
                                   Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The Ganodema & Titan have spawned in the forest/canyon! Hurry to kill them. Drop [Special Items 50% change.].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                     
#else
                        Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The Ganodema & Titan have spawned in the forest/canyon! Hurry to kill them. Drop [Special Items 50% change.].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));

#endif
                    }
                }
            }
            #endregion
            #region Titan
            if (DateNow.Minute == 16 && DateNow.Second == 0)//re-spawn titan
            {
                var map = Pool.ServerMaps[1020];
                if (!map.ContainMobID(3134))
                {

                    using (var rec = new ServerSockets.RecycledPacket())
                    {
                        var stream = rec.GetStream();
                        Server.AddMapMonster(stream, map, 3134, 419, 618, 18, 18, 1);
                        Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The Ganodema & Titan have spawned in the forest/canyon! Hurry to kill them. Drop [Special Items 50% change.].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                    }

                }
            }
            #endregion
            #region NemesisTyrant
            if ((DateNow.Minute == 15 || DateNow.Minute == 45) && DateNow.Second == 0)//re-spawn nemesys
            {
                var map = Pool.ServerMaps[10137];
                if (!map.ContainMobID(3978))
                {

                    using (var rec = new ServerSockets.RecycledPacket())
                    {
                        var stream = rec.GetStream();
                        Server.AddMapMonster(stream, map, 3978, 568, 372, 18, 18, 1);
#if Arabic
  Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The NemesisTyrant have spawned in the BloodShedSea on (118, 187) ! Hurry to kill them. Drop [SavageBone, DragonBalls].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));

#else
                        Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The NemesisTyrant have spawned in the BloodShedSea on (118, 187) ! Hurry to kill them. Drop [SavageBone, DragonBalls].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));

#endif

                        foreach (var user in Pool.GamePoll.Values)
                            user.Player.MessageBox(

#if Arabic
                                    "The NemesisTyrant have spawned in the BloodShedSea on (118, 187) ! Hurry to kill them. Drop [SavageBone, DragonBalls]."
#else
"The NemesisTyrant have spawned in the BloodShedSea on (118, 187) ! Hurry to kill them. Drop [SavageBone, DragonBalls]."
#endif
, new Action<Client.GameClient>(p =>
{
    p.Teleport(313, 346, 1002);
}
                                                  ), null, 60);

                    }
                }
            }
            #endregion
            #region ThrillingSpook
            if ((DateNow.Minute == 30 || DateNow.Minute == 00) && DateNow.Second == 14)
            {
                var map = Pool.ServerMaps[10137];
                if (!map.ContainMobID(3977))
                {

                    using (var rec = new ServerSockets.RecycledPacket())
                    {
                        var stream = rec.GetStream();
                        Server.AddMapMonster(stream, map, 3977, 349, 635, 18, 18, 1);
#if Arabic
  Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The ThrillingSpook have spawned in the BloodShedSea on (118, 187) ! Hurry to kill them. Drop [SavageBone, DragonBalls].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));

#else
                        Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The ThrillingSpook have spawned in the BloodShedSea on (349,635) ! Hurry to kill them. Drop [SavageBone, DragonBalls].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));

#endif

                        foreach (var user in Pool.GamePoll.Values)
                            user.Player.MessageBox(

#if Arabic
                                    "The ThrillingSpook have spawned in the BloodShedSea on (118, 187) ! Hurry to kill them. Drop [SavageBone, DragonBalls]."
#else
"The ThrillingSpook have spawned in the BloodShedSea on (349,635)  ! Hurry to kill them. Drop [SavageBone, DragonBalls]."
#endif
, new Action<Client.GameClient>(p =>
{
    p.Teleport(313, 346, 1002);
}
                                                  ), null, 60);

                    }
                }
            }
            #endregion
            #region Chaos Guard
            /* if ((DateNow.Minute == 30 || DateNow.Minute == 00) && DateNow.Second == 00)//re-spawn chaos guard
             {
                 var map = Pool.ServerMaps[1005];
                 if (!map.ContainMobID(213883))
                 {

                     using (var rec = new ServerSockets.RecycledPacket())
                     {
                         var stream = rec.GetStream();
                         Server.AddMapMonster(stream, map, 213883, 50, 50, 18, 18, 1);
                         //The ChaosGuard appeared in Arena (50,50)! Defeat it!
 #if Arabic
   Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The ChaosGuard appeared in Arena (50,50)! Defeat it!", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));

 #else
                         Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The ChaosGuard appeared in Arena (50,50)! Defeat it!", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));

 #endif

                         foreach (var user in Pool.GamePoll.Values)
                             user.Player.MessageBox(
 #if Arabic
                                     "The ChaosGuard appeared in Arena (50,50)! Defeat it!"
 #else
 "The ChaosGuard appeared in Arena (50,50)! Defeat it!"
 #endif

 , new Action<Client.GameClient>(p =>
 {
     p.Teleport(50, 50, 1005);
 }
                                                   ), null, 60);

                     }
                 }
             }*/
            #endregion
            #region Snow Banshee
            if ((DateNow.Minute == 27 || DateNow.Minute == 57) && DateNow.Second == 0)
            {
                var map = Pool.ServerMaps[10137];
                if (!map.ContainMobID(3976))
                {

                    using (var rec = new ServerSockets.RecycledPacket())
                    {
                        var stream = rec.GetStream();
                        Server.AddMapMonster(stream, map, 3976, 658, 718, 18, 18, 1);
#if Arabic
                              string Messaj = "The Snow Banshee appeared in Frozen Grotto 2(540,430)! Defeat it!";
#else
                        string Messaj = "The Snow Banshee appeared in Frozen Grotto 2(540,430)! Defeat it!";
#endif
                        //"The SnowBanshee have spawned in the FrozenGroto2 on (378,369) ! Hurry to kill them.";
                        Server.SendGlobalPacket(new Game.MsgServer.MsgMessage(Messaj, Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                        foreach (var user in Pool.GamePoll.Values)
                            user.Player.MessageBox(Messaj, new Action<Client.GameClient>(p =>
                            {
                                p.Teleport(313, 346, 1002);
                            }
                                                  ), null, 60);
                    }
                }
            }
            #endregion
            #region Saturn
            if (MsgSchedules.Saturn.Mode == ProcesType.Alive)
            {
                if (DateTime.Now.Second % 10 == 0)
                {
                    const ushort MapID = 2353;
                    var Map = Pool.ServerMaps[MapID];
                    ushort X = 0, Y = 0;
                    if (!Map.ContainMobID(20211, 0, 3))
                    {
                        Pool.ServerMaps[MapID].GetRandCoord(ref X, ref Y);

                        Map.AddMapMonster(new ServerSockets.RecycledPacket().GetStream(), 20211, X, Y, 1, 1, 1);
                        Map.SendSysMesage("Gold Box has appeared at (" + X + "," + Y + ") in Saturn. Hurry and go open the box and get it's token!");
                    }
                }
            }
            #endregion
            //#region VoteServer
            //if (DateNow.Hour == 15  && DateNow.Second < 45)//Spook
            //{
            //    using (var rec = new ServerSockets.RecycledPacket())
            //    {
            //        var stream = rec.GetStream();
            //        Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("[ VoteServer] Is About To Begin! Prize ConquerPoints].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
            //        foreach (var user in Pool.GamePoll.Values)
            //            user.Player.MessageBox("[ VoteServer ] Is About To Begin! Will You Prize [ConquerPoints] .", new Action<Client.GameClient>(p => { p.Teleport(329, 279, 1002); }), null, 60);

            //    }
            //}
            //#endregion
            #region DeityLand
            if (DateTime.Now.Second == 00)
            {
                const ushort MapID = 10250;
                var Map = Pool.ServerMaps[MapID];
                ushort X = 0, Y = 0;
                /*for (uint i = 3979; i < 3986; i++)
                {
                    if (i == 3980) i++;
                    while (!Map.ContainMobID(i, 0, 5))
                    {
                        Pool.ServerMaps[MapID].GetRandCoord(ref X, ref Y);

                        Map.AddMapMonster(new ServerSockets.RecycledPacket().GetStream(), i, X, Y, 1, 1, 1);
                    }
                }*/
                ushort mobID = 0;
                X = 0;
                Y = 0;
                switch (DateTime.Now.Minute)
                {
                    case 05:
                        {
                            mobID = 3971;
                            X = 163;
                            Y = 415;
                            break;
                        }
                    case 10:
                        {
                            if (DateTime.Now.Hour == 19 || DateTime.Now.Hour == 21)
                            {
                                mobID = 3970;
                                X = 640;
                                Y = 600;
                                Pool.ServerMaps[MapID].GetRandCoord(ref X, ref Y, 40);
                            }
                            break;
                        }
                    case 30:
                        {
                            mobID = 3977;
                            X = 1020;
                            Y = 698;
                            break;
                        }
                    case 45:
                        {
                            mobID = 3978;
                            X = 640;
                            Y = 600;
                            Pool.ServerMaps[MapID].GetRandCoord(ref X, ref Y, 40);
                            break;
                        }
                    case 57:
                        {
                            mobID = 3976;
                            X = 484;
                            Y = 176;
                            break;
                        }
                }
                if (mobID != 0)
                    if (!Map.ContainMobID(mobID))
                    {
                        Map.AddMapMonster(new ServerSockets.RecycledPacket().GetStream(), mobID, X, Y, 1, 1, 1);
                        Server.SendGlobalPacket(new Game.MsgServer.MsgMessage(Pool.MonsterFamilies[mobID].Name + " has appeared at (" + X + "," + Y + ") in the Deityland. Hurry and go kill the beast!", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.TopLeft).GetArray(new ServerSockets.RecycledPacket().GetStream()));
                        MsgSchedules.SendInvitation("" + Pool.MonsterFamilies[mobID].Name + " has appeared at (" + X + "," + Y + ") in the Deityland. Hurry and go kill the beast!", "", 1009, 1288, 10250, 0, 60);
                    }
            }
            #endregion
            #region MonsterGhost
            /*  if ((DateNow.Minute == 11) && DateNow.Second == 59)
              {

                  var map = Pool.ServerMaps[1015];
                  if (!map.ContainMobID(60979))
                  {

                      using (var rec = new ServerSockets.RecycledPacket())
                      {
                          var stream = rec.GetStream();
                          Server.AddMapMonster(stream, map, 60979, 796, 608, 18, 18, 1);
  #if Arabic
                                string Messaj = "The Snow Banshee appeared in Frozen Grotto 2(540,430)! Defeat it!";
  #else
                          string Messaj = "The Monster Ghost appeared Defeat it!";
  #endif
                          //"The SnowBanshee have spawned in the FrozenGroto2 on (378,369) ! Hurry to kill them.";
                          Server.SendGlobalPacket(new Game.MsgServer.MsgMessage(Messaj, Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                          Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("The Monster Ghost appeared Defeat it!", "ALLUSERS", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.BroadcastMessage).GetArray(stream));

                          foreach (var user in Pool.GamePoll.Values)
                              user.Player.MessageBox(Messaj, new Action<Client.GameClient>(p =>
                              {
                                  p.Teleport(795, 617, 1015);
                              }
                                                    ), null, 60);
                      }
                  }
              }*/
            #endregion
            #region Agents
            /* if ((DateTime.Now.Hour == 12 && DateTime.Now.Minute == 30 && DateTime.Now.Minute == 30 && DateTime.Now.Second == 0 || DateTime.Now.Hour == 21 && DateTime.Now.Minute == 0 && DateTime.Now.Second == 0 || DateTime.Now.Hour == 13 && DateTime.Now.Minute == 0 && DateTime.Now.Second == 0 || DateTime.Now.Hour == 22 && DateTime.Now.Minute == 30 && DateTime.Now.Second == 0 || DateTime.Now.Hour == 23 && DateTime.Now.Minute == 10 && DateTime.Now.Second == 0))
             {

                 var map = Pool.ServerMaps[3845];
                 if (!map.ContainMobID(6819))
                 {

                     using (var rec = new ServerSockets.RecycledPacket())
                     {
                         var stream = rec.GetStream();
                         Server.AddMapMonster(stream, map, 6819, 152, 55, 18, 18, 1);
 #if Arabic
                               string Messaj = "The Snow Banshee appeared in Frozen Grotto 2(540,430)! Defeat it!";
 #else
                         string Messaj = "The Agents appeared Defeat it!";
 #endif
                         //"The SnowBanshee have spawned in the FrozenGroto2 on (378,369) ! Hurry to kill them.";
                         Server.SendGlobalPacket(new Game.MsgServer.MsgMessage(Messaj, Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                         foreach (var user in Pool.GamePoll.Values)
                             user.Player.MessageBox(Messaj, new Action<Client.GameClient>(p =>
                             {
                                 p.Teleport(410, 354, 1002);
                             }
                                                   ), null, 60);
                     }
                 }
             }*/
            #endregion
            #region The Grave
            //if ((DateNow.Minute == 34) && DateNow.Second == 0)
            //{
            //    foreach (var user in Pool.GamePoll.Values)
            //        user.Player.MessageBox("The Grave is about to begin! Will you join it?", new Action<Client.GameClient>(p =>
            //        {
            //            p.Teleport(372, 363, 1002);
            //        }), null, 60);
            //}
            #endregion
            #region Best Fighter
            /*   if ((DateNow.Minute == 5) && DateNow.Second == 0)
               {
                   using (var rec = new ServerSockets.RecycledPacket())
                   {
                       var stream = rec.GetStream();
   #if Arabic
                                 string Messaj = "The Snow Banshee appeared in Frozen Grotto 2(540,430)! Defeat it!";
   #else
                       string Messaj = "BestFighter Open Now..";
   #endif
                       //"The SnowBanshee have spawned in the FrozenGroto2 on (378,369) ! Hurry to kill them.";
                       Server.SendGlobalPacket(new Game.MsgServer.MsgMessage(Messaj, Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                       Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("BestFighter Open Now..", "ALLUSERS", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.BroadcastMessage).GetArray(stream));
                       foreach (var user in Pool.GamePoll.Values)
                           user.Player.MessageBox(Messaj, new Action<Client.GameClient>(p =>
                           {
                               p.Teleport(372, 363, 1002);
                           }
                                                 ), null, 60);
                   }
               }*/
            #endregion
            #region Tops In Hour
            #region ConquerTop
            if (DateNow.Minute == 16 && DateNow.Second < 1)//Spook
            {
                using (var rec = new ServerSockets.RecycledPacket())
                {
                    var stream = rec.GetStream();
                    Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("[ ConquerTop ] Is About To Begin! Prize [ConquerPoints,StonePerfaction] .", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                    foreach (var user in Pool.GamePoll.Values)
                        user.Player.MessageBox("[ ConquerTop ] Is About To Begin! Will You Join It? Prize [ConquerPoints,StonePerfaction] .", new Action<Client.GameClient>(p => { p.Teleport(310, 249, 1002); }), null, 60);

                }
            }
            #endregion
            #region EmperorsTop
            if (DateNow.Minute == 25 && DateNow.Second < 2)//Spook
            {
                using (var rec = new ServerSockets.RecycledPacket())
                {
                    var stream = rec.GetStream();
                    Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("[ EmperorsTop ] Is About To Begin! Prize [ConquerPoints,StonePerfaction] .", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                    foreach (var user in Pool.GamePoll.Values)
                        user.Player.MessageBox("[ EmperorsTop ] Is About To Begin! Will You Join It? Prize [ConquerPoints,StonePerfaction] .", new Action<Client.GameClient>(p => { p.Teleport(310, 249, 1002); }), null, 60);

                }
            }
            #endregion
            #region FrozenTop
            if (DateNow.Minute == 32 && DateNow.Second > 5 && DateNow.Second < 10)
            {
                using (var rec = new ServerSockets.RecycledPacket())
                {
                    var stream = rec.GetStream();
                    Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("[ FrozenTop ] Is About To Begin! Prize [ConquerPoints,StonePerfaction] .", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                    foreach (var user in Pool.GamePoll.Values)
                        user.Player.MessageBox("[ FrozenTop ] Is About To Begin! Will You Join It? Prize [ConquerPoints,StonePerfaction] .", new Action<Client.GameClient>(p => { p.Teleport(310, 249, 1002); }), null, 60);

                }
            }
            #endregion
            #region LordsWar
            if (DateNow.Minute == 49 && DateNow.Second > 1 && DateNow.Second < 5)
            {
                using (var rec = new ServerSockets.RecycledPacket())
                {
                    var stream = rec.GetStream();
                    Server.SendGlobalPacket(new Game.MsgServer.MsgMessage("[ LordsWar ] Is About To Begin! Prize [ConquerPoints,StonePerfaction].", Game.MsgServer.MsgMessage.MsgColor.red, Game.MsgServer.MsgMessage.ChatMode.Center).GetArray(stream));
                    foreach (var user in Pool.GamePoll.Values)
                        user.Player.MessageBox("[ LordsWar ] Is About To Begin! Will You Join It? Prize [ConquerPoints,StonePerfaction] .", new Action<Client.GameClient>(p => { p.Teleport(310, 249, 1002); }), null, 60);

                }
            }
            #endregion
            #endregion


        }
        private static void ServerFunctions(int time)
        {
            if (Program.ExitRequested || !Server.FullLoading)
                return;
            DateTime DateNow = DateTime.Now;
            try
            {
                #region Broadcast
                Game.MsgTournaments.MsgBroadcast.Work();
                #endregion
                #region Restart
                if (DateNow.Hour == 10 && DateNow.Minute == 00)
                {
                    Program.ProcessConsoleEvent(0);

                    System.Diagnostics.Process hproces = new System.Diagnostics.Process();
                    hproces.StartInfo.FileName = "GameServer.exe";
                    hproces.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
                    hproces.Start();

                    Environment.Exit(0);
                }
                #endregion
                #region Random Seed
                if (DateNow > Pool.ResetRandom)
                {
                    Pool.GetRandom.SetSeed(Environment.TickCount);
                    Pool.ResetRandom = DateTime.Now.AddMinutes(30);
                }
                #endregion
                #region Save Database
                lock (Database.ServerDatabase.SavingObj)
                {
                    Server.SaveDBPayers();
                    foreach (var client in Pool.GamePoll.Values)
                    {
                        if (client.OnInterServer || client.Socket == null || !client.Socket.Alive)
                            continue;
                        if ((client.ClientFlag & Client.ServerFlag.LoginFull) == Client.ServerFlag.LoginFull)
                        {
                            client.ClientFlag |= Client.ServerFlag.QueuesSave;
                            Database.ServerDatabase.LoginQueue.TryEnqueue(client);
                        }
                        System.Threading.Thread.Sleep(1);
                    }
                }
                #endregion
                #region Reset Server
                Server.Reset();
                #endregion
            }
            catch (Exception e) { MyConsole.WriteException(e); }
        }
        public static void connectionReceive(ServerSockets.SecuritySocket wrapper, int time)
        {
            if (wrapper.ReceiveBuffer())
            {
                wrapper.HandlerBuffer();
            }
        }
        public static void connectionSend(ServerSockets.SecuritySocket wrapper, int time)
        {
            ServerSockets.SecuritySocket.TrySend(wrapper);
        }
        public static void _ConnectionReview(ServerSockets.SecuritySocket wrapper, int time)
        {
            ServerSockets.SecuritySocket.TryReview(wrapper);
        }
        #region Funcs
        public static void Execute(Action<int> action, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            GenericThreadPool.Subscribe(new LazyDelegate(action, timeOut, priority));
        }
        public static void Execute<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            GenericThreadPool.Subscribe<T>(new LazyDelegate<T>(action, timeOut, priority), param);
        }
        public static IDisposable Subscribe(Action<int> action, int period = 1, ThreadPriority priority = ThreadPriority.Normal)
        {
            return GenericThreadPool.Subscribe(new TimerRule(action, period, priority));
        }
        public static IDisposable Subscribe<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            return GenericThreadPool.Subscribe<T>(new TimerRule<T>(action, timeOut, priority), param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StandalonePool pool)
        {
            return pool.Subscribe<T>(rule, param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StaticPool pool)
        {
            return pool.Subscribe<T>(rule, param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param)
        {
            return GenericThreadPool.Subscribe<T>(rule, param);
        }
        #endregion
    }
}