using System;

namespace System
{
    public class RandomLite
    {
        private const uint Y = 842502087u;

        private const uint Z = 3579807591u;

        private const uint W = 273326509u;

        private volatile uint x;

        private volatile uint y;

        private volatile uint z;

        private volatile uint w;

        public RandomLite()
            : this(Environment.TickCount)
        {
        }

        public RandomLite(int seed)
        {
            Reseed(seed);
        }

        public void Reseed(int seed)
        {
            x = (uint)seed;
            y = 842502087u;
            z = 3579807591u;
            w = 273326509u;
        }

        public int Next()
        {
            uint num = x ^ (x << 11);
            x = y;
            y = z;
            z = w;
            w = (w ^ (w >> 19) ^ (num ^ (num >> 8)));
            uint num2 = w & 0x7FFFFFFF;
            if (num2 == 2147483647)
            {
                return Next();
            }
            return (int)num2;
        }

        public int Next(int upperBound)
        {
            if (upperBound < 0)
            {
                throw new ArgumentOutOfRangeException("upperBound", upperBound, "upperBound must be >=0");
            }
            if (upperBound == 0)
            {
                return 0;
            }
            return Next() % (upperBound + 1);
        }

        public int Next(int lowerBound, int upperBound)
        {
            if (lowerBound > upperBound)
            {
                throw new ArgumentOutOfRangeException("upperBound", upperBound, "upperBound must be >=lowerBound");
            }
            if (lowerBound == upperBound)
            {
                return lowerBound;
            }
            return lowerBound + Next() % (upperBound + 1 - lowerBound);
        }
    }
    public class SafeRandom
    {
        private Random Rand;

        public object SyncRoot;

        public SafeRandom(int seed = 0)
        {
            SyncRoot = new object();
            if (seed != 0)
            {
                Rand = new Random(seed);
            }
            else
            {
                Rand = new Random();
            }
        }

        public int Next(int minval, int maxval)
        {
            lock (SyncRoot)
            {
                return Rand.Next(minval, maxval);
            }
        }

        public int Next(int maxval)
        {
            lock (SyncRoot)
            {
                return Rand.Next(maxval);
            }
        }

        public int Next()
        {
            lock (SyncRoot)
            {
                return Rand.Next();
            }
        }

        public void SetSeed(int seed)
        {
            lock (SyncRoot)
            {
                Rand = new Random();
            }
        }
    }

}