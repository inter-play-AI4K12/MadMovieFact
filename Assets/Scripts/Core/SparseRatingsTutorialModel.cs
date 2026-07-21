namespace MadFact
{
    /// <summary>
    /// Level 4's final, deliberately sparse ratings table. Only Task cells are filled by
    /// the player; the remaining unknown cells stay empty to motivate matrix factorization.
    /// </summary>
    public sealed class SparseRatingsTutorialModel
    {
        public const int Rows = 5;
        public const int Columns = 9;

        public static readonly string[] CustomerNames =
            { "WENDELL", "DOT", "HANK", "PRIYA", "TIBBS TWINS" };

        public static readonly string[] MovieNames =
        {
            "STAR DRIFTER", "ASTRO BLASTERS", "BOOM TOWN", "QUASAR RUN", "DEMOLITION DAVE",
            "MIDNIGHT MANSION", "LAUGH TRACK", "CITY HEARTS", "WILD PLANET"
        };

        public readonly int[,] Target =
        {
            { 5, 4, 2, 3, 1, 2, 3, 4, 2 },
            { 1, 4, 5, 2, 1, 2, 3, 2, 3 },
            { 4, 5, 3, 2, 1, 2, 4, 3, 2 },
            { 3, 2, 1, 5, 4, 3, 2, 4, 2 },
            { 1, 2, 2, 1, 1, 3, 5, 5, 3 }
        };

        // Ratings the customers actually supplied. False cells were never rated.
        public readonly bool[,] Known =
        {
            { true,  true,  true,  false, true,  false, false, true,  false },
            { true,  false, true,  false, false, true,  false, false, true  },
            { true,  false, true,  true,  true,  false, true,  false, false },
            { false, true,  false, true,  true,  true,  false, false, true  },
            { true,  false, true,  false, true,  false, true,  true,  false }
        };

        // Five representative predictions; all other unknown cells remain visibly empty.
        public readonly bool[,] Task =
        {
            { false, false, false, true,  false, false, false, false, false },
            { false, false, false, false, false, false, true,  false, false },
            { false, true,  false, false, false, false, false, false, false },
            { false, false, false, false, false, false, false, true,  false },
            { false, false, false, false, false, true,  false, false, false }
        };

        public int TaskCount
        {
            get
            {
                int count = 0;
                for (int row = 0; row < Rows; row++)
                    for (int column = 0; column < Columns; column++)
                        if (Task[row, column]) count++;
                return count;
            }
        }
    }
}
