using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer
{
    public static class Constants
    {
        public static readonly ushort[] PriceUpdatePorf = new ushort[] { 36000, 36000, 36000, 36000, 36000, 36000, 18367, 12328, 7377, 6164, 3688, 3082, 3082, 3082, 3082, 2670, 1825, 1251, 866, 704 };
        public static readonly List<uint> ProtectMapSpells = new List<uint>() { 1038, Game.MsgTournaments.MsgSuperGuildWar.MapID };
        public static readonly List<uint> MapCounterHits = new List<uint>() { 1005, 6000, 5100, 5101, 5102, 5103, 5104, 5105, 5106, 5107, 5108 };
        public static readonly List<uint> NoDropItems = new List<uint>() { 1764, 700, 3954, 3820 };
        public static readonly List<uint> FreePkMap = new List<uint>() { 6011,5100,5101,5102,5103,5104,5105,5106,5107,5108,3998,6784,5342,8009,2353,5339,6964,3071,6546,3935,8437,9988,5599,2071,7357,6521,3825,5342,6891,8521,6525,6729, 6000, 6001,1505, 1005, 1038, 700,1508/*PkWar*/, Game.MsgTournaments.MsgSuperGuildWar.MapID, Game.MsgTournaments.MsgCaptureTheFlag.MapID
        , Game.MsgTournaments.MsgTeamDeathMatch.MapID };
        public static readonly List<uint> BlockAttackMap = new List<uint>() { 3825,15147,6072,3830,5040, 3820, 1004,3831, 3832,3834,3826,3827,3828,3829,3833, 9995,1068, 4020, 4000, 4003, 4006, 4008, 4009 , 1860 ,1858, 1801, 1780, 1779/*Ghost Map*/, 9972, 1806, 10364, 3954, 3081, 1036, 1008, 601, 1006, 1511, 1039, 700,10445,11100, 11101, 11102, 11103, 11104, 11105, 11106, 11107,11108,11109,11110,11111, Game.MsgTournaments.MsgEliteGroup.WaitingAreaID, (uint)Game.MsgTournaments.MsgSteedRace.Maps.DungeonRace, (uint)Game.MsgTournaments.MsgSteedRace.Maps.IceRace
        ,(uint)Game.MsgTournaments.MsgSteedRace.Maps.IslandRace, (uint)Game.MsgTournaments.MsgSteedRace.Maps.LavaRace, (uint)Game.MsgTournaments.MsgSteedRace.Maps.MarketRace};
        public static readonly List<uint> BlockTeleportMap = new List<uint>() { 601, 6000, 5040, 6001, 1005, 700, 1858, 1860,5100,5101,5102,5103,5104,5105,5106,5107,5108, 3852, Game.MsgTournaments.MsgEliteGroup.WaitingAreaID, 1768 };
        public static List<string> SuitRank = new List<string>
	{
		"2c".ToLower(),
		"3c".ToLower(),
		"4c".ToLower(),
		"5c".ToLower(),
		"6c".ToLower(),
		"7c".ToLower(),
		"8c".ToLower(),
		"9c".ToLower(),
		"Tc".ToLower(),
		"Jc".ToLower(),
		"Qc".ToLower(),
		"Kc".ToLower(),
		"Ac".ToLower(),
		"2d".ToLower(),
		"3d".ToLower(),
		"4d".ToLower(),
		"5d".ToLower(),
		"6d".ToLower(),
		"7d".ToLower(),
		"8d".ToLower(),
		"9d".ToLower(),
		"Td".ToLower(),
		"Jd".ToLower(),
		"Qd".ToLower(),
		"Kd".ToLower(),
		"Ad".ToLower(),
		"2h".ToLower(),
		"3h".ToLower(),
		"4h".ToLower(),
		"5h".ToLower(),
		"6h".ToLower(),
		"7h".ToLower(),
		"8h".ToLower(),
		"9h".ToLower(),
		"Th".ToLower(),
		"Jh".ToLower(),
		"Qh".ToLower(),
		"Kh".ToLower(),
		"Ah".ToLower(),
		"2s".ToLower(),
		"3s".ToLower(),
		"4s".ToLower(),
		"5s".ToLower(),
		"6s".ToLower(),
		"7s".ToLower(),
		"8s".ToLower(),
		"9s".ToLower(),
		"Ts".ToLower(),
		"Js".ToLower(),
		"Qs".ToLower(),
		"Ks".ToLower(),
		"As".ToLower()
	};

        public static List<string> HighCard = new List<string>
	{
		"2".ToLower(),
		"3".ToLower(),
		"4".ToLower(),
		"5".ToLower(),
		"6".ToLower(),
		"7".ToLower(),
		"8".ToLower(),
		"9".ToLower(),
		"T".ToLower(),
		"J".ToLower(),
		"Q".ToLower(),
		"K".ToLower(),
		"A".ToLower()
	};
    }
}
