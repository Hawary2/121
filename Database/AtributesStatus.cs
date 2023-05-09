﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameServer.Database
{
    public class AtributesStatus : Dictionary<uint, Dictionary<ushort, AtributesStatus.Instant>>
    {
        public class Instant
        {
            public ushort Strenght = 0;
            public ushort Vitality = 0;
            public ushort Agility = 0;
            public ushort Spirit;
        }

        public static bool IsTrojan(uint Job) { return Job >= 1000 && Job <= 1049; }
        public static bool IsWarrior(uint Job) { return Job >= 2000 && Job <= 2049; }
        public static bool IsArcher(uint Job) { return Job >= 4000 && Job <= 4049; }
        public static bool IsNinja(uint Job) { return Job >= 5000 && Job <= 5049; }
        public static bool IsMonk(uint Job) { return Job >= 6000 && Job <= 6049; }
        public static bool IsPirate(uint Job) { return Job >= 7000 && Job <= 7049; }
        public static bool IsLee(uint Job) { return Job >= 8000 && Job <= 8049; }
        public static bool IsThunderStriker(uint Job) { return Job >= 9000 && Job <= 9049; }
        public static bool IsWater(uint Job) { return Job >= 13002 && Job <= 13049; }
        public static bool IsFire(uint Job) { return Job >= 14002 && Job <= 14049; }
        public static bool IsTaoist(uint Job) { return Job >= 10000 && Job <= 14049; }
        public static bool IsWindWalker(uint Job) { return Job >= 16000 && Job <= 16049; }

        private uint PositionArray(uint Class)
        {
            if (Class >= 1000 && Class <= 1049)
                return 1005;
            else if (Class >= 2000 && Class <= 2049)
                return 2005;
            else if (Class >= 4000 && Class <= 4049)
                return 4005;
            else if (Class >= 5000 && Class <= 5049)
                return 5005;
            else if (Class >= 6000 && Class <= 6049)
                return 6005;
            else if (Class >= 7000 && Class <= 7049)
                return 7005;
            else if (Class >= 8000 && Class <= 8049)
                return 8005;
            else if (Class >= 9000 && Class <= 9049)
                return 9005;
            else if (Class >= 10000 && Class <= 14049)
                return 10000;
            else if (Class >= 16000 && Class <= 16049)
                return 16005;

            return 0;
        }
        public void ResetStatsNonReborn(Role.Player player)
        {
            var clas_stast = this[PositionArray(player.Class)];
            Instant stat = clas_stast[(byte)Math.Min(120, (int)player.Level)];
            player.Strength = stat.Strenght;
            player.Vitality = stat.Vitality;
            player.Spirit = stat.Spirit;
            player.Agility = stat.Agility;
            
        }
        public bool CheckStatus(Role.Player player)
        {
            var clas_stast = this[PositionArray(player.Class)];
            Instant stat = clas_stast[(byte)Math.Min(120, (int)player.Level)];
            return (player.Strength >= stat.Strenght && player.Agility >= stat.Agility && player.Spirit >= stat.Spirit && player.Vitality >= stat.Vitality);
       }
        public string InfoStr(byte Class, byte Level)
        {
            var clas_stast = this[PositionArray(Class)];
            Instant stat = clas_stast[(byte)Math.Min(120, (int)Level)];
#if Arabic
            return "You need to have Strenght : " + stat.Strenght + ", Agility : " + stat.Agility + ", Vitality: " + stat.Vitality + ", Spirit: " + stat.Spirit + "";
#else
            return "You need to have Strenght : " + stat.Strenght + ", Agility : " + stat.Agility + ", Vitality: " + stat.Vitality + ", Spirit: " + stat.Spirit + "";
#endif
            
        }
        public void GetStatus(Role.Player player,bool Atreborn =false)
        {
            if (!Atreborn)
            {
                if (player.Level > 120) return;
                if (player.Reborn > 0) return;
            }
            var clas_stast = this[PositionArray(player.Class)];
            Instant stat = clas_stast[player.Level];
            player.Strength = stat.Strenght;
            player.Agility = stat.Agility;
            player.Spirit = stat.Spirit;
            player.Vitality = stat.Vitality;
        }




        public void Load()
        {
            string[] baseplusText = System.IO.File.ReadAllLines(Program.ServerConfig.DbLocation + "Stats.ini");
            foreach (string line in baseplusText)
            {
                if (line.StartsWith("Monk"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 5));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(6005))
                    {
                        this[6005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(6005, sta);
                    }
                }
                else if (line.StartsWith("Archer"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 7));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(4005))
                    {
                        this[4005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(4005, sta);
                    }
                }
                else if (line.StartsWith("Ninja"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 6));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(5005))
                    {
                        this[5005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(5005, sta);
                    }
                }
                else if (line.StartsWith("Taoist"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 7));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(10000))
                    {
                        this[10000].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(10000, sta);
                    }
                }
                else if (line.StartsWith("Trojan"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 7));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(1005))
                    {
                        this[1005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(1005, sta);
                    }
                }
                else if (line.StartsWith("Warrior"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 8));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(2005))
                    {
                        this[2005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(2005, sta);
                    }
                }
                else if (line.StartsWith("Pirate"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 7));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(7005))
                    {
                        this[7005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(7005, sta);
                    }
                }
                else if (line.StartsWith("LongLee"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 8));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(8005))
                    {
                        this[8005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(8005, sta);
                    }
                }
                else if (line.StartsWith("WindWalker"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 11));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(16005))
                    {
                        this[16005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(16005, sta);
                    }
                }
                else if (line.StartsWith("Thunderstriker"))
                {
                    string[] lin = line.Split(']');
                    byte level = byte.Parse(lin[0].Remove(0, 15));

                    string data = lin[1].Remove(0, 1);
                    string[] ne_lin = data.Split(',');

                    Instant stat = new Instant();
                    stat.Strenght = ushort.Parse(ne_lin[0]);
                    stat.Vitality = ushort.Parse(ne_lin[1]);
                    stat.Agility = ushort.Parse(ne_lin[2]);
                    stat.Spirit = ushort.Parse(ne_lin[3]);

                    if (ContainsKey(9005))
                    {
                        this[9005].Add(level, stat);
                    }
                    else
                    {
                        Dictionary<ushort, AtributesStatus.Instant> sta = new Dictionary<ushort, Instant>();
                        sta.Add(level, stat);
                        Add(9005, sta);
                    }
                }
            }
           // MyConsole.WriteLine("Atributes Status was Loading! [Volcano] ");
        }
    }
}
