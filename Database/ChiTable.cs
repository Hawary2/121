using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameServer.Database
{
    public class ChiTable
    {
        public static void Load()
        {
            WindowsAPI.IniFile ini = new WindowsAPI.IniFile("");
            foreach (string fname in System.IO.Directory.GetFiles(Program.ServerConfig.DbLocation + "\\Users\\"))
            {
                ini.FileName = fname;
                
                uint UID = ini.ReadUInt32("Character", "UID", 0);
                Role.Instance.Chi playerchi = new Role.Instance.Chi(UID);
                string Name = ini.ReadString("Character", "Name", "None");
                playerchi.Name = Name;
                playerchi.ChiPoints = ini.ReadInt32("Character", "ChiPoints", 0);
                playerchi.Dragon.Load(ini.ReadString("Character", "Dragon", ""), UID, Name);
                playerchi.Phoenix.Load(ini.ReadString("Character", "Pheonix", ""), UID, Name);
                playerchi.Turtle.Load(ini.ReadString("Character", "Turtle", ""), UID, Name);
                playerchi.Tiger.Load(ini.ReadString("Character", "Tiger", ""), UID, Name);

                if (playerchi.Dragon.UnLocked)
                {
                    Role.Instance.Chi.ChiPool.TryAdd(playerchi.UID, playerchi);
                    Pool.ChiRanking.Upadte(Pool.ChiRanking.Dragon, playerchi.Dragon);
                }
                if(playerchi.Phoenix.UnLocked)
                    Pool.ChiRanking.Upadte(Pool.ChiRanking.Phoenix, playerchi.Phoenix);
                if (playerchi.Tiger.UnLocked)
                    Pool.ChiRanking.Upadte(Pool.ChiRanking.Tiger, playerchi.Tiger);
                if (playerchi.Turtle.UnLocked)
                    Pool.ChiRanking.Upadte(Pool.ChiRanking.Turtle, playerchi.Turtle);
            }

        }
    }
}
