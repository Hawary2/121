using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System
{
    public class Counter
    {
        private uint val;
        public Counter(uint start)
        {
            val = start;
        }
        public uint Next { get { return (val = val + 1); } }
        public uint Count { get { return val; } }

        public void Set(uint start)
        {
            val = start;
        }
    }
}
