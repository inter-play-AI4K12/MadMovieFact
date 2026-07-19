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
                "Heh... boss! There you are! Name's Gibbs. I represent certain interested parties.",
                "I saw the numbers that basement machine just printed. Impressive. But we could be RICHER.",
                "If we collect more data on your members, we personalize the system even harder. Hehehehe...",
                "Whaddya say, boss?"
            }, () => Ask(comms, gibbs, then, withInfoOption: true));
        }

        static void Ask(CommsBox comms, Sprite gibbs, Action then, bool withInfoOption)
        {
            var options = withInfoOption
                ? new[] { "YES — more data, more money", "NO — not like this", "Wait. What exactly would we collect?" }
                : new[] { "YES — more data, more money", "NO — not like this" };
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
                            "Oh hehe, boss, great question, very thorough, very professional.",
                            "Their names. Their ages. Their addresses. Their INCOME.",
                            "Their best friends and family! Who they watch tapes WITH! Hehehe...",
                            "So. Same question, boss."
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
                "Good choice, kiddo. If you'd done that, you'd have collected things they never agreed to share.",
                "And that's not right. Simple as that.",
                "Personalization is a deal: they give a LITTLE, you serve them BETTER. The moment it's not a deal, it's snooping."
            }, then);
        }

        static void SayYes(CommsBox comms, Sprite gibbs, Action then)
        {
            GameManager.I.AddMoney(DirtyMoney);
            GameManager.I.AddTrust(-40);
            if (AudioTension.I != null) AudioTension.I.ChaChing();

            comms.ShowNamed("GIBBS  (data broker)", "PLEASURE DOING BUSINESS", gibbs, new[]
            {
                $"HEHEHE! Smart boss! +${DirtyMoney}, up front, no receipts.",
                "The files are already... let's say 'circulating'. Don't ask where."
            }, () => comms.Show(Speaker.System, new[]
            {
                "MIDTOWN GAZETTE — FRONT PAGE: \"VIDEO STORE SELLS MEMBER FILES: names, addresses, incomes.\"",
                "A crowd is gathering outside the store. Several members are cutting their cards in half.",
                "COMMUNITY TRUST HAS COLLAPSED. Fewer customers are willing to walk in."
            }, () => comms.Show(Speaker.OldDude, new[]
            {
                "Kid. Forty years I knew every customer by name, and they trusted me with exactly ONE thing: what tape they liked.",
                "Their addresses? Their INCOME? We were never owed that.",
                "There's still time to make it right. Barely."
            }, () => comms.AskChoice(Speaker.OldDude, "Shred the files and give the money back?", new[]
            {
                "Shred everything and apologize",
                "Keep the money — it'll blow over"
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
                        "That's the right call. Money comes back. Trust comes back SLOW — but it comes back.",
                        "Remember this one, kid: the more personal the data, the better the guesses — and the bigger the betrayal.",
                        "That trade-off never goes away. You just have to stand somewhere decent on it."
                    }, then);
                }
                else
                {
                    comms.Show(Speaker.OldDude, new[]
                    {
                        "...Your store, kid. Your name on the door.",
                        "But watch that trust meter. Empty stores don't need recommender systems.",
                        "Nothing to recommend to nobody."
                    }, then);
                }
            }))));
        }
    }
}
