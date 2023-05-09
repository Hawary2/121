using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace GameServer
{
    public static class BaseFunc
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Int64 srand(UInt64 seed);

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Int64 rand();
        public static byte AnimaUpgradeRate(uint AnimaID, bool forging = false)
        {
            switch (AnimaID % 100)
            {
                case 10:
                case 11:
                    return 35;

                case 12:
                    return 25;

                case 13:
                case 14:
                case 15:
                case 16:
                case 17:
                case 18:
                    return 100;

                case 19:
                    return 0;
            }

            return (byte)(forging ? 58 : 55);
        }
        /*public static byte AnimaUpgradeRate(uint AnimaID, bool forging = false)
        {
            switch (AnimaID % 100)
            {
                case 1: return 90;
                case 2: return 90;
                case 3: return 60;
                case 4: return 60;
                case 5: return 60;
                case 6: return 60;
                case 7: return 60;
                case 8: return 60;
                case 9: return 60;
                case 10: return 60;
                case 11: return 35;
                case 12: return 1;
                case 13: return 1;
                case 14: return 45;
                case 15: return 20;
                case 16: return 20;
                case 17: return 20;
                case 18: return 20;
                case 19: return 0;
            }

            return 0;
        }*/

        public static int RandGet(int nMax, bool bRealRand)
        {
            if (nMax <= 0)
                nMax = 1;

            if (bRealRand)
                srand((ulong)DateTime.Now.Value());
            long val = rand();
            return (int)(val % nMax);
        }
        public static byte SteedSpeed(byte plus)
        {
            return _SteedSpeed[(int)System.Math.Min(plus, (byte)12)];
        }
        private static byte[] _SteedSpeed = new byte[]
		{
			0,
			5,
			10,
			15,
			20,
			30,
			40,
			50,
			65,
			85,
			90,
			95,
			100
		};
        //////////////////////////////////////////////////////////////////////
        public static double RandomRateGet(double dRange)
        {
            double pi = 3.1415926;

            int nRandom = RandGet(999, true) + 1;
            double a = Math.Sin(nRandom * pi / 1000);
            double b;
            if (nRandom >= 90)
                b = (1.0 + dRange) - Math.Sqrt(Math.Sqrt(a)) * dRange;
            else
                b = (1.0 - dRange) + Math.Sqrt(Math.Sqrt(a)) * dRange;

            return b;
        }
        
        public static uint ProficiencyLevelExperience(byte Level)
        {
            return proficiencyLevelExperience[Math.Min(Level, (byte)20)];
        }

        private static uint[] proficiencyLevelExperience = new uint[21]
        {
            0, 1200, 68000, 250000, 640000, 1600000, 4000000, 10000000, 22000000, 40000000, 90000000, 95000000, 142500000,
            213750000, 320625000, 480937500, 721406250, 1082109375, 1623164062, 2100000000, 3200000000
        };
        public static bool NameStrCheck(string name, bool ExceptedSize = true)
        {
            if (name == null)
                return false;
            if (name == "")
                return false;
            string ValidChars = "[^A-Za-z0-9ء-ي*~.&.$]$";
            System.Text.RegularExpressions.Regex r = new System.Text.RegularExpressions.Regex(ValidChars);
            if (r.IsMatch(name))
                return false;

            if (name.Contains('/'))
                return false;
            if (name.Contains(@"\"))
                return false;
            if (name.Contains(@"'"))
                return false;
            //    if (name.Contains('#'))
            //      return false;
            if (name.Contains("GM") ||
                name.Contains("PM") ||
                name.Contains("SYSTEM") ||
                 name.Contains("None") ||
                  name.Contains("Clicker") ||
                   name.Contains("SpeedHack") ||
                    name.Contains("Aimbot") ||
                    name.Contains("CheatEngine") ||
                    name.Contains("Hack") ||
                    name.Contains("Engine") ||
                    name.Contains("Cheat") ||
                    name.Contains("clicker") ||
                    name.Contains("speed") ||
                     name.Contains("GM") ||
                      name.Contains("PM") ||
                       name.Contains("GMPM") ||
                        name.Contains("Moksha") ||
                        name.Contains("TQ") ||
                        name.Contains("TQGMPM") ||
                        name.Contains("TQPM") ||
                        name.Contains("TQGM") ||
                        name.Contains("guard1") ||
                        name.Contains("Guard") ||
                        name.Contains("Guard1") ||
                        name.Contains("Guard") ||
                        name.Contains("Kosamk") ||
                        name.Contains("kosamk") ||
                //  name.Contains("") ||
                name.Contains("{") || name.Contains("}") || name.Contains("[") || name.Contains("]"))
                return false;
            if (name.Length > 16 && ExceptedSize)
                return false;
            for (int x = 0; x < name.Length; x++)
                if (name[x] == 25)
                    return false;
            return true;
        }
        public static string ReverseString(string text)
        {
            char[] cArray = text.ToCharArray();
            string reverse = "";
            for (int i = cArray.Length - 1; i > -1; i--)
            {
                reverse += cArray[i];
            }
            return reverse;
        }
       
        public static uint RandFromGivingNums(uint[] nums)
        {
            if (nums == null || nums.Length == 0) return 0;
            return nums[Pool.GetRandom.Next(0, nums.Length)];
        }
    }
}
