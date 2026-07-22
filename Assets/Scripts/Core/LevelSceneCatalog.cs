namespace MadFact
{
    /// <summary>
    /// Authoritative list of gameplay scene assets. The current bootstrap can still run the
    /// whole game from one scene, but these paths define the target production structure:
    /// one dedicated scene/background per learning scenario.
    /// </summary>
    public static class LevelSceneCatalog
    {
        public const string MainMenu = "Assets/Scenes/MainMenu.unity";
        public const string GameMenu = "Assets/Scenes/GameMenu.unity";
        public const string FullGame = "Assets/Scenes/MadMovieFact.unity";
        public const string Storefront = "Assets/Scenes/Storefront.unity";
        public const string ManualRecommendation = "Assets/Scenes/Level01_ManualRecommendation.unity";
        public const string RuleBasedRecommendation = "Assets/Scenes/Level02_RuleBasedRecommendation.unity";
        public const string RatingsTable = "Assets/Scenes/Level03_RatingsTable.unity";
        public const string CollaborativeFiltering = "Assets/Scenes/Level04_CollaborativeFiltering.unity";
        public const string MatrixFactorization = "Assets/Scenes/Level05_MatrixFactorization.unity";
        public const string ContentBasedRecommendation = "Assets/Scenes/Level06_ContentBasedRecommendation.unity";
        public const string MarketGapResearch = "Assets/Scenes/Level07_MarketGapResearch.unity";
        public const string PosterGeneration = "Assets/Scenes/Level08_PosterGeneration.unity";
        public const int MaxPlayableLevel = 8;
        // Kept as the menu's final displayed slot for compatibility with its authored loop.
        public const int ComingSoonLevel = 8;

        public static string PathForLevel(int level)
        {
            switch (level)
            {
                case 0: return Storefront;
                case 1: return ManualRecommendation;
                case 2: return RuleBasedRecommendation;
                case 3: return RatingsTable;
                case 4: return CollaborativeFiltering;
                case 5: return MatrixFactorization;
                case 6: return ContentBasedRecommendation;
                case 7: return MarketGapResearch;
                case 8: return PosterGeneration;
                default: return Storefront;
            }
        }

        /// <summary>
        /// Returns the next playable level in the same day, or 0 when the completed
        /// level ends its day.
        /// </summary>
        public static int NextLevelInSameDay(int completedLevel)
        {
            switch (completedLevel)
            {
                case 1: return 2; // Day 1
                case 3: return 4; // Day 2
                case 4: return 5; // Day 2
                case 6: return 7; // Day 3
                case 7: return 8; // Day 3
                default: return 0;
            }
        }

        public static string PathForPhase(Phase phase)
        {
            switch (phase)
            {
                case Phase.Storefront: return Storefront;
                case Phase.Level1: return ManualRecommendation;
                case Phase.Level2: return RuleBasedRecommendation;
                case Phase.Level3: return RatingsTable;
                case Phase.Level4: return CollaborativeFiltering;
                case Phase.Level5: return MatrixFactorization;
                case Phase.Level6: return ContentBasedRecommendation;
                case Phase.Level7: return MarketGapResearch;
                case Phase.Level8: return PosterGeneration;
                default: return Storefront;
            }
        }

        public static string BackgroundKeyForPhase(Phase phase)
        {
            switch (phase)
            {
                case Phase.Storefront: return "storefront-vhs-shop";
                case Phase.Level1: return "manual-counter-customer-file";
                case Phase.Level2: return "robot-rule-terminal";
                case Phase.Level3: return "customer-ratings-table";
                case Phase.Level4: return "collaborative-filtering-ratings-matrix";
                case Phase.Level5: return "matrix-factorization-mainframe";
                case Phase.Level6: return "content-feature-wall";
                case Phase.Level7: return "market-gap-corkboard-studio";
                case Phase.Level8: return "ai-poster-generation-studio";
                default: return "storefront-vhs-shop";
            }
        }
    }
}
