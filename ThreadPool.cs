using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using GameServer.Game.MsgServer;
using System.Threading.Generic;
using GameServer.Game.MsgFloorItem;
using GameServer.Game.MsgServer.AttackHandler;
using GameServer.Game.MsgTournaments;
using GameServer.Client;
using GameServer.Role.Instance;
using GameServer.Threading;

namespace GameServer.Threading
{
    public class ThreadPool
    {
        public TimerRule<ServerSockets.SecuritySocket> ConnectionReceive ,ConnectionSend, ConnectionReview;

        public static StaticPool GenericThreadPool;
        public static TimerRule<GameClient> MainCallBack, Floor;
        
        public StaticPool ReceivePool,SendPool;
        //public static int Online
        //{
        //    get
        //    {
        //        int current = Pool.GamePoll.Count;
        //        return current;
        //    }
        //}
        //public static int MaxOnline;
       
        public ThreadPool()
        {
            GenericThreadPool = new StaticPool(32).Run();
            ReceivePool = new StaticPool(32).Run();
            SendPool = new StaticPool(32).Run();
            MainCallBack = new TimerRule<GameClient>(_MainCallback, 250);
            ConnectionReceive = new TimerRule<ServerSockets.SecuritySocket>(connectionReceive, 1, ThreadPriority.Highest);
            ConnectionSend = new TimerRule<ServerSockets.SecuritySocket>(connectionSend, 1, ThreadPriority.Highest);
            ConnectionReview = new TimerRule<ServerSockets.SecuritySocket>(_ConnectionReview, 60000, ThreadPriority.Lowest);
            Floor = new TimerRule<GameClient>(FloorCallback, 300, ThreadPriority.BelowNormal);
        }
        public static void _MainCallback(GameClient client, int time)
        {
            try
            {
                if (client == null || client.Player == null || client.Player.View == null)
                {
                    return;
                }

                DateTime now64 = DateTime.Now;
                if (now64 > client.StampAutoAttack)
                {
                   Basic.ThreadInvoke(new Action(client.AutoAttackCallback));
                    client.StampAutoAttack = now64.AddMilliseconds(StampValue.AutoAttack);
                }
                if (now64 > client.StampMainPlayerThread)
                {
                    Basic.ThreadInvoke(new Action(client.MainPlayerThreadCallback));
                    client.StampMainPlayerThread = now64.AddMilliseconds(StampValue.MainPlayer);
                }
                if (now64 > client.StampMonsterThread)
                {
                    Basic.ThreadInvoke(new Action(client.MonsterCallback));
                    client.StampMonsterThread = now64.AddMilliseconds(StampValue.Monster);
                }
            }
            catch (Exception e)
            {
                MyConsole.SaveException(e);
            }
        }
        public static bool Register(GameClient client)
        {
            if (client.TimerSubscriptions == null)
            {
                client.TimerSubscriptions = new IDisposable[]
                {
                   Subscribe(MainCallBack, client),
                   Subscribe(Floor, client)
                };
                return true;
            }
            return false;
        }
        public static void Unregister(GameClient client)
        {
            lock (client.TimerSyncRoot)
            {
                if (client.TimerSubscriptions != null)
                {
                    foreach (var Now in client.TimerSubscriptions)
                        Now.Dispose();
                    client.TimerSubscriptions = null;
                }
            }
        }
        private static unsafe void FloorCallback(Client.GameClient client, int time)
        {
            if (Program.ExitRequested)
                return;
            try
            {
                if (client == null || !client.FullLoading || client.Player == null || client.Fake)
                    return;
                DateTime Now = DateTime.Now;

                if (client.Player.FloorSpells.Count != 0)
                {
                    foreach (var ID in client.Player.FloorSpells)
                    {
                        switch (ID.Key)
                        {



                            #region ShadowOfChaser
                            case (ushort)Role.Flags.SpellID.ShadowofChaser:
                                {

                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);


                                                spellclient.X = spell.FloorPacket.m_X;
                                                spellclient.Y = spell.FloorPacket.m_Y;

                                                spellclient.CreateMsgSpell(0);



                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Range.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Range.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            //AnimationObj.Damage = AnimationObj.Damage * 250 / 100;
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Range.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                            #region HorrorOfStomper
                            case (ushort)Role.Flags.SpellID.HorrorofStomper:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);
                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 1;
                                                spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                                                spellclient.SpellPacket.X = spell.FloorPacket.OwnerX;
                                                spellclient.SpellPacket.Y = spell.FloorPacket.OwnerY;
                                                spellclient.SpellPacket.SpellLevel = spell.DBSkill.Level;


                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    for (int i = 0; i < 2; i++)
                                                    {
                                                        var monster = obj as Game.MsgMonster.MonsterRole;
                                                        byte myAngle = (byte)spell.FloorPacket.Angle;

                                                        if (myAngle > 3)
                                                            myAngle -= 2;
                                                        else
                                                            myAngle += 2;
                                                        if (i != 0)
                                                        {
                                                            if (myAngle > 3)
                                                                myAngle -= 4;
                                                            else
                                                                myAngle += 4;
                                                        }
                                                        uint xxxx = spell.FloorPacket.m_X;
                                                        uint yyyy = spell.FloorPacket.m_Y;
                                                        client.Map.Pushback(ref xxxx, ref yyyy, (Role.Flags.ConquerAngle)myAngle, 7);
                                                        if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) < 9)
                                                        {
                                                            GameServer.Game.MsgServer.AttackHandler.Algoritms.Fan sector = new GameServer.Game.MsgServer.AttackHandler.Algoritms.Fan(spell.FloorPacket.m_X, spell.FloorPacket.m_Y, (ushort)xxxx, (ushort)yyyy, 2, 40);
                                                            if (sector.IsInFan(obj.X, obj.Y))
                                                            {
                                                                if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                                {
                                                                    Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                                    Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                                    Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                                    AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                                    spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                                }
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    for (int i = 0; i < 2; i++)
                                                    {
                                                        byte myAngle = (byte)spell.FloorPacket.Angle;

                                                        if (myAngle > 3)
                                                            myAngle -= 2;
                                                        else
                                                            myAngle += 2;
                                                        if (i != 0)
                                                        {
                                                            if (myAngle > 3)
                                                                myAngle -= 4;
                                                            else
                                                                myAngle += 4;
                                                        }
                                                        uint xxxx = spell.FloorPacket.m_X;
                                                        uint yyyy = spell.FloorPacket.m_Y;
                                                        client.Map.Pushback(ref xxxx, ref yyyy, (Role.Flags.ConquerAngle)myAngle, 7);
                                                        var target = obj as Role.SobNpc;

                                                        GameServer.Game.MsgServer.AttackHandler.Algoritms.Fan sector = new GameServer.Game.MsgServer.AttackHandler.Algoritms.Fan(spell.FloorPacket.m_X, spell.FloorPacket.m_Y, (ushort)xxxx, (ushort)yyyy, 2, 40);
                                                        if (sector.IsInFan(obj.X, obj.Y))
                                                        {
                                                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                            {
                                                                //     return;
                                                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                                Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                                Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                                AnimationObj.Hit = 1;//??
                                                                spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                            }
                                                            break;
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                {

                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                                }


                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion
                            #region PeaceOfStomper
                            case (ushort)Role.Flags.SpellID.PeaceofStomper:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);
                                                spellclient.UID = spell.FloorPacket.ItemOwnerUID;
                                                spellclient.X = spell.FloorPacket.m_X;
                                                spellclient.Y = spell.FloorPacket.m_Y;

                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.bomb = 1;

                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            //    return;
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??
                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);


                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));


                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);

                                    }
                                    break;
                                }
                            #endregion
                            #region SeaBurial
                            case (ushort)Role.Flags.SpellID.SeaBurial:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);

                                                uint xX = spell.FloorPacket.m_X, yY = spell.FloorPacket.m_Y;
                                                client.Map.Pushback(ref xX, ref yY, Role.Core.GetAngle(spell.FloorPacket.m_X, spell.FloorPacket.m_Y, spell.FloorPacket.OwnerX, spell.FloorPacket.OwnerY), 18);


                                                spellclient.CreateMsgSpell(0);
                                                spellclient.SpellPacket.UID = spell.FloorPacket.m_UID;
                                                spellclient.SpellPacket.X = (ushort)xX;
                                                spellclient.SpellPacket.Y = (ushort)yY;
                                                spellclient.SpellPacket.bomb = 1;
                                                Game.MsgServer.AttackHandler.Algoritms.InLineAlgorithm Line = new Game.MsgServer.AttackHandler.Algoritms.InLineAlgorithm(spell.FloorPacket.m_X, spell.FloorPacket.OwnerX, spell.FloorPacket.m_Y, spell.FloorPacket.OwnerY, client.Map, 18, 0);

                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            //AnimationObj.Damage = AnimationObj.Damage * 60 / 100;
                                                            // AnimationObj.Damage = AnimationObj.Damage * 95 / 100;
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Line.InLine(obj.X, obj.Y, 2))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);
                                                spell.FloorPacket.Timer = 0;
                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }

                                    break;
                                }
                            #endregion
                            #region TideTrap
                            case (ushort)Role.Flags.SpellID.TideTrap:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();


                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);
                                                //if (Attack)
                                                spellclient.X = spell.FloorPacket.OwnerX;
                                                spellclient.Y = spell.FloorPacket.OwnerY;

                                                spellclient.CreateMsgSpell(0);

                                                ushort X = spell.FloorPacket.m_X, Y = spell.FloorPacket.m_Y;
                                                var coord = Game.MsgServer.AttackHandler.Algoritms.MoveCoords.CheckBladeTeampsCoords(spellclient.X, spellclient.Y, X
                                                , Y, null, 12);

                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    bool hit = false;
                                                    for (int j = 0; j < coord.Count; j++)
                                                        if (Game.MsgServer.AttackHandler.Calculate.Base.GetDDistance(obj.X, obj.Y, (ushort)coord[j].X, (ushort)coord[j].Y) <= 2)
                                                            hit = true;
                                                    if (hit)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    bool hit = false;
                                                    for (int j = 0; j < coord.Count; j++)
                                                        if (Game.MsgServer.AttackHandler.Calculate.Base.GetDDistance(obj.X, obj.Y, (ushort)coord[j].X, (ushort)coord[j].Y) <= 2)
                                                            hit = true;
                                                    if (hit)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            AnimationObj.Damage = AnimationObj.Damage * 60 / 100;
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    bool hit = false;
                                                    for (int j = 0; j < coord.Count; j++)
                                                        if (Game.MsgServer.AttackHandler.Calculate.Base.GetDDistance(obj.X, obj.Y, (ushort)coord[j].X, (ushort)coord[j].Y) <= 2)
                                                            hit = true;
                                                    if (hit)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }

                                    break;
                                }
                            #endregion
                            #region RageOfWar
                            case (ushort)Role.Flags.SpellID.RageofWar:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);
                                                //if (Attack)
                                                spellclient.X = spell.FloorPacket.m_X;
                                                spellclient.Y = spell.FloorPacket.m_Y;

                                                spellclient.CreateMsgSpell(0);


                                                spellclient.SpellPacket.bomb = 1;
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            AnimationObj.Damage = AnimationObj.Damage * 60 / 100;
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }

                                    break;
                                }
                            #endregion
                            #region WrathOfTheEmperor & InfernalEcho
                            case (ushort)Role.Flags.SpellID.WrathoftheEmperor:
                            case (ushort)Role.Flags.SpellID.InfernalEcho:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;
                                                RemoveSpells.Enqueue(spell);

                                                spellclient.X = spell.FloorPacket.m_X;
                                                spellclient.Y = spell.FloorPacket.m_Y;

                                                spellclient.CreateMsgSpell(0);


                                                spellclient.SpellPacket.bomb = 1;
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= (int)(ID.Key == (ushort)Role.Flags.SpellID.WrathoftheEmperor ? 2 : 3))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= (int)(ID.Key == (ushort)Role.Flags.SpellID.WrathoftheEmperor ? 2 : 3))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj);
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= (int)(ID.Key == (ushort)Role.Flags.SpellID.WrathoftheEmperor ? 2 : 3))
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                     p => Role.Core.GetDistance(p.X, p.Y, spellclient.X, spellclient.Y) <= 18))
                                                    user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                            }
                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }

                                    break;
                                }
                            #endregion
                            #region TwilightDance
                            case (ushort)Role.Flags.SpellID.TwilightDance:
                                {
                                    var spellclient = ID.Value;
                                    Queue<Role.FloorSpell> RemoveSpells = new Queue<Role.FloorSpell>();

                                    using (var rec = new ServerSockets.RecycledPacket())
                                    {
                                        var stream = rec.GetStream();
                                        var spells = spellclient.Spells.ToArray();
                                        foreach (var spell in spells)
                                        {
                                            if (spellclient.CheckInvocke(Now, spell))
                                            {
                                                uint Experience = 0;

                                                RemoveSpells.Enqueue(spell);
                                                spellclient.CreateMsgSpell(client.Player.UID);

                                                int increased_attack = 0;
                                                if (spellclient.Spells.Count == 3)
                                                    increased_attack = 15;
                                                else if (spellclient.Spells.Count == 2)
                                                    increased_attack = 25;
                                                else if (spellclient.Spells.Count == 1)
                                                    increased_attack = 35;

                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Monster))
                                                {
                                                    var monster = obj as Game.MsgMonster.MonsterRole;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(client, monster, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnMonster(client.Player, monster, spell.DBSkill, out AnimationObj);
                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, client, monster);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.Player))
                                                {
                                                    var target = obj as Role.Player;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnPlayer(client.Player, target, spell.DBSkill, out AnimationObj, true, increased_attack);
                                                            //int increased_attack = 0;
                                                            if (spellclient.Spells.Count == 3)
                                                                AnimationObj.Damage = AnimationObj.Damage * 100 / 100;
                                                            else if (spellclient.Spells.Count == 2)
                                                                AnimationObj.Damage = AnimationObj.Damage * 110 / 100;
                                                            else if (spellclient.Spells.Count == 1)
                                                                AnimationObj.Damage = AnimationObj.Damage * 120 / 100;
                                                            Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, client, target);
                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                foreach (var obj in client.Player.View.Roles(Role.MapObjectType.SobNpc))
                                                {
                                                    var target = obj as Role.SobNpc;

                                                    if (Role.Core.GetDistance(obj.X, obj.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 3)
                                                    {
                                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(client, target, spell.DBSkill))
                                                        {
                                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                            Game.MsgServer.AttackHandler.Calculate.Physical.OnNpcs(client.Player, target, spell.DBSkill, out AnimationObj);


                                                            Experience += Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, client, target);


                                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, spellclient.LevelHu);
                                                            AnimationObj.Hit = 1;//??

                                                            spellclient.SpellPacket.Targets.Enqueue(AnimationObj);
                                                        }
                                                    }
                                                }
                                                spellclient.SendView(stream, client);

                                                ActionQuery action = new ActionQuery()
                                                {
                                                    ObjId = spell.FloorPacket.ItemOwnerUID,
                                                    TargetPositionY = spell.FloorPacket.m_Y,
                                                    TargetPositionX = spell.FloorPacket.m_X,
                                                    PositionX = spell.FloorPacket.OwnerX,
                                                    PositionY = spell.FloorPacket.OwnerY,
                                                    Type = ActionType.RemoveTrap
                                                };

                                                //client.Player.View.SendView(stream.ActionCreate(action), false);

                                                spell.FloorPacket.DropType = Game.MsgFloorItem.MsgDropID.RemoveEffect;

                                                foreach (var user in spellclient.GMap.View.Roles(Role.MapObjectType.Player, spellclient.X, spellclient.Y,
                                                 p => Role.Core.GetDistance(p.X, p.Y, spell.FloorPacket.m_X, spell.FloorPacket.m_Y) <= 18))
                                                {
                                                    if (user.DynamicID == client.Player.DynamicID)
                                                    {
                                                        user.Send(stream.ActionCreate(action));

                                                        user.Send(stream.ItemPacketCreate(spell.FloorPacket));
                                                    }
                                                }

                                                Game.MsgServer.AttackHandler.Updates.IncreaseExperience.Up(stream, client, Experience);

                                            }

                                        }
                                    }
                                    while (RemoveSpells.Count > 0)
                                        spellclient.RemoveItem(RemoveSpells.Dequeue());

                                    if (spellclient.Spells.Count == 0)
                                    {
                                        Role.FloorSpell.ClientFloorSpells FloorSpell;
                                        client.Player.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                    }
                                    break;
                                }
                            #endregion

                        }
                    }
                }

                #region Check Items
                foreach (var item in client.Player.View.Roles(Role.MapObjectType.Item))
                {
                    if (!item.Alive)
                    {
                        using (var rec = new ServerSockets.RecycledPacket())
                        {
                            var stream = rec.GetStream();
                            var PItem = item as Game.MsgFloorItem.MsgItem;
                            if (PItem.ItemBase != null) PItem.ItemBase.Color = 0;
                            PItem.SendAll(stream, item.IsTrap() ? Game.MsgFloorItem.MsgDropID.RemoveEffect : Game.MsgFloorItem.MsgDropID.Remove);
                            client.Map.View.LeaveMap<Role.IMapObj>(item);
                            continue;
                        }
                    }
                    if (item.IsTrap())
                    {
                        if (client.Player.Map == 4006 && client.Player.JoinTowerOfMysteryLayer == 7)
                        {
                            if (!(Role.Core.GetDistance(client.Player.X, client.Player.Y, 44, 62) <= 3))
                            {
                                if (Role.Core.GetDistance(client.Player.X, client.Player.Y, item.X, item.Y) <= 2)
                                {

                                    if (DateTime.Now > client.Player.TowerOfMysteryFrezeeStamp)
                                    {
                                        client.Player.AddFlag(MsgUpdate.Flags.Freeze, 3, true);
                                        client.Player.TowerOfMysteryFrezeeStamp = DateTime.Now.AddSeconds(5);
                                    }

                                }
                                foreach (var user in client.Player.View.Roles(Role.MapObjectType.Player))
                                {
                                    if (Role.Core.GetDistance(user.X, user.Y, item.X, item.Y) <= 2)
                                    {
                                        var _user = user as Role.Player;
                                        if (DateTime.Now > _user.TowerOfMysteryFrezeeStamp)
                                        {
                                            _user.AddFlag(MsgUpdate.Flags.Freeze, 3, true);
                                            _user.TowerOfMysteryFrezeeStamp = DateTime.Now.AddSeconds(5);
                                        }

                                    }
                                }
                            }
                        }
                        var FloorItem = item as Game.MsgFloorItem.MsgItem;
                        if (FloorItem.ItemBase == null)
                            continue;
                        if (FloorItem.ItemBase.ITEM_ID == Game.MsgFloorItem.MsgItemPacket.NormalDaggerStorm
                           || FloorItem.ItemBase.ITEM_ID == Game.MsgFloorItem.MsgItemPacket.SoulOneDaggerStorm
                           || FloorItem.ItemBase.ITEM_ID == Game.MsgFloorItem.MsgItemPacket.SoulTwoDaggerStorm)
                        {
                            if (Now >= FloorItem.AttackStamp.AddMilliseconds(900))
                            {
                                FloorItem.AttackStamp = Now;
                                using (var rec = new ServerSockets.RecycledPacket())
                                {
                                    var stream = rec.GetStream();
                                    MsgSpellAnimation MsgSpell = new MsgSpellAnimation(FloorItem.OwnerEffert.Player.UID
                                        , 0, FloorItem.X, FloorItem.Y, FloorItem.DBSkill.ID
                                        , FloorItem.DBSkill.Level, FloorItem.OwnerEffert.MySpells.ClientSpells[FloorItem.DBSkill.ID].UseSpellSoul);
                                    foreach (var _monster in FloorItem.GMap.View.Roles(Role.MapObjectType.Monster, FloorItem.X, FloorItem.Y
                                        , p => Role.Core.GetDistance(p.X, p.Y, FloorItem.MsgFloor.m_X, FloorItem.MsgFloor.m_Y) <= 3))
                                    {
                                        var monster = _monster as Game.MsgMonster.MonsterRole;
                                        if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackMonster.Verified(FloorItem.OwnerEffert, monster, FloorItem.DBSkill))
                                        {
                                            Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                            Game.MsgServer.AttackHandler.Calculate.Range.OnMonster(FloorItem.OwnerEffert.Player, monster, FloorItem.DBSkill, out AnimationObj);
                                            Game.MsgServer.AttackHandler.ReceiveAttack.Monster.Execute(stream, AnimationObj, FloorItem.OwnerEffert, monster);
                                            AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, FloorItem.SpellSoul);


                                            MsgSpell.Targets.Enqueue(AnimationObj);

                                        }
                                    }
                                    foreach (var player in FloorItem.GMap.View.Roles(Role.MapObjectType.Player, FloorItem.X, FloorItem.Y
                                        , p => Game.MsgServer.AttackHandler.Calculate.Base.GetDistance(p.X, p.Y, FloorItem.MsgFloor.m_X, FloorItem.MsgFloor.m_Y) <= 3))
                                    {
                                        if (player.UID != FloorItem.OwnerEffert.Player.UID)
                                        {
                                            var atacked = player as Role.Player;
                                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackPlayer.Verified(FloorItem.OwnerEffert, atacked, FloorItem.DBSkill))
                                            {

                                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                Game.MsgServer.AttackHandler.Calculate.Range.OnPlayer(FloorItem.OwnerEffert.Player, atacked, FloorItem.DBSkill, out AnimationObj);
                                                Game.MsgServer.AttackHandler.ReceiveAttack.Player.Execute(AnimationObj, FloorItem.OwnerEffert, atacked);
                                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, FloorItem.SpellSoul);

                                                MsgSpell.Targets.Enqueue(AnimationObj);

                                            }
                                        }

                                    }
                                    foreach (var player in FloorItem.GMap.View.Roles(Role.MapObjectType.SobNpc, FloorItem.X, FloorItem.Y
                                        , p => Game.MsgServer.AttackHandler.Calculate.Base.GetDistance(p.X, p.Y, FloorItem.MsgFloor.m_X, FloorItem.MsgFloor.m_Y) <= 3))
                                    {
                                        if (player.UID != FloorItem.OwnerEffert.Player.UID)
                                        {
                                            var atacked = player as Role.SobNpc;
                                            if (Game.MsgServer.AttackHandler.CheckAttack.CanAttackNpc.Verified(FloorItem.OwnerEffert, atacked, FloorItem.DBSkill))
                                            {

                                                Game.MsgServer.MsgSpellAnimation.SpellObj AnimationObj;
                                                Game.MsgServer.AttackHandler.Calculate.Range.OnNpcs(FloorItem.OwnerEffert.Player, atacked, FloorItem.DBSkill, out AnimationObj);
                                                Game.MsgServer.AttackHandler.ReceiveAttack.Npc.Execute(stream, AnimationObj, FloorItem.OwnerEffert, atacked);
                                                AnimationObj.Damage = Game.MsgServer.AttackHandler.Calculate.Base.CalculateSoul(AnimationObj.Damage, FloorItem.SpellSoul);


                                                MsgSpell.Targets.Enqueue(AnimationObj);

                                            }
                                        }

                                    }

                                    MsgSpell.SetStream(stream);
                                    MsgSpell.Send(client);
                                }
                            }
                        }

                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                MyConsole.WriteException(e);
            }

        }
        public static void connectionReceive(ServerSockets.SecuritySocket wrapper, int time)
        {
            if (wrapper.ReceiveBuffer())
            {
                wrapper.HandlerBuffer();
            }
        }
        public static void connectionSend(ServerSockets.SecuritySocket wrapper, int time)
        {
            ServerSockets.SecuritySocket.TrySend(wrapper);
        }
        public static void _ConnectionReview(ServerSockets.SecuritySocket wrapper, int time)
        {
            ServerSockets.SecuritySocket.TryReview(wrapper);
        }
        #region Funcs
        //public static IDisposable Subscribe(Action<int> action, int period = 1, ThreadPriority priority = ThreadPriority.Normal)
        //{
        //    return GenericThreadPool.Subscribe(new TimerRule(action, period, priority));
        //}
        //public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StaticPool pool)
        //{
        //    return pool.Subscribe<T>(rule, param);
        //}
        //public static IDisposable Subscribe<T>(TimerRule<T> rule, T param)
        //{
        //    return GenericThreadPool.Subscribe<T>(rule, param);
        //}
        #endregion
        #region Funcs
        public static void Execute(Action<int> action, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            GenericThreadPool.Subscribe(new LazyDelegate(action, timeOut, priority));
        }
        public static void Execute<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            GenericThreadPool.Subscribe<T>(new LazyDelegate<T>(action, timeOut, priority), param);
        }
        public static IDisposable Subscribe(Action<int> action, int period = 1, ThreadPriority priority = ThreadPriority.Normal)
        {
            return GenericThreadPool.Subscribe(new TimerRule(action, period, priority));
        }
        public static IDisposable Subscribe<T>(Action<T, int> action, T param, int timeOut = 0, ThreadPriority priority = ThreadPriority.Normal)
        {
            return GenericThreadPool.Subscribe<T>(new TimerRule<T>(action, timeOut, priority), param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StandalonePool pool)
        {
            return pool.Subscribe<T>(rule, param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param, StaticPool pool)
        {
            return pool.Subscribe<T>(rule, param);
        }
        public static IDisposable Subscribe<T>(TimerRule<T> rule, T param)
        {
            return GenericThreadPool.Subscribe<T>(rule, param);
        }
        #endregion

    }
}