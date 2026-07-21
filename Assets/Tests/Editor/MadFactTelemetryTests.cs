using MadFact.Telemetry;
using NUnit.Framework;
using UnityEngine;

namespace MadFact.Tests
{
    public sealed class MadFactTelemetryTests
    {
        [TearDown]
        public void TearDown()
        {
            if (MadFactLokiLogger.Instance != null)
                Object.DestroyImmediate(MadFactLokiLogger.Instance.gameObject);
            if (MadFactSessionManager.Instance != null)
                Object.DestroyImmediate(MadFactSessionManager.Instance.gameObject);
        }

        [TestCase("", "", true, "Please enter your display name.")]
        [TestCase("Player", "bad id", true, "The participant ID contains unsupported characters.")]
        [TestCase("Player", "study_42-A", false, "Please review and accept the logging consent.")]
        [TestCase("Player", "study_42-A", true, null)]
        public void ParticipantValidationMatchesStudyRules(
            string displayName, string participantId, bool consent, string expected)
        {
            Assert.That(MadFactSessionManager.Validate(displayName, participantId, consent),
                Is.EqualTo(expected));
        }

        [Test]
        public void EveryStartedSessionGetsANewGameSessionId()
        {
            var manager = new GameObject("Session Test").AddComponent<MadFactSessionManager>();
            string first = manager.StartSession("Player", "study_42", true).game_session_id;
            manager.EndSession("test");
            string second = manager.StartSession("Player", "study_42", true).game_session_id;

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void InvalidEventNamesAreNormalized()
        {
            Assert.That(MadFactLokiLogger.NormalizeEventType("Movie Recommended!"),
                Is.EqualTo("movie_recommended"));
            Assert.That(MadFactLokiLogger.NormalizeEventType(" Level__Started "),
                Is.EqualTo("level_started"));
        }

        [Test]
        public void StructuredDataSerializesAndEscapes()
        {
            string json = TelemetryJson.Serialize(new
            {
                movie_id = "The \"Best\"\nTape",
                score = 4.5f,
                accepted = true
            });

            Assert.That(json, Does.Contain("\"movie_id\":\"The \\\"Best\\\"\\nTape\""));
            Assert.That(json, Does.Contain("\"score\":4.5"));
            Assert.That(json, Does.Contain("\"accepted\":true"));
        }

        [Test]
        public void LokiPayloadUsesOnlyStableLabels()
        {
            string record = TelemetryJson.Serialize(new
            {
                participant_id = "study_42",
                game_session_id = "session_7",
                event_type = "level_started"
            });
            string payload = MadFactLokiLogger.BuildLokiPayload("123456789", record);

            Assert.That(payload, Does.Contain("\"stream\":{\"app\":\"madfact\",\"source\":\"unity\"}"));
            Assert.That(payload, Does.Not.Contain("\"stream\":{\"participant_id\""));
            Assert.That(payload, Does.Contain("\\\"participant_id\\\":\\\"study_42\\\""));
        }

        [TestCase(0, true)]
        [TestCase(408, true)]
        [TestCase(429, true)]
        [TestCase(503, true)]
        [TestCase(400, false)]
        [TestCase(401, false)]
        [TestCase(403, false)]
        public void RetryClassificationIsBounded(long status, bool expected)
        {
            Assert.That(MadFactTelemetryConfig.IsRetryableStatus(status), Is.EqualTo(expected));
        }

        [TestCase("http://localhost:8080/", "http://localhost:8080/api/telemetry")]
        [TestCase("http://192.168.1.20:9000/index.html",
            "http://192.168.1.20:9000/api/telemetry")]
        [TestCase("https://example.test/games/madfact/", "https://example.test/api/telemetry")]
        public void WebGlRelayUsesThePageOrigin(string pageUrl, string expected)
        {
            Assert.That(MadFactTelemetryConfig.BuildWebGlRelayEndpoint(pageUrl),
                Is.EqualTo(expected));
        }

        [Test]
        public void NoSessionMeansNoRemoteQueue()
        {
            var logger = new GameObject("Logger Test").AddComponent<MadFactLokiLogger>();
            logger.Log("level_started", "Local-only event", new { level_id = 1 });
            Assert.That(logger.QueuedEventCount, Is.Zero);
        }

        [TestCase(3, LevelSceneCatalog.RatingsTable)]
        [TestCase(4, LevelSceneCatalog.CollaborativeFiltering)]
        [TestCase(5, LevelSceneCatalog.MatrixFactorization)]
        [TestCase(6, LevelSceneCatalog.ContentBasedRecommendation)]
        [TestCase(7, LevelSceneCatalog.MarketGapResearch)]
        public void LearningLevelsHaveDedicatedSceneRoutes(int level, string expectedPath)
        {
            Assert.That(LevelSceneCatalog.PathForLevel(level), Is.EqualTo(expectedPath));
        }

        [Test]
        public void ThreeByThreeFactorizationTutorialUsesKnownCharactersAndOneMissingRating()
        {
            var model = new MatrixTutorialModel();
            int missing = 0;
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    if (!model.Known[row, column]) missing++;

            Assert.That(missing, Is.EqualTo(1));
            Assert.That(MatrixTutorialModel.CustomerNames,
                Is.EqualTo(new[] { "WENDELL", "DOT", "HANK" }));
            Assert.That(MatrixTutorialModel.MovieNames[2], Is.EqualTo("GALAXY RAIDERS"));
            Assert.That(model.Target[2, 2], Is.EqualTo(5f));
        }

        [Test]
        public void TwoByFiveCollaborativeTutorialUsesMatchingKnownCharacters()
        {
            var model = new CollaborativeFilteringTutorialModel();
            Assert.That(CollaborativeFilteringTutorialModel.Rows, Is.EqualTo(2));
            Assert.That(CollaborativeFilteringTutorialModel.Columns, Is.EqualTo(5));
            Assert.That(CollaborativeFilteringTutorialModel.CustomerNames,
                Is.EqualTo(new[] { "WENDELL", "PRIYA" }));

            int missing = 0;
            for (int row = 0; row < CollaborativeFilteringTutorialModel.Rows; row++)
                for (int column = 0; column < CollaborativeFilteringTutorialModel.Columns; column++)
                {
                    if (!model.Known[row, column]) missing++;
                    Assert.That(model.Target[0, column], Is.EqualTo(model.Target[1, column]));
                }

            Assert.That(missing, Is.EqualTo(1));
            Assert.That(model.MissingRow, Is.EqualTo(1));
            Assert.That(model.MissingColumn, Is.EqualTo(4));
            Assert.That(model.MissingRating, Is.EqualTo(1));
        }

        [Test]
        public void ThreeByThreeFactorizationStartsAboveTheManualErrorGoal()
        {
            var model = new MatrixTutorialModel();
            Assert.That(model.MeanError(), Is.GreaterThan(MatrixTutorialModel.GoalMeanError));
        }
    }
}
