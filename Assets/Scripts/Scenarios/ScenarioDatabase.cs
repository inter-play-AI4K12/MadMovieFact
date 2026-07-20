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
                        "A regular customer. He trusts us when we remember that he loves space movies."),
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
                        "She means exactly what she says: she wants lots of action."),
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
                        "They want a mix of horror and comedy. Our shelf may not have it."),
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
                        "He has returned. His last visit should affect how much he trusts us.",
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
                        "He likes two kinds of movies. One genre is not enough to describe his taste."),
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
                        "She likes smart space stories, not only loud action."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Good. Not just lasers and shouting.", "You have taste, apparently." })),

                new LevelScenario(
                    "l1_rosa_heart_thing",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_rosa_heart_thing",
                        GameData.CustomerIndex("ROSA"),
                        Genre.Romance,
                        Genre.Romance,
                        "Something that makes my heart do the thing.",
                        "She wants a strong feeling, even if she cannot name it."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "THE THING. My heart did the thing.", "You're getting a regular out of this, you know." },
                        "rosa_regular"),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "My heart did NOT do the thing.", "It did a different, worse thing." })),

                new LevelScenario(
                    "l1_earl_real_footage",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_earl_real_footage",
                        GameData.CustomerIndex("EARL"),
                        Genre.Documentary,
                        Genre.Documentary,
                        "Real footage. Real facts. None of that made-up stuff.",
                        "He loves facts and checks if documentaries are correct."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "Now THAT is real footage. I will be back Thursday to check two of those facts." }),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "This is made up. I asked for real facts.", "I want a refund." })),

                new LevelScenario(
                    "l1_nguyen_two_dogs",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_nguyen_two_dogs",
                        GameData.CustomerIndex("THE NGUYEN KIDS"),
                        Genre.Animation,
                        Genre.Animation,
                        "Cartoons! With a dog in them! Or TWO dogs!!",
                        "They are eight years old. Check the age rating on the box."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "DOGS!! Did you SEE the part with the... the... WE'RE WATCHING IT AGAIN." }),
                    new ScenarioOutcome(
                        DialogueTarget.OldDude,
                        new[] { "Kid, look at the box next time. The sticker in the corner isn't decoration.", "Wrong tape for an eight-year-old is worse than no tape at all." })),

                new LevelScenario(
                    "l1_babs_feel_something",
                    Phase.Level1,
                    new CustomerVisit(
                        "visit_babs_feel_something",
                        GameData.CustomerIndex("BABS"),
                        Genre.Drama,
                        Genre.Drama,
                        "I want to FEEL something. Preferably in black and white.",
                        "Do not recommend anything with a laugh track."),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "...I felt something.", "I'm not going to tell you what it was. Good tape." }),
                    new ScenarioOutcome(
                        DialogueTarget.Customer,
                        new[] { "I felt NOTHING. This was the wrong movie for me.", "I want a full refund." }))
            };
        }
    }
}
