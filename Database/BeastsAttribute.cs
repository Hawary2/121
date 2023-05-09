using System;
using System.Collections.Generic;
using System.IO;

namespace GameServer.Database
{
    public class BeastsAtrribute
    {
        public static Dictionary<byte, Attribute> Attributes = new Dictionary<byte, Attribute>();

        public class Attribute
        {
            public byte Level;
            public uint RequiredPoints;


            public uint HPAdd;
            public uint PAttack;
            public uint MAttack;
            public uint PDefense;
            public uint MDefense;
            public uint FinalPDmgDealt;
            public uint FinalMDmgDealt;
            public uint FinalPDmgReceived;
            public uint FinalMDmgReceived;

            public byte YellowRuneSlots;
        }

        public static void Load()
        {
            if (File.Exists(Program.ServerConfig.DbLocation + "beasts_attr.txt"))
            {
                string[] Lines = File.ReadAllLines(Program.ServerConfig.DbLocation + "beasts_attr.txt");
                foreach (var line in Lines)
                {
                    var spilitline = line.Split(new string[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
                    Attribute attr = new Attribute();
                    attr.Level = byte.Parse(spilitline[0]);
                    attr.RequiredPoints = uint.Parse(spilitline[1]);
                    attr.HPAdd = uint.Parse(spilitline[2]);
                    attr.PAttack = uint.Parse(spilitline[3]);
                    attr.MAttack = uint.Parse(spilitline[4]);
                    attr.PDefense = uint.Parse(spilitline[5]);
                    attr.MDefense = uint.Parse(spilitline[6]);
                    attr.FinalPDmgDealt = uint.Parse(spilitline[7]);
                    attr.FinalMDmgDealt = uint.Parse(spilitline[8]);
                    attr.FinalPDmgReceived = uint.Parse(spilitline[9]);
                    attr.FinalMDmgReceived = uint.Parse(spilitline[10]);
                    attr.YellowRuneSlots = byte.Parse(spilitline[16]);

                    Attributes.Add(attr.Level, attr);
                }
            }
        }
    }
}