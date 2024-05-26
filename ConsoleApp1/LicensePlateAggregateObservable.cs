

using System;
using System.Collections.Generic;
using System.Linq;
using F23.StringSimilarity;

namespace VideoANPR.Observables
{
    public class PlateAggregator
    {
        private readonly Dictionary<string, int> _plateCounts = new Dictionary<string, int>();
        private readonly LongestCommonSubsequence _comparer = new LongestCommonSubsequence();

        public string Aggregate(List<string> plates)
        {
            foreach (var plate in plates)
            {
                bool found = false;
                foreach (var existingPlate in _plateCounts.Keys.ToList())
                {
                    if (_comparer.Distance(existingPlate, plate) < 3) // Customize threshold as needed
                    {
                        _plateCounts[existingPlate]++;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    _plateCounts[plate] = 1;
                }               
            }

            return _plateCounts.MaxBy(i => i.Value).Key;
        }
    }
}
