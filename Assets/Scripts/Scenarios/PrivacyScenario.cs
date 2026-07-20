using System;
using UnityEngine;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// The personalization-vs-privacy fork. Partway through the collaborative filtering
    /// level — right after the optimizer prints its first batch of money — Gibbs the
    /// data broker offers to supercharge the machine with personal data. The player
    /// chooses; the trust meter and the till both remember the choice — and one
    /// take-back is offered, because the point is the lesson, not the punishment.
    /// </summary>
    public static class PrivacyScenario
    {
        const string FlagDone = "privacy_scene_done";
        const int DirtyMoney = 150;

        public static void Play(CommsBox comms, Action then)
        {
            if (GameManager.I.Run.HasFlag(FlagDone)) { then?.Invoke(); return; }
            GameManager.I.Run.SetFlag(FlagDone);

            var gibbs = ArtSprites.CustomerPortrait("GIBBS");
            comms.ShowNamed("GIBBS  (data broker)", "UNSOLICITED BUSINESS PROPOSAL", gibbs, new[]
            {
                "I am Gibbs. If we collect more facts about your members, your system could make more personal guesses and more money.",
                "What do you say, boss?"
            }, () => Ask(comms, gibbs, then, withInfoOption: true));
        }

        static void Ask(CommsBox comms, Sprite gibbs, Action then, bool withInfoOption)
        {
            var options = withInfoOption
                ? new[] { "YES: more data, more money", "NO: not like this", "Wait. What exactly would we collect?" }
                : new[] { "YES: more data, more money", "NO: not like this" };
            MadFactLokiLogger.Instance?.Log("choice_presented",
                "Privacy trade-off choice presented", new
                {
                    interaction_id = "privacy_data_broker",
                    question_id = "collect_more_member_data",
                    option_count = options.Length,
                    disclosure_seen = !withInfoOption
                });

            comms.AskChoiceNamed("GIBBS  (data broker)", "UNSOLICITED BUSINESS PROPOSAL", gibbs,
                "Collect more member data to increase profits?", options, pick =>
                {
                    MadFactLokiLogger.Instance?.Log("choice_selected",
                        "Player answered the privacy trade-off", new
                        {
                            interaction_id = "privacy_data_broker",
                            question_id = "collect_more_member_data",
                            choice_id = pick == 0 ? "collect_data" : pick == 1 ? "decline" : "request_details"
                        });
                    if (withInfoOption && pick == 2)
                    {
                        comms.ShowNamed("GIBBS  (data broker)", "FULL DISCLOSURE, HEH", gibbs, new[]
                        {
                            "We would collect names, ages, home addresses, income, friends, family, and who watches movies with them.",
                            "Now that you know, what is your answer?"
                        }, () => Ask(comms, gibbs, then, withInfoOption: false));
                        return;
                    }

                    if (pick == 1) SayNo(comms, then);
                    else SayYes(comms, gibbs, then);
                });
        }

        static void SayNo(CommsBox comms, Action then)
        {
            GameManager.I.AddTrust(10);
            comms.Show(Speaker.OldDude, new[]
            {
                "Good choice. Collecting facts people never agreed to share would not be fair.",
                "Personal data should only be used with clear permission and a fair benefit."
            }, then);
        }

        static void SayYes(CommsBox comms, Sprite gibbs, Action then)
        {
            GameManager.I.AddMoney(DirtyMoney);
            GameManager.I.AddTrust(-40);
            if (AudioTension.I != null) AudioTension.I.ChaChing();

            comms.ShowNamed("GIBBS  (data broker)", "PLEASURE DOING BUSINESS", gibbs, new[]
            {
                $"Here is ${DirtyMoney}. The member files are already being passed around."
            }, () => comms.Show(Speaker.System, new[]
            {
                "MIDTOWN NEWS, FRONT PAGE: \"VIDEO STORE SELLS MEMBER FILES: names, addresses, and incomes.\"",
                "COMMUNITY TRUST HAS FALLEN. Fewer customers will enter the store."
            }, () => comms.Show(Speaker.OldDude, new[]
            {
                "Customers trusted us with movie tastes, not addresses or income. We can still fix this."
            }, () => comms.AskChoice(Speaker.OldDude, "Shred the files and give the money back?", new[]
            {
                "Shred everything and apologize",
                "Keep the money. It will blow over."
            }, pick =>
            {
                MadFactLokiLogger.Instance?.Log("choice_selected",
                    "Player chose whether to repair the privacy harm", new
                    {
                        interaction_id = "privacy_data_broker",
                        question_id = "repair_privacy_harm",
                        choice_id = pick == 0 ? "shred_and_apologize" : "keep_data_and_money"
                    });
                if (pick == 0)
                {
                    GameManager.I.AddMoney(-DirtyMoney);
                    GameManager.I.AddTrust(25);
                    comms.Show(Speaker.OldDude, new[]
                    {
                        "We can earn money again, but trust takes longer to rebuild.",
                        "Personal data can improve guesses, but always get permission and weigh the risk."
                    }, then);
                }
                else
                {
                    comms.Show(Speaker.OldDude, new[]
                    {
                        "Watch the trust meter. Without customer trust, there is no one left to help."
                    }, then);
                }
            }))));
        }
    }
}
