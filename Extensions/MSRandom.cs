﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System
{
    public  class MSRandom
    {
        /// <summary>
        /// This macro expands to an integral constant expression whose value is the maximum value returned by
        /// the rand function.
        /// This value is library dependent, but is granted to be at least 32767.
        /// </summary>
        public const Int32 RAND_MAX = 0x7FFF;

        private UInt32 Seed = 1;

        /// <summary>
        /// The pseudo-random number generator is initialized using its initial value.
        /// Two different initializations with the same seed, instructs the pseudo-random generator to generate
        /// the same succession of results for the subsequent calls to rand in both cases.
        /// </summary>
        public MSRandom()
        {
            Seed = 1;
        }

        /// <summary>
        /// The pseudo-random number generator is initialized using the argument passed as seed.
        /// For every different seed value used in a call to srand, the pseudo-random number generator can
        /// be expected to generate a different succession of results in the subsequent calls to rand.
        /// Two different initializations with the same seed, instructs the pseudo-random generator to generate
        /// the same succession of results for the subsequent calls to rand in both cases.
        /// If seed is set to 1, the generator is initialized to its initial value.
        /// </summary>
        public MSRandom(UInt32 seed)
        {
            Seed = seed;
        }

        /// <summary>
        /// Returns a pseudo-random integral number in the range 0 to RAND_MAX.
        /// This number is generated by an algorithm that returns a sequence of apparently non-related numbers
        /// each time it is called. This algorithm uses a seed to generate the series, which should be initialized
        /// to some distinctive value using srand.
        /// </summary>
        public Int32 Next()
        {
            //This is the Microsoft's implementation since NT 2.0 and probably before.
            //Others OS have different implementation.
            return (Int32)(((Seed = Seed * 0x343FD + 0x269EC3) >> 16) & RAND_MAX);
        }
    }
}
