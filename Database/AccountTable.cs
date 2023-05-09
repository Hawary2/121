// * Created by AccServer
// * Copyright © 2020-2021
// * AccServer - Project

using System;
using System.IO;
using System.Text;

namespace AccServer.Database
{
    public unsafe class AccountTable
    {
        public enum AccountState : byte
        {
            NotActivated = 100,
            ProjectManager = 255,
            GameHelper = 5,
            GameMaster = 3,
            Player = 2,
            Banned = 1,
            DoesntExist = 0
        }
        public string Username;
        public string Password;
        public string IP;
        public AccountState State;
        public uint EntityID;
        public bool exists = false;
        public bool Banned;

        public AccountTable(string username)
        {
            if (username == null) return;
            Username = username;
            Password = "";
            IP = "";
            State = AccountState.DoesntExist;
            EntityID = 0;
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("accounts").Where("Username", username))
            using (var reader = new MySqlReader(cmd))
            {
                if (reader.Read())
                {
                    exists = true;
                    Password = reader.ReadString("Password");
                    IP = reader.ReadString("IP");
                    EntityID = reader.ReadUInt32("ID");
                    State = (AccountState)reader.ReadInt32("State");
                    if (State == AccountState.Banned)
                    {
                        Banned = true;
                    }
                }
            }
        }
        public static void UpdateEarth(string Account, string str)
        {
            using (MySqlCommand command = new MySqlCommand(MySqlCommandType.SELECT).Select("accounts").Where("Username", Account))
            {
                using (MySqlReader reader = new MySqlReader(command))
                {
                    if (reader.Read())
                    {
                        using (MySqlCommand command2 = new MySqlCommand(MySqlCommandType.UPDATE))
                        {
                            command2.Update("accounts").Set("EarthID", str).Where("Username", Account).Execute();
                        }
                    }
                }
            }
        }
        public static void UpdateIP(string Account, string str)
        {
            using (MySqlCommand command = new MySqlCommand(MySqlCommandType.SELECT).Select("accounts").Where("Username", Account))
            {
                using (MySqlReader reader = new MySqlReader(command))
                {
                    if (reader.Read())
                    {
                        using (MySqlCommand command2 = new MySqlCommand(MySqlCommandType.UPDATE))
                        {
                            command2.Update("accounts").Set("IP", str).Where("Username", Account).Execute();
                        }
                    }
                }
            }
        }
        public static void Banned12(string Account,bool unbanned = false)
        {
            using (MySqlCommand command = new MySqlCommand(MySqlCommandType.SELECT).Select("accounts").Where("Username", Account))
            {
                using (MySqlReader reader = new MySqlReader(command))
                {
                    if (reader.Read())
                    {
                        if (unbanned)
                        {
                            using (MySqlCommand command2 = new MySqlCommand(MySqlCommandType.UPDATE))
                            {
                                command2.Update("accounts").Set("State", 0).Where("Username", Account).Execute();
                            }
                        }
                        else
                        {
                            using (MySqlCommand command2 = new MySqlCommand(MySqlCommandType.UPDATE))
                            {
                                command2.Update("accounts").Set("State", 1).Where("Username", Account).Execute();
                            }
                        }
                        Console.WriteLine("Done");
                    }
                    else
                    {
                        Console.WriteLine("This Account DoesntExist");
                        
                    }
                }
            }
        }
        public void Save()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("accounts").Set("ID", EntityID)
                    .Where("Username", Username).Execute();
        }
    }
}