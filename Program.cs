// * Created by AccServer
// * Copyright © 2020-2021
// * AccServer - Project

using System;
using AccServer.Network;
using AccServer.Database;
using System.Windows.Forms;
using AccServer.Network.Sockets;
using AccServer.Network.AuthPackets;
using System.Threading.Tasks;
using System.IO;
namespace AccServer
{
    public unsafe class Program
    {
        public static Counter EntityUID;
        public static FastRandom Random = new FastRandom();
        public static ServerSocket AuthServer;
        public static World World;
        public static ushort Port = 9961;
        private static object SyncLogin;
        private static System.Collections.Concurrent.ConcurrentDictionary<uint, int> LoginProtection;
        private const int TimeLimit = 10000;
        private static void WorkConsole()
        {
            while (true)
            {
                try
                {
                    CommandsAI(Console.ReadLine());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        static void Main(string[] args)
        {
            Console.Title = "Account Server";
            World = new World();
            World.Init();
            EntityUID = new Counter(0);
            LoginProtection = new System.Collections.Concurrent.ConcurrentDictionary<uint, int>();
            SyncLogin = new object();
            DataHolder.CreateConnection("regear", "3243418..");
            Database.Server.Load();
            Network.Cryptography.AuthCryptography.PrepareAuthCryptography();
            AuthServer = new ServerSocket();
            AuthServer.OnClientConnect += AuthServer_OnClientConnect;
            AuthServer.OnClientReceive += AuthServer_OnClientReceive;
            AuthServer.OnClientDisconnect += AuthServer_OnClientDisconnect;
            AuthServer.Enable(Port, "0.0.0.0");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("------AccServer-------");
            Console.ForegroundColor = ConsoleColor.Gray;
            WorkConsole();
            CommandsAI(Console.ReadLine());
        }
        public static void CommandsAI(string command)
        {
            if (command == null) return;
            string[] data = command.Split(' ');
            switch (data[0].ToLower())
            {
                case "@memo":
                    {
                        var proc = System.Diagnostics.Process.GetCurrentProcess();
                        Console.WriteLine("Thread count: " + proc.Threads.Count);
                        Console.WriteLine("Memory set(MB): " + ((double)((double)proc.WorkingSet64 / 1024)) / 1024);
                        proc.Close();
                        break;
                    }
                case "@clear":
                    {
                        Console.Clear();
                        break;
                    }
                case "banned":
                    {
                        AccountTable.Banned12(data[1]);
                        break;
                    }
                case "unbanned":
                    {
                        AccountTable.Banned12(data[1], true);
                        break;
                    }
                case "@restart":
                    {
                        AuthServer.Disable();
                        Application.Restart();
                        Environment.Exit(0);
                        break;
                    }
            }
        }

        private static void AuthServer_OnClientReceive(byte[] buffer, int length, ClientWrapper arg3)
        {
            var player = arg3.Connector as Client.AuthClient;
            player.Cryptographer.Decrypt(buffer, length);
            player.Queue.Enqueue(buffer, length);
            while (player.Queue.CanDequeue())
            {
                byte[] packet = player.Queue.Dequeue();
                ushort len = BitConverter.ToUInt16(packet, 0);
                ushort ID = BitConverter.ToUInt16(packet, 2);
                if (len == 472 && ID == 1742)
                {
                    player.Info = new Authentication();
                    player.Info.Deserialize(packet);
                    player.Account = new AccountTable(player.Info.Username);
                    Database.ServerInfo Server = null;
                    Forward Fw = new Forward();
                    if (Database.Server.Servers.TryGetValue(player.Info.Server, out Server))
                    {
                        if (!player.Account.exists)
                            Fw.Type = Forward.ForwardType.WrongAccount;
                        else
                        {
                            if (player.Account.Password == player.Info.Password)
                            {
                                if (!player.Account.Banned)
                                {
                                    Fw.Type = Forward.ForwardType.Ready;
                                    //AccountTable.UpdateEarth(player.Info.Username, player.Info.MacAddress);
                                    AccountTable.UpdateIP(player.Info.Username, player.IP);
                                    if (player.Account.EntityID == 0)
                                    {
                                        using (MySqlCommand cmd = new MySqlCommand(MySqlCommandType.SELECT))
                                        {
                                            cmd.Select("configuration");
                                            using (MySqlReader r = new MySqlReader(cmd))
                                            {
                                                if (r.Read())
                                                {
                                                    EntityUID = new Counter(r.ReadUInt32("ID"));
                                                    player.Account.EntityID = EntityUID.Next;
                                                    using (MySqlCommand cmd2 = new MySqlCommand(MySqlCommandType.UPDATE).Update("configuration")
                                                    .Set("ID", player.Account.EntityID)) cmd2.Execute();
                                                    player.Account.Save();
                                                }
                                            }
                                        }
                                    }
                                }
                                else Fw.Type = Forward.ForwardType.Banned;
                            }
                            else
                                Fw.Type = Forward.ForwardType.InvalidInfo;
                        }
                        lock (SyncLogin)
                        {
                            if (Fw.Type == Forward.ForwardType.Ready)
                            {
                                Fw.Identifier = player.Account.EntityID;
                                Fw.IP = Server.IP;
                                Fw.Port = Server.Port;
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("{0} is connect to {1}! IP: {2}.", player.Info.Username, player.Info.Server, player.IP);
                                String folderN = DateTime.Now.Year + "-" + DateTime.Now.Month,
                                       Path = "LogMacID\\",
                                        NewPath = System.IO.Path.Combine(Path, folderN);
                                if (!File.Exists(NewPath + folderN))
                                {
                                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, folderN));
                                }
                                if (!File.Exists(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                                {
                                    using (System.IO.FileStream fs = System.IO.File.Create(NewPath + "\\" + DateTime.Now.Day + ".txt"))
                                    {
                                        fs.Close();
                                    }
                                }
                                using (System.IO.StreamWriter file = new System.IO.StreamWriter(NewPath + "\\" + DateTime.Now.Day + ".txt", true))
                                {
                                    file.WriteLine("UserName[Acc] |" + player.Info.Username + "| UIDAccount |" + player.Account.EntityID + " | IPAccount | " + player.IP + " |  PasswordAccount  [" + player.Info.Password + "] [The Clock] " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);
                                    file.Close();
                                }
                            }
                            player.Send(Fw);
                        }
                    }
                    else
                        arg3.Disconnect();

                }
                else
                {
                    Forward Fw = new Forward();
                    Fw.Type = Forward.ForwardType.InvalidAuthenticationProtocol;
                    player.Send(Fw);
                }
            }
        }
        private static void AuthServer_OnClientDisconnect(ClientWrapper obj)
        {
            obj.Disconnect();
        }
        private static void AuthServer_OnClientConnect(ClientWrapper obj)
        {
            Client.AuthClient authState;
            obj.Connector = (authState = new Client.AuthClient(obj));
            authState.Cryptographer = new Network.Cryptography.AuthCryptography();
            Network.AuthPackets.PasswordCryptographySeed pcs = new PasswordCryptographySeed();
            pcs.Seed = Program.Random.Next();
            authState.PasswordSeed = pcs.Seed;
            authState.Send(pcs);
        }
    }
}