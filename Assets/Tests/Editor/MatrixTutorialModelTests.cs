using NUnit.Framework;

namespace MadFact.Tests
{
    public sealed class MatrixTutorialModelTests
    {
        [Test]
        public void TutorialUsesFamiliarCharactersAndTwoHiddenFactors()
        {
            CollectionAssert.AreEqual(new[] { "WENDELL", "DOT", "HANK" },
                MatrixTutorialModel.CustomerNames);
            Assert.AreEqual(2, MatrixTutorialModel.FactorCount);
        }

        [Test]
        public void UnusedTutorialFactorsStayAtZero()
        {
            var model = new MatrixTutorialModel();
            for (int row = 0; row < model.U.Length; row++)
            {
                Assert.AreEqual(0f, model.U[row][2]);
                Assert.AreEqual(0f, model.U[row][3]);
            }
            for (int column = 0; column < model.V.Length; column++)
            {
                Assert.AreEqual(0f, model.V[column][2]);
                Assert.AreEqual(0f, model.V[column][3]);
            }
        }

        [Test]
        public void TwoFactorManualSolutionCanBeatTheErrorGoal()
        {
            var model = new MatrixTutorialModel();
            model.U[0] = new Latent(1f, 0f, 0f, 0f);       // Wendell: factor 1
            model.U[1] = new Latent(0f, 1f, 0f, 0f);       // Dot: factor 2
            model.U[2] = new Latent(.75f, .75f, 0f, 0f);   // Hank: both

            Assert.Less(model.MeanError(), MatrixTutorialModel.GoalMeanError);
        }
    }
}
