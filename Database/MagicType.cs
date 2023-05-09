using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
//using IDodge.DBFunctionality;

namespace GameServer.Database
{
    public class MagicType : Dictionary<ushort, Dictionary<ushort, MagicType.Magic>>
    {
        public enum WeaponsType : ushort
        {
            Boxing = 0,
            Blade = 410,
            Sword = 420,
            Backsword = 421,
            Hook = 430,
            Whip = 440,
            Mace = 441,
            Axe = 450,
            Hammer = 460,
            Crutch = 470,
            Club = 480,
            Scepter = 481,
            Dagger = 490,
            Prod = 491,
            Fan = 492,
            Flute = 493,
            Glaive = 510,
            Scythe = 511,
            Epee = 520,
            Zither = 521,
            Lute = 522,
            Poleaxe = 530,
            LongHammer = 540,
            //Scythe = 550,
            Spear = 560,
            Pickaxe = 562,
            Spade = 570,
            Halbert = 580,
            Wand = 561,
            Bow = 500,
            NinjaSword = 601,
            ThrowingKnife = 613,

            //Boxing = 700,
            //Blade = 710,
            //Sword = 720,
            MagicSword = 721,
            //Hook = 730,
            //Whip = 740,
            //Mace = 741,
            //Axe = 750,
            //Hammer = 760,
            //Crutch = 770,
            //Club = 780,
            //Scepter = 781,
            //Dagger = 790,
            //Prod = 791,
            //Fan = 792,
            //Flute = 793,
            Shield = 900,
            Other = 422,
            PrayerBeads = 610,
            Rapier = 611,
            Pistol = 612,
            CrossSaber = 614,
            TwistedClaw = 616,
            Nunchaku = 617,
            Fist = 624,
            WindFan = 626,
            OceanDominator = 670,
            EpicRapier = 671,
            Flashaxe = 680,
            Stormhammer = 681
        }
        public enum MagicSort
        {

            Attack = 1,
            Recruit = 2,
            Cross = 3,
            Sector = 4,
            Bomb = 5,
            AttachStatus = 6,
            DetachStatus = 7,
            Square = 8,
            JumpAttack = 9,
            RandomTransport = 10,
            DispatchXp = 11,
            Collide = 12,
            SerialCut = 13,
            Line = 14,
            AtkRange = 15,
            AttackStatus = 16,
            CallTeamMember = 17,
            RecordTransportSpell = 18,
            Transform = 19,
            AddMana = 20,
            LayTrap = 21,
            Dance = 22,
            CallPet = 23,
            Vampire = 24,
            Instead = 25,
            DecLife = 26,
            Toxic = 27,
            ShurikenVortex = 28,
            CounterKill = 29,

            Spook = 30,
            WarCry = 31,
            Riding = 32,
            Shock = 34,
            ChainBolt = 37,
            StarArrow = 38,
            DragonWhirl = 40,
            RemoveBuffers = 46,
            Tranquility = 47,
            DirectAttack = 48,
            Compasion = 50,
            Auras = 51,
            ShieldBlock = 52,
            Oblivion = 53,
            WhirlwindKick = 54,
            PhysicalSpells = 55,
            ScurvyBomb = 56,
            CannonBarrage = 57,
            BlackSpot = 58,
            Summon = 72,
            BombLine = 60,
            MoveLine = 61,
            AddBlackSpot = 62,
            PirateXpSkill = 63,
            ChargingVortex = 64,
            MortalDrag = 65,
            KineticSpark = 67,
            BladeFlurry = 68,
            SectorBack = 69,
            BreathFocus = 70,
            FatalCross = 71,
            Summons = 72,
            FatalSpin = 73,
            DragonCyclone = 75,
            StraightFist = 76,
            Perimeter = 78,
            AirKick = 79,
            Strike = 81,
            SectorPasive = 82,
            ManiacDance = 83,
            Omnipotence = 85,
            Pounce = 84,
            BurntFrost = 86,

            Rectangle = 87,
            RemoveStamin = 88,
            PetAttachStatus = 89,
            Wildwind = 90,
            SwirlingStorm = 91,
            Pitching = 92,
            ThunderRampage = 94,
            HeavensWrath = 95,
            HellVortex = 98,
        }
        //public static Dictionary<uint, Magic> RuneSpell = new Dictionary<uint, Magic>();
        public void Load()
        {
            WindowsAPI.IniFile ini = new WindowsAPI.IniFile("MagicEffect.ini");
            if (File.Exists(Program.ServerConfig.DbLocation + "magictype.txt"))
            {
                using (StreamReader read = File.OpenText(Program.ServerConfig.DbLocation + "magictype.txt"))
                {
                    string Linebas;
                    while ((Linebas = read.ReadLine()) != null)
                    {
                        string[] line = Linebas.Split(new string[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);

                        Magic spell = new Magic();

                        spell.ID = ushort.Parse(line[1]);
                        spell.isRuneSkill = ini.ReadByte(spell.ID.ToString() + "00", "IsRuneSkill", 0) != 0;
                        //spell.AttackInFly = AttackInFlay(spell);
                        spell.AttackInFly = byte.Parse(line[5]) == 0;
                        spell.isTrojanArchiveSkill = (spell.ID >= (ushort)Role.Flags.SpellID.ThunderStrike && spell.ID <= (ushort)Role.Flags.SpellID.AxeShadow);
                        if (!AttackInFlay(spell) && spell.AttackInFly)
                            spell.AttackInFly = AttackInFlay(spell);

                        spell.Type = (MagicSort)byte.Parse(line[2]);
                        spell.Name = line[3];
                        spell.Level = byte.Parse(line[8]);
                        spell.UseMana = ushort.Parse(line[9]);


                        if (spell.ID == (ushort)Role.Flags.SpellID.RevengeTail || spell.ID == (ushort)Role.Flags.SpellID.Duel || spell.ID == (ushort)Role.Flags.SpellID.Sacrifice || spell.ID == (ushort)Role.Flags.SpellID.DragonSwing || spell.ID == (ushort)Role.Flags.SpellID.RiseofTaoism || spell.ID == (ushort)Role.Flags.SpellID.FuryStrike || spell.ID == (ushort)Role.Flags.SpellID.FineRain)
                            spell.Damage = int.Parse(line[10]);
                        else if (spell.ID == (ushort)Role.Flags.SpellID.ToxicFog)
                            spell.Damage = (byte)((double.Parse(line[10]) % 100));
                        else
                        {
                            if ((spell.Type == MagicSort.AddMana || spell.Type == MagicSort.Recruit || spell.Type == MagicSort.Attack) && double.Parse(line[10]) < 10000 || spell.Type == MagicSort.Auras)
                                spell.Damage = int.Parse(line[10]);
                            else
                                spell.Damage = Math.Min((float)(double.Parse(line[10]) % 1000), 500);//(float)((double.Parse(line[10]) % 1000) / 10000);
                        }
                        if (spell.ID == 12580 || spell.ID == 12590 || spell.ID == 12600)
                            spell.Damage = 100;



                        spell.CoolDown = ushort.Parse(line[33]);//(ushort)Math.Max(500, uint.Parse(line[11]));
                        if (spell.ID == 10415)
                            spell.CoolDown = 800;
                        if (spell.Damage == 0)
                            spell.Damage = 100;
                        if (spell.ID == 11140)
                            spell.Damage = 150;

                        if (spell.ID == 10490)
                            spell.Damage = 150;
                        if (spell.ID == 3306)
                            spell.Damage = 1;
                        if (spell.ID == 12380)
                            spell.Damage = 5000;
                        spell.Rate = int.Parse(line[12]);
                        if (spell.ID == (ushort)Role.Flags.SpellID.Celestial)
                            spell.Rate += 30;
                        //  spell.Damage = (float)(double.Parse(line[10]));

                        spell.MaxTargets = int.Parse(line[14]);
                        spell.Experience = uint.Parse(line[18]);
                        spell.NeedLevel = ushort.Parse(line[20]);

                        spell.WeaponType = uint.Parse(line[22]);
                        spell.UseStamina = byte.Parse(line[29]);
                        if (spell.ID == (ushort)Role.Flags.SpellID.ToxicFog)
                            spell.Duration = 20;
                        else
                            spell.Duration = uint.Parse(line[13]);
                        spell.CpsCost = ushort.Parse(line[48]);
                        spell.AutoLearn = byte.Parse(line[30]);

                        spell.DamageOnHuman = int.Parse(line[35]);
                        spell.Damage2 = int.Parse(line[36]);
                        spell.Damage3 = int.Parse(line[37]);

                        spell.DamageOnMonster = int.Parse(line[44]);
                        spell.ColdTime = int.Parse(line[47]);

                        spell.Range = byte.Parse(line[15]);//14
                        spell.Status = int.Parse(line[16]);
                        spell.Sector = (ushort)(spell.Range * 20);

                        var subtypes = new List<ushort>();
                        if (spell.WeaponType != 0 && spell.WeaponType != 50000)
                        {
                            var subtype1 = (ushort)(spell.WeaponType % 1000);
                            if (subtype1 == 60000)
                                subtype1 = 614;
                            var subtype2 = (ushort)((spell.WeaponType / 1000) % 1000);
                            if (subtype1 > 0) subtypes.Add(subtype1);
                            if (subtype2 > 0) subtypes.Add(subtype2);

                        }
                        if (spell.ID == 12070)
                            spell.Damage = 104;

                        // WindowsAPI.IniFile read_MagicType = new WindowsAPI.IniFile("MagicEffect.ini");
                        // spell.CoolDown = read_MagicType.ReadUInt16("" + spell.ID + "0", "CoolDown", 700);
                        /*  if (spell.ID == (ushort)Role.Flags.SpellID.SuperTwofoldBlade)
                              spell.CoolDown = 650;//read_MagicType.ReadUInt16("" + spell.ID + "0", "CoolDown", 1000);*/
                        /* if (spell.ID == (ushort)Role.Flags.SpellID.RadiantPalm)
                             spell.CoolDown = 450;//read_MagicType*/
                        //  if (spell.ID == (ushort)Role.Flags.SpellID.AngerofStomper)
                        //  spell.CoolDown = 980;//read_MagicType
                        //  if (spell.ID == (ushort)Role.Flags.SpellID.DragonSlash)
                        //  spell.CoolDown = 800;//read_MagicType
                        spell.Passive = spell.isTrojanArchiveSkill || ini.ReadString(spell.ID.ToString() + "00", "DescEx", "").Contains("Passive");
                      /*  if (RuneSpells(spell))
                            if (!RuneSpell.ContainsKey((ushort)spell.ID))
                                RuneSpell.Add(spell.ID, spell);*/
                        if (this.ContainsKey(spell.ID))
                        {
                            if (!this[spell.ID].ContainsKey(spell.Level))
                                this[spell.ID].Add(spell.Level, spell);
                        }
                        else
                        {
                            this.Add(spell.ID, new Dictionary<ushort, Magic>());
                            this[spell.ID].Add(spell.Level, spell);
                        }

                        if (subtypes.Count != 0)
                        {
                            switch (spell.ID)
                            {
                                case 5010:
                                case 7020:
                                case 1290:
                                case 1260:
                                case 5030:
                                case 5040:
                                case 7000:
                                case 7010:
                                case 7030:
                                case 7040:
                                case 1250:
                                case 5050:
                                case 5020:
                                case 10490:
                                case (ushort)Role.Flags.SpellID.Windstorm:
                                case 1300:
                                case (ushort)Role.Flags.SpellID.MortalStrike:
                                case 12240:
                                case (ushort)Role.Flags.SpellID.Sector:
                                case (ushort)Role.Flags.SpellID.Rectangle:
                                case (ushort)Role.Flags.SpellID.Circle:

                                case (ushort)Role.Flags.SpellID.LeftHook:
                                case (ushort)Role.Flags.SpellID.RightHook:
                                case (ushort)Role.Flags.SpellID.StraightFist:
                                case (ushort)Role.Flags.SpellID.FatalSpin:

                                case (ushort)Role.Flags.SpellID.AirStrike:
                                case (ushort)Role.Flags.SpellID.EarthSweep:
                                case (ushort)Role.Flags.SpellID.Kick:

                                case (ushort)Role.Flags.SpellID.UpSweep:
                                case (ushort)Role.Flags.SpellID.DownSweep:
                                case (ushort)Role.Flags.SpellID.Strike:

                                case (ushort)Role.Flags.SpellID.NormalAttack1:
                                case (ushort)Role.Flags.SpellID.NormalAttack2:
                                case (ushort)Role.Flags.SpellID.NormalAttack3:

                                case (ushort)Role.Flags.SpellID.LeftChop:
                                case (ushort)Role.Flags.SpellID.RightChop:
                                    foreach (var subtype in subtypes)
                                    {
                                        if (!Pool.WeaponSpells.ContainsKey(subtype))
                                            Pool.WeaponSpells.Add(subtype, new List<ushort>());
                                        if (!Pool.WeaponSpells[subtype].Contains(spell.ID))
                                            Pool.WeaponSpells[subtype].Add(spell.ID);
                                    }
                                    break;
                            }
                        }

                    }
                }
            }
            //MyConsole.WriteLine("Loading " + this.Count + " DBSkills");


            /* if (Pool.WeaponSpells.ContainsKey(420))//sword
                 Pool.WeaponSpells[420] = 5030;
             if (Pool.WeaponSpells.ContainsKey(410))//blade
                 Pool.WeaponSpells.Remove(410);
             if (Pool.WeaponSpells.ContainsKey(900))//shield460
                 Pool.WeaponSpells.Remove(900);
             if (!Pool.WeaponSpells.ContainsKey(460))
                 Pool.WeaponSpells.Add(460, 5040);
             if (Pool.WeaponSpells.ContainsKey(601))//ninja sword
                 Pool.WeaponSpells.Remove(601);
             if (Pool.WeaponSpells.ContainsKey(610))//MonkSpell
                 Pool.WeaponSpells[610] = 10490;
             if (Pool.WeaponSpells.ContainsKey(611))//Rapier
                 Pool.WeaponSpells[611] = 11140;

             if (Pool.WeaponSpells.ContainsKey(612))//pistol
                 Pool.WeaponSpells[612] = 11120;
             else
             {
                 Pool.WeaponSpells.Add(612, 11120);
             }
             if (Pool.WeaponSpells.ContainsKey(616))//TwistedClaw
                 Pool.WeaponSpells[616] = 12110;
             if (Pool.WeaponSpells.ContainsKey(617))//Nunchaku
                 Pool.WeaponSpells[617] = 12240;

             if (Pool.WeaponSpells.ContainsKey(511))//scytle
                 Pool.WeaponSpells[511] = 0;
             if (!Pool.WeaponSpells.ContainsKey(624))//ScarofEarth
                 Pool.WeaponSpells.Add(624, 12670);*/


            Add((ushort)Role.Flags.SpellID.ShurikenEffect, this[(ushort)Role.Flags.SpellID.ShurikenVortex]);
        }
        public static List<UInt16> Weaponextra = new List<UInt16>();
        public bool AttackInFlay(Magic DBSpell)
        {
            if (DBSpell.ID == 1045 || DBSpell.ID == 1046
    || DBSpell.ID == 1250 || DBSpell.ID == 1260 || DBSpell.ID == 1290
    || DBSpell.ID >= 5010 && DBSpell.ID <= 5050 || DBSpell.ID == 6000
    || DBSpell.ID == 6001 || DBSpell.ID >= 7000 && DBSpell.ID <= 7040
    || DBSpell.ID == 10315 || DBSpell.ID == 10381 || DBSpell.ID == 10415
    || DBSpell.ID == 10490 || DBSpell.ID == 11000 || DBSpell.ID == 11005
    || DBSpell.ID == 11040 || DBSpell.ID == 11070 || DBSpell.ID == 11110
    || DBSpell.ID == 11140 || DBSpell.ID == 11170 || DBSpell.ID == 11180
    || DBSpell.ID == 11190 || DBSpell.ID == 11230 || DBSpell.ID == 6010)
                return false;
            else
                return true;
        }
      /*  public bool RuneSpells(Magic DBSpell)
        {
            if (DBSpell.ID == 13930 || DBSpell.ID == 14260 || DBSpell.ID == 14270 || DBSpell.ID == 14350
                || DBSpell.ID == 14190 || DBSpell.ID == 14280 || DBSpell.ID == 14380 || DBSpell.ID == 14290
                || DBSpell.ID == 14410 || DBSpell.ID == 14300 || DBSpell.ID == 14160 || DBSpell.ID == 14250 || DBSpell.ID == 14440
    || DBSpell.ID == 14310 || DBSpell.ID == 14470 || DBSpell.ID == 14320
    || DBSpell.ID == 14500 || DBSpell.ID == 14330 || DBSpell.ID == 14220
    || DBSpell.ID == 14340 || DBSpell.ID == 14530)
                return true;
            else
                return false;
        }*/
        public uint GetSpell(string name)
        {
            uint spellid = 0;
            foreach (var spells in Values)
            {
                foreach (var spell in spells.Values)
                {
                    if (spell.Name != null)
                        if (spell.Name.Contains(name))
                            spellid = spell.ID;
                }
            }
            return spellid;
        }
        public class Magic
        {
            public ushort ID;
            public string Name;
            public MagicSort Type;
            public byte Level;
            public ushort UseMana;
            public float Damage;

            public int Rate = 0;
            public uint Experience;
            public byte Range;
            public ushort Sector;
            public uint Duration;
            public uint WeaponType;
            public byte UseStamina;
            public uint GiveHitPoints;
            public bool AttackInFly, isTrojanArchiveSkill;
            public int Status;
            public ushort NeedLevel = 0;
            public uint CpsCost = 0;
            public int MaxTargets = 0;
            public byte IncreaseStamin = 0;
            public ushort CoolDown = 100;
            public byte AutoLearn = 0;

            public int ColdTime = 0;
            public bool IsSpellWithColdTime
            {
                get { return ColdTime > 0; }
            }

            public int Damage2;
            public int Damage3;
            public int DamageOnHuman;
            public int DamageOnMonster;
            public bool Passive;

            public bool isRuneSkill;
        }
    }

}
