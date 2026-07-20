namespace MadFact
{
    /// <summary>
    /// Central home for fixed story beats. Scenario-specific dialogue can live next to
    /// scenarios; broad progression beats live here so MadFactBootstrap stays focused
    /// on transitions instead of storing all narrative copy inline.
    /// </summary>
    public static class NarrativeDatabase
    {
        public static readonly string[] IntroOldDude =
        {
            "Welcome to PELLINGS VIDEO. I matched people to tapes by hand for forty years, and now the shop is yours.",
            "The line is growing. Match each customer with the right tape and earn enough to keep the store open."
        };

        public static string[] Level1GoalOldDude(int money) => new[]
        {
            $"${money}! Great work, but the line is now too long to serve by hand.",
            "My nephew built a robot helper. Give it clear IF/THEN rules so it can automate the work."
        };

        public static readonly string[] Level1GoalRobot =
        {
            "HELLO, STORE OWNER. I AM UNIT B-EIGE. GIVE ME IF/THEN RULES, BUT REMEMBER: I DO EXACTLY WHAT YOU SAY."
        };

        public static readonly string[] Level2GoalRobot =
        {
            "MY RULES ARE TOO STRICT FOR REAL PEOPLE. SIMPLE RULES CANNOT HANDLE EVERY KIND OF TASTE.",
            "NEXT PLAN: LOOK AT THE FEATURES OF EACH MOVIE."
        };

        public static readonly string[] Level2GoalOldDude =
        {
            "A content-based system matches movie features with what a customer likes.",
            "It works better than simple rules, but it only knows facts written on the box."
        };

        public static readonly string[] ContentBasedCompleteOldDude =
        {
            "Movie features help, but some likes are hard to describe.",
            "The mainframe will compare many ratings to find hidden patterns. This is collaborative filtering."
        };

        public static readonly string[] Level4GoalOldDude =
        {
            "The hidden dials show this group wants something SPOOKY and FUNNY: a scary comedy.",
            "We do not stock one, so use the corkboard to design the movie they want."
        };

        public static readonly string[] GreenlitOldDude =
        {
            "That is it: spooky AND funny, just like the ratings showed.",
            "You used hand choices, rules, and computer systems to recommend and even design a movie. I am proud of you."
        };
    }
}
