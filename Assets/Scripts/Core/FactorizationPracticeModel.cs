using UnityEngine;

namespace MadFact
{
    public enum FactorizationPracticeKind
    {
        FiveByFiveTwoFactors,
        FiveByFiveFourFactors,
        SparseFiveByNineFourFactors
    }

    /// <summary>
    /// Configurable Level 5 practice data. It deliberately reuses Level 4's exact
    /// tables so the comparison screen contrasts two methods on the same ratings.
    /// </summary>
    public sealed class FactorizationPracticeModel
    {
        public const float TwoFactorGoalMeanError = 1.10f;

        public readonly FactorizationPracticeKind Kind;
        public readonly int Rows;
        public readonly int Columns;
        public readonly int FactorCount;
        public readonly string[] CustomerNames;
        public readonly string[] MovieNames;
        public readonly float[,] Target;
        public readonly bool[,] Known;
        public readonly Latent[] U;
        public readonly Latent[] V;

        readonly Latent[] _seedU;
        readonly Latent[] _seedV;

        public FactorizationPracticeModel(FactorizationPracticeKind kind)
        {
            Kind = kind;
            bool sparse = kind == FactorizationPracticeKind.SparseFiveByNineFourFactors;
            Rows = sparse ? SparseRatingsTutorialModel.Rows : CollaborativeFilteringMainModel.Rows;
            Columns = sparse ? SparseRatingsTutorialModel.Columns : CollaborativeFilteringMainModel.Columns;
            FactorCount = kind == FactorizationPracticeKind.FiveByFiveTwoFactors ? 2 : 4;
            CustomerNames = sparse
                ? SparseRatingsTutorialModel.CustomerNames
                : CollaborativeFilteringMainModel.CustomerNames;
            MovieNames = sparse
                ? SparseRatingsTutorialModel.MovieNames
                : CollaborativeFilteringMainModel.MovieNames;
            Target = new float[Rows, Columns];
            Known = new bool[Rows, Columns];
            U = new Latent[Rows];
            V = new Latent[Columns];
            _seedU = new Latent[Rows];
            _seedV = new Latent[Columns];

            if (sparse)
            {
                var source = new SparseRatingsTutorialModel();
                for (int row = 0; row < Rows; row++)
                    for (int column = 0; column < Columns; column++)
                    {
                        Target[row, column] = source.Target[row, column];
                        Known[row, column] = source.Known[row, column];
                    }
            }
            else
            {
                var source = new CollaborativeFilteringMainModel();
                for (int row = 0; row < Rows; row++)
                    for (int column = 0; column < Columns; column++)
                    {
                        Target[row, column] = source.Target[row, column];
                        Known[row, column] = source.Known[row, column];
                    }
            }

            Seed();
            Reset();
        }

        void Seed()
        {
            if (FactorCount == 2)
            {
                for (int row = 0; row < Rows; row++)
                    _seedU[row] = new Latent(0.45f, 0.45f, 0f, 0f);
                _seedV[0] = new Latent(0.90f, 0.10f, 0f, 0f);
                _seedV[1] = new Latent(0.20f, 0.80f, 0f, 0f);
                _seedV[2] = new Latent(0.55f, 0.55f, 0f, 0f);
                _seedV[3] = new Latent(0.85f, 0.15f, 0f, 0f);
                _seedV[4] = new Latent(0.15f, 0.85f, 0f, 0f);
                return;
            }

            for (int row = 0; row < Rows; row++)
            {
                float offset = row * 0.035f;
                _seedU[row] = new Latent(0.34f + offset, 0.38f, 0.32f, 0.36f - offset * 0.4f);
            }
            for (int column = 0; column < Columns; column++)
            {
                float a = 0.22f + 0.10f * (column % 4);
                float b = 0.26f + 0.08f * ((column + 1) % 4);
                float c = 0.20f + 0.09f * ((column + 2) % 4);
                float d = 0.24f + 0.07f * ((column + 3) % 4);
                _seedV[column] = new Latent(a, b, c, d);
            }
        }

        public void Reset()
        {
            for (int row = 0; row < Rows; row++) U[row] = _seedU[row];
            for (int column = 0; column < Columns; column++) V[column] = _seedV[column];
        }

        public float Guess(int row, int column) => MfMath.Predict(U[row], V[column]);
        float GuessRaw(int row, int column) => MfMath.PredictRaw(U[row], V[column]);

        public float MeanError()
        {
            float total = 0f;
            int count = 0;
            for (int row = 0; row < Rows; row++)
                for (int column = 0; column < Columns; column++)
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
            for (int row = 0; row < Rows; row++)
                for (int column = 0; column < Columns; column++)
                    if (Known[row, column])
                        worst = Mathf.Max(worst, Mathf.Abs(Target[row, column] - Guess(row, column)));
            return worst;
        }

        public float StepGradient(float learningRate)
        {
            var gradientU = new Latent[Rows];
            var gradientV = new Latent[Columns];
            for (int row = 0; row < Rows; row++)
                for (int column = 0; column < Columns; column++)
                {
                    if (!Known[row, column]) continue;
                    float residual = Target[row, column] - GuessRaw(row, column);
                    float multiplier = -2f * residual * MfMath.Scale;
                    for (int factor = 0; factor < FactorCount; factor++)
                    {
                        gradientU[row][factor] += multiplier * V[column][factor];
                        gradientV[column][factor] += multiplier * U[row][factor];
                    }
                }

            for (int row = 0; row < Rows; row++)
                for (int factor = 0; factor < FactorCount; factor++)
                    U[row][factor] = Mathf.Clamp(U[row][factor] - learningRate * gradientU[row][factor], 0f, 1.3f);
            for (int column = 0; column < Columns; column++)
                for (int factor = 0; factor < FactorCount; factor++)
                    V[column][factor] = Mathf.Clamp(V[column][factor] - learningRate * gradientV[column][factor], 0f, 1.3f);
            return MeanError();
        }
    }
}
