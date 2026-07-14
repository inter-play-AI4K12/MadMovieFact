using System.Collections.Generic;
using UnityEngine;

namespace MadFact
{
    public enum Genre { SciFi, Action, Horror, Comedy }

    [System.Serializable]
    public class MovieData
    {
        public string Title;
        public Genre Primary;
        public int Year;
        public Latent Vibe;       // ground-truth catalog features (known to the store)
        public Color PosterA, PosterB;
        public string Blurb;

        public MovieData(string title, Genre g, int year, Latent vibe, Color a, Color b, string blurb)
        { Title = title; Primary = g; Year = year; Vibe = vibe; PosterA = a; PosterB = b; Blurb = blurb; }
    }

    [System.Serializable]
    public class CustomerData
    {
        public string Name;
        public Genre HistoryGenre;     // what they rented before (a clue in L1/L2)
        public Genre StatedGenre;      // what they say they want (a clue in L1/L2)
        public Latent TrueVibe;        // hidden ground-truth taste
        public Color Skin, Shirt;
        public string Quip;            // flavour line at the counter
        public bool Underserved;       // member of the Spooky+Funny market gap
        public string Tag;             // demographic label

        public CustomerData(string name, Genre hist, Genre stated, Latent vibe, Color skin, Color shirt, string quip, bool gap = false, string tag = "")
        { Name = name; HistoryGenre = hist; StatedGenre = stated; TrueVibe = vibe; Skin = skin; Shirt = shirt; Quip = quip; Underserved = gap; Tag = tag; }
    }

    /// <summary>Static, hand-authored game content shared by every level.</summary>
    public static class GameData
    {
        static Color Col(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        // ---- The catalog: a tired store stuffed with sci-fi & action, no horror/comedy ----
        public static readonly List<MovieData> Movies = new List<MovieData>
        {
            new MovieData("STAR DRIFTER",     Genre.SciFi,  1988, new Latent(1.00f,0.05f,0.10f,0.30f), Col(40,60,120),  Col(120,180,230), "A lonely pilot drifts between dying stars."),
            new MovieData("ASTRO BLASTERS",   Genre.SciFi,  1991, new Latent(0.90f,0.05f,0.15f,0.90f), Col(30,40,90),   Col(230,140,40),  "Laser marines vs. the moon cartel."),
            new MovieData("BOOM TOWN",        Genre.Action, 1989, new Latent(0.20f,0.10f,0.10f,1.00f), Col(90,30,20),   Col(240,180,40),  "One cop. One city. Many explosions."),
            new MovieData("QUASAR RUN",       Genre.SciFi,  1990, new Latent(0.95f,0.00f,0.20f,0.55f), Col(20,30,70),   Col(90,200,220),  "Hyperspace courier outruns the void."),
            new MovieData("DEMOLITION DAVE",  Genre.Action, 1987, new Latent(0.10f,0.15f,0.20f,0.95f), Col(70,50,20),   Col(220,90,40),   "Dave does not read demolition permits."),
        };

        // ---- The customers ----
        // The Spooky+Funny crowd (Underserved) rate the whole catalog ~1-2: the market gap.
        public static readonly List<CustomerData> Customers = new List<CustomerData>
        {
            new CustomerData("WENDELL",  Genre.SciFi,  Genre.SciFi,  new Latent(1.00f,0.05f,0.10f,0.40f), Col(224,178,140), Col(70,110,160),  "Got anything with... y'know, SPACE in it?"),
            new CustomerData("DOT",      Genre.Action, Genre.Action, new Latent(0.20f,0.10f,0.10f,1.00f), Col(238,200,170), Col(160,60,50),   "I want stuff blowin' up. That's the whole ask."),
            new CustomerData("HANK",     Genre.SciFi,  Genre.Action, new Latent(0.90f,0.05f,0.10f,0.90f), Col(205,160,120), Col(60,90,80),    "Spaceships AND gunfights. Don't make me choose."),
            new CustomerData("PRIYA",    Genre.SciFi,  Genre.SciFi,  new Latent(0.85f,0.05f,0.30f,0.50f), Col(196,150,110), Col(110,70,130),  "Something clever. Smart-clever, not dumb-clever."),
            // --- The underserved demographic: high Spooky + high Funny ---
            new CustomerData("THE TIBBS TWINS", Genre.Horror, Genre.Comedy, new Latent(0.12f,0.95f,0.95f,0.12f), Col(214,176,150), Col(120,40,120), "We want to be SCARED and then LAUGH. Both. Together.", true, "SPOOK-COMEDY"),
            new CustomerData("MORTICIA",  Genre.Horror, Genre.Comedy, new Latent(0.10f,0.92f,0.85f,0.15f), Col(225,205,205), Col(60,40,90),  "Funny ghosts. Is that so much to ask?", true, "SPOOK-COMEDY"),
            new CustomerData("GIGGLES",   Genre.Comedy, Genre.Horror, new Latent(0.15f,0.85f,0.98f,0.10f), Col(235,195,160), Col(40,90,70),  "I cackle at the macabre. Recommend accordingly.", true, "SPOOK-COMEDY"),
        };

        // ---- The matrix used by Level 3 (subset of customers x all movies) ----
        // Rows for the Mainframe grid. We include the genre regulars + the gap crowd so the
        // optimizer surfaces the Spook-Comedy cluster as uniformly low.
        public static readonly int[] MatrixCustomers = { 0, 1, 2, 3, 4 }; // Wendell, Dot, Hank, Priya, Tibbs

        public static int CustomerIndex(string name)
        {
            for (int i = 0; i < Customers.Count; i++)
                if (Customers[i].Name == name) return i;
            return 0;
        }

        public static CustomerData CustomerByName(string name) => Customers[CustomerIndex(name)];

        /// <summary>Ground-truth star rating (1..5) of a customer for a movie.</summary>
        public static float TrueRating(CustomerData c, MovieData m) => MfMath.Predict(c.TrueVibe, m.Vibe);

        /// <summary>Best rating this customer can get from the current catalog (their ceiling).</summary>
        public static float BestAvailable(CustomerData c)
        {
            float best = 0f;
            foreach (var m in Movies) best = Mathf.Max(best, TrueRating(c, m));
            return best;
        }

        /// <summary>Average true taste of the underserved customers = the market-gap vibe.</summary>
        public static Latent MarketGapVibe()
        {
            Latent sum = new Latent(0, 0, 0, 0); int n = 0;
            foreach (var c in Customers) if (c.Underserved) { for (int i = 0; i < Latent.Dim; i++) sum[i] += c.TrueVibe[i]; n++; }
            if (n == 0) return sum;
            for (int i = 0; i < Latent.Dim; i++) sum[i] /= n;
            return sum;
        }

        public static int UnderservedCount()
        {
            int n = 0; foreach (var c in Customers) if (c.Underserved) n++; return n;
        }
    }
}
