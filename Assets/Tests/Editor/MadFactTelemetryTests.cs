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
        [TestCase(8, LevelSceneCatalog.PosterGeneration)]
        public void LearningLevelsHaveDedicatedSceneRoutes(int level, string expectedPath)
        {
            Assert.That(LevelSceneCatalog.PathForLevel(level), Is.EqualTo(expectedPath));
        }

        [Test]
        public void DayThreeAdvancesFromCreativeBriefToPosterGeneration()
        {
            Assert.That(LevelSceneCatalog.NextLevelInSameDay(7), Is.EqualTo(8));
            Assert.That(LevelSceneCatalog.MaxPlayableLevel, Is.EqualTo(8));
        }

        [Test]
        public void PosterRunStateStartsWithAUniqueRelaySessionAndEmptyHistory()
        {
            var first = new RunState();
            var second = new RunState();

            Assert.That(first.PosterGenerationSessionId, Has.Length.EqualTo(32));
            Assert.That(second.PosterGenerationSessionId, Is.Not.EqualTo(first.PosterGenerationSessionId));
            Assert.That(first.PosterGenerations, Is.Empty);
            Assert.That(first.SelectedPosterIndex, Is.EqualTo(-1));
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
                Is.EqualTo(new[] { "WENDELL", "PRIYA", "HANK" }));
            Assert.That(MatrixTutorialModel.MovieNames[2], Is.EqualTo("BOOM TOWN"));
            Assert.That(model.Target[1, 2], Is.EqualTo(2f));
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
        public void ThreeByThreeCollaborativeBridgeHasOneMissingRatingAndOneClearMatch()
        {
            var model = new CollaborativeFilteringBridgeModel();
            int missing = 0;
            for (int row = 0; row < CollaborativeFilteringBridgeModel.Rows; row++)
                for (int column = 0; column < CollaborativeFilteringBridgeModel.Columns; column++)
                    if (!model.Known[row, column]) missing++;

            Assert.That(missing, Is.EqualTo(1));
            Assert.That(model.MissingRow, Is.EqualTo(1));
            Assert.That(model.MissingColumn, Is.EqualTo(2));
            Assert.That(model.MissingRating, Is.EqualTo(2));
            for (int column = 0; column < CollaborativeFilteringBridgeModel.Columns; column++)
                Assert.That(model.Target[0, column], Is.EqualTo(model.Target[1, column]));
        }

        [Test]
        public void FiveByFiveCollaborativeExerciseKeepsTibbsAtTwoAndUsesAClosestRowAverage()
        {
            var model = new CollaborativeFilteringMainModel();
            Assert.That(model.Known[CollaborativeFilteringMainModel.TibbsRow,
                CollaborativeFilteringMainModel.TibbsMissingColumn], Is.False);
            Assert.That(model.Target[CollaborativeFilteringMainModel.TibbsRow,
                CollaborativeFilteringMainModel.TibbsMissingColumn], Is.EqualTo(2f));
            Assert.That(model.Known[CollaborativeFilteringMainModel.AverageRow,
                CollaborativeFilteringMainModel.AverageColumn], Is.False);
            Assert.That(model.Target[CollaborativeFilteringMainModel.AverageRow,
                CollaborativeFilteringMainModel.AverageColumn], Is.EqualTo(model.AverageClueRating));
            Assert.That(model.AverageClueRating, Is.EqualTo(2f));

            int missingTwos = 0;
            for (int row = 0; row < CollaborativeFilteringMainModel.Rows; row++)
                for (int column = 0; column < CollaborativeFilteringMainModel.Columns; column++)
                    if (!model.Known[row, column] && model.Target[row, column] == 2f) missingTwos++;
            Assert.That(missingTwos, Is.EqualTo(2));
        }

        [Test]
        public void SparseCollaborativeHintsExcludeTheSelectedLineAndKeepOnlyTwoDistantExceptions()
        {
            var model = new SparseRatingsTutorialModel();
            int exceptions = 0;
            var exceptionCells = new System.Collections.Generic.List<UnityEngine.Vector2Int>();
            for (int row = 0; row < SparseRatingsTutorialModel.Rows; row++)
                for (int column = 0; column < SparseRatingsTutorialModel.Columns; column++)
                {
                    if (!model.Task[row, column]) continue;
                    int[] pair = model.HintIndices(row, column);
                    int[] rowPair = model.ClosestRowIndices(row, column);
                    int[] columnPair = model.ClosestColumnIndices(row, column);
                    Assert.That(pair, Has.Length.EqualTo(2));
                    Assert.That(pair[0], Is.Not.EqualTo(pair[1]));
                    Assert.That(rowPair, Has.Length.EqualTo(2));
                    Assert.That(rowPair[0], Is.Not.EqualTo(row));
                    Assert.That(rowPair[1], Is.Not.EqualTo(row));
                    Assert.That(model.Known[rowPair[0], column], Is.True);
                    Assert.That(model.Known[rowPair[1], column], Is.True);
                    Assert.That(columnPair, Has.Length.EqualTo(2));
                    Assert.That(columnPair[0], Is.Not.EqualTo(column));
                    Assert.That(columnPair[1], Is.Not.EqualTo(column));
                    Assert.That(model.Known[row, columnPair[0]], Is.True);
                    Assert.That(model.Known[row, columnPair[1]], Is.True);
                    if (model.HintUsesRows(row, column))
                    {
                        Assert.That(pair[0], Is.Not.EqualTo(row));
                        Assert.That(pair[1], Is.Not.EqualTo(row));
                        Assert.That(model.Known[pair[0], column], Is.True);
                        Assert.That(model.Known[pair[1], column], Is.True);
                    }
                    else
                    {
                        Assert.That(pair[0], Is.Not.EqualTo(column));
                        Assert.That(pair[1], Is.Not.EqualTo(column));
                        Assert.That(model.Known[row, pair[0]], Is.True);
                        Assert.That(model.Known[row, pair[1]], Is.True);
                    }

                    float average = model.SuggestedAverage(row, column);
                    if (model.IsException(row, column))
                    {
                        exceptions++;
                        exceptionCells.Add(new UnityEngine.Vector2Int(column, row));
                        Assert.That(UnityEngine.Mathf.Abs(model.Target[row, column] - average),
                            Is.GreaterThanOrEqualTo(2f));
                    }
                    else
                        Assert.That(model.Target[row, column], Is.EqualTo(average).Within(0.001f));
                }

            Assert.That(exceptions, Is.EqualTo(2));
            Assert.That(UnityEngine.Mathf.Abs(exceptionCells[0].x - exceptionCells[1].x) +
                UnityEngine.Mathf.Abs(exceptionCells[0].y - exceptionCells[1].y),
                Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void ThreeByThreeFactorizationStartsAboveTheManualErrorGoal()
        {
            var model = new MatrixTutorialModel();
            Assert.That(model.MeanError(), Is.GreaterThan(MatrixTutorialModel.GoalMeanError));
        }

        [Test]
        public void LevelFivePracticeModelsReuseTheExactLevelFourTables()
        {
            var collaborative = new CollaborativeFilteringMainModel();
            var two = new FactorizationPracticeModel(FactorizationPracticeKind.FiveByFiveTwoFactors);
            var four = new FactorizationPracticeModel(FactorizationPracticeKind.FiveByFiveFourFactors);
            Assert.That(two.Rows, Is.EqualTo(5));
            Assert.That(two.Columns, Is.EqualTo(5));
            Assert.That(two.FactorCount, Is.EqualTo(2));
            Assert.That(four.FactorCount, Is.EqualTo(4));
            Assert.That(two.MeanError(), Is.GreaterThan(FactorizationPracticeModel.TwoFactorGoalMeanError));
            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 5; column++)
                {
                    Assert.That(two.Target[row, column], Is.EqualTo(collaborative.Target[row, column]));
                    Assert.That(two.Known[row, column], Is.EqualTo(collaborative.Known[row, column]));
                    Assert.That(four.Target[row, column], Is.EqualTo(collaborative.Target[row, column]));
                    Assert.That(four.Known[row, column], Is.EqualTo(collaborative.Known[row, column]));
                }
        }

        [Test]
        public void FourFactorOptimizersLowerErrorOnFiveByFiveAndSparseTables()
        {
            foreach (FactorizationPracticeKind kind in new[]
            {
                FactorizationPracticeKind.FiveByFiveFourFactors,
                FactorizationPracticeKind.SparseFiveByNineFourFactors
            })
            {
                var model = new FactorizationPracticeModel(kind);
                float initial = model.MeanError();
                for (int step = 0; step < 384; step++) model.StepGradient(0.0035f);
                Assert.That(model.MeanError(), Is.LessThan(initial * 0.25f), kind.ToString());
            }
        }

        [Test]
        public void RunStateKeepsLevelFourRatingSnapshotsForLevelFive()
        {
            var run = new RunState();
            var snapshot = new RatingsComparisonSnapshot("level4_3x3", "TEST", 1, 2,
                new[] { 4f, 2f }, new[] { 5f, 2f }, new[] { true, false });
            run.SaveRatingsSnapshot(snapshot);
            Assert.That(run.TryGetRatingsSnapshot("level4_3x3", out var restored), Is.True);
            Assert.That(restored.ValueAt(0, 0), Is.EqualTo(4f));
            Assert.That(restored.OriginalAt(0, 0), Is.EqualTo(5f));
            Assert.That(restored.IsTask(0, 0), Is.True);
            Assert.That(CommsBox.DiscussionSeconds, Is.EqualTo(120f));
        }
    }
}
