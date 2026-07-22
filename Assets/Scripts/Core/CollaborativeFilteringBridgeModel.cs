namespace MadFact
{
    /// <summary>
    /// Level 4's bridge between the two-person example and the full five-person table.
    /// Priya matches Wendell on the two visible movies, so his third rating is the clue.
    /// </summary>
    public sealed class CollaborativeFilteringBridgeModel
    {
        public const int Rows = 3;
        public const int Columns = 3;

        public static readonly string[] CustomerNames = { "WENDELL", "PRIYA", "HANK" };
        public static readonly string[] MovieNames =
        {
            "STAR DRIFTER", "ASTRO BLASTERS", "BOOM TOWN"
        };

        public readonly float[,] Target =
        {
            { 5f, 4f, 2f },
            { 5f, 4f, 2f },
            { 4f, 5f, 3f }
        };

        public readonly bool[,] Known =
        {
            { true, true, true },
            { true, true, false },
            { true, true, true }
        };

        public int MissingRow => 1;
        public int MissingColumn => 2;
        public int MissingRating => (int)Target[MissingRow, MissingColumn];
    }
}
