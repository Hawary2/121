using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Database
{
    public unsafe class Loading
    {
        private static Int32 Next = -1;
        private static String[] Array = new String[] { "|", "/", "-", "\\" };

        public static String NextChar()
        {
            Next++;
            if (Next > 3)
                Next = 0;
            return Array[Next];
        }
    }
}
