using System;

namespace Snake3D
{
    /// <summary>與網頁版 rules.js 的 rng 完全相同的種子亂數（mulberry32），可跨平台重現。</summary>
    public sealed class Rng
    {
        private uint a;

        public Rng(uint seed) { a = seed; }

        public double Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = a;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
