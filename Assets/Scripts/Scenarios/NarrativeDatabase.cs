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
            "NEXT PLAN: ASK CUSTOMERS TO RATE MOVIES THEY HAVE ALREADY WATCHED."
        };

        public static readonly string[] Level2GoalOldDude =
        {
            "A rating records how much one customer liked one movie, from zero to five stars.",
            "Put many ratings in a table. Each row is a customer, and each column is a movie."
        };

        public static readonly string[] RatingsTableCompleteOldDude =
        {
            "Good work. One row shows one customer's ratings. Looking down a column compares how everyone rated one movie."
        };

        public static readonly string[] ContentBasedCompleteOldDude =
        {
            "Movie features help us explain recommendations, but they still do not cover every person's taste.",
            "Next, combine features with the rating patterns you found to spot a movie our store is missing."
        };

        public static readonly string[] Level4GoalOldDude =
        {
            "The mainframe found hidden taste patterns in the ratings.",
            "Next shift, compare those patterns with movie features to make recommendations we can explain."
        };

        public static readonly string[] GreenlitOldDude =
        {
            "That poster brings your spooky and funny idea to life.",
            "You used choices, rules, ratings, computer systems, and a creative prompt to design a movie for real customer needs."
        };
    }
}
