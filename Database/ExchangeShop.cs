using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameServer.Database
{
   public static class ExchangeShop
    {
       public class Item
       {
           public int Index;
           public uint ItemID;
           public byte RankType;
           public uint CountItems;
           public uint UnKnow;
           public uint ReuquestItem;
           public uint RequestItemCount;

       }
       public static Dictionary<int, Item> Items = new Dictionary<int, Item>();

     


       public static void LoadDBInfo()
       {
             string[] baseText = System.IO.File.ReadAllLines(Program.ServerConfig.DbLocation + "exchange_shop_goods.txt");
             foreach (var bas_line in baseText)
             {
                 var line = bas_line.Split(' ');

                 Item obj = new Item()
                 {
                     Index = int.Parse(line[0]),
                     ItemID = uint.Parse(line[1]),
                     RankType = byte.Parse(line[2]),
                     CountItems = uint.Parse(line[3]),
                     ReuquestItem = uint.Parse(line[5]),
                     RequestItemCount = uint.Parse(line[6]),
                  
                 };
                 Items.Add(obj.Index, obj);
                
             }
       }
    }
}
