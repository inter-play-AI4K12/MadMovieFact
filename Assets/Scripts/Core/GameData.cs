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
        public string Short;      // compact display name for tight UI (matrix headers)
        public Genre Primary;
        public int Year;
        public AgeRating Rating;
        public Latent Vibe;       // hidden ground-truth vibes (the 4-dim latent space)
        public float[] Features;  // what's printed on the box: 0..1 per Genre (8 entries)
        public Color PosterA, PosterB;
        public string Blurb;

        public MovieData(string title, Genre g, int year, AgeRating rating, Latent vibe,
            float[] features, Color a, Color b, string blurb, string shortName = null)
        {
            Title = title; Primary = g; Year = year; Rating = rating; Vibe = vibe;
            Features = features; PosterA = a; PosterB = b; Blurb = blurb;
            Short = shortName ?? title;
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
        // Famous, era-appropriate rentals (per Erfan: real movies, none rated R, at most
        // ten per genre). The mainframe uses its own fictional stock (MatrixMovieSet).
        // Authoring guardrails:
        //  - no tape carries BOTH spooky>=0.6 AND funny>=0.6 — that combination is the
        //    unserved market gap Level 5 is about, so the shelf must never contain it
        //    (which is also why no Ghostbusters/Beetlejuice on these shelves).
        //  - every non-gap customer has at least one near-perfect tape somewhere.
        public static readonly List<MovieData> Movies = new List<MovieData>
        {
            new MovieData("STAR WARS",          Genre.SciFi,       1977, AgeRating.PG,   new Latent(1.00f,0.05f,0.20f,0.55f), F(Genre.SciFi,0.95f, Genre.Action,0.60f),                      Col(15,20,45),   Col(240,205,80),  "A farm kid, a princess, and one very big laser."),
            new MovieData("RAIDERS OF THE LOST ARK", Genre.Action, 1981, AgeRating.PG,   new Latent(0.15f,0.15f,0.25f,0.85f), F(Genre.Action,0.95f, Genre.Drama,0.30f),                      Col(80,50,20),   Col(230,170,60),  "Snakes. Why did it have to be snakes?", "RAIDERS"),
            new MovieData("E.T.",               Genre.SciFi,       1982, AgeRating.PG,   new Latent(0.85f,0.05f,0.40f,0.10f), F(Genre.SciFi,0.80f, Genre.Drama,0.50f),                       Col(20,25,60),   Col(150,190,240), "A boy, a bike, and a friend from very far away."),
            new MovieData("TOP GUN",            Genre.Action,      1986, AgeRating.PG,   new Latent(0.30f,0.05f,0.15f,0.90f), F(Genre.Action,0.90f, Genre.Romance,0.40f),                    Col(25,40,70),   Col(220,120,60),  "Jets, aviators, and the danger zone."),
            new MovieData("BACK TO THE FUTURE", Genre.SciFi,       1985, AgeRating.PG,   new Latent(0.80f,0.10f,0.35f,0.35f), F(Genre.SciFi,0.85f, Genre.Comedy,0.55f),                      Col(30,25,55),   Col(250,140,40),  "88 miles per hour and a very confused teenager.", "BACK 2 FUTURE"),

            new MovieData("JAWS",               Genre.Horror,      1975, AgeRating.PG,   new Latent(0.05f,0.85f,0.05f,0.50f), F(Genre.Horror,0.90f, Genre.Drama,0.40f),                      Col(10,30,55),   Col(200,225,240), "You're gonna need a bigger boat."),
            new MovieData("POLTERGEIST",        Genre.Horror,      1982, AgeRating.PG13, new Latent(0.05f,0.95f,0.10f,0.30f), F(Genre.Horror,0.95f),                                          Col(25,20,40),   Col(180,190,230), "They're heeere. The TV is not your friend."),
            new MovieData("HOME ALONE",         Genre.Comedy,      1990, AgeRating.PG,   new Latent(0.05f,0.15f,0.90f,0.35f), F(Genre.Comedy,0.95f),                                          Col(140,30,30),  Col(250,220,150), "One kid. Two burglars. Zero mercy."),
            new MovieData("MRS. DOUBTFIRE",     Genre.Comedy,      1993, AgeRating.PG13, new Latent(0.02f,0.05f,0.85f,0.10f), F(Genre.Comedy,0.90f, Genre.Drama,0.40f),                      Col(90,60,110),  Col(240,210,190), "The nanny is not who she appears to be.", "MRS DOUBTFIRE"),
            new MovieData("THE PRINCESS BRIDE", Genre.Romance,     1987, AgeRating.PG,   new Latent(0.10f,0.20f,0.80f,0.30f), F(Genre.Romance,0.80f, Genre.Comedy,0.70f),                    Col(60,80,50),   Col(235,215,160), "As you wish. Fencing, fighting, true love."),
            new MovieData("SLEEPLESS IN SEATTLE", Genre.Romance,   1993, AgeRating.PG,   new Latent(0.02f,0.02f,0.95f,0.02f), F(Genre.Romance,0.95f, Genre.Drama,0.30f),                     Col(35,45,80),   Col(230,190,210), "Two coasts, one radio show, zero chance meetings.", "SLEEPLESS"),
            new MovieData("GHOST",              Genre.Romance,     1990, AgeRating.PG13, new Latent(0.05f,0.45f,0.60f,0.10f), F(Genre.Romance,0.90f, Genre.Drama,0.40f, Genre.Horror,0.30f), Col(30,30,55),   Col(190,200,235), "Love, clay pottery, and unfinished business."),
            new MovieData("DEAD POETS SOCIETY", Genre.Drama,       1989, AgeRating.PG,   new Latent(0.05f,0.95f,0.10f,0.02f), F(Genre.Drama,0.95f),                                          Col(45,55,70),   Col(210,200,170), "Carpe diem. Seize the day, boys.", "DEAD POETS"),
            new MovieData("FIELD OF DREAMS",    Genre.Drama,       1989, AgeRating.PG,   new Latent(0.45f,0.50f,0.30f,0.05f), F(Genre.Drama,0.90f),                                          Col(30,60,40),   Col(220,225,190), "If you build it, he will come."),
            new MovieData("FOR ALL MANKIND",    Genre.Documentary, 1989, AgeRating.G,    new Latent(0.95f,0.02f,0.05f,0.15f), F(Genre.Documentary,0.90f, Genre.SciFi,0.50f),                 Col(20,25,45),   Col(200,205,215), "The Apollo missions, told by the men who flew them."),
            new MovieData("HOOP DREAMS",        Genre.Documentary, 1994, AgeRating.PG13, new Latent(0.10f,0.30f,0.30f,0.35f), F(Genre.Documentary,0.95f, Genre.Drama,0.50f),                 Col(60,35,25),   Col(235,150,60),  "Five years, two kids, one impossible dream."),
            new MovieData("THE LION KING",      Genre.Animation,   1994, AgeRating.G,    new Latent(0.15f,0.30f,0.70f,0.40f), F(Genre.Animation,0.95f, Genre.Drama,0.40f),                   Col(120,50,20),  Col(250,180,60),  "The circle of life, with excellent songs."),
            new MovieData("ALADDIN",            Genre.Animation,   1992, AgeRating.G,    new Latent(0.30f,0.10f,0.85f,0.45f), F(Genre.Animation,0.90f, Genre.Comedy,0.60f),                  Col(40,30,90),   Col(90,200,190),  "A lamp, a genie, and a whole new world."),
            new MovieData("THE ROCKETEER",      Genre.Action,      1991, AgeRating.PG,   new Latent(0.60f,0.10f,0.30f,0.70f), F(Genre.Action,0.80f, Genre.SciFi,0.50f),                      Col(90,30,30),   Col(220,180,90),  "A stunt pilot straps on a jetpack. It goes okay."),

            new MovieData("THE EMPIRE STRIKES BACK", Genre.SciFi,  1980, AgeRating.PG,   new Latent(0.95f,0.15f,0.15f,0.60f), F(Genre.SciFi,0.95f, Genre.Action,0.60f),                      Col(12,18,40),   Col(190,200,220), "The one where everyone finds out.", "EMPIRE"),
            new MovieData("RETURN OF THE JEDI", Genre.SciFi,       1983, AgeRating.PG,   new Latent(0.90f,0.10f,0.30f,0.65f), F(Genre.SciFi,0.90f, Genre.Action,0.60f),                      Col(20,35,25),   Col(120,200,110), "Ewoks vs. empire. Ewoks win.", "JEDI"),
            new MovieData("FLIGHT OF THE NAVIGATOR", Genre.SciFi,  1986, AgeRating.PG,   new Latent(0.85f,0.10f,0.50f,0.20f), F(Genre.SciFi,0.85f, Genre.Comedy,0.40f),                      Col(25,45,70),   Col(170,215,235), "A boy, a chrome ship, eight missing years.", "NAVIGATOR"),
            new MovieData("THE KARATE KID",     Genre.Action,      1984, AgeRating.PG,   new Latent(0.05f,0.10f,0.35f,0.60f), F(Genre.Action,0.80f, Genre.Drama,0.50f),                      Col(160,120,40), Col(240,230,200), "Wax on. Wax off. Crane kick.", "KARATE KID"),
            new MovieData("JURASSIC PARK",      Genre.Action,      1993, AgeRating.PG13, new Latent(0.50f,0.55f,0.15f,0.80f), F(Genre.Action,0.80f, Genre.SciFi,0.70f),                      Col(20,40,25),   Col(230,60,40),   "Life finds a way. Run."),
            new MovieData("THE FUGITIVE",       Genre.Action,      1993, AgeRating.PG13, new Latent(0.05f,0.35f,0.05f,0.75f), F(Genre.Action,0.90f, Genre.Drama,0.50f),                      Col(30,35,45),   Col(180,190,205), "One-armed man. One very determined marshal."),
            new MovieData("GREMLINS",           Genre.Horror,      1984, AgeRating.PG,   new Latent(0.05f,0.55f,0.45f,0.40f), F(Genre.Horror,0.60f, Genre.Comedy,0.60f),                     Col(30,50,35),   Col(180,230,120), "Three rules. They break all of them."),
            new MovieData("SOMETHING WICKED THIS WAY COMES", Genre.Horror, 1983, AgeRating.PG, new Latent(0.05f,0.90f,0.10f,0.10f), F(Genre.Horror,0.85f, Genre.Drama,0.40f),               Col(35,25,45),   Col(200,170,220), "The carnival arrives at midnight.", "SMTH WICKED"),
            new MovieData("THE WATCHER IN THE WOODS", Genre.Horror, 1980, AgeRating.PG,  new Latent(0.02f,0.88f,0.05f,0.10f), F(Genre.Horror,0.85f),                                          Col(20,35,25),   Col(150,200,160), "Something is watching from the trees.", "THE WATCHER"),
            new MovieData("ARACHNOPHOBIA",      Genre.Horror,      1990, AgeRating.PG13, new Latent(0.02f,0.80f,0.30f,0.35f), F(Genre.Horror,0.80f, Genre.Comedy,0.40f),                     Col(40,30,25),   Col(210,170,110), "Eight legs. Zero chill."),
            new MovieData("THE SANDLOT",        Genre.Comedy,      1993, AgeRating.PG,   new Latent(0.02f,0.10f,0.85f,0.15f), F(Genre.Comedy,0.85f, Genre.Drama,0.40f),                      Col(120,90,50),  Col(245,220,170), "Baseball, s'mores, and The Beast."),
            new MovieData("COOL RUNNINGS",      Genre.Comedy,      1993, AgeRating.PG,   new Latent(0.02f,0.02f,0.90f,0.20f), F(Genre.Comedy,0.90f, Genre.Drama,0.40f),                      Col(20,90,60),   Col(250,215,70),  "Jamaica has a bobsled team. Feel the rhythm."),
            new MovieData("HONEY, I SHRUNK THE KIDS", Genre.Comedy, 1989, AgeRating.PG,  new Latent(0.45f,0.15f,0.80f,0.40f), F(Genre.Comedy,0.80f, Genre.SciFi,0.50f),                      Col(50,90,40),   Col(200,240,130), "The backyard is now a jungle.", "HONEY SHRUNK"),
            new MovieData("DENNIS THE MENACE",  Genre.Comedy,      1993, AgeRating.PG,   new Latent(0.02f,0.05f,0.88f,0.30f), F(Genre.Comedy,0.90f),                                          Col(150,60,40),  Col(250,225,160), "Mr. Wilson's worst nightmare is five years old.", "DENNIS"),
            new MovieData("WHILE YOU WERE SLEEPING", Genre.Romance, 1995, AgeRating.PG,  new Latent(0.02f,0.10f,0.95f,0.02f), F(Genre.Romance,0.90f, Genre.Comedy,0.50f),                    Col(45,55,90),   Col(235,205,215), "She saved his life. It's complicated.", "WHILE SLEEPING"),
            new MovieData("ROXANNE",            Genre.Romance,     1987, AgeRating.PG,   new Latent(0.02f,0.02f,0.90f,0.05f), F(Genre.Romance,0.85f, Genre.Comedy,0.70f),                    Col(140,70,60),  Col(245,210,180), "Big nose. Bigger heart."),
            new MovieData("SOMEWHERE IN TIME",  Genre.Romance,     1980, AgeRating.PG,   new Latent(0.30f,0.20f,0.60f,0.02f), F(Genre.Romance,0.90f, Genre.Drama,0.50f),                    Col(70,60,80),   Col(220,205,225), "He willed himself back to 1912 for her.", "SOMEWHERE"),
            new MovieData("FORREST GUMP",       Genre.Drama,       1994, AgeRating.PG13, new Latent(0.10f,0.30f,0.60f,0.30f), F(Genre.Drama,0.90f, Genre.Comedy,0.50f),                      Col(80,110,140), Col(240,240,230), "Life is like a box of chocolates."),
            new MovieData("A LEAGUE OF THEIR OWN", Genre.Drama,    1992, AgeRating.PG,   new Latent(0.02f,0.15f,0.70f,0.10f), F(Genre.Drama,0.80f, Genre.Comedy,0.60f),                      Col(140,40,50),  Col(245,225,190), "There's no crying in baseball.", "A LEAGUE"),
            new MovieData("THE SECRET GARDEN",  Genre.Drama,       1993, AgeRating.G,    new Latent(0.05f,0.50f,0.30f,0.02f), F(Genre.Drama,0.85f),                                          Col(35,70,45),   Col(190,230,170), "A locked door, a hidden key, a garden waking up.", "SECRET GARDEN"),
            new MovieData("SEARCHING FOR BOBBY FISCHER", Genre.Drama, 1993, AgeRating.PG, new Latent(0.05f,0.40f,0.30f,0.05f), F(Genre.Drama,0.90f),                                         Col(60,50,40),   Col(225,210,180), "A chess prodigy learns when not to move.", "BOBBY FISCHER"),
            new MovieData("THE ENDLESS SUMMER", Genre.Documentary, 1966, AgeRating.G,    new Latent(0.15f,0.02f,0.50f,0.15f), F(Genre.Documentary,0.90f),                                    Col(220,120,40), Col(255,220,120), "Two surfers chase summer around the world.", "ENDLESS SUMMER"),
            new MovieData("KOYAANISQATSI",      Genre.Documentary, 1982, AgeRating.G,    new Latent(0.60f,0.35f,0.02f,0.30f), F(Genre.Documentary,0.90f),                                    Col(50,45,60),   Col(200,140,90),  "Life out of balance, in time-lapse."),
            new MovieData("BLUE PLANET",        Genre.Documentary, 1990, AgeRating.G,    new Latent(0.75f,0.05f,0.10f,0.05f), F(Genre.Documentary,0.95f, Genre.SciFi,0.30f),                 Col(15,45,85),   Col(120,200,235), "Earth from orbit, narrated with awe."),
            new MovieData("BEAUTY AND THE BEAST", Genre.Animation, 1991, AgeRating.G,    new Latent(0.05f,0.35f,0.60f,0.15f), F(Genre.Animation,0.95f, Genre.Romance,0.60f),                 Col(55,45,90),   Col(240,210,120), "Tale as old as time.", "BEAUTY&BEAST"),
            new MovieData("THE LITTLE MERMAID", Genre.Animation,   1989, AgeRating.G,    new Latent(0.15f,0.15f,0.70f,0.15f), F(Genre.Animation,0.95f, Genre.Romance,0.50f),                 Col(20,70,90),   Col(130,220,200), "She traded her voice for legs.", "LIL MERMAID"),
            new MovieData("TOY STORY",          Genre.Animation,   1995, AgeRating.G,    new Latent(0.35f,0.05f,0.90f,0.30f), F(Genre.Animation,0.95f, Genre.Comedy,0.70f),                  Col(40,80,140),  Col(250,215,80),  "The toys are alive, and they have opinions."),
            new MovieData("AN AMERICAN TAIL",   Genre.Animation,   1986, AgeRating.G,    new Latent(0.05f,0.25f,0.55f,0.10f), F(Genre.Animation,0.90f, Genre.Drama,0.40f),                   Col(60,45,35),   Col(215,180,140), "A mouse emigrates. America has cats.", "AMERICAN TAIL"),
        };

        // ---- The mainframe's own stock -------------------------------------
        // Deliberately FICTIONAL tapes (per Erfan): students must not recognize the
        // titles, or they'd set the sliders from prior knowledge instead of letting
        // the optimizer discover the hidden features. Same latent space, same gap.
        public static readonly List<MovieData> MatrixMovieSet = new List<MovieData>
        {
            new MovieData("STAR DRIFTER",     Genre.SciFi,  1988, AgeRating.PG,   new Latent(1.00f,0.05f,0.10f,0.30f), F(Genre.SciFi,0.95f),  Col(40,60,120),  Col(120,180,230), "A lonely pilot drifts between dying stars."),
            new MovieData("ASTRO BLASTERS",   Genre.SciFi,  1991, AgeRating.PG13, new Latent(0.90f,0.05f,0.15f,0.90f), F(Genre.SciFi,0.90f),  Col(30,40,90),   Col(230,140,40),  "Laser marines vs. the moon cartel."),
            new MovieData("BOOM TOWN",        Genre.Action, 1989, AgeRating.PG13, new Latent(0.20f,0.10f,0.10f,1.00f), F(Genre.Action,0.95f), Col(90,30,20),   Col(240,180,40),  "One cop. One city. Many explosions."),
            new MovieData("QUASAR RUN",       Genre.SciFi,  1990, AgeRating.PG,   new Latent(0.95f,0.00f,0.20f,0.55f), F(Genre.SciFi,0.90f),  Col(20,30,70),   Col(90,200,220),  "Hyperspace courier outruns the void."),
            new MovieData("DEMOLITION DAVE",  Genre.Action, 1987, AgeRating.PG13, new Latent(0.10f,0.15f,0.20f,0.95f), F(Genre.Action,0.95f), Col(70,50,20),   Col(220,90,40),   "Dave does not read demolition permits."),
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
            new CustomerData("VICTOR", 28, Genre.Drama, Genre.Romance,   new Latent(0.02f,0.40f,0.95f,0.02f), F(Genre.Drama,0.70f, Genre.Romance,0.80f),                        Col(200,155,115), Col(50,60,100),   "Something aching. But, like, gently aching."),
            new CustomerData("THE NGUYEN KIDS", 8, Genre.Animation, Genre.Animation, new Latent(0.30f,0.02f,0.85f,0.40f), F(Genre.Animation,0.95f, Genre.Comedy,0.60f),         Col(215,175,135), Col(240,170,60),  "Cartoons! With a dog in them! Or TWO dogs!!"),
        };

        // ---- The matrix used by Level 4 (subset of customers x MatrixMovieSet) ----
        // Rows for the Mainframe grid. We include the genre regulars + the gap crowd so the
        // optimizer surfaces the Spook-Comedy cluster as uniformly low. Columns come from
        // MatrixMovieSet (fictional titles), keeping the grid a readable 5x5.
        public static readonly int[] MatrixCustomers = { 0, 1, 2, 3, 4 }; // Wendell, Dot, Hank, Priya, Tibbs

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
