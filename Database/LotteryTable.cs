using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameServer.Database
{
    public class LotteryTable
    {
       
        public class LotteryItem
        {
            public int Rank, Chance;
            public string Name;
            public uint ID;
            public byte Color;
            public byte Sockets;
            public byte Plus;

            public override string ToString()
            {
                return Rank + " " + Chance + " " + Name + " " + ID + " " + Color + " " + Sockets + " " + Plus;
            }
        }
        public LotteryRandom RandomGenerator;
        private Dictionary<int, List<LotteryItem>> LotteryItems;
        private Random Rand;

        

        public void LoadLotteryItems()
        {
            Rand = new Random();
            RandomGenerator = new LotteryRandom();
            LotteryItems = new Dictionary<int, List<LotteryItem>>();

            using (DBActions.Read reader = new DBActions.Read("lottery.ini"))
            {
                if (reader.Reader())
                {
                    for (int x = 0; x < reader.Count; x++)
                    {
                        DBActions.ReadLine line = new DBActions.ReadLine(reader.ReadString(""), ' ');
                        LotteryItem item = new LotteryItem();
                        item.Rank = line.Read((int)0);
                        item.Chance = line.Read((int)0);
                        item.Name = line.Read("");
                        item.ID = line.Read((uint)0);
                        item.Color = line.Read((byte)0);
                        item.Sockets = line.Read((byte)0);
                        item.Plus = line.Read((byte)0);

                        if (!LotteryItems.ContainsKey(item.Rank))
                            LotteryItems.Add(item.Rank, new List<LotteryItem>());
                        LotteryItems[item.Rank].Add(item);
                    }
                }
            }
         


        }
        public class LotteryRandom
        {
            public class RankRateWatcher
            {
                private int tick;
                private int count;
                public static implicit operator bool(RankRateWatcher q)
                {
                    bool result = false;
                    q.count++;
                    if (q.count == q.tick)
                    {
                        q.count = 0;
                        result = true;
                    }
                    return result;
                }
                public RankRateWatcher(int Tick)
                {
                    tick = Tick;
                    count = 0;
                }
            }
            public RankRateWatcher[] Ranks;
          
#if Encore
                    public LotteryRandom()
            {
                Ranks = new RankRateWatcher[5];
                Ranks[0] = new RankRateWatcher(500);  
                Ranks[1] = new RankRateWatcher(350);
                Ranks[2] = new RankRateWatcher(150);
                Ranks[3] = new RankRateWatcher(50);
                Ranks[4] = new RankRateWatcher(15); 
            }

#else
            public LotteryRandom()
            {
                Ranks = new RankRateWatcher[5];
                Ranks[0] = new RankRateWatcher(600);
                Ranks[1] = new RankRateWatcher(500);
                Ranks[2] = new RankRateWatcher(300);
                Ranks[3] = new RankRateWatcher(100);
                Ranks[4] = new RankRateWatcher(40);
            }
#endif

            public int GenerateRank()
            {
                for (int x = 0; x < Ranks.Length; x++)
                {
                    if (Ranks[x])
                    {
                        return x + 1;
                    }
                }
                return 6;
            }

        }
        public int LotteryEntry(byte vipLevel)
        {
            return int.MaxValue;
        }
        public LotteryItem GenerateLotteryItem(Client.GameClient user)
        {
            int Rank = RandomGenerator.GenerateRank();
         
            var items = LotteryItems[Rank];
            if (items.Count == 1)
                return items[0];
            return items[(byte)BaseFunc.RandGet(items.Count, true)];
        }
        public Game.MsgServer.MsgGameItem CreateGameItem(LotteryItem Item)
        {
            Game.MsgServer.MsgGameItem GameItem = new Game.MsgServer.MsgGameItem();
            GameItem.ITEM_ID = Item.ID;
            GameItem.Color = (Role.Flags.Color)(byte)Pool.GetRandom.Next(4, 8);//Item.Color;
            GameItem.Plus = Item.Plus;
            if (Item.Sockets > 0)
                GameItem.SocketOne = Role.Flags.Gem.EmptySocket;
            if (Item.Sockets > 1)
                GameItem.SocketTwo = Role.Flags.Gem.EmptySocket;
            GameItem.UID = Pool.ITEM_Counter.Next;
            var DBItem = Pool.ItemsBase[Item.ID];
            GameItem.Durability = GameItem.MaximDurability = DBItem.Durability;
            return GameItem;
        }
    }
}
