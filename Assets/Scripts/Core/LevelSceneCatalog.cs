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
        public const string FullGame = "Assets/Scenes/MadMovieFact.unity";
        public const string Storefront = "Assets/Scenes/Storefront.unity";
        public const string ManualRecommendation = "Assets/Scenes/Level01_ManualRecommendation.unity";
        public const string RuleBasedRecommendation = "Assets/Scenes/Level02_RuleBasedRecommendation.unity";
        public const string ContentBasedRecommendation = "Assets/Scenes/Level03_ContentBasedRecommendation.unity";
        public const string CollaborativeFiltering = "Assets/Scenes/Level04_CollaborativeFiltering.unity";
        public const string MarketGapResearch = "Assets/Scenes/Level05_MarketGapResearch.unity";

        public static string PathForLevel(int level)
        {
            switch (level)
            {
                case 0: return Storefront;
                case 1: return ManualRecommendation;
                case 2: return RuleBasedRecommendation;
                case 3: return ContentBasedRecommendation;
                case 4: return CollaborativeFiltering;
                case 5: return MarketGapResearch;
                default: return Storefront;
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
                case Phase.Level4: return CollaborativeFiltering;
                case Phase.Level5: return MarketGapResearch;
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
                case Phase.Level4: return "collaborative-mainframe-crt";
                case Phase.Level5: return "market-gap-corkboard-studio";
                default: return "storefront-vhs-shop";
            }
        }
    }
}
