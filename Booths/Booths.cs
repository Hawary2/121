using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using GameServer.Client;
using GameServer.Game;

using GameServer.Role;
using GameServer.Game.MsgServer;
using GameServer.Role.Instance;
using GameServer.Database;

namespace GameServer
{
    public class Booths
    {
        public enum BoothType
        {
            Npc = 0,
            Entity = 1
        }
        public class booth
        {
            public uint UID;
            public uint Mesh = 100;
            public string Name;
            public ushort Map1;
            public Role.GameMap Map;
            public ushort X;
            public ushort Y;
            public List<string> Items;
            public BoothType Type;
            public string BotMessage = "Selling Items.[Boothing AI]";
            public uint Garment = 194300;
            public uint Head = 112259;
            public uint WeaponR = 601439;
            public uint WeaponL = 601439;
            public uint Armor = 135259;
        }
        public static System.SafeDictionary<uint, booth> Boooths = new System.SafeDictionary<uint, booth>();
        public static void Load()
        {
            string[] text = File.ReadAllLines(Program.ServerConfig.DbLocation + "Booths.txt");
            booth booth = new booth();
            for (int x = 0; x < text.Length; x++)
            {
                string line = text[x];
                string[] split = line.Split('=');
                if (split[0] == "ID")
                {
                    if (booth.UID == 0)
                        booth.UID = uint.Parse(split[1]);
                    else
                    {
                        if (!Boooths.ContainsKey(booth.UID))
                        {
                            Boooths.Add(booth.UID, booth);
                            booth = new booth();
                            booth.UID = uint.Parse(split[1]);
                        }
                    }
                }
                else if (split[0] == "Type")
                {
                    booth.Type = (BoothType)byte.Parse(split[1]);
                }
                else if (split[0] == "Name")
                {
                    booth.Name = split[1];
                }

                else if (split[0] == "BotMessage")
                {
                    booth.BotMessage = split[1];
                }
                else if (split[0] == "Garment")
                {
                    booth.Garment = uint.Parse(split[1]);
                }
                else if (split[0] == "Head")
                {
                    booth.Head = uint.Parse(split[1]);
                }
                else if (split[0] == "WeaponR")
                {
                    booth.WeaponR = uint.Parse(split[1]);
                }
                else if (split[0] == "WeaponL")
                {
                    booth.WeaponL = uint.Parse(split[1]);
                }
                else if (split[0] == "Armor")
                {
                    booth.Armor = uint.Parse(split[1]);
                }
                else if (split[0] == "Mesh")
                {
                    booth.Mesh = ushort.Parse(split[1]);
                }
                else if (split[0] == "Map")
                {
                    booth.Map1 = ushort.Parse(split[1]);
                }
                else if (split[0] == "X")
                {
                    booth.X = ushort.Parse(split[1]);
                }
                else if (split[0] == "Y")
                {
                    booth.Y = ushort.Parse(split[1]);
                }
                else if (split[0] == "ItemAmount")
                {
                    booth.Items = new List<string>(ushort.Parse(split[1]));
                }
                else if (split[0].Contains("Item") && split[0] != "ItemAmount")
                {
                    string name = split[1];
                    booth.Items.Add(name);
                }
            }
            if (!Boooths.ContainsKey(booth.UID))
                Boooths.Add(booth.UID, booth);
            CreateBooths();
        }
        public static void UpdateCoordonatesForAngle(ref ushort X, ref ushort Y, ushort angle)
        {
            sbyte xi = 0, yi = 0;
            switch (angle)
            {
                case (ushort)Role.Flags.ConquerAngle.North: xi = 1; yi = 1; break;
                case (ushort)Role.Flags.ConquerAngle.South: xi = -1; yi = -1; break;
                case (ushort)Role.Flags.ConquerAngle.East: xi = -1; yi = 1; break;
                case (ushort)Role.Flags.ConquerAngle.West: xi = 1; yi = -1; break;
                case (ushort)Role.Flags.ConquerAngle.NorthWest: xi = 1; break;
                case (ushort)Role.Flags.ConquerAngle.SouthWest: yi = -1; break;
                case (ushort)Role.Flags.ConquerAngle.NorthEast: yi = 1; break;
                case (ushort)Role.Flags.ConquerAngle.SouthEast: xi = -1; break;
            }
            X = (ushort)(X + xi);
            Y = (ushort)(Y + yi);
        }

        public static void CreateBooths()
        {
            foreach (var bo in Boooths.Values)
            {
                Game.Booth booth = new Game.Booth();

                SobNpc Base = new SobNpc();
                Base.UID = bo.UID;
                if (Booth.Booths2.ContainsKey(Base.UID))
                    Booth.Booths2.Remove(Base.UID);
                Booth.Booths2.Add(Base.UID, booth);
                Base.ObjType = MapObjectType.SobNpc;
                Base.Mesh = (GameServer.Role.SobNpc.StaticMesh)bo.Mesh;
                Base.Type = Role.Flags.NpcType.Booth;
                Base.Name = bo.Name;
                Base.Map = bo.Map1;
                Base.Booth = booth;
                Base.X = bo.X;
                Base.Y = bo.Y;
                booth.Base = Base;

                if (Pool.ServerMaps.ContainsKey(Base.Map))
                {
                    Pool.ServerMaps[Base.Map].View.EnterMap<Role.IMapObj>(Base);

                }

                for (int i = 0; i < bo.Items.Count; i++)
                {
                    var line = bo.Items[i].Split(new string[] { "@@", "@" }, StringSplitOptions.RemoveEmptyEntries);
                    #region booth
                    Game.BoothItem item = new Game.BoothItem();

                    item.Item = new MsgGameItem();
                    item.Item.UID = MsgGameItem.ItemUID.Next;
                    item.Item.ITEM_ID = uint.Parse(line[0]);
                    if (line.Length >= 2)
                        item.Cost = uint.Parse(line[1]);
                    if (line.Length >= 3)
                        item.Item.Plus = byte.Parse(line[2]);
                    if (line.Length >= 4)
                        item.Item.Enchant = byte.Parse(line[3]);
                    if (line.Length >= 5)
                        item.Item.Bless = byte.Parse(line[4]);
                    if (line.Length >= 6)
                        item.Item.SocketOne = (Role.Flags.Gem)byte.Parse(line[5]);
                    if (line.Length >= 7)
                        item.Item.SocketTwo = (Role.Flags.Gem)byte.Parse(line[6]);
                    if (line.Length >= 8)
                        item.Item.StackSize = ushort.Parse(line[7]);

                    Database.ItemType.DBItem CIBI = new Database.ItemType.DBItem();
                    if (Pool.ItemsBase.TryGetValue(item.Item.ITEM_ID, out CIBI))
                    {
                        if (CIBI == null)
                            break;
                        item.Item.Durability = CIBI.Durability;
                        item.Item.MaximDurability = CIBI.Durability;
                        item.Cost_Type = Game.BoothItem.CostType.ConquerPoints;
                        booth.ItemList.Add(item.Item.UID, item);
                    }
                    #endregion
                }

            }
            Console.WriteLine("" + Booth.Booths2.Count + " New Booths Loaded.");
        }
    }
}