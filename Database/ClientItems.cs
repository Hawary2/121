using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.IO;

namespace GameServer.Database
{
    public class ClientItems
    {
        public struct Perfection
        {
            public uint ItemUID;
            public uint Level;
            public uint Progres;
            public uint OwnerUID;
            public unsafe fixed sbyte _OwnerName[32];
            public unsafe fixed sbyte _SpecialText[32];

            public unsafe string OwnerName
            {
                get { fixed (sbyte* bp = _OwnerName) { return new string(bp); } }
                set
                {
                    string text = value;
                    fixed (sbyte* bp = _OwnerName)
                    {
                        for (int i = 0; i < text.Length; i++)
                            bp[i] = (sbyte)text[i];
                    }
                }
            }
            public unsafe string SpecialText
            {
                get { fixed (sbyte* bp = _SpecialText) { return new string(bp); } }
                set
                {
                    string text = value;
                    fixed (sbyte* bp = _SpecialText)
                    {
                        for (int i = 0; i < text.Length; i++)
                            bp[i] = (sbyte)text[i];
                    }
                }
            }
        }
        public struct DBItem
        {
            public uint UID;
            public uint ITEM_ID;
            public ushort Durability;
            public ushort MaximDurability;
            public ushort Position;
            public uint SocketProgress;
            public Role.Flags.Gem SocketOne;
            public Role.Flags.Gem SocketTwo;
            public Role.Flags.ItemEffect Effect;
            public byte Plus;
            public byte Bless;
            public byte Bound;
            public byte Enchant;
            public byte Suspicious;
            public byte Locked;

            public uint PlusProgress;
            public ushort Inscribed;
            public uint Activate;
            public uint TimeLeftInMinutes;
            public ushort StackSize;
            public uint WH_ID;
            public Role.Flags.Color Color;
            public int UnLockTimer;

            public uint ItemPoints;
            public uint RemainingTime;
            public uint test3;
            public uint test4;

            //Artefacts 
            public uint PurificationItemID;
            public uint PurificationLevel;
            public uint PurificationDuration;
            public long PurificationAddedOn;

            //Refinary
            public uint EffectID;
            public uint EffectLevel;
            public uint EffectPercent;
            public uint EffectPercent2;
            public uint EffectDuration;
            public long EffectAddedOn;

            public uint DepositeCount;

            public uint AnimaItemID;
            public uint RuneEXP;
            public uint Attribute1;
            public uint Attribute2;
            public uint Attribute3;
            public uint Attribute4;
            public uint Attribute5;


            public Perfection GetPerfectionInfo(Game.MsgServer.MsgGameItem DataItem)
            {
                Perfection info = new Perfection();
                info.ItemUID = DataItem.UID;
                info.Level = DataItem.PerfectionLevel;
                info.Progres = DataItem.PerfectionProgress;
                info.OwnerUID = DataItem.OwnerUID;
                info.OwnerName = DataItem.OwnerName;
                info.SpecialText = DataItem.Signature;
                return info;
            }
            public DBItem GetDBItem(Game.MsgServer.MsgGameItem DataItem)
            {
                UID = DataItem.UID;
                ITEM_ID = DataItem.ITEM_ID;
                Durability = DataItem.Durability;
                MaximDurability = DataItem.MaximDurability;
                Position = DataItem.Position;
                SocketProgress = DataItem.SocketProgress;
                SocketOne = DataItem.SocketOne;
                SocketTwo = DataItem.SocketTwo;
                Effect = DataItem.Effect;
                Plus = DataItem.Plus;
                Bless = DataItem.Bless;
                Bound = DataItem.Bound;
                Enchant = DataItem.Enchant;
                Suspicious = DataItem.Suspicious;
                Locked = DataItem.Locked;
                PlusProgress = DataItem.PlusProgress;
                Inscribed = DataItem.Inscribed;
                Activate = DataItem.Activate;
                TimeLeftInMinutes = DataItem.TimeLeftInMinutes;
                StackSize = DataItem.StackSize;
                WH_ID = DataItem.WH_ID;
                Color = DataItem.Color;
                ItemPoints = DataItem.ItemPoints;
                PurificationItemID = DataItem.Purification.PurificationItemID;
                PurificationAddedOn = DataItem.Purification.AddedOn.Ticks;
                PurificationDuration = DataItem.Purification.PurificationDuration;
                PurificationLevel = DataItem.Purification.PurificationLevel;

                EffectAddedOn = DataItem.Refinary.AddedOn.Ticks;
                EffectDuration = DataItem.Refinary.EffectDuration;
                EffectID = DataItem.Refinary.EffectID;
                EffectLevel = DataItem.Refinary.EffectLevel;
                EffectPercent = DataItem.Refinary.EffectPercent;
                EffectPercent2 = DataItem.Refinary.EffectPercent2;

                DepositeCount = (uint)DataItem.Deposite.Count;
                UnLockTimer = DataItem.UnLockTimer;
                RemainingTime = DataItem.RemainingTime;
                AnimaItemID = DataItem.AnimaItemID;
                RuneEXP = DataItem.RuneEXP;
                Attribute1 = DataItem.RelicAttributes[0];
                Attribute2 = DataItem.RelicAttributes[1];
                Attribute3 = DataItem.RelicAttributes[2];
                Attribute4 = DataItem.RelicAttributes[3];
                Attribute5 = DataItem.RelicAttributes[4];
                return this;
            }

            public Game.MsgServer.MsgGameItem GetDataItem()
            {
                Game.MsgServer.MsgGameItem DataItem = new Game.MsgServer.MsgGameItem();
                DataItem.UID = UID;
                DataItem.Effect = Effect;
                DataItem.ITEM_ID = ITEM_ID;
                DataItem.Durability = Durability;
                DataItem.MaximDurability = MaximDurability;
                DataItem.Position = Position;
                DataItem.SocketProgress = SocketProgress;
                DataItem.SocketOne = SocketOne;
                DataItem.SocketTwo = SocketTwo;
                DataItem.Plus = Plus;
                DataItem.Bless = Bless;
                DataItem.Bound = Bound;
                DataItem.Enchant = Enchant;
                DataItem.Suspicious = Suspicious;
                DataItem.Locked = Locked;
                DataItem.PlusProgress = PlusProgress;
                DataItem.Inscribed = Inscribed;
                DataItem.Activate = Activate;
                DataItem.TimeLeftInMinutes = TimeLeftInMinutes;
                DataItem.StackSize = StackSize;
                DataItem.ItemPoints = ItemPoints;
                Database.ItemType.DBItem DbItem;
                if (Pool.ItemsBase.TryGetValue(ITEM_ID, out DbItem))
                {
                    DataItem.RemainingTime = DbItem.StackSize > 1 ? 0 : uint.MaxValue;
                }
                DataItem.UnLockTimer = UnLockTimer;

                DataItem.WH_ID = WH_ID;
                DataItem.Color = Color;
                DataItem.Purification.ItemUID = UID;
                DataItem.Purification.PurificationItemID = PurificationItemID;
                DataItem.Purification.PurificationLevel = PurificationLevel;
                DataItem.Purification.PurificationDuration = PurificationDuration;
                DataItem.Purification.AddedOn = DateTime.FromBinary(PurificationAddedOn > DateTime.MinValue.Ticks && PurificationAddedOn < DateTime.MaxValue.Ticks ? PurificationAddedOn : 0);
                if (!DataItem.Purification.InLife)
                    DataItem.Purification = new Game.MsgServer.MsgItemExtra.Purification();

                DataItem.Refinary.ItemUID = UID;
                DataItem.Refinary.EffectID = EffectID;
                DataItem.Refinary.EffectLevel = EffectLevel;
                DataItem.Refinary.EffectPercent = EffectPercent;
                DataItem.Refinary.EffectPercent2 = EffectPercent2;
                DataItem.Refinary.EffectDuration = EffectDuration;
                DataItem.Refinary.AddedOn = DateTime.FromBinary(EffectAddedOn);
                if (!DataItem.Refinary.InLife)
                    DataItem.Refinary = new Game.MsgServer.MsgItemExtra.Refinery();

                DataItem.AnimaItemID = AnimaItemID;
                DataItem.RuneEXP = RuneEXP;
                DataItem.RelicAttributes = new Role.Instance.RelicAttribute[5];
                DataItem.RelicAttributes[0] = new Role.Instance.RelicAttribute(Attribute1);
                DataItem.RelicAttributes[1] = new Role.Instance.RelicAttribute(Attribute2);
                DataItem.RelicAttributes[2] = new Role.Instance.RelicAttribute(Attribute3);
                DataItem.RelicAttributes[3] = new Role.Instance.RelicAttribute(Attribute4);
                DataItem.RelicAttributes[4] = new Role.Instance.RelicAttribute(Attribute5);
                return DataItem;
            }

        }
    }
}