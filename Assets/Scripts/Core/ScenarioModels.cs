using System.Collections.Generic;
using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// A specific customer appearance inside a level. The same CustomerData can return
    /// many times with different recent history, stated demand, and dialogue.
    /// </summary>
    [System.Serializable]
    public class CustomerVisit
    {
        public string Id;
        public int CustomerIndex;
        public Genre HistoryGenre;
        public Genre StatedGenre;
        public string DemandLine;
        public string FileNote;
        public bool ReturnVisit;

        public CustomerData Customer => GameData.Customers[Mathf.Clamp(CustomerIndex, 0, GameData.Customers.Count - 1)];

        public CustomerVisit(string id, int customerIndex, Genre historyGenre, Genre statedGenre, string demandLine, string fileNote = "", bool returnVisit = false)
        {
            Id = id;
            CustomerIndex = customerIndex;
            HistoryGenre = historyGenre;
            StatedGenre = statedGenre;
            DemandLine = demandLine;
            FileNote = fileNote;
            ReturnVisit = returnVisit;
        }
    }

    public enum DialogueTarget { Customer, OldDude, Robot, System }

    /// <summary>
    /// Optional narrative reaction to a scenario outcome. Keeping this small lets a
    /// customer, mentor, or system character comment on player choices without turning
    /// the game into a full quest-graph engine.
    /// </summary>
    [System.Serializable]
    public class ScenarioOutcome
    {
        public DialogueTarget Target;
        public string[] Lines;
        public string FlagToSet;

        public ScenarioOutcome(DialogueTarget target, string[] lines, string flagToSet = "")
        {
            Target = target;
            Lines = lines;
            FlagToSet = flagToSet;
        }
    }

    /// <summary>
    /// A playable task inside a level: who arrives, what they ask for, and what the
    /// story says afterwards based on the player's recommendation.
    /// </summary>
    [System.Serializable]
    public class LevelScenario
    {
        public string Id;
        public Phase Phase;
        public CustomerVisit Visit;
        public ScenarioOutcome Success;
        public ScenarioOutcome Failure;

        public LevelScenario(string id, Phase phase, CustomerVisit visit, ScenarioOutcome success = null, ScenarioOutcome failure = null)
        {
            Id = id;
            Phase = phase;
            Visit = visit;
            Success = success;
            Failure = failure;
        }

        public ScenarioOutcome OutcomeFor(SaleTier tier)
        {
            return tier == SaleTier.Terrible ? Failure : Success;
        }
    }

    /// <summary>One recommendation made by the player, kept for later callbacks.</summary>
    public class RecommendationRecord
    {
        public string ScenarioId;
        public string CustomerName;
        public string MovieTitle;
        public Phase Phase;
        public SaleTier Tier;
        public float Satisfaction;
        public int QuestionsAsked;

        public RecommendationRecord(string scenarioId, string customerName, string movieTitle, Phase phase, SaleTier tier, float satisfaction, int questionsAsked)
        {
            ScenarioId = scenarioId;
            CustomerName = customerName;
            MovieTitle = movieTitle;
            Phase = phase;
            Tier = tier;
            Satisfaction = satisfaction;
            QuestionsAsked = questionsAsked;
        }
    }

    /// <summary>
    /// Mutable run memory. This is intentionally tiny: enough for returning customers,
    /// consequence flags, and later dialogue callbacks, but not a heavyweight save system.
    /// </summary>
    public class RunState
    {
        readonly Dictionary<string, int> _scenarioCursorByTrack = new Dictionary<string, int>();
        readonly Dictionary<string, int> _visitsByCustomer = new Dictionary<string, int>();
        readonly Dictionary<string, float> _lastSatisfactionByCustomer = new Dictionary<string, float>();
        readonly HashSet<string> _flags = new HashSet<string>();

        public readonly List<RecommendationRecord> Recommendations = new List<RecommendationRecord>();

        public int NextScenarioIndex(string trackId, int count)
        {
            if (count <= 0) return 0;
            _scenarioCursorByTrack.TryGetValue(trackId, out int index);
            _scenarioCursorByTrack[trackId] = index + 1;
            return index % count;
        }

        public int VisitsFor(string customerName)
        {
            _visitsByCustomer.TryGetValue(customerName, out int count);
            return count;
        }

        public float LastSatisfactionFor(string customerName, float fallback = 0f)
        {
            return _lastSatisfactionByCustomer.TryGetValue(customerName, out float value) ? value : fallback;
        }

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
        }

        public void RecordRecommendation(LevelScenario scenario, MovieData movie, SaleTier tier, float satisfaction, int questionsAsked)
            => RecordRecommendation(scenario.Id, scenario.Visit.Customer.Name, scenario.Phase, movie, tier, satisfaction, questionsAsked);

        public void RecordRecommendation(string scenarioId, string customerName, Phase phase, MovieData movie, SaleTier tier, float satisfaction, int questionsAsked = 0)
        {
            Recommendations.Add(new RecommendationRecord(scenarioId, customerName, movie.Title, phase, tier, satisfaction, questionsAsked));

            _visitsByCustomer.TryGetValue(customerName, out int visits);
            _visitsByCustomer[customerName] = visits + 1;
            _lastSatisfactionByCustomer[customerName] = satisfaction;
        }

        /// <summary>Has this customer already taken this exact tape home during the run?</summary>
        public bool HasServed(string customerName, string movieTitle)
        {
            foreach (var r in Recommendations)
                if (r.CustomerName == customerName && r.MovieTitle == movieTitle) return true;
            return false;
        }

        /// <summary>
        /// Removes progress earned inside one failed level while preserving records from
        /// earlier levels. Used when bankruptcy restarts the current level from its start.
        /// </summary>
        public void ResetPhase(Phase phase, string scenarioTrackId = null, bool clearFlags = false)
        {
            Recommendations.RemoveAll(record => record.Phase == phase);
            if (!string.IsNullOrEmpty(scenarioTrackId))
                _scenarioCursorByTrack.Remove(scenarioTrackId);
            if (clearFlags) _flags.Clear();

            _visitsByCustomer.Clear();
            _lastSatisfactionByCustomer.Clear();
            foreach (var record in Recommendations)
            {
                _visitsByCustomer.TryGetValue(record.CustomerName, out int visits);
                _visitsByCustomer[record.CustomerName] = visits + 1;
                _lastSatisfactionByCustomer[record.CustomerName] = record.Satisfaction;
            }
        }
    }
}
