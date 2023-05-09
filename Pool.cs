using GameServer.Database;
using GameServer.Insults;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GameServer;

namespace GameServer
{
    public static class Pool
    {
        public static List<Smelt> SmeltingSuccesses = new List<Smelt>(10);
        public class Smelt
        {
            public string Prize, Name;
            public byte Furnace;
        }
        public static List<byte> SmeltingSessions = new List<byte>(10);
        public static string ReverseString(string text)
        {
            char[] cArray = text.ToCharArray();
            string reverse = "";
            for (int i = cArray.Length - 1; i > -1; i--)
            {
                reverse += cArray[i];
            }
            return reverse;
        }
        public static Dictionary<uint, DeitylandSacrificeRanking> DeitylandSacrificeRankings = new Dictionary<uint, DeitylandSacrificeRanking>();
        public class DeitylandSacrificeRanking
        {
            public string Prize, Name;
            public uint Jades;
        } public static bool Nobility = false;
        public static System.Time32 CurrentTime
        {
            get
            {
                return new System.Time32();
            }
        }
        
        public static Time32 smeltFloorStamp;
        public static ConcurrentDictionary<uint, Client.GameClient> GamePool = new ConcurrentDictionary<uint, Client.GameClient>();
        public static List<Game.MsgServer.MsgMelterRankList.Instance> MelterRankList = new List<Game.MsgServer.MsgMelterRankList.Instance>();
        public static Cryptography.TransferCipher transferCipher;
       // public static Role.Instance.Nobility.NobilityRanking NobilityRanking = new Role.Instance.Nobility.NobilityRanking();
        public static Role.Instance.Nobility.NobilityRanking NobilityRanking = new Role.Instance.Nobility.NobilityRanking();
        public static Role.Instance.ChiRank ChiRanking = new Role.Instance.ChiRank();
        public static Role.Instance.Flowers.FlowersRankingToday FlowersRankToday = new Role.Instance.Flowers.FlowersRankingToday();
        public static Role.Instance.Flowers.FlowerRanking GirlsFlowersRanking = new Role.Instance.Flowers.FlowerRanking();
        public static Role.Instance.Flowers.FlowerRanking BoysFlowersRanking = new Role.Instance.Flowers.FlowerRanking(false);
        public static DateTime ResetRandom = new DateTime();
        public static System.SafeRandom GetRandom = new System.SafeRandom();
        public static System.RandomLite LiteRandom = new System.RandomLite();
        public static Dictionary<DBLevExp.Sort, Dictionary<byte, DBLevExp>> LevelInfo = new Dictionary<DBLevExp.Sort, Dictionary<byte, DBLevExp>>();
        public static ConcurrentDictionary<uint, TheCrimeTable> TheCrimePoll = new ConcurrentDictionary<uint, TheCrimeTable>();
        public static Database.ActivityTask ActivityTasks = new ActivityTask();
        public static InfoHeroReward TableHeroRewards = new InfoHeroReward();
        public static List<uint> RedeemActivated = new List<uint>();
        public static Dictionary<ushort, List<ushort>> WeaponSpells = new Dictionary<ushort, List<ushort>>();
        public static MagicType Magic = new MagicType();
        public static LotteryTable Lottery = new LotteryTable();
        public static SubProfessionInfo SubClassInfo = new SubProfessionInfo();
        public static Dictionary<uint, Game.MsgMonster.MonsterFamily> MonsterFamilies = new Dictionary<uint, Game.MsgMonster.MonsterFamily>();
        public static System.Counter ITEM_Counter = new System.Counter(1);
        public static Rifinery RifineryItems;
        public static GameServer.CachedAttributeInvocation<System.Action<GameServer.Client.GameClient, GameServer.ServerSockets.Packet>, GameServer.PacketAttribute, ushort> MsgInvoker;
        public static InsultManager Insults = new InsultManager();
        public static RefinaryBoxes DBRerinaryBoxes;
        public static ItemType ItemsBase;
        public static MapDictionary<uint, Role.GameMap> ServerMaps;
        public static ConcurrentDictionary<uint, Client.GameClient> GamePoll;
        public static ConcurrentDictionary<uint, Client.GameClient> DisconnectPool = new ConcurrentDictionary<uint, Client.GameClient>();
        public static List<int> NameUsed;
        public static RebornInfomations RebornInfo;
        public static ArenaTable Arena = new ArenaTable();
        public static TeamArenaTable TeamArena = new TeamArenaTable();
        public static System.Counter ClientCounter = new System.Counter(1000000);
        public static System.Counter DominoCounter = new System.Counter(300000000);
        public static ConfiscatorTable QueueContainer = new ConfiscatorTable();
        public static System.SafeDictionary<uint, Game.MsgServer.MsgGameItem> ChatItems = new System.SafeDictionary<uint, Game.MsgServer.MsgGameItem>();

        public static uint RandFromGivingNums(uint[] nums)
        {
            if (nums == null || nums.Length == 0) return 0;
            return nums[GetRandom.Next(0, nums.Length)];
        }
    }
}
