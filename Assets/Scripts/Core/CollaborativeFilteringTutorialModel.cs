namespace MadFact
{
    /// <summary>
    /// The first Level 4 collaborative-filtering example. Wendell and Priya gave the
    /// same ratings to four movies, so Wendell's last rating is a clear clue for Priya's
    /// one missing value.
    /// </summary>
    public sealed class CollaborativeFilteringTutorialModel
    {
        public const int Rows = 2;
        public const int Columns = 5;

        public static readonly string[] CustomerNames = { "WENDELL", "PRIYA" };
        public static readonly string[] MovieNames =
        {
            "STAR DRIFTER", "ASTRO BLASTERS", "BOOM TOWN", "QUASAR RUN", "DEMOLITION DAVE"
        };

        public readonly float[,] Target =
        {
            { 5f, 4f, 2f, 3f, 1f },
            { 5f, 4f, 2f, 3f, 1f }
        };

        public readonly bool[,] Known =
        {
            { true, true, true, true, true },
            { true, true, true, true, false }
        };

        public int MissingRow => 1;
        public int MissingColumn => 4;
        public int MissingRating => (int)Target[MissingRow, MissingColumn];
    }
}
