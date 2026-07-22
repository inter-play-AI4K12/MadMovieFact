using UnityEngine;

namespace MadFact
{
    /// <summary>The four continuous latent features ("Vibes") that drive Matrix Factorization.</summary>
    public enum Vibe { Spacey = 0, Spooky = 1, Funny = 2, Explosions = 3 }

    /// <summary>A 4-dimensional latent vector. Used for both movie features and customer tastes.</summary>
    [System.Serializable]
    public struct Latent
    {
        public float Spacey, Spooky, Funny, Explosions;

        public Latent(float spacey, float spooky, float funny, float explosions)
        {
            Spacey = spacey; Spooky = spooky; Funny = funny; Explosions = explosions;
        }

        public const int Dim = 4;
        public static readonly string[] Names = { "SPACE-Y", "SPOOKY", "FUNNY", "EXPLOSIONS" };
        public static readonly Color[] Colors =
        {
            new Color(0.36f, 0.55f, 0.95f), // spacey  - blue
            new Color(0.62f, 0.40f, 0.85f), // spooky  - purple
            new Color(0.97f, 0.78f, 0.25f), // funny   - yellow
            new Color(0.93f, 0.42f, 0.24f), // explosions - orange
        };

        public float this[int i]
        {
            get
            {
                switch (i) { case 0: return Spacey; case 1: return Spooky; case 2: return Funny; default: return Explosions; }
            }
            set
            {
                switch (i) { case 0: Spacey = value; break; case 1: Spooky = value; break; case 2: Funny = value; break; default: Explosions = value; break; }
            }
        }

        public float Dot(Latent o) => Spacey * o.Spacey + Spooky * o.Spooky + Funny * o.Funny + Explosions * o.Explosions;

        public Latent Clamped(float min = 0f, float max = 1.2f)
            => new Latent(Mathf.Clamp(Spacey, min, max), Mathf.Clamp(Spooky, min, max),
                          Mathf.Clamp(Funny, min, max), Mathf.Clamp(Explosions, min, max));

        public float Magnitude => Mathf.Sqrt(Dot(this));

        public static Latent Lerp(Latent a, Latent b, float t)
            => new Latent(Mathf.Lerp(a.Spacey, b.Spacey, t), Mathf.Lerp(a.Spooky, b.Spooky, t),
                          Mathf.Lerp(a.Funny, b.Funny, t), Mathf.Lerp(a.Explosions, b.Explosions, t));

        public override string ToString()
            => $"[Sp {Spacey:0.0} Sk {Spooky:0.0} Fn {Funny:0.0} Ex {Explosions:0.0}]";
    }

    /// <summary>Shared rating math so every level speaks the same language.</summary>
    public static class MfMath
    {
        // prediction = clamp(1 + SCALE * dot, 1, 5). SCALE chosen so a strong single-axis
        // match (dot ~1.0) yields ~5 stars.
        public const float Scale = 4f;
        public const float Min = 1f;
        public const float Max = 5f;

        /// <summary>Predicted star rating (1..5) of a customer taste vector against a movie feature vector.</summary>
        public static float Predict(Latent customer, Latent movie)
            => Mathf.Clamp(1f + Scale * customer.Dot(movie), Min, Max);

        /// <summary>Raw (unclamped) prediction, used by the optimizer's gradients.</summary>
        public static float PredictRaw(Latent customer, Latent movie) => 1f + Scale * customer.Dot(movie);

        /// <summary>Loss for a single interaction = |target - guess|.</summary>
        public static float Loss(float target, float guess) => Mathf.Abs(target - guess);
    }
}
