namespace MadFact
{
    /// <summary>
    /// Level 4's five-person collaborative-filtering exercise. It is deliberately kept
    /// separate from MfModel, which is the trainable matrix used by Level 5.
    /// </summary>
    public sealed class CollaborativeFilteringMainModel
    {
        public const int Rows = 5;
        public const int Columns = 5;

        public static readonly string[] CustomerNames =
            { "WENDELL", "DOT", "HANK", "PRIYA", "THE TIBBS TWINS" };

        public static readonly string[] MovieNames =
        {
            "STAR DRIFTER", "ASTRO BLASTERS", "BOOM TOWN", "QUASAR RUN", "DEMOLITION DAVE"
        };

        public readonly float[,] Target =
        {
            { 5f, 2f, 4f, 5f, 3f },
            { 3f, 5f, 5f, 3f, 5f },
            { 5f, 2f, 4f, 5f, 4f },
            { 5f, 2f, 4f, 5f, 3f },
            { 1f, 3f, 2f, 1f, 2f }
        };

        public readonly bool[,] Known =
        {
            { true,  true,  true,  false, true  },
            { false, true,  true,  true,  true  },
            { true,  true,  true,  true,  false },
            { true,  false, true,  true,  true  },
            { true,  true,  false, true,  true  }
        };

        public const int AverageRow = 3;
        public const int AverageColumn = 1;
        public const int AverageSourceRowA = 0;
        public const int AverageSourceRowB = 2;
        public const int TibbsRow = 4;
        public const int TibbsMissingColumn = 2;

        public float AverageClueRating =>
            (Target[AverageSourceRowA, AverageColumn] + Target[AverageSourceRowB, AverageColumn]) * 0.5f;
    }
}
