using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// Standalone 3×3 teaching dataset used before the main 5×5 matrix. None of these
    /// people, movies, ratings, or factor vectors are taken from the mainframe model.
    /// </summary>
    public sealed class MatrixTutorialModel
    {
        public static readonly string[] CustomerNames = { "MAYA", "LEO", "SAM" };
        public static readonly string[] MovieNames = { "STAR VOYAGE", "BOOM PATROL", "GALAXY RAIDERS" };

        public readonly float[,] Target =
        {
            { 5f, 1f, 4f },
            { 1f, 5f, 4f },
            { 4f, 4f, 5f }
        };

        public readonly bool[,] Known =
        {
            { true, true, true },
            { true, true, true },
            { true, true, false }
        };

        public readonly Latent[] U = new Latent[3];
        public readonly Latent[] V = new Latent[3];

        public MatrixTutorialModel() => Reset();

        public void Reset()
        {
            for (int i = 0; i < U.Length; i++)
                U[i] = new Latent(.5f, 0f, 0f, .5f);

            V[0] = new Latent(1f, 0f, 0f, .02f);       // space
            V[1] = new Latent(.02f, 0f, 0f, 1f);       // action
            V[2] = new Latent(.70f, 0f, 0f, .70f);     // space + action
        }

        public float Guess(int row, int column) => MfMath.Predict(U[row], V[column]);

        public float MeanError()
        {
            float total = 0f;
            int count = 0;
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    if (Known[row, column])
                    {
                        total += Mathf.Abs(Target[row, column] - Guess(row, column));
                        count++;
                    }
            return count == 0 ? 0f : total / count;
        }

        public float WorstError()
        {
            float worst = 0f;
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    if (Known[row, column])
                        worst = Mathf.Max(worst, Mathf.Abs(Target[row, column] - Guess(row, column)));
            return worst;
        }

        public float StepGradient(float learningRate)
        {
            var gradientU = new Latent[3];
            var gradientV = new Latent[3];

            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                {
                    if (!Known[row, column]) continue;
                    float residual = Target[row, column] - MfMath.PredictRaw(U[row], V[column]);
                    float factor = -2f * residual * MfMath.Scale;
                    for (int dimension = 0; dimension < Latent.Dim; dimension++)
                    {
                        gradientU[row][dimension] += factor * V[column][dimension];
                        gradientV[column][dimension] += factor * U[row][dimension];
                    }
                }

            for (int row = 0; row < 3; row++)
                for (int dimension = 0; dimension < Latent.Dim; dimension++)
                    U[row][dimension] = Mathf.Clamp(
                        U[row][dimension] - learningRate * gradientU[row][dimension], 0f, 1.3f);

            for (int column = 0; column < 3; column++)
                for (int dimension = 0; dimension < Latent.Dim; dimension++)
                    V[column][dimension] = Mathf.Clamp(
                        V[column][dimension] - learningRate * gradientV[column][dimension], 0f, 1.3f);

            return MeanError();
        }
    }
}
