using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// "Corporate Lo-Fi" art direction: 1990s bureaucratic tech, muted fluorescent
    /// lighting, heavy plastics, manila folders, chunky Windows-95 bevels and a glowing
    /// CRT for the Mainframe. Core UI visuals are generated procedurally
    /// so the game is fully self-contained.
    /// </summary>
    public static class Theme
    {
        // ---- Palette -------------------------------------------------------
        public static readonly Color Wall      = C(0xC8, 0xC9, 0xB8); // fluorescent greige
        public static readonly Color WallDark  = C(0xB0, 0xB2, 0xA0);
        public static readonly Color Lino      = C(0x4F, 0x5C, 0x57); // institutional floor
        public static readonly Color Counter   = C(0x6E, 0x5A, 0x42); // wood-laminate counter

        public static readonly Color Face      = C(0xC2, 0xC2, 0xB2); // win95 panel face
        public static readonly Color FaceLight = C(0xF2, 0xF2, 0xE6);
        public static readonly Color FaceDark  = C(0x6C, 0x6C, 0x60);
        public static readonly Color FaceShade = C(0x9A, 0x9A, 0x8C);

        public static readonly Color Manila    = C(0xDB, 0xBE, 0x7E); // folder body
        public static readonly Color ManilaTab = C(0xC9, 0xA8, 0x60);
        public static readonly Color ManilaEdge= C(0xA8, 0x8A, 0x4E);

        public static readonly Color Ink       = C(0x26, 0x26, 0x1E); // typed text
        public static readonly Color InkSoft   = C(0x52, 0x50, 0x44);

        public static readonly Color CommsGray  = C(0xB4, 0xB4, 0xAC);
        public static readonly Color TitleBar   = C(0x20, 0x20, 0x6E); // win95 navy title bar
        public static readonly Color TitleText  = C(0xF4, 0xF4, 0xF0);

        public static readonly Color Plastic    = C(0xD7, 0xCE, 0xAC); // beige robot plastic
        public static readonly Color PlasticDk  = C(0xA6, 0x9E, 0x7C);

        // CRT phosphor
        public static readonly Color CrtBg      = C(0x06, 0x12, 0x08);
        public static readonly Color CrtBgSoft  = C(0x0C, 0x20, 0x10);
        public static readonly Color CrtGreen   = C(0x55, 0xF0, 0x6A);
        public static readonly Color CrtGreenDim= C(0x2A, 0x86, 0x36);
        public static readonly Color CrtAmber   = C(0xF2, 0xB0, 0x3A);

        // Feedback
        public static readonly Color ErrorRed   = C(0xD7, 0x26, 0x3D);
        public static readonly Color ErrorRedDk = C(0x7A, 0x12, 0x20);
        public static readonly Color Cash        = C(0x47, 0xA8, 0x52);
        public static readonly Color Coin        = C(0xC9, 0xB0, 0x54);

        static Color C(int r, int g, int b, int a = 255) => new Color(r / 255f, g / 255f, b / 255f, a / 255f);

        // ---- Sprites -------------------------------------------------------
        static Sprite _solid;
        /// <summary>A 1-colour white sprite; tint via Image.color.</summary>
        public static Sprite Solid
        {
            get
            {
                if (_solid == null)
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    var px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    tex.SetPixels32(px); tex.Apply();
                    tex.hideFlags = HideFlags.HideAndDontSave;
                    _solid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4f);
                    _solid.hideFlags = HideFlags.HideAndDontSave;
                }
                return _solid;
            }
        }

        static Sprite _raised, _sunken, _crtRaised;
        /// <summary>Chunky raised Windows-95 bevel (9-sliced).</summary>
        public static Sprite Raised => _raised != null ? _raised : (_raised = Bevel(Face, FaceLight, FaceDark, false));
        /// <summary>Pressed / inset bevel (9-sliced).</summary>
        public static Sprite Sunken => _sunken != null ? _sunken : (_sunken = Bevel(Face, FaceDark, FaceLight, true));
        /// <summary>Dark inset bevel used for CRT panels.</summary>
        public static Sprite CrtInset => _crtRaised != null ? _crtRaised : (_crtRaised = Bevel(CrtBg, CrtGreenDim, Color.black, true));

        /// <summary>Build a 9-sliced bevel sprite. size=16, border=4 px.</summary>
        public static Sprite Bevel(Color face, Color topLeft, Color botRight, bool sunken)
        {
            const int size = 16, b = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color hi = topLeft, lo = botRight;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color col = face;
                    bool left = x < b, right = x >= size - b;
                    bool bottom = y < b, top = y >= size - b;
                    // Unity texture y=0 is the bottom row.
                    if (top || left) col = hi;
                    if (bottom || right) col = lo;
                    // corner priority: outer-most highlight wins on top-left, shade on bottom-right
                    if (top && right) col = Color.Lerp(hi, lo, 0.5f);
                    if (bottom && left) col = Color.Lerp(hi, lo, 0.5f);
                    if (top && left) col = hi;
                    if (bottom && right) col = lo;
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        // ---- CRT overlays --------------------------------------------------
        static Sprite _scan;
        /// <summary>Horizontal scanline strip, tile vertically (Image type = Tiled).</summary>
        public static Sprite Scanlines
        {
            get
            {
                if (_scan == null)
                {
                    int h = 3;
                    var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < 2; x++)
                            tex.SetPixel(x, y, y == 0 ? new Color(0, 0, 0, 0.30f) : new Color(0, 0, 0, 0f));
                    tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
                    _scan = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(.5f, .5f), 2f);
                    _scan.hideFlags = HideFlags.HideAndDontSave;
                }
                return _scan;
            }
        }

        static Sprite _vignette;
        /// <summary>Radial darkening for CRT curvature feel; stretch full-screen.</summary>
        public static Sprite Vignette
        {
            get
            {
                if (_vignette == null)
                {
                    int n = 64;
                    var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    Vector2 c = new Vector2(n / 2f, n / 2f);
                    float maxD = c.magnitude;
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x, y), c) / maxD; // 0..1
                            float a = Mathf.Clamp01(Mathf.Pow(d, 2.4f)) * 0.85f;
                            tex.SetPixel(x, y, new Color(0, 0, 0, a));
                        }
                    tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
                    _vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
                    _vignette.hideFlags = HideFlags.HideAndDontSave;
                }
                return _vignette;
            }
        }

        static Sprite _glow;
        /// <summary>Soft radial glow (white centre, transparent edges) for highlights.</summary>
        public static Sprite Glow
        {
            get
            {
                if (_glow == null)
                {
                    int n = 64;
                    var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    Vector2 c = new Vector2(n / 2f, n / 2f);
                    float maxD = c.magnitude;
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x, y), c) / maxD;
                            float a = Mathf.Clamp01(1f - d);
                            a = a * a;
                            tex.SetPixel(x, y, new Color(1, 1, 1, a));
                        }
                    tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
                    _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
                    _glow.hideFlags = HideFlags.HideAndDontSave;
                }
                return _glow;
            }
        }

        static Sprite _disc;
        /// <summary>Filled circle sprite (for sticker dots, customer heads, etc.).</summary>
        public static Sprite Disc
        {
            get
            {
                if (_disc == null)
                {
                    int n = 48;
                    var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                    Vector2 c = new Vector2(n / 2f - .5f, n / 2f - .5f);
                    float r = n / 2f - 1f;
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x, y), c);
                            float a = Mathf.Clamp01(r - d);
                            tex.SetPixel(x, y, new Color(1, 1, 1, a));
                        }
                    tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
                    _disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
                    _disc.hideFlags = HideFlags.HideAndDontSave;
                }
                return _disc;
            }
        }

        // ---- Fonts ---------------------------------------------------------
        static Font _typewriter, _system, _fallback;

        public static Font Fallback =>
            _fallback != null ? _fallback : (_fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Uneven mechanical typewriter look (Old Dude / typed files).</summary>
        public static Font Typewriter =>
            _typewriter != null ? _typewriter : (_typewriter = LoadOS(new[] { "Courier New", "Consolas", "Lucida Console" }));

        /// <summary>Rigid aliased system font (Robot / OS chrome).</summary>
        public static Font SystemSans =>
            _system != null ? _system : (_system = LoadOS(new[] { "Microsoft Sans Serif", "Tahoma", "Segoe UI", "Arial" }));

        static Font LoadOS(string[] names)
        {
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(names, 16);
                if (f != null) return f;
            }
            catch { /* headless / font unavailable */ }
            return Fallback;
        }
    }
}
