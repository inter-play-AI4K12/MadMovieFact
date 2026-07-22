using NUnit.Framework;

namespace MadFact.Tests
{
    public sealed class SparseRatingsTutorialModelTests
    {
        [Test]
        public void SparseTableIsFiveByNineWithFivePredictionTasks()
        {
            var model = new SparseRatingsTutorialModel();

            Assert.AreEqual(5, SparseRatingsTutorialModel.Rows);
            Assert.AreEqual(9, SparseRatingsTutorialModel.Columns);
            Assert.AreEqual(5, model.TaskCount);
            Assert.AreEqual(5, model.Target.GetLength(0));
            Assert.AreEqual(9, model.Target.GetLength(1));
        }

        [Test]
        public void PredictionTasksAreMissingRatingsAndTableStaysSparse()
        {
            var model = new SparseRatingsTutorialModel();
            int missing = 0;

            for (int row = 0; row < SparseRatingsTutorialModel.Rows; row++)
                for (int column = 0; column < SparseRatingsTutorialModel.Columns; column++)
                {
                    if (!model.Known[row, column]) missing++;
                    if (model.Task[row, column])
                        Assert.IsFalse(model.Known[row, column],
                            $"Task at ({row}, {column}) must not already be a known rating.");
                }

            Assert.GreaterOrEqual(missing, 20, "The final table should visibly model sparse real-world data.");
        }

        [Test]
        public void EveryOriginalRatingUsesTheOneToFiveScale()
        {
            var model = new SparseRatingsTutorialModel();
            for (int row = 0; row < SparseRatingsTutorialModel.Rows; row++)
                for (int column = 0; column < SparseRatingsTutorialModel.Columns; column++)
                    Assert.That(model.Target[row, column], Is.InRange(1, 5));
        }
    }
}
