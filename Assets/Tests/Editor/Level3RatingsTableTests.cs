using NUnit.Framework;

namespace MadFact.Tests
{
    public sealed class Level3RatingsTableTests
    {
        [Test]
        public void EveryProfileHasUnambiguousMostAndLeastFavoriteMovies()
        {
            int[] expectedMost = { 0, 2, 1, 3 };
            int[] expectedLeast = { 2, 4, 4, 2 };

            for (int profile = 0; profile < Level3RatingsTable.ProfileCount; profile++)
            {
                Assert.AreEqual(expectedMost[profile], Level3RatingsTable.FavoriteForProfile(profile));
                Assert.AreEqual(expectedLeast[profile], Level3RatingsTable.LeastFavoriteForProfile(profile));
            }
        }

        [Test]
        public void OverallQuestionsUseColumnAverages()
        {
            Assert.AreEqual(1, Level3RatingsTable.OverallFavorite());
            Assert.AreEqual(4, Level3RatingsTable.OverallLeastFavorite());
            Assert.AreEqual(3.75f, Level3RatingsTable.AverageForMovie(1));
            Assert.AreEqual(1.75f, Level3RatingsTable.AverageForMovie(4));
        }

        [TestCase(0, "☆☆☆☆☆")]
        [TestCase(2, "★★☆☆☆")]
        [TestCase(5, "★★★★★")]
        public void StarsShowsTheZeroToFiveScale(int rating, string expected)
        {
            Assert.AreEqual(expected, Level3RatingsTable.Stars(rating));
        }
    }
}
