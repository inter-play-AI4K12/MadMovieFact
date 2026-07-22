using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// Small 3×3 teaching dataset used before the main 5×5 matrix. It reuses familiar
    /// game characters and only two hidden taste factors so students can reduce error
    /// by hand before the optimizer is introduced.
    /// </summary>
    public sealed class MatrixTutorialModel
    {
        public const int FactorCount = 2;
        public const float GoalMeanError = 0.35f;
        public static readonly string[] CustomerNames = { "WENDELL", "PRIYA", "HANK" };
        public static readonly string[] MovieNames = { "STAR DRIFTER", "ASTRO BLASTERS", "BOOM TOWN" };

        public readonly float[,] Target =
        {
            { 5f, 4f, 2f },
            { 5f, 4f, 2f },
            { 4f, 5f, 3f }
        };

        public readonly bool[,] Known =
        {
            { true, true, true },
            { true, true, false },
            { true, true, true }
        };

        public readonly Latent[] U = new Latent[3];
        public readonly Latent[] V = new Latent[3];

        public MatrixTutorialModel() => Reset();

        public void Reset()
        {
            for (int i = 0; i < U.Length; i++)
                U[i] = new Latent(.5f, .5f, 0f, 0f);

            V[0] = new Latent(1f, .02f, 0f, 0f);       // mostly factor 1
            V[1] = new Latent(.02f, 1f, 0f, 0f);       // mostly factor 2
            V[2] = new Latent(.70f, .70f, 0f, 0f);     // both factors
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
