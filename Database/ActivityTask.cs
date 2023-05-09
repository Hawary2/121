using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameServer.Database
{
    public class ActivityTask : System.SafeDictionary<uint, ActivityTask.Task>
    {

        public class Task
        {
            public uint ID;
            public byte NeedProgress = 0;
            public byte ActiveValue = 0;
            public uint OpenLev;

            public bool CheckOpen(ushort level, byte reborn)
            {
                uint needlevel = OpenLev % 1000;
                uint needreborn = (OpenLev - needlevel) / 1000;

                return level >= needlevel && reborn >= needreborn;
            }

        }

        
        public void Load()
        {
            WindowsAPI.IniFile Reader = new WindowsAPI.IniFile("ActivityTask.ini");

            int TaskCount = Reader.ReadByte("ActivityTask", "Num", 0);

            for (uint x = 1; x <= TaskCount; x++)
            {
                Add(x, new Task()
                {
                    ID = x, NeedProgress = Reader.ReadByte(x.ToString(), "Progress",0),
                    ActiveValue = Reader.ReadByte(x.ToString(), "ActiveValue", 0),
                    OpenLev = Reader.ReadUInt32(x.ToString(), "OpenLev", 0)
                });
            }

        }
    }
}
