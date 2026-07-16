using System.Collections.Generic;
using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// The live Matrix Factorization model behind the Mainframe (Level 4).
    /// Rows = customers (latent taste U, editable), Cols = movies (latent features V, editable).
    /// guess(i,j) = 1 + scale * dot(U_i, V_j). The Optimizer runs real gradient descent
    /// over the known cells to minimise squared error.
    /// </summary>
    public class MfModel
    {
        public readonly List<CustomerData> Customers = new List<CustomerData>();
        public readonly List<MovieData> Movies = new List<MovieData>();

        public Latent[] U;          // customer latent taste (what the player/optimizer solves)
        public Latent[] V;          // movie latent features (start at catalog truth)
        public float[,] Target;     // known target ratings
        public bool[,] Known;       // is this a known interaction?

        public int Rows => Customers.Count;
        public int Cols => Movies.Count;

        public MfModel()
        {
            foreach (var idx in GameData.MatrixCustomers) Customers.Add(GameData.Customers[idx]);
            // The grid is pinned to the original stock: it keeps the board a readable 5x5
            // and the '91 mainframe never indexed the newer shelf sections anyway.
            foreach (var idx in GameData.MatrixMovies) Movies.Add(GameData.Movies[idx]);
            int r = Rows, c = Cols;

            U = new Latent[r];
            V = new Latent[c];
            Target = new float[r, c];
            Known = new bool[r, c];

            // movie features start at catalog truth (a little noised so the optimizer has work)
            for (int j = 0; j < c; j++)
            {
                var v = Movies[j].Vibe;
                V[j] = new Latent(
                    Mathf.Clamp01(v.Spacey + Random.Range(-0.05f, 0.05f)),
                    Mathf.Clamp01(v.Spooky + Random.Range(-0.05f, 0.05f)),
                    Mathf.Clamp01(v.Funny + Random.Range(-0.05f, 0.05f)),
                    Mathf.Clamp01(v.Explosions + Random.Range(-0.05f, 0.05f)));
            }
            // customer taste starts neutral -> lots of initial error -> loud static
            for (int i = 0; i < r; i++)
                U[i] = new Latent(0.5f, 0.5f, 0.5f, 0.5f);

            // targets from ground truth; hide a scatter of cells as "to be predicted"
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                {
                    Target[i, j] = Mathf.Round(GameData.TrueRating(Customers[i], Movies[j]) * 2f) / 2f; // .5 steps
                    Known[i, j] = true;
                }
            // hidden cells (the satisfying "empty nodes populate" moment)
            SetHidden(0, 3); SetHidden(1, 0); SetHidden(2, 4); SetHidden(3, 1); SetHidden(4, 2);
        }

        void SetHidden(int i, int j) { if (i < Rows && j < Cols) Known[i, j] = false; }

        public float Guess(int i, int j) => MfMath.Predict(U[i], V[j]);
        public float GuessRaw(int i, int j) => MfMath.PredictRaw(U[i], V[j]);

        public float CellError(int i, int j) => Known[i, j] ? Mathf.Abs(Target[i, j] - Guess(i, j)) : 0f;

        /// <summary>Mean absolute error over all known cells (0 = perfect). Drives the audio static.</summary>
        public float MeanError()
        {
            float sum = 0; int n = 0;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    if (Known[i, j]) { sum += Mathf.Abs(Target[i, j] - Guess(i, j)); n++; }
            return n == 0 ? 0 : sum / n;
        }

        public float WorstError()
        {
            float w = 0;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Cols; j++)
                    if (Known[i, j]) w = Mathf.Max(w, Mathf.Abs(Target[i, j] - Guess(i, j)));
            return w;
        }

        // ---- Gradient descent --------------------------------------------
        const float Lambda = 0.02f;   // L2 regularisation
        const float ClampMax = 1.3f;

        /// <summary>One step of gradient descent over known cells. Returns the mean error afterwards.</summary>
        public float StepGradient(float lr)
        {
            int r = Rows, c = Cols;
            var gU = new Latent[r];
            var gV = new Latent[c];

            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                {
                    if (!Known[i, j]) continue;
                    float e = Target[i, j] - GuessRaw(i, j); // residual on raw prediction
                    float k = -2f * e * MfMath.Scale;
                    for (int d = 0; d < Latent.Dim; d++)
                    {
                        gU[i][d] += k * V[j][d];
                        gV[j][d] += k * U[i][d];
                    }
                }
            // regularisation
            for (int i = 0; i < r; i++) for (int d = 0; d < Latent.Dim; d++) gU[i][d] += 2f * Lambda * U[i][d];
            for (int j = 0; j < c; j++) for (int d = 0; d < Latent.Dim; d++) gV[j][d] += 2f * Lambda * V[j][d];

            for (int i = 0; i < r; i++)
                for (int d = 0; d < Latent.Dim; d++)
                    U[i][d] = Mathf.Clamp(U[i][d] - lr * gU[i][d], 0f, ClampMax);
            for (int j = 0; j < c; j++)
                for (int d = 0; d < Latent.Dim; d++)
                    V[j][d] = Mathf.Clamp(V[j][d] - lr * gV[j][d], 0f, ClampMax);

            return MeanError();
        }

        public void ResetCustomerTaste()
        {
            for (int i = 0; i < Rows; i++) U[i] = new Latent(0.5f, 0.5f, 0.5f, 0.5f);
        }
    }
}
