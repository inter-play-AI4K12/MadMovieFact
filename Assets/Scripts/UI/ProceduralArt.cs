using System.Collections.Generic;
using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// Generated VHS poster art for catalog entries beyond the five atlas covers.
    /// Style follows the design sketch: a strong two-colour gradient with a chunky
    /// pixel motif per genre, framed like a rental box.
    /// </summary>
    public static class ProceduralPosters
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        const int W = 80, H = 120;

        public static Sprite Cover(int movieIndex)
        {
            string key = "gen_cover_" + movieIndex;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var m = GameData.Movies[Mathf.Clamp(movieIndex, 0, GameData.Movies.Count - 1)];
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };

            Color border = new Color(0.07f, 0.07f, 0.09f);
            Color silhouette = m.PosterA * 0.45f; silhouette.a = 1f;
            Color accent = Color.Lerp(m.PosterB, Color.white, 0.55f);

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (x < 3 || x >= W - 3 || y < 3 || y >= H - 3) { tex.SetPixel(x, y, border); continue; }
                    float t = (y - 3f) / (H - 6f);                      // 0 bottom .. 1 top
                    var c = Color.Lerp(m.PosterA, m.PosterB, t);
                    // faint horizontal banding for a printed look
                    if (y % 7 == 0) c *= 0.94f;
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }

            DrawMotif(tex, m.Primary, silhouette, accent);
            DrawVhsTag(tex, border);

            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Small icon chip for genres without an atlas icon (Drama+).</summary>
        public static Sprite GenreChip(Genre genre)
        {
            string key = "gen_chip_" + (int)genre;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            const int n = 28;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color bg = new Color(0.09f, 0.08f, 0.11f);
            Color frame = new Color(0.42f, 0.40f, 0.30f);
            Color tint = GenreInfo.Colors[(int)genre];

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, (x < 2 || x >= n - 2 || y < 2 || y >= n - 2) ? frame : bg);

            switch (genre)
            {
                case Genre.Drama:
                    // a tall window with light panes
                    FillRect(tex, 9, 6, 10, 16, tint * 0.55f);
                    FillRect(tex, 11, 8, 3, 5, tint); FillRect(tex, 15, 8, 2, 5, tint);
                    FillRect(tex, 11, 15, 3, 5, tint); FillRect(tex, 15, 15, 2, 5, tint);
                    break;
                case Genre.Romance:
                    DrawHeart(tex, 14, 13, 8, tint);
                    break;
                case Genre.Documentary:
                    DrawCircleOutline(tex, 14, 14, 9, tint);
                    for (int x = 6; x <= 22; x++) { tex.SetPixel(x, 14, tint); }
                    for (int y = 6; y <= 22; y++) if (InsideCircle(14, y, 14, 14, 9)) tex.SetPixel(14, y, tint * 0.8f);
                    break;
                default: // Animation
                    FillCircle(tex, 10, 11, 4, tint);
                    FillCircle(tex, 18, 16, 3, Color.Lerp(tint, Color.white, 0.4f));
                    FillCircle(tex, 20, 8, 2, tint * 0.8f);
                    break;
            }

            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        // ---- motifs --------------------------------------------------------
        static void DrawMotif(Texture2D t, Genre g, Color dark, Color light)
        {
            switch (g)
            {
                case Genre.SciFi:
                    Stars(t, 23); FillCircle(t, 24, 88, 9, dark);
                    for (int x = 12; x <= 37; x++) t.SetPixel(x, 84, light); // ring
                    break;
                case Genre.Action:
                    Burst(t, 40, 55, 20, light, dark);
                    break;
                case Genre.Horror:
                    // ground, tombstones, moon
                    FillRect(t, 3, 3, W - 6, 20, dark);
                    FillRect(t, 12, 22, 10, 13, dark); FillRect(t, 14, 35, 6, 2, dark);
                    FillRect(t, 42, 22, 12, 17, dark); FillRect(t, 45, 39, 6, 2, dark);
                    FillCircle(t, 60, 95, 8, light);
                    break;
                case Genre.Comedy:
                    FillCircle(t, 32, 70, 4, dark); FillCircle(t, 48, 70, 4, dark);
                    for (int x = 26; x <= 54; x++)
                    {
                        int y = 52 - Mathf.RoundToInt(Mathf.Sin((x - 26) / 28f * Mathf.PI) * 8f);
                        FillRect(t, x, y, 1, 3, dark);
                    }
                    break;
                case Genre.Drama:
                    FillRect(t, 26, 30, 28, 55, dark);
                    FillRect(t, 30, 62, 9, 18, light); FillRect(t, 42, 62, 8, 18, light);
                    FillRect(t, 30, 36, 9, 20, light); FillRect(t, 42, 36, 8, 20, light);
                    break;
                case Genre.Romance:
                    DrawHeart(t, 40, 58, 22, light);
                    DrawHeart(t, 40, 58, 16, dark);
                    break;
                case Genre.Documentary:
                    DrawCircleOutline(t, 40, 62, 22, light);
                    DrawCircleOutline(t, 40, 62, 21, light);
                    for (int x = 19; x <= 61; x++) if (InsideCircle(x, 62, 40, 62, 22)) t.SetPixel(x, 62, light);
                    for (int x = 22; x <= 58; x++)
                    {
                        if (InsideCircle(x, 72, 40, 62, 22)) t.SetPixel(x, 72, light * 0.9f);
                        if (InsideCircle(x, 52, 40, 62, 22)) t.SetPixel(x, 52, light * 0.9f);
                    }
                    break;
                default: // Animation
                    FillCircle(t, 24, 50, 9, light);
                    FillCircle(t, 46, 68, 7, dark);
                    FillCircle(t, 58, 44, 5, Color.Lerp(light, dark, 0.5f));
                    FillRect(t, 14, 38, 8, 2, dark); FillRect(t, 36, 58, 7, 2, dark);
                    break;
            }
        }

        static void Stars(Texture2D t, int count)
        {
            var rng = new System.Random(t.GetHashCode() & 0xffff | count);
            for (int i = 0; i < count; i++)
            {
                int x = 6 + rng.Next(W - 12), y = 40 + rng.Next(H - 48);
                var c = new Color(0.95f, 0.95f, 0.9f, 1f);
                t.SetPixel(x, y, c);
                if (rng.Next(3) == 0) { t.SetPixel(x + 1, y, c * 0.7f); t.SetPixel(x, y + 1, c * 0.7f); }
            }
        }

        static void Burst(Texture2D t, int cx, int cy, int r, Color light, Color dark)
        {
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.PI * 2f / 16f;
                int len = (a % 2 == 0) ? r : r / 2;
                for (int d = 2; d < len; d++)
                {
                    int x = cx + Mathf.RoundToInt(Mathf.Cos(ang) * d);
                    int y = cy + Mathf.RoundToInt(Mathf.Sin(ang) * d);
                    if (x > 3 && x < W - 4 && y > 3 && y < H - 4) t.SetPixel(x, y, d < len / 2 ? light : dark);
                }
            }
            FillCircle(t, cx, cy, 4, light);
        }

        static void DrawHeart(Texture2D t, int cx, int cy, int size, Color c)
        {
            // classic implicit heart, scanline filled
            for (int y = -size; y <= size; y++)
                for (int x = -size; x <= size; x++)
                {
                    float fx = x / (size * 0.9f), fy = y / (size * 0.9f) + 0.25f;
                    float a = fx * fx + fy * fy - 0.35f;
                    if (a * a * a - fx * fx * fy * fy * fy < 0f)
                    {
                        int px = cx + x, py = cy + y;
                        if (px > 3 && px < W - 4 && py > 3 && py < H - 4) t.SetPixel(px, py, c);
                        else if (px >= 0 && px < t.width && py >= 0 && py < t.height && t.width < 40) t.SetPixel(px, py, c);
                    }
                }
        }

        static bool InsideCircle(int x, int y, int cx, int cy, int r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

        static void FillCircle(Texture2D t, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (x >= 0 && x < t.width && y >= 0 && y < t.height && InsideCircle(x, y, cx, cy, r))
                        t.SetPixel(x, y, c);
        }

        static void DrawCircleOutline(Texture2D t, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (x >= 0 && x < t.width && y >= 0 && y < t.height &&
                        InsideCircle(x, y, cx, cy, r) && !InsideCircle(x, y, cx, cy, r - 2))
                        t.SetPixel(x, y, c);
        }

        static void FillRect(Texture2D t, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 0 && x < t.width && y >= 0 && y < t.height) t.SetPixel(x, y, c);
        }

        static void DrawVhsTag(Texture2D t, Color border)
        {
            FillRect(t, 6, 6, 16, 9, new Color(0.92f, 0.92f, 0.88f));
            FillRect(t, 8, 8, 3, 5, border); FillRect(t, 12, 8, 3, 5, border); FillRect(t, 16, 8, 3, 5, border);
        }
    }

    /// <summary>
    /// Generated portrait cards for characters that have no slot in the CharacterCards
    /// atlas: newer customers plus one-off narrative figures (the data broker, the
    /// indie filmmaker, Timmy's mother). Same card aspect as the atlas portraits.
    /// </summary>
    public static class ProceduralPortraits
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        const int W = 72, H = 100;

        enum Hair { Short, Long, Curly, Balding, Slick, Cap, Beret, Bun }
        enum Extra { None, Glasses, Sunglasses, Earrings }
        enum Mouth { Neutral, Smile, Grin, Stern }

        struct Look
        {
            public Color Skin, HairC, Shirt;
            public Hair Hair;
            public Extra Extra;
            public Mouth Mouth;
            public bool Suit, Kid;
        }

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        static Look For(string name)
        {
            switch (name)
            {
                case "TIMMY": return new Look { Skin = C(232, 190, 158), HairC = C(105, 70, 40), Shirt = C(190, 55, 45), Hair = Hair.Short, Mouth = Mouth.Smile, Kid = true };
                case "ROSA": return new Look { Skin = C(206, 155, 118), HairC = C(45, 30, 28), Shirt = C(212, 105, 140), Hair = Hair.Long, Extra = Extra.Earrings, Mouth = Mouth.Smile };
                case "EARL": return new Look { Skin = C(226, 192, 165), HairC = C(170, 170, 168), Shirt = C(110, 112, 82), Hair = Hair.Balding, Extra = Extra.Glasses, Mouth = Mouth.Neutral };
                case "BABS": return new Look { Skin = C(228, 186, 156), HairC = C(140, 70, 45), Shirt = C(105, 75, 130), Hair = Hair.Curly, Extra = Extra.Earrings, Mouth = Mouth.Neutral };
                case "VICTOR": return new Look { Skin = C(200, 155, 116), HairC = C(28, 26, 26), Shirt = C(52, 62, 100), Hair = Hair.Slick, Mouth = Mouth.Neutral };
                case "THE NGUYEN KIDS": return new Look { Skin = C(214, 172, 132), HairC = C(30, 28, 26), Shirt = C(235, 165, 60), Hair = Hair.Cap, Mouth = Mouth.Grin, Kid = true };
                case "TIMMY'S MOM": return new Look { Skin = C(230, 188, 158), HairC = C(95, 62, 38), Shirt = C(50, 120, 118), Hair = Hair.Bun, Mouth = Mouth.Stern };
                case "GIBBS": return new Look { Skin = C(222, 186, 160), HairC = C(22, 20, 20), Shirt = C(40, 38, 42), Hair = Hair.Slick, Extra = Extra.Sunglasses, Mouth = Mouth.Grin, Suit = true };
                case "INDIE IRIS": return new Look { Skin = C(198, 150, 112), HairC = C(35, 30, 32), Shirt = C(128, 45, 55), Hair = Hair.Beret, Mouth = Mouth.Neutral };
                default:
                    int h = 17;
                    foreach (char ch in name) h = h * 31 + ch;
                    var skins = new[] { C(232, 190, 158), C(206, 155, 118), C(190, 140, 100), C(226, 192, 165) };
                    var hairs = new[] { C(45, 30, 28), C(105, 70, 40), C(170, 170, 168), C(140, 70, 45) };
                    var shirts = new[] { C(70, 110, 160), C(160, 60, 50), C(60, 90, 80), C(110, 70, 130), C(235, 165, 60) };
                    return new Look
                    {
                        Skin = skins[Mathf.Abs(h) % skins.Length],
                        HairC = hairs[Mathf.Abs(h / 7) % hairs.Length],
                        Shirt = shirts[Mathf.Abs(h / 13) % shirts.Length],
                        Hair = (Hair)(Mathf.Abs(h / 3) % 5),
                        Mouth = Mouth.Neutral
                    };
            }
        }

        public static Sprite Card(string name)
        {
            // a generated portrait dropped into Resources/Portraits/<slug>.png wins
            var user = ArtSprites.UserArt("Portraits", name);
            if (user != null) return user;

            if (Cache.TryGetValue(name, out var cached)) return cached;

            var look = For(name);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };

            Color frame = C(139, 133, 96);          // khaki card frame (matches atlas cards)
            Color bgA = C(32, 24, 22), bgB = C(24, 18, 17);
            Color ink = C(18, 16, 15);

            // frame + striped backdrop
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (x < 3 || x >= W - 3 || y < 3 || y >= H - 3) { tex.SetPixel(x, y, frame); continue; }
                    tex.SetPixel(x, y, (y % 6 < 2) ? bgB : bgA);
                }

            int cx = W / 2;
            int headY = look.Kid ? 58 : 62;         // head centre
            int rx = look.Kid ? 12 : 14, ry = look.Kid ? 14 : 17;

            // body / shirt
            int shoulderY = headY - ry - 2;
            for (int y = 4; y <= shoulderY + 6; y++)
            {
                int half = Mathf.Min(26, 10 + (shoulderY + 6 - y));
                for (int x = cx - half; x <= cx + half; x++)
                    if (x > 3 && x < W - 4) tex.SetPixel(x, y, look.Shirt);
            }
            if (look.Suit)
            {
                // white shirt V + tie
                for (int y = 4; y <= shoulderY + 4; y++)
                {
                    int vw = Mathf.Max(1, (shoulderY + 4 - y) / 3);
                    for (int x = cx - vw; x <= cx + vw; x++) tex.SetPixel(x, y, C(235, 232, 222));
                }
                for (int y = 6; y <= shoulderY; y++) { tex.SetPixel(cx, y, C(200, 160, 40)); tex.SetPixel(cx + 1, y, C(200, 160, 40)); }
            }
            // neck
            for (int y = shoulderY; y <= shoulderY + 6; y++)
                for (int x = cx - 3; x <= cx + 3; x++) tex.SetPixel(x, y, look.Skin);

            // head (ellipse)
            for (int y = headY - ry; y <= headY + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float dx = (x - cx) / (float)rx, dy = (y - headY) / (float)ry;
                    if (dx * dx + dy * dy <= 1f) tex.SetPixel(x, y, look.Skin);
                }

            // hair
            switch (look.Hair)
            {
                case Hair.Short:
                    Cap(tex, cx, headY, rx, ry, look.HairC, 0.35f);
                    tex.SetPixel(cx - 6, headY + ry, look.HairC); tex.SetPixel(cx + 4, headY + ry + 1, look.HairC);
                    break;
                case Hair.Long:
                    Cap(tex, cx, headY, rx, ry, look.HairC, 0.30f);
                    FillRect(tex, cx - rx - 3, headY - ry - 2, 4, ry + 14, look.HairC);
                    FillRect(tex, cx + rx - 1, headY - ry - 2, 4, ry + 14, look.HairC);
                    break;
                case Hair.Curly:
                    for (int i = -3; i <= 3; i++)
                        FillCircle(tex, cx + i * 4, headY + ry - 2 + ((i % 2 == 0) ? 3 : 5), 4, look.HairC);
                    break;
                case Hair.Balding:
                    FillRect(tex, cx - rx - 1, headY - 2, 3, 8, look.HairC);
                    FillRect(tex, cx + rx - 1, headY - 2, 3, 8, look.HairC);
                    break;
                case Hair.Slick:
                    Cap(tex, cx, headY, rx, ry, look.HairC, 0.22f);
                    for (int x = cx - rx + 2; x <= cx + rx - 2; x += 3) tex.SetPixel(x, headY + ry - 1, Color.Lerp(look.HairC, Color.white, 0.35f));
                    break;
                case Hair.Cap:
                    Cap(tex, cx, headY, rx, ry, look.HairC, 0.30f);
                    FillRect(tex, cx - rx - 2, headY + ry - 6, rx + 4, 4, C(60, 90, 160));
                    FillRect(tex, cx - rx - 2, headY + ry - 2, rx * 2 + 4, 5, C(60, 90, 160));
                    break;
                case Hair.Beret:
                    Cap(tex, cx, headY, rx, ry, look.HairC, 0.28f);
                    FillRect(tex, cx - rx, headY + ry - 3, rx * 2 + 4, 6, C(90, 30, 40));
                    tex.SetPixel(cx + 2, headY + ry + 4, C(90, 30, 40));
                    break;
                case Hair.Bun:
                    Cap(tex, cx, headY, rx, ry, look.HairC, 0.30f);
                    FillCircle(tex, cx, headY + ry + 3, 5, look.HairC);
                    break;
            }

            // eyes / accessories
            int eyeY = headY + 2;
            int exL = cx - 5, exR = cx + 4;
            if (look.Extra == Extra.Sunglasses)
            {
                FillRect(tex, exL - 3, eyeY - 1, 7, 4, ink);
                FillRect(tex, exR - 1, eyeY - 1, 7, 4, ink);
                FillRect(tex, exL + 4, eyeY + 1, 4, 1, ink);
            }
            else
            {
                FillRect(tex, exL, eyeY, 2, 2, ink);
                FillRect(tex, exR, eyeY, 2, 2, ink);
                if (look.Extra == Extra.Glasses)
                {
                    RectOutline(tex, exL - 3, eyeY - 2, 7, 6, C(80, 78, 70));
                    RectOutline(tex, exR - 2, eyeY - 2, 7, 6, C(80, 78, 70));
                    tex.SetPixel(cx, eyeY + 1, C(80, 78, 70));
                }
            }
            if (look.Extra == Extra.Earrings)
            {
                tex.SetPixel(cx - rx, headY - 2, C(220, 190, 90));
                tex.SetPixel(cx + rx, headY - 2, C(220, 190, 90));
            }

            // mouth
            int my = headY - ry / 2 - 1;
            switch (look.Mouth)
            {
                case Mouth.Smile:
                    FillRect(tex, cx - 3, my, 7, 1, ink);
                    tex.SetPixel(cx - 4, my + 1, ink); tex.SetPixel(cx + 4, my + 1, ink);
                    break;
                case Mouth.Grin:
                    FillRect(tex, cx - 4, my, 9, 3, C(240, 238, 230));
                    RectOutline(tex, cx - 5, my - 1, 11, 5, ink);
                    break;
                case Mouth.Stern:
                    FillRect(tex, cx - 4, my, 9, 1, ink);
                    break;
                default:
                    FillRect(tex, cx - 2, my, 5, 1, ink);
                    break;
            }

            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "gen_portrait_" + name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[name] = sprite;
            return sprite;
        }

        /// <summary>Hair cap: covers the top fraction of the head ellipse.</summary>
        static void Cap(Texture2D t, int cx, int cy, int rx, int ry, Color c, float fraction)
        {
            int from = cy + Mathf.RoundToInt(ry * (1f - fraction * 2f));
            for (int y = from; y <= cy + ry + 1; y++)
                for (int x = cx - rx - 1; x <= cx + rx + 1; x++)
                {
                    float dx = (x - cx) / (rx + 1f), dy = (y - cy) / (ry + 1f);
                    if (dx * dx + dy * dy <= 1f) t.SetPixel(x, y, c);
                }
        }

        static void FillCircle(Texture2D t, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (x >= 3 && x < t.width - 3 && y >= 3 && y < t.height - 3 &&
                        (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                        t.SetPixel(x, y, c);
        }

        static void FillRect(Texture2D t, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 3 && x < t.width - 3 && y >= 3 && y < t.height - 3) t.SetPixel(x, y, c);
        }

        static void RectOutline(Texture2D t, int x0, int y0, int w, int h, Color c)
        {
            for (int x = x0; x < x0 + w; x++) { Px(t, x, y0, c); Px(t, x, y0 + h - 1, c); }
            for (int y = y0; y < y0 + h; y++) { Px(t, x0, y, c); Px(t, x0 + w - 1, y, c); }
        }

        static void Px(Texture2D t, int x, int y, Color c)
        {
            if (x >= 3 && x < t.width - 3 && y >= 3 && y < t.height - 3) t.SetPixel(x, y, c);
        }
    }
}
