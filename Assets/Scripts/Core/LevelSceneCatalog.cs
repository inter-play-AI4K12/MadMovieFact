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
        public const string ContentBasedRecommendation = "Assets/Scenes/Level03_ContentBasedRecommendation.unity";
        public const string GroundTruthMatrix = "Assets/Scenes/Level04_GroundTruthMatrix.unity";
        public const string MatrixFactorization = "Assets/Scenes/Level05_MatrixFactorization.unity";
        public const string MarketGapResearch = "Assets/Scenes/Level06_MarketGapResearch.unity";

        public static string PathForLevel(int level)
        {
            switch (level)
            {
                case 0: return Storefront;
                case 1: return ManualRecommendation;
                case 2: return RuleBasedRecommendation;
                case 3: return ContentBasedRecommendation;
                case 4: return GroundTruthMatrix;
                case 5: return MatrixFactorization;
                case 6: return MarketGapResearch;
                default: return Storefront;
            }
        }

        /// <summary>
        /// Returns the next playable level in the same day, or 0 when the completed
        /// level ends its day. Day 3 currently ends at Level 6 because Level 7 is not
        /// playable yet.
        /// </summary>
        public static int NextLevelInSameDay(int completedLevel)
        {
            switch (completedLevel)
            {
                case 1: return 2; // Day 1
                case 3: return 4; // Day 2
                case 4: return 5; // Day 2
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
                case Phase.Level3: return ContentBasedRecommendation;
                case Phase.Level4: return GroundTruthMatrix;
                case Phase.Level5: return MatrixFactorization;
                case Phase.Level6: return MarketGapResearch;
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
                case Phase.Level3: return "content-feature-wall";
                case Phase.Level4: return "ground-truth-ratings-matrix";
                case Phase.Level5: return "matrix-factorization-mainframe";
                case Phase.Level6: return "market-gap-corkboard-studio";
                default: return "storefront-vhs-shop";
            }
        }
    }
}
