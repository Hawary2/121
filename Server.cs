using GameServer.Game.MsgFloorItem;
using GameServer.Game.MsgServer;
using GameServer.Game.MsgTournaments.ClassPoleWar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GameServer.Database;

namespace GameServer
{
    public class Server
    {
        public static bool FullLoading = false;
        public static void SendGlobalPacket(ServerSockets.Packet data)
        {
            var array = Pool.GamePoll.Values.ToArray();
            foreach (var user in Pool.GamePoll.Values)
            {
                user.Send(data);
            }
        }
        public static uint ResetServerDay = 0;
        public static unsafe void Reset()
        {
            if (DateTime.Now.DayOfYear != ResetServerDay)
            {
                try
                {
                    Pool.Arena.ResetArena();
                    Pool.TeamArena.ResetArena();

                    foreach (var flowerclient in Role.Instance.Flowers.ClientPoll.Values)
                    {
                        foreach (var flower in flowerclient)
                            flower.Amount2day = 0;
                    }
                    using (var rec = new ServerSockets.RecycledPacket())
                    {
                        var stream = rec.GetStream();

                        foreach (var client in Pool.GamePoll.Values)
                        {


                            client.Player.TodayChampionPoints = 0;

                            client.Player.ChangeEpicTrojan = client.Player.ChangeArrayEpicTrojan =
                    client.Player.ChangeMr_MirrorEpicTrojan = client.Player.ChangeGeneralPakEpicTrojan = 0;
                            client.Player.CanChangeEpicMaterial = client.Player.CanChangeArrayEpicMaterial =
                                client.Player.CanChangeMr_MirrorEpicMaterial = client.Player.CanChangGeneralPakMaterial = 1;

                            client.Player.UseChiToken = 0;

                            client.Player.RamdanBag = 0;
                            client.Player.TowerOfMysterychallenge = 3;
                            client.Player.TOMChallengeToday = 0;
                            client.Player.TowerOfMysteryChallengeFlag = 0;
                            client.Player.TOMSelectChallengeToday = 0;
                            client.Player.ClaimTowerAmulets = 0;
                            client.Player.TOMClaimTeamReward = 0;
                            client.Player.TOMRefreshReward = 0;
                            client.Player.QuestGUI.RemoveQuest(6126);




                            client.Player.OpenHousePack = 0;
                            client.MyExchangeShop.Reset();
                            if (client.Player.DailyMonth == 0)
                                client.Player.DailyMonth = (byte)DateTime.Now.Month;
                            if (client.Player.DailyMonth != DateTime.Now.Month)
                            {
                                client.Player.DailySignUpRewards = 0;
                                client.Player.DailySignUpDays = 0;
                                client.Player.DailyMonth = (byte)DateTime.Now.Month;
                            }
                            if (client.Player.MyJiangHu != null)
                            {
                                client.Player.MyJiangHu.FreeCourse = 30000;
                                client.Player.MyJiangHu.FreeTimeToday = 0;
                                client.Player.MyJiangHu.RoundBuyPoints = 0;
                            }
                            client.Player.ArenaKills = client.Player.ArenaDeads = 0;
                            client.Player.HitShoot = client.Player.MisShoot = 0;

                            client.Player.DbTry = false;
                            client.Player.LotteryEntries = 0;
                            client.Player.Day = DateTime.Now.DayOfYear;
                            client.Player.BDExp = 0;
                            client.Player.TCCaptainTimes = 0;
                            client.Player.ExpBallUsed = 0;
                            client.Player.VotePoints = 0;
                            client.DemonExterminator.FinishToday = 0;

                            if (client.Player.MyChi != null)
                            {
                                client.Player.MyChi.ChiPoints = client.Player.MyChi.ChiPoints + 300;
                                Game.MsgServer.MsgChiInfo.MsgHandleChi.SendInfo(client, Game.MsgServer.MsgChiInfo.Action.Upgrade);
                            }
                            client.Player.Flowers.FreeFlowers = 1;
                            foreach (var flower in client.Player.Flowers)
                                flower.Amount2day = 0;


                            if (client.Player.Flowers.FreeFlowers > 0)
                            {
                                client.Send(stream.FlowerCreate(Role.Core.IsBoy(client.Player.Body)
                                    ? Game.MsgServer.MsgFlower.FlowerAction.FlowerSender
                                    : Game.MsgServer.MsgFlower.FlowerAction.Flower
                                    , 0, 0, client.Player.Flowers.FreeFlowers));
                            }

                            if (client.Player.Level >= 90)
                            {
                                client.Player.Enilghten = ServerDatabase.CalculateEnlighten(client.Player);
                                client.Player.SendUpdate(stream, client.Player.Enilghten, Game.MsgServer.MsgUpdate.DataType.EnlightPoints);
                            }

                            client.Player.BuyKingdomDeeds = 0;
                            client.Player.QuestGUI.RemoveQuest(35024);
                            client.Player.QuestGUI.RemoveQuest(35007);
                            client.Player.QuestGUI.RemoveQuest(35025);
                            client.Player.QuestGUI.RemoveQuest(35028);
                            client.Player.QuestGUI.RemoveQuest(35034);

                            //---- reset Quests
                            client.Player.QuestGUI.RemoveQuest(6390);
                            client.Player.QuestGUI.RemoveQuest(6329);
                            client.Player.QuestGUI.RemoveQuest(6245);
                            client.Player.QuestGUI.RemoveQuest(6049);
                            client.Player.QuestGUI.RemoveQuest(6366);
                            client.Player.QuestGUI.RemoveQuest(6014);
                            client.Player.QuestGUI.RemoveQuest(2375);
                            client.Player.QuestGUI.RemoveQuest(6126);
                            client.Player.DailyHeavenChance = client.Player.DailyMagnoliaChance
                                = client.Player.DailyMagnoliaItemId
                                = client.Player.DailyHeavenChance = client.Player.DailySpiritBeadCount = client.Player.DailyRareChance = 0;
                            //
                        }
                    }
                    ResetServerDay = (uint)DateTime.Now.DayOfYear;
                }
                catch (Exception e) { MyConsole.WriteLine(e.ToString()); }
            }
        }
        public static void SaveDBPayers()
        {
            if (!Program.ServerConfig.IsInterServer)
            {
               SaveDatabase();
            }
        }
        public static void Initialize()
        {
            MyConsole.WriteLine("Loading Server...", ConsoleColor.Magenta);
            Pool.ServerMaps = new MapDictionary<uint, Role.GameMap>();
            Pool.GamePoll = new ConcurrentDictionary<uint, Client.GameClient>();
            Pool.NameUsed = new List<int>();

            WindowsAPI.IniFile IniFile = new WindowsAPI.IniFile(System.IO.Directory.GetCurrentDirectory() + "\\shell.ini", true);
            Program.ServerConfig.IPAddres = IniFile.ReadString("ServerInfo", "AddresIP", "");
            Program.ServerConfig.GamePort = IniFile.ReadUInt16("ServerInfo", "Game_Port", 0);
            Program.ServerConfig.ServerName = IniFile.ReadString("ServerInfo", "ServerName", "");
            Program.ServerConfig.IsInterServer = IniFile.ReadBool("ServerInfo", "IsInterServer", false);

            Program.ServerConfig.Port_BackLog = IniFile.ReadUInt16("InternetPort", "BackLog", 100);
            Program.ServerConfig.Port_ReceiveSize = IniFile.ReadUInt16("InternetPort", "ReceiveSize", 4096);
            Program.ServerConfig.Port_SendSize = IniFile.ReadUInt16("InternetPort", "SendSize", 2048);

            Program.ServerConfig.DbLocation = IniFile.ReadString("Database", "Location", "");
            Program.ServerConfig.CO2Folder = IniFile.ReadString("Database", "CO2FOLDER", "");


            GameServer.DBFunctionality.DataHolder.CreateConnection("localhost", "root", "3243418..", "regear");

            Pool.MsgInvoker = new GameServer.CachedAttributeInvocation<System.Action<GameServer.Client.GameClient, GameServer.ServerSockets.Packet>, GameServer.PacketAttribute, ushort>(PacketAttribute.Translator);
            Pool.RebornInfo = new RebornInfomations();
            Pool.RebornInfo.Load();

            Pool.ITEM_Counter.Set(IniFile.ReadUInt32("Database", "ItemUID", 0));
            uint nextitem = Pool.ITEM_Counter.Next;
            Pool.ClientCounter.Set(IniFile.ReadUInt32("Database", "ClientUID", 1000000));
            Pool.DominoCounter.Set(IniFile.ReadUInt32("Database", "DominoUID", 300000000));
            uint nextclient = Pool.ClientCounter.Next;
            Game.MsgTournaments.MsgSchedules.Create();
            Game.MsgTournaments.MsgSchedules.PkWar.WinnerUID = IniFile.ReadUInt32("Tournaments", "PkWarWinner", 0);
            ResetServerDay = IniFile.ReadUInt32("Database", "Day", 0);

            Pool.ItemsBase = new ItemType();
            Pool.RifineryItems = new Rifinery();
            Pool.DBRerinaryBoxes = new RefinaryBoxes();
            Pool.ItemsBase.Loading();

            //-------------------------- Load shops -------------------
            Database.Shops.ChampionShop.Load();
            Database.Shops.EShopFile.Load();
            Database.Shops.EShopV2File.Load();
            Database.Shops.HonorShop.Load();
            Database.Shops.RacePointShop.Load();
            Database.Shops.ShopFile.Load();
            //--------------------------
            Relic.cq_xuanbao_compose_attr_limit.Load();
            SystemBanned.Load();
            SystemBannedAccount.Load();

            LoadExpInfo();
            DataCore.AtributeStatus.Load();
            Role.GameMap.LoadMaps();
            Pool.Magic.Load();
            LoadMonsters();
            Tranformation.Int();
            Database.Poker.Load();
            QuestInfo.Init();
            Pool.SubClassInfo.Load();
            BeastsAtrribute.Load();
            Database.RuneRank.Load();
            Database.HairfaceStorageTable.Load();
            RuneTable.Load();
            Database.HWRank.Load();
            Database.HundredWeapons.Load();
            MeltingTypeTable.Load();
            FateExpTable.Load();
            SpiritTable.Load();
            ChiTable.Load();
            FlowersTable.Load();
            NobilityTable.Load();
            Role.Instance.Associate.Load();
            CoatStorage.Load();
            NinjaFile.Load();
            NinjaRank.Load();
          Game.MsgTournaments.MsgSchedules.GuildWar.CreateFurnitures();
            Game.MsgTournaments.MsgSchedules.GuildWar.Load();
            Game.MsgTournaments.MsgSchedules.SuperGuildWar.CreateFurnitures();
            Game.MsgTournaments.MsgSchedules.SuperGuildWar.Load();
            Game.MsgTournaments.MsgSchedules.EliteGuildWar.Load();
            Game.MsgTournaments.MsgSchedules.ChampionsOfWarr.Load();
            Game.MsgTournaments.MsgSchedules.TopWarScore.Load();
            Game.MsgTournaments.MsgEmperorWar.Load();
            Game.MsgTournaments.MsgWarOfPlayers.Load();
            Game.MsgTournaments.ClassPoleWar.MsgWarriorGod.Load();
            Game.MsgTournaments.ClassPoleWar.MsgArcherMaster.Load();
            Game.MsgTournaments.ClassPoleWar.MsgNinjaMaster.Load();
            Game.MsgTournaments.ClassPoleWar.MsgTrojanMaster.Load();
            Game.MsgTournaments.ClassPoleWar.MsgTaoistMaster.Load();
            Game.MsgTournaments.ClassPoleWar.MsgFireMaster.Load();
            Game.MsgTournaments.ClassPoleWar.MsgPirateMaster.Load();
            Game.MsgTournaments.ClassPoleWar.MsgMonkMaster.Load();
            Game.MsgTournaments.ClassPoleWar.MsgKungfuKing.Load();
            Game.MsgTournaments.ClassPoleWar.MsgWindWalker.Load();
            Game.MsgTournaments.ClassPoleWar.MsgClassPolaWar.Load();
            Game.MsgTournaments.MsgSchedules.VolcanoWar.Load();
            Game.MsgTournaments.MsgWarFighters.Load();
            LeagueTable.Load();
            GuildTable.Load();
            Database.ClanTable.Load();
            JianHuTable.LoadStatus();
            JianHuTable.LoadJiangHu();
            TaskRewards.Load();
            InnerPowerTable.LoadDBInformation();
            ExchangeShop.LoadDBInfo();
            QuizShow.Load();
            Game.MsgTournaments.MsgSchedules.ClassPkWar.Load();
            Game.MsgTournaments.MsgSchedules.ElitePkTournament.Load();
            Game.MsgTournaments.MsgSchedules.TeamPkTournament.Load();
            Game.MsgTournaments.MsgSchedules.SkillTeamPkTournament.Load();
            Database.PrestigeRanking.Load();
            RankItems.LoadAllItems();
            TitleStorage.LoadDBInformation();
            ItemRefineUpgrade.Load();
            Database.ProfessionTable.Load();
            Roulettes.Load();
            TheCrimeTable.Load();
            Pool.ActivityTasks.Load();
            Role.Statue.Load();
            Role.KOBoard.KOBoardRanking.Load();
            Pool.TableHeroRewards.LoadInformations();
            Database.Disdain.Load();
            Booths.Load();
            RechargeShop.Load();
            Pool.Arena.Load();
            Pool.TeamArena.Load();
            InnerPowerTable.Load();

            var tournament = Game.MsgTournaments.MsgSchedules.Tournaments[Game.MsgTournaments.TournamentType.BattleField];
            (tournament as Game.MsgTournaments.MsgBattleField).Load();

            MsgInterServer.Instance.CrossElitePKTournament.Load();

            Database.TutorInfo.Load();

            InfoDemonExterminators.Create();
            Pool.Lottery.LoadLotteryItems();
            Pool.QueueContainer.Load();
            GroupServerList.Load();
            VoteSystem.Load();
            Database.RanksTable.Initialize();

          //  Database.NpcServer.LoadServerTraps();
            Server.LoadDatabase();
            Pool.Insults.Load();
        }
        public static void ColletResource()
        {
            /*  Console.WriteLine("Memory used before collection:       {0:N0}",
                        GC.GetTotalMemory(false));

              // Collect all generations of memory.
              GC.Collect();
              Console.WriteLine("Memory used after full collection:   {0:N0}",
                                GC.GetTotalMemory(true));*/
        }
        public static void LoadMapName(uint id)
        {

            if (System.IO.File.Exists(Program.ServerConfig.DbLocation + "GameMapEx.ini"))
            {
                WindowsAPI.IniFile ini = new WindowsAPI.IniFile("GameMapEx.ini");
                Pool.ServerMaps[id].Name = ini.ReadString(id.ToString(), "Name", Program.ServerConfig.ServerName);
            }
        }
        public static void LoadExpInfo()
        {
            if (System.IO.File.Exists(Program.ServerConfig.DbLocation + "levexp.txt"))
            {
                using (System.IO.StreamReader read = System.IO.File.OpenText(Program.ServerConfig.DbLocation + "levexp.txt"))
                {
                    while (true)
                    {
                        string GetLine = read.ReadLine();
                        if (GetLine == null) return;
                        string[] line = GetLine.Split(' ');
                        DBLevExp exp = new DBLevExp();
                        exp.Action = (DBLevExp.Sort)byte.Parse(line[0]);
                        exp.Level = byte.Parse(line[1]);
                        exp.Experience = ulong.Parse(line[2]);
                        exp.UpLevTime = int.Parse(line[3]);
                        exp.MentorUpLevTime = int.Parse(line[4]);

                        if (!Pool.LevelInfo.ContainsKey(exp.Action))
                            Pool.LevelInfo.Add(exp.Action, new Dictionary<byte, DBLevExp>());

                        Pool.LevelInfo[exp.Action].Add(exp.Level, exp);

                    }
                }
            }
            GC.Collect();
        }
        public static void LoadMonsters(uint id = 0)
        {
            try
            {
                WindowsAPI.IniFile ini = new WindowsAPI.IniFile("");
                if (System.IO.Directory.Exists(Program.ServerConfig.DbLocation + "\\MonsterSpawns\\" + id.ToString() + "\\"))
                    foreach (string fmap in System.IO.Directory.GetDirectories(Program.ServerConfig.DbLocation + "\\MonsterSpawns\\" + id.ToString() + "\\"))
                    {
                        Game.MsgMonster.MobCollection colletion = new Game.MsgMonster.MobCollection(id);
                        if (colletion.ReadMap())
                        {
                            foreach (string fmobtype in System.IO.Directory.GetDirectories(fmap))
                            {
                                foreach (string ffile in System.IO.Directory.GetFiles(fmobtype))
                                {
                                    ini.FileName = ffile;
                                    colletion.LocationSpawn = ffile;

                                    uint ID = ini.ReadUInt32("cq_generator", "npctype", 0);

                                    Game.MsgMonster.MonsterFamily famil;
                                    if (!Pool.MonsterFamilies.TryGetValue(ID, out famil))
                                    {
                                        continue;
                                    }
                                    if (Game.MsgMonster.MonsterRole.SpecialMonsters.Contains(famil.ID))
                                        continue;
                                    Game.MsgMonster.MonsterFamily Monster = famil.Copy();

                                    Monster.SpawnX = ini.ReadUInt16("cq_generator", "bound_x", 0);
                                    Monster.SpawnY = ini.ReadUInt16("cq_generator", "bound_y", 0);
                                    Monster.MaxSpawnX = (ushort)(Monster.SpawnX + ini.ReadUInt16("cq_generator", "bound_cx", 0));
                                    Monster.MaxSpawnY = (ushort)(Monster.SpawnY + ini.ReadUInt16("cq_generator", "bound_cy", 0));
                                    Monster.MapID = ini.ReadUInt32("cq_generator", "mapid", 0);
                                    Monster.SpawnCount = ini.ReadByte("cq_generator", "max_per_gen", 0);//"maxnpc", 0);//max_per_gen", 0);
                                    Monster.rest_secs = ini.ReadInt32("cq_generator", "rest_secs", 0);


                                    if (Monster.MapID == 1011 || Monster.MapID == 3071 || Monster.MapID == 1770 || Monster.MapID == 1771 || Monster.MapID == 1772
                                        || Monster.MapID == 1773 || Monster.MapID == 1774 || Monster.MapID == 1775 || Monster.MapID == 1777
                                        || Monster.MapID == 1782 || Monster.MapID == 1785 || Monster.MapID == 1786 || Monster.MapID == 1787
                                        || Monster.MapID == 1794)
                                        Monster.SpawnCount = ini.ReadByte("cq_generator", "maxnpc", 0);
                                    colletion.Add(Monster);
                                }
                            }
                        }
                    }
                //  LoadMapMonsters("Monsters1015.txt");
                GC.Collect();
            }
            catch (Exception e) { MyConsole.WriteLine(e.ToString()); }
        }
        public static void LoadMyMonsters(uint id)
        {
            try
            {
                Game.MsgMonster.MobCollection colletion = new Game.MsgMonster.MobCollection(id);
                if (colletion.ReadMap())
                {
                    string[] text = System.IO.File.ReadAllLines(Program.ServerConfig.DbLocation + "MonsterSpawns\\myMonsters.txt");
                    foreach (string line in text)
                    {
                        string[] data = line.Split(' ');
                        ushort MapID = (ushort)long.Parse(data[1]);
                        if (id != MapID) continue;
                        uint monsterID = (uint)long.Parse(data[9]);
                        ushort CircleDiameter = (ushort)long.Parse(data[6]);
                        ushort X = (ushort)long.Parse(data[2]);
                        ushort Y = (ushort)long.Parse(data[3]);
                        ushort XPlus = (ushort)long.Parse(data[4]);
                        ushort YPlus = (ushort)long.Parse(data[5]);
                        int Amount = (int)long.Parse(data[8]);
                        uint respawn = (uint)long.Parse(data[7]);

                        Game.MsgMonster.MonsterFamily famil;
                        if (!Pool.MonsterFamilies.TryGetValue(monsterID, out famil))
                        {
                            continue;
                        }
                        if (Game.MsgMonster.MonsterRole.SpecialMonsters.Contains(famil.ID))
                            continue;
                        Game.MsgMonster.MonsterFamily Monster = famil.Copy();

                        Monster.SpawnX = X;
                        Monster.SpawnY = Y;
                        Monster.MaxSpawnX = (ushort)(X + XPlus);
                        Monster.MaxSpawnY = (ushort)(Y + YPlus);
                        Monster.MapID = MapID;
                        Monster.SpawnCount = (byte)Amount;
                        Monster.rest_secs = (int)respawn;
                        colletion.Add(Monster);
                    }
                }
                //  LoadMapMonsters("Monsters1015.txt");
                GC.Collect();
            }
            catch (Exception e) { MyConsole.WriteLine(e.ToString()); }
        }
        public static void LoadMonsters()
        {
            try
            {
                WindowsAPI.IniFile ini = new WindowsAPI.IniFile("");
                foreach (string fname in System.IO.Directory.GetFiles(Program.ServerConfig.DbLocation + "\\Monsters\\"))
                {
                    ini.FileName = fname;
                    Game.MsgMonster.MonsterFamily Family = new Game.MsgMonster.MonsterFamily();
                    Family.ID = ini.ReadUInt32("cq_monstertype", "id", 0);
                    Family.Name = ini.ReadString("cq_monstertype", "name", "INVALID_MOB");

                    Family.Level = ini.ReadUInt16("cq_monstertype", "level", 0);
                    Family.MaxAttack = ini.ReadInt32("cq_monstertype", "attack_max", 0);
                    Family.MinAttack = ini.ReadInt32("cq_monstertype", "attack_min", 0);
                    if (Family.Name == "INVALID_MOB" || Family.Level == 0 || Family.ID == 0 || Family.MinAttack > Family.MaxAttack)
                    {
                        MyConsole.WriteLine("[Error] Error Reading Monster File: " + fname + "", ConsoleColor.Magenta);
                        continue;
                    }
                    Family.Defense = ini.ReadUInt16("cq_monstertype", "defence", 0);
                    Family.Mesh = ini.ReadUInt16("cq_monstertype", "lookface", 0);
                    Family.MaxHealth = ini.ReadInt32("cq_monstertype", "life", 0);
                    Family.ViewRange = 16;
                    Family.AttackRange = ini.ReadSByte("cq_monstertype", "attack_range", 0);
                    Family.Dodge = ini.ReadByte("cq_monstertype", "dodge", 0);
                    Family.DropBoots = ini.ReadByte("cq_monstertype", "drop_shoes", 0);
                    Family.DropNecklace = ini.ReadByte("cq_monstertype", "drop_necklace", 0);
                    Family.DropRing = ini.ReadByte("cq_monstertype", "drop_ring", 0);
                    Family.DropArmet = ini.ReadByte("cq_monstertype", "drop_armet", 0);
                    Family.DropArmor = ini.ReadByte("cq_monstertype", "drop_armor", 0);
                    Family.DropShield = ini.ReadByte("cq_monstertype", "drop_shield", 0);
                    Family.DropWeapon = ini.ReadByte("cq_monstertype", "drop_weapon", 0);
                    Family.DropMoney = ini.ReadUInt16("cq_monstertype", "drop_money", 0);
                    Family.DropHPItem = ini.ReadUInt32("cq_monstertype", "drop_hp", 0);
                    Family.DropMPItem = ini.ReadUInt32("cq_monstertype", "drop_mp", 0);
                    Family.Boss = ini.ReadByte("cq_monstertype", "Boss", 0);
                    Family.Defense2 = ini.ReadInt32("cq_monstertype", "defence2", 0);
                    if (Family.Boss != 0)
                        Family.AttackRange = 3;
                    //defence2

                    //if (Family.Boss != 0)
                    //    MyConsole.WriteLine(Family.Name);
                    //if (Family.Dodge > 50)
                    //    MyConsole.WriteLine(Family.Name);
                    Family.MoveSpeed = ini.ReadInt32("cq_monstertype", "move_speed", 0);
                    Family.AttackSpeed = ini.ReadInt32("cq_monstertype", "attack_speed", 0);
                    Family.SpellId = ini.ReadUInt32("cq_monstertype", "magic_type", 0);

                    Family.ExtraCritical = ini.ReadUInt32("cq_monstertype", "critical", 0);
                    Family.ExtraBreack = ini.ReadUInt32("cq_monstertype", "break", 0);

                    Family.extra_battlelev = ini.ReadInt32("cq_monstertype", "extra_battlelev", 0);
                    Family.extra_exp = ini.ReadInt32("cq_monstertype", "extra_exp", 0);
                    Family.extra_damage = ini.ReadInt32("cq_monstertype", "extra_damage", 0);


                    if (Family.Boss == 0 && Family.MaxAttack > 3000)
                    {
                        Family.MaxAttack = Family.MaxAttack / 2;
                        Family.MinAttack = Family.MinAttack / 2;
                    }

                    Family.DropSpecials = new Game.MsgMonster.SpecialItemWatcher[ini.ReadInt32("SpecialDrop", "Count", 0)];
                    for (int i = 0; i < Family.DropSpecials.Length; i++)
                    {
                        string[] Data = ini.ReadString("SpecialDrop", i.ToString(), "", 32).Split(',');

                        Family.DropSpecials[i] = new Game.MsgMonster.SpecialItemWatcher(uint.Parse(Data[0]), int.Parse(Data[1]));
                    }

                    Family.CreateItemGenerator();
                    Family.CreateMonsterSettings();
                    Pool.MonsterFamilies.Add(Family.ID, Family);
                }
            }
            catch (Exception e) { MyConsole.WriteLine(e.ToString()); }
        }
        public static void LoadMapMonsters(string file)
        {
            if (System.IO.File.Exists(Program.ServerConfig.DbLocation + "MonsterSpawns\\" + file + ""))
            {
                using (System.IO.StreamReader read = System.IO.File.OpenText(Program.ServerConfig.DbLocation + "MonsterSpawns\\" + file + ""))
                {
                    while (true)
                    {

                        string aline = read.ReadLine();
                        if (aline != null && aline != "")
                        {
                            try
                            {
                                string[] line = aline.Split(',');
                                string name = line[2];
                                if (name.Contains("Titan"))
                                    continue;
                                ushort X = ushort.Parse(line[3]);
                                ushort Y = ushort.Parse(line[4]);
                                uint Map = uint.Parse(line[5]);
                                var GMap = Pool.ServerMaps[Map];
                                if (GMap == null) return;
                                if (GMap.MonstersColletion == null)
                                {
                                    GMap.MonstersColletion = new Game.MsgMonster.MobCollection(GMap.ID);
                                }
                                else if (GMap.MonstersColletion.DMap == null)
                                    GMap.MonstersColletion.DMap = GMap;
                                foreach (var _monster in Pool.MonsterFamilies.Values)
                                {
                                    /* if (Map == 1000)
                                     {
                                         if (_monster.Name == "RockMonster" && _monster.ID == 51)
                                         {
                                             Game.MsgMonster.MonsterFamily Monster = _monster.Copy();

                                             Monster.SpawnX = X;
                                             Monster.SpawnY = Y;
                                             Monster.MaxSpawnX = (ushort)(X + 1);
                                             Monster.MaxSpawnY = (ushort)(Y + 1);
                                             Monster.MapID = GMap.ID;
                                             Monster.SpawnCount = 1;

                                             Game.MsgMonster.MonsterRole rolemonster = GMap.MonstersColletion.Add(Monster, false, 0, true);
                                             break;
                                         }
                                         else*/
                                    if (_monster.Name == name)
                                    {

                                        Game.MsgMonster.MonsterFamily Monster = _monster.Copy();

                                        Monster.SpawnX = X;
                                        Monster.SpawnY = Y;
                                        Monster.MaxSpawnX = (ushort)(X + 1);
                                        Monster.MaxSpawnY = (ushort)(Y + 1);
                                        Monster.MapID = GMap.ID;
                                        Monster.SpawnCount = 1;

                                        Game.MsgMonster.MonsterRole rolemonster = GMap.MonstersColletion.Add(Monster, false, 0, true);
                                        break;
                                    }


                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                                break;
                            }
                        }
                        else
                            break;

                    }
                }
            }
        }
        public unsafe static void AddMapMonster(ServerSockets.Packet stream, Role.GameMap map, uint ID, ushort x, ushort y, ushort max_x, ushort max_y, byte count, uint DinamicID = 0, bool RemoveOnDead = false
            , Game.MsgFloorItem.MsgItemPacket.EffectMonsters m_effect = Game.MsgFloorItem.MsgItemPacket.EffectMonsters.None, string streffect = "")
        {
            if (map.MonstersColletion == null)
            {
                map.MonstersColletion = new Game.MsgMonster.MobCollection(map.ID);
            }
            if (map.MonstersColletion.ReadMap())
            {

                Game.MsgMonster.MonsterFamily famil;
                if (Pool.MonsterFamilies.TryGetValue(ID, out famil))
                {
                    Game.MsgMonster.MonsterFamily Monster = famil.Copy();

                    Monster.SpawnX = x;
                    Monster.SpawnY = y;
                    Monster.MaxSpawnX = (ushort)(x + max_x);
                    Monster.MaxSpawnY = (ushort)(y + max_y);
                    Monster.MapID = map.ID;
                    Monster.SpawnCount = count;
                    Game.MsgMonster.MonsterRole rolemonster = map.MonstersColletion.Add(Monster, RemoveOnDead, DinamicID, true);

                    if (rolemonster == null)
                    {
                        MyConsole.WriteLine("[Error] Add Map Monster (" + ID.ToString() + ")", ConsoleColor.Magenta);
                        return;
                    }
                    //   map.View.EnterMap<Role.IMapObj>(rolemonster);

                    Game.MsgServer.ActionQuery action = new Game.MsgServer.ActionQuery()
                    {
                        ObjId = rolemonster.UID,
                        Type = Game.MsgServer.ActionType.ReviveMonster,
                        PositionX = rolemonster.X,
                        PositionY = rolemonster.Y
                    };
                    rolemonster.Send(stream.ActionCreate(action));
                    rolemonster.Send(rolemonster.GetArray(stream, false));

                    if (streffect != null)
                    {
                        rolemonster.SendString(stream, MsgStringPacket.StringID.Effect, streffect);
                    }



                    if (m_effect != Game.MsgFloorItem.MsgItemPacket.EffectMonsters.None && rolemonster != null)
                    {
                        Game.MsgFloorItem.MsgItemPacket effect = Game.MsgFloorItem.MsgItemPacket.Create();
                        effect.m_UID = (uint)m_effect;
                        effect.m_X = rolemonster.X;
                        effect.m_Y = rolemonster.Y;
                        effect.DropType = MsgDropID.Earth;
                        rolemonster.Send(stream.ItemPacketCreate(effect));
                        rolemonster.SendString(stream, Game.MsgServer.MsgStringPacket.StringID.Effect, "glebesword");
                    }
                    if (rolemonster.HitPoints > 65535)
                    {
                        Game.MsgServer.MsgUpdate Upd = new Game.MsgServer.MsgUpdate(stream, rolemonster.UID, 1);
                        stream = Upd.Append(stream, Game.MsgServer.MsgUpdate.DataType.MaxHitpoints, rolemonster.Family.MaxHealth);
                        stream = Upd.GetArray(stream);
                        rolemonster.Send(stream);
                        stream = Upd.Append(stream, Game.MsgServer.MsgUpdate.DataType.Hitpoints, rolemonster.HitPoints);
                        stream = Upd.GetArray(stream);
                        rolemonster.Send(stream);
                    }
                }
            }
        }
        public unsafe static bool AddFloor(ServerSockets.Packet stream, Role.GameMap map, uint ID, ushort x, ushort y, ushort spelllevel, Database.MagicType.Magic dbspell, Client.GameClient Owner, uint GuildID, uint OwnerUID, uint DinamicID = 0, string Name = "", bool RemoveOnDead = true)
        {
            try
            {
                if (map.MonstersColletion == null)
                {
                    map.MonstersColletion = new Game.MsgMonster.MobCollection(map.ID);
                }
                if (map.MonstersColletion.ReadMap())
                {

                    Game.MsgMonster.MonsterFamily famil;
                    if (Pool.MonsterFamilies.TryGetValue(1, out famil))
                    {
                        Game.MsgMonster.MonsterFamily Monster = famil.Copy();

                        Monster.SpawnX = x;
                        Monster.SpawnY = y;
                        Monster.MaxSpawnX = (ushort)(x + 1);
                        Monster.MaxSpawnY = (ushort)(y + 1);
                        Monster.MapID = map.ID;
                        Monster.SpawnCount = 1;
                        Game.MsgMonster.MonsterRole rolemonster = map.MonstersColletion.Add(Monster, RemoveOnDead, DinamicID, true);
                        if (rolemonster == null)
                        {
                            //invalid x ,y
                            return false;
                        }
                        rolemonster.Family.ID = ID;
                        rolemonster.IsFloor = true;
                        rolemonster.FloorStampTimer = DateTime.Now.AddSeconds(7);
                        rolemonster.Family.Settings = Game.MsgMonster.MonsterSettings.Lottus;

                        rolemonster.FloorPacket = new MsgItemPacket();
                        rolemonster.FloorPacket.m_UID = rolemonster.UID;
                        rolemonster.FloorPacket.m_ID = ID;
                        rolemonster.FloorPacket.m_X = x;
                        rolemonster.FloorPacket.m_Y = y;
                        rolemonster.FloorPacket.MaxLife = 25;
                        rolemonster.FloorPacket.Life = 25;
                        rolemonster.FloorPacket.DropType = MsgDropID.Effect;
                        rolemonster.FloorPacket.m_Color = 13;
                        rolemonster.FloorPacket.ItemOwnerUID = OwnerUID;
                        rolemonster.FloorPacket.GuildID = GuildID;
                        rolemonster.FloorPacket.FlowerType = 2;//2;
                        rolemonster.FloorPacket.Timer = Role.Core.TqTimer(rolemonster.FloorStampTimer);
                        rolemonster.FloorPacket.Name = Name;

                        rolemonster.DBSpell = dbspell;
                        rolemonster.Family.MaxHealth = 25;
                        rolemonster.HitPoints = 25;
                        rolemonster.OwnerFloor = Owner;
                        rolemonster.SpellLevel = spelllevel;


                        if (rolemonster == null)
                        {
                            Console.WriteLine("Eror monster spawn. Server.");
                            return false;
                        }
                        map.View.EnterMap<Role.IMapObj>(rolemonster);
                        rolemonster.Send(rolemonster.GetArray(stream, false));
                        return true;
                    }
                }
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }
            return false;

        }
        public unsafe static void LoadDatabase()
        {
            try
            {
                foreach (string fname in System.IO.Directory.GetFiles(Program.ServerConfig.DbLocation + "\\Users\\"))
                {
                    WindowsAPI.IniFile IniFile = new WindowsAPI.IniFile(fname);
                    IniFile.FileName = fname;
                    string name = IniFile.ReadString("Character", "Name", "");
                    Pool.NameUsed.Add(name.GetHashCode());
                }
            }
            catch (Exception e)
            {
                MyConsole.WriteException(e);
            }
        }
        public unsafe static void SaveDatabase()
        {
            if (!FullLoading)
                return;
            try
            {

                Role.Instance.Clan.ProcessChangeNames();
                Role.Instance.Guild.ProcessChangeNames();

                Save(new Action(Database.JianHuTable.SaveJiangHu));
                Save(new Action(Role.Instance.Associate.Save));
                Save(new Action(Database.GuildTable.Save));
                Save(new Action(Database.ClanTable.Save));
                Save(new Action(Pool.QueueContainer.Save));
                Save(new Action(Game.MsgTournaments.MsgSchedules.GuildWar.Save));
                Save(new Action(Game.MsgTournaments.MsgSchedules.SuperGuildWar.Save));
                Save(new Action(TheCrimeTable.Save));
                Save(new Action(Pool.Arena.Save));
                Save(new Action(Pool.TeamArena.Save));
                Save(new Action(HWRank.Save));
                Save(new Action(NinjaRank.Save));
                Save(new Action(Game.MsgTournaments.MsgSchedules.ClassPkWar.Save));
                Save(new Action(Game.MsgTournaments.MsgSchedules.ElitePkTournament.Save));
                Save(new Action(Game.MsgTournaments.MsgSchedules.TeamPkTournament.Save));
                Save(new Action(Game.MsgTournaments.MsgSchedules.SkillTeamPkTournament.Save));
                Save(new Action((Game.MsgTournaments.MsgSchedules.Tournaments[Game.MsgTournaments.TournamentType.BattleField]
                    as Game.MsgTournaments.MsgBattleField).Save));
                Save(new Action(SystemBanned.Save));
                Save(new Action(SystemBannedAccount.Save));
                Save(new Action(InnerPowerTable.Save));
                Save(new Action(VoteSystem.Save));
                Save(new Action(LeagueTable.Save));
                Save(new Action(RechargeShop.Save));
                Save(new Action(Game.MsgTournaments.MsgSchedules.ClanWar.Save));
                Save(new Action(RankItems.SaveRanks));
                Save(new Action(Role.Statue.Save));
                Save(new Action(PrestigeRanking.Save));
                Save(new Action(Role.KOBoard.KOBoardRanking.Save));
                Save(new Action(MsgInterServer.Instance.CrossElitePKTournament.Save));
                Save(new Action(Pool.Insults.Save));
                Save(new Action(RuneRank.Save));

                WindowsAPI.IniFile IniFile = new WindowsAPI.IniFile("");
                IniFile.FileName = System.IO.Directory.GetCurrentDirectory() + "\\shell.ini";
                IniFile.Write<uint>("Database", "ItemUID", Pool.ITEM_Counter.Count);
                IniFile.Write<uint>("Database", "DominoUID", Pool.DominoCounter.Count);
                IniFile.Write<uint>("Database", "ClientUID", Pool.ClientCounter.Count);
                IniFile.Write<uint>("Database", "Day", ResetServerDay);
                IniFile.Write<uint>("Tournaments", "PkWarWinner", Game.MsgTournaments.MsgSchedules.PkWar.WinnerUID);
            }
            catch (Exception e) { MyConsole.WriteException(e); }
        }
        public static void Save(Action obj)
        {
            try
            {
                obj.Invoke();
            }
            catch (Exception e) { MyConsole.SaveException(e); }
        }
        public static void LoadPortals(uint id = 0)
        {

            if (System.IO.File.Exists(Program.ServerConfig.DbLocation + "portals.ini"))
            {
                using (System.IO.StreamReader read = System.IO.File.OpenText(Program.ServerConfig.DbLocation + "portals.ini"))
                {
                    ushort count = 0;
                    while (true)
                    {
                        string lines = read.ReadLine();
                        if (lines == null)
                            break;
                        ushort Map = ushort.Parse(lines.Split('[')[1].ToString().Split(']')[0]);
                        ushort Count = ushort.Parse(read.ReadLine().Split('=')[1]);
                        for (ushort x = 0; x < Count; x++)
                        {
                            Role.Portal portal = new Role.Portal();
                            string[] line = read.ReadLine().Split('=')[1].Split(' ');
                            portal.MapID = ushort.Parse(line[0]);
                            portal.X = ushort.Parse(line[1]);
                            portal.Y = ushort.Parse(line[2]);

                            string[] dline = read.ReadLine().Split('=')[1].Split(' ');
                            portal.Destiantion_MapID = ushort.Parse(dline[0]);
                            portal.Destiantion_X = ushort.Parse(dline[1]);
                            portal.Destiantion_Y = ushort.Parse(dline[2]);
                            if (id != 0 && id != Map) continue;
                            if (Pool.ServerMaps.Base.ContainsKey(portal.MapID))
                                Pool.ServerMaps[portal.MapID].Portals.Add(portal);
                            count++;
                        }
                    }
                    //  MyConsole.WriteLine("Loading " + count + " portals [Volcano]");
                }
            }
            GC.Collect();
        }
    }
}
