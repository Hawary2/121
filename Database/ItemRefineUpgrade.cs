using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace GameServer.Database
{
    public class ItemRefineUpgrade
    {

        public static Dictionary<uint, uint> ProgresUpdates = new Dictionary<uint, uint>();
        public static Dictionary<uint, EffectEX> EffectsEX = new Dictionary<uint, EffectEX>();

        public class EffectEX
        {
            public uint ID;
            public uint ItemID;
            public uint StarReq;
            public uint AttributeNum;
            public uint Value;
        }
        public static void Load()
        {

            string[] baseText = File.ReadAllLines(Program.ServerConfig.DbLocation + "item_refine_upgrade.txt");
            foreach (var bas_line in baseText)
            {
                Database.DBActions.ReadLine line = new DBActions.ReadLine(bas_line, ' ');
                if (line.Read((uint)0) != 1) continue;
                uint Level = line.Read((uint)0);
                uint Progres = line.Read((uint)0);
                ProgresUpdates.Add(Level, Progres);
            }

            baseText = File.ReadAllLines(Program.ServerConfig.DbLocation + "item_refine_effect_ex.txt");
            foreach (var bas_line in baseText)
            {
                Database.DBActions.ReadLine line = new DBActions.ReadLine(bas_line, ' ');

                EffectEX iru = new EffectEX();
                iru.ID = line.Read((ushort)0);
                iru.ItemID = line.Read((uint)0);
                iru.StarReq = line.Read((uint)0);
                iru.AttributeNum = line.Read((uint)0);
                iru.Value = line.Read((uint)0);
                EffectsEX.Add(iru.ID, iru);
            }
        }

    }
}
