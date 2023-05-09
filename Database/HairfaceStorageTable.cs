using System;
using System.Collections.Generic;
using System.IO;

namespace GameServer.Database
{
    public class HairfaceStorageTable
    {
        public class Hairface
        {
            public Game.MsgServer.MsgHairfaceStorage.Type Type;
            public uint ID;
            public string Name;
            public byte RareLevel;
            public byte RequiredVIPLevel;
            public uint Cost;
            public List<byte> Classes;
            public byte Sex;
            public byte EquippedColor;
            public byte[] Colors;
        }

        public class HairColor
        {
            public uint ID;
            public uint Cost;
            public byte Color;
        }

        public static List<Hairface> Hairfaces = new List<Hairface>();
        public static List<HairColor> HairColors = new List<HairColor>();

        public static void LoadFaces()
        {
            if (File.Exists(Program.ServerConfig.DbLocation + "hairface_storage_type.txt"))
            {
                string[] Lines = File.ReadAllLines((Program.ServerConfig.DbLocation + "hairface_storage_type.txt"));
                foreach (var line in Lines)
                {
                    var spilitline = line.Split(new string[] { "@@" }, StringSplitOptions.None);
                    Hairface Hf = new Hairface();
                    Hf.Type = (Game.MsgServer.MsgHairfaceStorage.Type)Convert.ToByte(spilitline[0]);
                    Hf.ID = Convert.ToUInt32(spilitline[1]);
                    Hf.Name = spilitline[3];
                    Hf.RareLevel = Convert.ToByte(spilitline[4]);
                    Hf.RequiredVIPLevel = Convert.ToByte(spilitline[5]);
                    Hf.Cost = Convert.ToUInt32(spilitline[7]);
                    Hf.Classes = new List<byte>();
                    var spilit = spilitline[9].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in spilit)
                        Hf.Classes.Add(byte.Parse(item));
                    try
                    {
                        Hf.Sex = Convert.ToByte(spilitline[10]);
                    }
                    catch { Hf.Sex = 0; }
                    Hf.Colors = new byte[7] { 1, 0, 0, 0, 0, 0, 0 };
                    Hairfaces.Add(Hf);
                }
            }
        }
        public static void LoadColors()
        {
            if (File.Exists(Program.ServerConfig.DbLocation + "hair_color_type.txt"))
            {
                string[] Lines = File.ReadAllLines((Program.ServerConfig.DbLocation + "hair_color_type.txt"));
                foreach (var line in Lines)
                {
                    var spilitline = line.Split(new string[] { "@@" }, StringSplitOptions.None);
                    HairColor Hc = new HairColor();
                    Hc.ID = Convert.ToUInt32(spilitline[0]);
                    Hc.Color = Convert.ToByte(spilitline[1]);
                    Hc.Cost = Convert.ToUInt32(spilitline[4]);
                    HairColors.Add(Hc);
                }
            }
        }
        public static void Load()
        {
            LoadFaces();
            LoadColors();
        }
    }
}