using System.Collections.Generic;
using UnityEngine;

namespace MadFact
{
    public enum Genre { SciFi, Action, Horror, Comedy, Drama, Romance, Documentary, Animation }

    /// <summary>MPAA-style sticker on the box. Gates who a tape may be handed to.</summary>
    public enum AgeRating { G, PG, PG13, R }

    public static class GenreInfo
    {
        public const int Count = 8;

        public static readonly string[] Names =
            { "SCI-FI", "ACTION", "HORROR", "COMEDY", "DRAMA", "ROMANCE", "DOCUMENTARY", "ANIMATION" };

        public static readonly Color[] Colors =
        {
            new Color(0.36f, 0.55f, 0.95f), // sci-fi   - blue
            new Color(0.93f, 0.42f, 0.24f), // action   - orange
            new Color(0.62f, 0.40f, 0.85f), // horror   - purple
            new Color(0.97f, 0.78f, 0.25f), // comedy   - yellow
            new Color(0.55f, 0.62f, 0.70f), // drama    - slate
            new Color(0.92f, 0.45f, 0.62f), // romance  - pink
            new Color(0.45f, 0.75f, 0.60f), // doc      - teal
            new Color(0.60f, 0.85f, 0.95f), // animation- sky
        };

        public static string Name(Genre g) => Names[(int)g];

        public static string RatingLabel(AgeRating r)
        {
            switch (r)
            {
                case AgeRating.G: return "G";
                case AgeRating.PG: return "PG";
                case AgeRating.PG13: return "PG-13";
                default: return "R";
            }
        }

        /// <summary>Minimum customer age a tape with this sticker may be handed to.</summary>
        public static int MinAge(AgeRating r)
        {
            switch (r)
            {
                case AgeRating.G: return 0;
                case AgeRating.PG: return 8;
                case AgeRating.PG13: return 13;
                default: return 17;
            }
        }
    }

    [System.Serializable]
    public class MovieData
    {
        public string Title;
        public Genre Primary;
        public int Year;
        public AgeRating Rating;
        public Latent Vibe;       // hidden ground-truth vibes (the 4-dim latent space)
        public float[] Features;  // what's printed on the box: 0..1 per Genre (8 entries)
        public Color PosterA, PosterB;
        public string Blurb;

        public MovieData(string title, Genre g, int year, AgeRating rating, Latent vibe,
            float[] features, Color a, Color b, string blurb)
        {
            Title = title; Primary = g; Year = year; Rating = rating; Vibe = vibe;
            Features = features; PosterA = a; PosterB = b; Blurb = blurb;
        }

        public string TitleWithYear => Title + " (" + Year + ")";
    }

    [System.Serializable]
    public class CustomerData
    {
        public string Name;
        public int Age;
        public Genre HistoryGenre;     // what they rented before (a clue in L1/L2)
        public Genre StatedGenre;      // what they say they want (a clue in L1/L2)
        public Latent TrueVibe;        // hidden ground-truth taste
        public float[] GenreTaste;     // visible genre-level profile built from their history (L3)
        public Color Skin, Shirt;
        public string Quip;            // flavour line at the counter
        public bool Underserved;       // member of the Spooky+Funny market gap
        public string Tag;             // demographic label

        public CustomerData(string name, int age, Genre hist, Genre stated, Latent vibe,
            float[] genreTaste, Color skin, Color shirt, string quip, bool gap = false, string tag = "")
        {
            Name = name; Age = age; HistoryGenre = hist; StatedGenre = stated; TrueVibe = vibe;
            GenreTaste = genreTaste; Skin = skin; Shirt = shirt; Quip = quip; Underserved = gap; Tag = tag;
        }
    }

    /// <summary>Static, hand-authored game content shared by every level.</summary>
    public static class GameData
    {
        static Color Col(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        /// <summary>Genre feature vector shorthand: F(Genre.X, 0.9f, Genre.Y, 0.4f, ...)</summary>
        static float[] F(params object[] pairs)
        {
            var f = new float[GenreInfo.Count];
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                f[(int)(Genre)pairs[i]] = (float)pairs[i + 1];
            return f;
        }

        // ---- The catalog -------------------------------------------------
        // The first five tapes are the original stock and the only ones wired into the
        // Level 4 mainframe matrix (see MatrixMovies). The catalog beyond them exists so
        // Levels 1-3 have real shelves to browse: eight genres, age stickers, box features.
        // Authoring guardrails:
        //  - no tape carries BOTH spooky>=0.6 AND funny>=0.6 — that combination is the
        //    unserved market gap Level 5 is about, so the shelf must never contain it.
        //  - every non-gap customer has at least one near-perfect tape somewhere.
        public static readonly List<MovieData> Movies = new List<MovieData>
        {
            new MovieData("STAR DRIFTER",       Genre.SciFi,       1988, AgeRating.PG,   new Latent(1.00f,0.05f,0.10f,0.30f), F(Genre.SciFi,0.95f, Genre.Drama,0.35f),                       Col(40,60,120),  Col(120,180,230), "A lonely pilot drifts between dying stars."),
            new MovieData("ASTRO BLASTERS",     Genre.SciFi,       1991, AgeRating.PG13, new Latent(0.90f,0.05f,0.15f,0.90f), F(Genre.SciFi,0.90f, Genre.Action,0.80f),                      Col(30,40,90),   Col(230,140,40),  "Laser marines vs. the moon cartel."),
            new MovieData("BOOM TOWN",          Genre.Action,      1989, AgeRating.PG13, new Latent(0.20f,0.10f,0.10f,1.00f), F(Genre.Action,0.95f, Genre.Drama,0.20f),                      Col(90,30,20),   Col(240,180,40),  "One cop. One city. Many explosions."),
            new MovieData("QUASAR RUN",         Genre.SciFi,       1990, AgeRating.PG,   new Latent(0.95f,0.00f,0.20f,0.55f), F(Genre.SciFi,0.90f, Genre.Action,0.50f),                      Col(20,30,70),   Col(90,200,220),  "Hyperspace courier outruns the void."),
            new MovieData("DEMOLITION DAVE",    Genre.Action,      1987, AgeRating.PG13, new Latent(0.10f,0.15f,0.20f,0.95f), F(Genre.Action,0.95f, Genre.Comedy,0.30f),                     Col(70,50,20),   Col(220,90,40),   "Dave does not read demolition permits."),

            new MovieData("THE YELLOW WALLPAPER", Genre.Horror,    1988, AgeRating.R,    new Latent(0.05f,0.95f,0.05f,0.10f), F(Genre.Horror,0.95f, Genre.Drama,0.40f),                      Col(60,45,10),   Col(180,140,30),  "The pattern in the wallpaper moves at night."),
            new MovieData("GRAVEYARD SHIFT VI", Genre.Horror,      1990, AgeRating.R,    new Latent(0.10f,0.90f,0.10f,0.35f), F(Genre.Horror,0.90f, Genre.Action,0.30f),                     Col(25,20,35),   Col(140,40,60),   "The night crew keeps digging things up."),
            new MovieData("MONSTER MASH HIGH",  Genre.Comedy,      1991, AgeRating.PG,   new Latent(0.05f,0.60f,0.50f,0.15f), F(Genre.Comedy,0.70f, Genre.Horror,0.50f),                     Col(40,80,50),   Col(170,220,90),  "New school. Old curse. Pop quiz Friday."),
            new MovieData("LOVE ON THE FERRIS WHEEL", Genre.Romance, 1989, AgeRating.PG, new Latent(0.05f,0.05f,0.92f,0.02f), F(Genre.Romance,0.95f, Genre.Comedy,0.40f, Genre.Drama,0.30f), Col(150,60,90),  Col(250,180,200), "Two strangers, one stuck gondola, ninety minutes."),
            new MovieData("MIDNIGHT CONFESSIONS", Genre.Romance,   1992, AgeRating.PG13, new Latent(0.02f,0.25f,0.88f,0.05f), F(Genre.Romance,0.80f, Genre.Drama,0.60f),                     Col(50,30,80),   Col(220,120,160), "A late-night radio host falls for a caller."),
            new MovieData("THE LONG WINTER",    Genre.Drama,       1987, AgeRating.PG,   new Latent(0.05f,0.95f,0.08f,0.02f), F(Genre.Drama,0.95f),                                          Col(70,80,95),   Col(200,210,225), "A farmhouse, a frozen field, an unspoken debt."),
            new MovieData("COURTROOM OF DUST",  Genre.Drama,       1991, AgeRating.PG13, new Latent(0.05f,0.60f,0.15f,0.30f), F(Genre.Drama,0.90f),                                          Col(95,75,45),   Col(215,190,140), "A small-town trial nobody wanted to win."),
            new MovieData("PLANET OCEAN",       Genre.Documentary, 1990, AgeRating.G,    new Latent(0.55f,0.05f,0.10f,0.05f), F(Genre.Documentary,0.95f, Genre.Drama,0.20f),                 Col(15,60,90),   Col(80,190,210),  "Four years of filming beneath the waves."),
            new MovieData("APOLLO: THE UNTOLD HOURS", Genre.Documentary, 1989, AgeRating.G, new Latent(0.95f,0.02f,0.05f,0.15f), F(Genre.Documentary,0.90f, Genre.SciFi,0.50f),              Col(20,25,45),   Col(200,205,215), "The mission tapes they never broadcast."),
            new MovieData("CAPTAIN COTTONTAIL", Genre.Animation,   1992, AgeRating.G,    new Latent(0.10f,0.05f,0.85f,0.20f), F(Genre.Animation,0.95f, Genre.Comedy,0.60f),                  Col(90,150,70),  Col(240,230,120), "A rabbit with a badge and nothing to lose."),
            new MovieData("ROBO-PUPS",          Genre.Animation,   1991, AgeRating.G,    new Latent(0.35f,0.02f,0.80f,0.45f), F(Genre.Animation,0.90f, Genre.SciFi,0.40f, Genre.Comedy,0.50f), Col(45,90,130), Col(250,200,90),  "Good dogs. Better lasers."),
            new MovieData("STARLIGHT SERENADE", Genre.Romance,     1990, AgeRating.PG,   new Latent(0.82f,0.02f,0.65f,0.02f), F(Genre.Romance,0.80f, Genre.SciFi,0.60f),                     Col(60,40,110),  Col(230,180,240), "An astronomer writes love letters to a comet."),
        };

        // ---- The customers -------------------------------------------------
        // The Spooky+Funny crowd (Underserved) crave both AT ONCE — a taste no single-genre
        // tape can serve. They top out at "close" on the shelf, and rate the original
        // mainframe stock ~1-2 stars: the market gap Level 4 reveals and Level 5 fills.
        public static readonly List<CustomerData> Customers = new List<CustomerData>
        {
            new CustomerData("WENDELL", 27, Genre.SciFi,  Genre.SciFi,  new Latent(1.00f,0.05f,0.10f,0.40f), F(Genre.SciFi,0.90f, Genre.Action,0.40f, Genre.Documentary,0.30f), Col(224,178,140), Col(70,110,160),  "Got anything with... y'know, SPACE in it?"),
            new CustomerData("DOT", 24, Genre.Action, Genre.Action,     new Latent(0.20f,0.10f,0.10f,1.00f), F(Genre.Action,0.95f, Genre.SciFi,0.30f),                          Col(238,200,170), Col(160,60,50),   "I want stuff blowin' up. That's the whole ask."),
            new CustomerData("HANK", 41, Genre.SciFi,  Genre.Action,    new Latent(0.90f,0.05f,0.10f,0.90f), F(Genre.SciFi,0.70f, Genre.Action,0.80f),                          Col(205,160,120), Col(60,90,80),    "Spaceships AND gunfights. Don't make me choose."),
            new CustomerData("PRIYA", 31, Genre.SciFi,  Genre.SciFi,    new Latent(0.85f,0.05f,0.30f,0.50f), F(Genre.SciFi,0.85f, Genre.Drama,0.40f, Genre.Documentary,0.40f),  Col(196,150,110), Col(110,70,130),  "Something clever. Smart-clever, not dumb-clever."),
            // --- The underserved demographic: high Spooky + high Funny, TOGETHER ---
            new CustomerData("THE TIBBS TWINS", 16, Genre.Horror, Genre.Comedy, new Latent(0.12f,0.62f,0.62f,0.12f), F(Genre.Horror,0.80f, Genre.Comedy,0.80f), Col(214,176,150), Col(120,40,120), "We want to be SCARED and then LAUGH. Both. Together.", true, "SPOOK-COMEDY"),
            new CustomerData("MORTICIA", 29, Genre.Horror, Genre.Comedy, new Latent(0.10f,0.62f,0.60f,0.12f), F(Genre.Horror,0.85f, Genre.Comedy,0.70f), Col(225,205,205), Col(60,40,90),  "Funny ghosts. Is that so much to ask?", true, "SPOOK-COMEDY"),
            new CustomerData("GIGGLES", 38, Genre.Comedy, Genre.Horror,  new Latent(0.15f,0.60f,0.65f,0.10f), F(Genre.Comedy,0.90f, Genre.Horror,0.70f), Col(235,195,160), Col(40,90,70),  "I cackle at the macabre. Recommend accordingly.", true, "SPOOK-COMEDY"),
            // --- The wider neighbourhood (Levels 1-3 shelf traffic) ---
            new CustomerData("TIMMY", 9, Genre.Horror, Genre.Horror,     new Latent(0.10f,0.80f,0.35f,0.40f), F(Genre.Horror,0.80f, Genre.Animation,0.50f, Genre.Action,0.40f), Col(230,190,160), Col(200,60,40),   "I want the SCARIEST one. I can handle it. Probably."),
            new CustomerData("ROSA", 34, Genre.Romance, Genre.Romance,   new Latent(0.05f,0.08f,0.95f,0.05f), F(Genre.Romance,0.95f, Genre.Comedy,0.50f, Genre.Drama,0.40f),    Col(210,160,120), Col(220,110,140), "Something that makes my heart do the thing."),
            new CustomerData("EARL", 61, Genre.Documentary, Genre.Documentary, new Latent(0.95f,0.02f,0.05f,0.10f), F(Genre.Documentary,0.95f, Genre.Drama,0.50f, Genre.SciFi,0.30f), Col(222,190,165), Col(120,110,90), "Real footage. Real facts. None of that made-up stuff."),
            new CustomerData("BABS", 45, Genre.Drama, Genre.Drama,       new Latent(0.05f,0.92f,0.10f,0.05f), F(Genre.Drama,0.95f, Genre.Romance,0.50f),                        Col(228,185,155), Col(90,70,110),   "I want to FEEL something. Preferably in black and white."),
            new CustomerData("VICTOR", 28, Genre.Drama, Genre.Romance,   new Latent(0.02f,0.40f,0.90f,0.02f), F(Genre.Drama,0.70f, Genre.Romance,0.80f),                        Col(200,155,115), Col(50,60,100),   "Something aching. But, like, gently aching."),
            new CustomerData("THE NGUYEN KIDS", 8, Genre.Animation, Genre.Animation, new Latent(0.30f,0.02f,0.85f,0.40f), F(Genre.Animation,0.95f, Genre.Comedy,0.60f),         Col(215,175,135), Col(240,170,60),  "Cartoons! With a dog in them! Or TWO dogs!!"),
        };

        // ---- The matrix used by Level 4 (subset of customers x original movies) ----
        // Rows for the Mainframe grid. We include the genre regulars + the gap crowd so the
        // optimizer surfaces the Spook-Comedy cluster as uniformly low.
        public static readonly int[] MatrixCustomers = { 0, 1, 2, 3, 4 }; // Wendell, Dot, Hank, Priya, Tibbs
        // The mainframe was installed in '91 and only ever indexed the original stock;
        // it also keeps the grid readable at 5x5.
        public static readonly int[] MatrixMovies = { 0, 1, 2, 3, 4 };

        public static int CustomerIndex(string name)
        {
            for (int i = 0; i < Customers.Count; i++)
                if (Customers[i].Name == name) return i;
            return 0;
        }

        public static CustomerData CustomerByName(string name) => Customers[CustomerIndex(name)];

        public static int MovieIndex(string title)
        {
            for (int i = 0; i < Movies.Count; i++)
                if (Movies[i].Title == title) return i;
            return 0;
        }

        /// <summary>All catalog indices whose Primary genre matches (shelf sections).</summary>
        public static List<int> MoviesInGenre(Genre g)
        {
            var list = new List<int>();
            for (int i = 0; i < Movies.Count; i++)
                if (Movies[i].Primary == g) list.Add(i);
            return list;
        }

        /// <summary>Ground-truth star rating (1..5) of a customer for a movie.</summary>
        public static float TrueRating(CustomerData c, MovieData m) => MfMath.Predict(c.TrueVibe, m.Vibe);

        /// <summary>
        /// What a content-based engine SEES: cosine similarity of the customer's visible
        /// genre profile against the movie's box features (0..1). This is the number the
        /// Level 3 engine proudly prints — and it only knows what's written on the box.
        /// </summary>
        public static float FeatureMatch(CustomerData c, MovieData m)
        {
            float dot = 0f, ca = 0f, cb = 0f;
            for (int i = 0; i < GenreInfo.Count; i++)
            {
                dot += c.GenreTaste[i] * m.Features[i];
                ca += c.GenreTaste[i] * c.GenreTaste[i];
                cb += m.Features[i] * m.Features[i];
            }
            if (ca <= 0f || cb <= 0f) return 0f;
            return Mathf.Clamp01(dot / (Mathf.Sqrt(ca) * Mathf.Sqrt(cb)));
        }

        /// <summary>Is this tape age-appropriate for this customer?</summary>
        public static bool AgeOk(CustomerData c, MovieData m) => c.Age >= GenreInfo.MinAge(m.Rating);

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
