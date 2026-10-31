using System.Collections.Generic;

namespace MayaAdventure
{
    /// <summary>All levels, the stars each one holds and how many stars open its portal.</summary>
    public static class Levels
    {
        public static readonly string[] Names = { "אי השמש", "פסגות השלג", "לגונת האלמוגים", "מבצר השמיים", "יער הדמדומים" };
        public static readonly int[] StarCounts = { 5, 5, 5, 5, 5 };
        public static readonly int[] Required = { 0, 2, 6, 11, 16 };

        public static int Count => Names.Length;

        public static int TotalStars
        {
            get
            {
                int n = 0;
                foreach (var c in StarCounts) n += c;
                return n;
            }
        }

        public static LevelInfo Build(int index)
        {
            PowerStar.BeginLevel(index);
            switch (index)
            {
                case 1: return SnowLevel.Build();
                case 2: return LagoonLevel.Build();
                case 3: return SkyFortressLevel.Build();
                case 4: return DuskLevel.Build();
                default: return LevelBuilder.Build();
            }
        }

        public static int CountCollected(int level, HashSet<string> ids)
        {
            int n = 0;
            string prefix = level + "-";
            foreach (var id in ids)
                if (id.StartsWith(prefix)) n++;
            return n;
        }
    }
}
