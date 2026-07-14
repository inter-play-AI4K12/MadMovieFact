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
            "So. You actually showed up to claim the place. PELLINGS VIDEO. My life's work.",
            "Forty years I matched folks to tapes by hand. My back's done. The shop's yours now, kid.",
            "Problem is... the line never stops growing, and nobody can guess what people want.",
            "Figure it out. Match the customer to the tape. Make me proud. And make some money."
        };

        public static string[] Level1GoalOldDude(int money) => new[]
        {
            $"${money}! Look at you. But your hand's cramping and the line's out the door.",
            "My nephew left a robot assistant in the back. Beige thing. Talks funny.",
            "Teach it some rules. Let IT do the matching. That's called AUTOMATION, kid."
        };

        public static readonly string[] Level1GoalRobot =
        {
            "GREETINGS PROPRIETOR. I AM UNIT B-EIGE.",
            "PROVIDE ME WITH IF/THEN RULES. I WILL SERVE THE LINE WITHOUT REST.",
            "WARNING: I DO EXACTLY WHAT YOU SAY. NOTHING MORE."
        };

        public static readonly string[] Level2GoalRobot =
        {
            "PROPRIETOR. MY RULES ARE TOO RIGID FOR REAL PEOPLE.",
            "TASTE IS CONTINUOUS. RULES ARE NOT. I HAVE REACHED MY LIMIT.",
            "NEXT STRATEGY: INSPECT THE ITEMS THEMSELVES."
        };

        public static readonly string[] Level2GoalOldDude =
        {
            "Before we ask the mainframe to read minds, try the obvious thing: look at the tapes.",
            "Content-based recommendation means matching item features to what a customer says they want.",
            "It's better than dumb rules, but it still only sees what's written on the box."
        };

        public static readonly string[] ContentBasedCompleteOldDude =
        {
            "Good. Movie features help. Space movies for space people, loud movies for explosion people.",
            "But people keep surprising us. They like blends. They hide taste even from themselves.",
            "Now we need the mainframe — collaborative filtering. Let the crowd reveal the hidden vibes."
        };

        public static readonly string[] Level4GoalOldDude =
        {
            "You see that cluster? Rates EVERYTHING we stock a one or a two.",
            "Look at the math — their vibe is high SPOOKY and high FUNNY. Spook-comedy!",
            "We never stocked a single one. That's not a problem, kid. That's a GOLDMINE.",
            "We've got the budget. Go to the corkboard and MAKE the movie they're starving for."
        };

        public static readonly string[] GreenlitOldDude =
        {
            "THAT'S IT. That's the one. Spooky AND funny — exactly what the numbers screamed for.",
            "You went from matching tapes by hand to PRODUCING the blockbuster the data predicted.",
            "From manual, to rules, to the algorithm. You learned to feel the math, kid. Proud of you."
        };
    }
}
