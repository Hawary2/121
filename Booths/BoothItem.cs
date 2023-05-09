
using GameServer.Game.MsgServer;
using GameServer.Role;
using System;
using System.Collections.Generic;

namespace GameServer.Game
{
    public struct BoothItem
    {
        public void Regenerate(BoothItem item, Booth booth)
        {
            booth.ItemList.Remove(item.Item.UID);
            this.Cost = item.Cost;
            this.Item = new MsgGameItem();
            this.Item.ITEM_ID = item.Item.ITEM_ID;
            this.Item.UID = MsgGameItem.ItemUID.Next;
            this.Item.Plus = item.Item.Plus;
            this.Item.Enchant = item.Item.Enchant;
            this.Item.Bless = item.Item.Bless;
            this.Item.SocketOne = item.Item.SocketOne;
            this.Item.SocketTwo = item.Item.SocketTwo;
            this.Item.StackSize = item.Item.StackSize;
            Database.ItemType.DBItem CIBI = new Database.ItemType.DBItem();
            if (Pool.ItemsBase.TryGetValue(this.Item.ITEM_ID, out CIBI))
            {
                if (CIBI == null)
                    return;
                this.Item.Durability = CIBI.Durability;
                this.Item.MaximDurability = CIBI.Durability;
                this.Cost_Type = item.Cost_Type;
                booth.ItemList.Add(this.Item.UID, this);
            }


        }
        public enum CostType : byte
        {
            Silvers = 1,
            ConquerPoints = 3,

        }
        public MsgGameItem Item;
        public uint Cost;
        public CostType Cost_Type;


    }
    public class Booth
    {
        public static GameServer.Counter BoothCounter = new GameServer.Counter(1) { Finish = 10000 };
        private static Dictionary<uint, Booth> Booths = new Dictionary<uint, Booth>();
        public static Dictionary<uint, Booth> Booths2 = new Dictionary<uint, Booth>();
        public static object SyncRoot = new Object();
        public static bool TryGetValue(uint uid, out Booth booth)
        {
            lock (SyncRoot)
                return Booths.TryGetValue(uid, out booth);
        }
        public static bool TryGetValue2(uint uid, out Booth booth)
        {
            lock (SyncRoot)
                return Booths2.TryGetValue(uid, out booth);
        }

        public System.SafeDictionary<uint, BoothItem> ItemList;
        Client.GameClient Owner;
        public SobNpc Base;
        public MsgMessage HawkMessage;
        public Booth(Client.GameClient client, ServerSockets.Packet stream)
        {
            Owner = client;
            Owner.Player.Action = Role.Flags.ConquerAction.Sit;
            ItemList = new System.SafeDictionary<uint, BoothItem>(20);
            Base = new SobNpc();
            lock (SyncRoot)
            {
                Base.UID = BoothCounter.Next;
                while (Booths.ContainsKey(Base.UID))
                    Base.UID = BoothCounter.Next;
                Booths.Add(Base.UID, this);
            }
            Base.Mesh = (GameServer.Role.SobNpc.StaticMesh)406;
            Base.Type = Role.Flags.NpcType.Booth;
            Base.Name = Name;
            Base.Booth = this;
            Base.Map = client.Player.Map;
            Base.X = (ushort)(Owner.Player.X + 1);
            Base.Y = Owner.Player.Y;
            Owner.Map.View.EnterMap<Role.IMapObj>(Base);

            foreach (var IObj in Owner.Player.View.Roles(MapObjectType.Player))
            {
                Role.Player screenObj = IObj as Role.Player;
                screenObj.View.CanAdd(Base, true, stream);
            }
            Owner.Player.Send(Base.GetArray(stream, false));
            ActionQuery action = new ActionQuery()
            {
                dwParam = Base.UID,
                TargetPositionX = Base.X,
                TargetPositionY = Base.Y,
                Type = ActionType.StartVendor
            };
        }

        public Booth()
        {
            ItemList = new System.SafeDictionary<uint, BoothItem>(20);

        }
        public string Name
        {
            get
            {
                return Owner.Player.Name;
            }
        }
        public static implicit operator SobNpc(Booth booth)
        {
            return booth.Base;
        }
        public void Remove()
        {
            ActionQuery action = new ActionQuery()
            {
                ObjId = Base.UID,
                Type = ActionType.RemoveEntity
            };
            lock (SyncRoot) Booths.Remove(Base.UID);
        }
    }
}