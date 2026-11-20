using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    /// <summary>
    /// Co-op bookkeeping shared with the core game code. While Track is on, local changes that other players need
    /// (cleaned dirt cells, foam, mowed grass, pulled weeds, picked trash) are collected here for the next network send.
    /// Remote is on while applying someone else's changes, so they are not collected and sent back.
    /// </summary>
    public static class NetHooks
    {
        public static bool Track;
        public static bool Remote;
        public static readonly List<int> Cuts = new List<int>(), Pulls = new List<int>(), Picks = new List<int>();
        public struct FoamStamp { public int Surf; public Vector3 P; public float R, Amount; public int Agent; }
        public static readonly List<FoamStamp> Foams = new List<FoamStamp>();
        /// <summary>Water sprayed where there was foam: others rinse the foam off at the same spots.</summary>
        public static readonly List<FoamStamp> Rinses = new List<FoamStamp>();

        public static bool Recording => Track && !Remote;

        public static void Clear() { Cuts.Clear(); Pulls.Clear(); Picks.Clear(); Foams.Clear(); Rinses.Clear(); }
    }
}
