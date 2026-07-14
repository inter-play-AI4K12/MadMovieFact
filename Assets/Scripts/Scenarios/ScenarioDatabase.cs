using System.Collections.Generic;

namespace MadFact
{
    /// <summary>
    /// Hand-authored scenario content. This starts as code so the POC can evolve quickly;
    /// once the shape feels right, these definitions are good candidates for ScriptableObjects.
    /// </summary>
    public static class ScenarioDatabase
    {
        public const string Level1Track = "level1_manual_counter";

        static List<LevelScenario> _level1Manual;

        public static IReadOnlyList<LevelScenario> Level1Manual
        {
            get
            {
                if (_level1Manual == null) _level1Manual = BuildLevel1Manual();
                return _level1Manual;
            }
        }

        public static LevelScenario NextLevel1Manual(RunState run)
        {
            var scenarios = Level1Manual;
            int index = run.NextScenarioIndex(Level1Track, scenarios.Count);
            return scenarios[index];
        }

        static List<LevelScenario> BuildLevel1Manual()
        {
            return new List<LevelScenario>
            {
                new LevelScenario(
                    "l1_wendell_first_space",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_wendell_first_space",
                        GameData.CustomerIndex("WENDELL"),
                        Genre.SciFi,
                        Genre.SciFi,
                        "Got anything with... y'know, SPACE in it?",
                        "Regular. Trusts the store if we remember his space kick."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Yeah. That's the stuff. Feels like someone here actually reads the back of the box.", "I'll come back after this one." },
                        "wendell_trusts_store"),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Hmm. Not really my orbit.", "No hard feelings, but I'm keeping the receipt." },
                        "wendell_left_unsure")),

                new LevelScenario(
                    "l1_dot_explosions",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_dot_explosions",
                        GameData.CustomerIndex("DOT"),
                        Genre.Action,
                        Genre.Action,
                        "I want stuff blowin' up. That's the whole ask.",
                        "Very literal. Explosions are not a metaphor."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Boom. Perfect. No notes.", "If the building is still standing by the credits, I want my money back." },
                        "dot_wants_more_action"),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Too quiet. I could hear myself thinking.", "That's not what I came to a video store for." })),

                new LevelScenario(
                    "l1_tibbs_gap_seed",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_tibbs_gap_seed",
                        GameData.CustomerIndex("THE TIBBS TWINS"),
                        Genre.Horror,
                        Genre.Comedy,
                        "We want to be SCARED and then LAUGH. Both. Together.",
                        "Odd request. Current catalog may not actually satisfy it."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Close, but it's missing the joke hiding inside the scream.", "Maybe nobody stocks what we want yet." },
                        "spook_comedy_gap_seeded"),
                    new ScenarioOutcome(
                        DialogueTarget.OldDude,
                        new[] { "Don't ignore that reaction. Sometimes a bad sale is a better clue than a good one.", "If the shelf can't satisfy a customer, the shelf might be the problem." },
                        "spook_comedy_gap_seeded")),

                new LevelScenario(
                    "l1_wendell_returns",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_wendell_returns",
                        GameData.CustomerIndex("WENDELL"),
                        Genre.SciFi,
                        Genre.SciFi,
                        "Back again. Got another space one, or did I already drain the shelf?",
                        "Return visit. Earlier satisfaction should affect later trust.",
                        returnVisit: true),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Okay, now you're building a streak.", "I used to browse blind. This is faster." },
                        "wendell_repeat_success"),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Second trip, same uncertainty.", "Maybe the robot thing on the counter could do better?" },
                        "wendell_needs_better_system")),

                new LevelScenario(
                    "l1_hank_mixed_signals",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_hank_mixed_signals",
                        GameData.CustomerIndex("HANK"),
                        Genre.SciFi,
                        Genre.Action,
                        "Spaceships AND gunfights. Don't make me choose.",
                        "Mixed taste: stated genre alone is not enough."),
                    new ScenarioOutcome(
                        DialogueTarget.OldDude,
                        new[] { "See? Some folks are a blend. You can't sort people into one shelf forever.", "That's why the old way starts breaking." })),

                new LevelScenario(
                    "l1_priya_clever_space",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_priya_clever_space",
                        GameData.CustomerIndex("PRIYA"),
                        Genre.SciFi,
                        Genre.SciFi,
                        "Something clever. Smart-clever, not dumb-clever.",
                        "Prefers space, but dislikes pure noise."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Good. Not just lasers and shouting.", "You have taste, apparently." }))
            };
        }
    }
}
