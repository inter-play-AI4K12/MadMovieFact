using System.Collections.Generic;
using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// Runtime slices for the supplied pixel-art source atlases. Crop coordinates use a
    /// top-left authoring origin and are converted to Unity's bottom-left texture origin.
    /// </summary>
    public static class ArtSprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static readonly Dictionary<string, int> CustomerIndices = new Dictionary<string, int>
        {
            { "MOVIE-FAN", 0 }, { "MR. PELLINGS", 1 }, { "UNIT B-EIGE", 2 },
            { "WENDELL", 3 }, { "DOT", 4 }, { "HANK", 5 }, { "PRIYA", 6 },
            { "THE TIBBS TWINS", 7 }, { "MORTICIA", 8 }, { "GIGGLES", 9 }
        };

        public static Sprite CustomerPortrait(string name)
        {
            if (!CustomerIndices.TryGetValue(name, out int index))
                return ProceduralPortraits.Card(name);
            int col = index % 5;
            int row = index / 5;
            return Crop("CharacterCards", "customer_" + index,
                40 + col * 280, 122 + row * 476, 250, 346);
        }

        public static Sprite MovieFanAvatar() => CustomerPortrait("MOVIE-FAN");

        public static Sprite MovieFanFullBody()
        {
            const string key = "customer_movie_fan_full";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("Characters/Customers/MovieFan");
            if (texture == null) return Theme.Solid;
            texture.filterMode = FilterMode.Point;
            var rect = new Rect(texture.width * 0.3365f, texture.height * 0.1268f,
                texture.width * 0.3238f, texture.height * 0.7887f);
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite QueueBack(int index)
        {
            // Eight persistent single-sprite assets are shared cyclically by long queues.
            index = Mathf.Abs(index) % 8;
            var sprite = Resources.Load<Sprite>("Characters/Queue/CustomerBack_" + index);
            return sprite != null ? sprite : Theme.Solid;
        }

        public static Sprite StorefrontBackground()
        {
            const string key = "background_storefront";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("Backgrounds/Storefront");
            if (texture == null) return Theme.Solid;
            texture.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>"APOLLO: THE UNTOLD HOURS" -> "apollo_the_untold_hours" (asset file names).</summary>
        public static string Slug(string name)
        {
            var sb = new System.Text.StringBuilder();
            bool lastUnderscore = true;
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) { sb.Append(c); lastUnderscore = false; }
                else if (!lastUnderscore) { sb.Append('_'); lastUnderscore = true; }
            }
            return sb.ToString().TrimEnd('_');
        }

        /// <summary>Loads a hand-generated image dropped into Resources, or null.</summary>
        public static Sprite UserArt(string folder, string name)
        {
            string key = "user_" + folder + "_" + Slug(name);
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(folder + "/" + Slug(name));
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite MovieCover(int index)
        {
            // Priority: a generated poster dropped into Resources/Posters/<slug>.png,
            // then the atlas art for the five original tapes, then the procedural
            // gradient poster in the style of the design sketch.
            if (index < 0) index = 0;
            var user = UserArt("Posters", GameData.Movies[Mathf.Min(index, GameData.Movies.Count - 1)].Title);
            if (user != null) return user;
            if (index <= 4) return Crop("MovieCatalog", "cover_" + index, 30 + index * 281, 92, 258, 500);
            return ProceduralPosters.Cover(index);
        }

        public static Sprite MovieSpine(int index)
        {
            index = Mathf.Clamp(index, 0, 4);
            return Crop("MovieCatalog", "spine_" + index, 30 + index * 281, 625, 258, 78);
        }

        public static Sprite GenreIcon(Genre genre)
        {
            // Atlas icons cover the four original genres; the rest are generated chips.
            int g = (int)genre;
            if (g <= 3) return CatalogIcon(g);
            return ProceduralPosters.GenreChip(genre);
        }

        public static Sprite VibeIcon(int vibe) => CatalogIcon(4 + Mathf.Clamp(vibe, 0, 3));

        /// <summary>Full-stage backdrop for a store era (2 = computerized store, 3 = startup, 4 = corporate HQ).</summary>
        public static Sprite LevelBackground(int level)
        {
            string key = "background_level_" + level;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("Backgrounds/MadFact_Level" + level + "_Background");
            if (texture == null) return Theme.Solid;
            // The source renders carry baked-in white margins on the sides; crop them off
            // so the stage fills edge to edge.
            float insetX = texture.width * 0.045f;
            float insetY = texture.height * 0.012f;
            var rect = new Rect(insetX, insetY, texture.width - insetX * 2f, texture.height - insetY * 2f);
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        static Sprite CatalogIcon(int index) =>
            Crop("MovieCatalog", "catalog_icon_" + index, 42 + index * 171, 782, 140, 170);

        public static Sprite Sticker(int index)
        {
            index = Mathf.Clamp(index, 0, 11);
            int col = index % 4;
            int row = index / 4;
            return Crop("Stickers", "sticker_" + index, 82 + col * 326, 66 + row * 330, 306, 304);
        }

        public static Sprite CinemaClapper() => Crop("CinemaUI", "clapper", 774, 66, 132, 116);
        public static Sprite FilmReel() => Crop("CinemaUI", "film_reel", 591, 58, 142, 122);
        public static Sprite ButtonChrome() => CropSliced("CinemaUI", "button_chrome", 1024, 603, 366, 82, new Vector4(28, 22, 28, 22));
        public static Sprite DialogWindow() => CropSliced("CinemaUI", "dialog_window", 57, 708, 405, 212, new Vector4(16, 16, 16, 42));
        public static Sprite PanelChrome() => CropSliced("CinemaUI", "panel_chrome", 1048, 708, 158, 113, new Vector4(15, 15, 15, 15));

        public static Sprite StoreLogo() => Ui("store", 40, 66, 120, 100);
        public static Sprite CashRegister() => Ui("cash", 196, 66, 120, 100);
        public static Sprite Goal() => Ui("goal", 337, 64, 124, 100);
        public static Sprite Close() => Ui("close", 505, 70, 110, 96);
        public static Sprite Back() => Ui("back", 650, 72, 120, 94);
        public static Sprite Next() => Ui("next", 817, 72, 120, 94);
        public static Sprite Play() => Ui("play", 1136, 72, 116, 94);
        public static Sprite Stop() => Ui("stop", 1284, 68, 120, 100);
        public static Sprite Add() => Ui("add", 38, 240, 120, 112);
        public static Sprite Remove() => Ui("remove", 174, 240, 116, 112);
        public static Sprite Clear() => Ui("clear", 307, 240, 112, 112);
        public static Sprite Reset() => Ui("reset", 443, 236, 120, 112);
        public static Sprite Optimize() => Ui("optimize", 585, 235, 120, 113);
        public static Sprite Confirm() => Ui("confirm", 730, 237, 112, 112);
        public static Sprite CustomerPerson() => Ui("customer", 447, 430, 112, 108);
        public static Sprite MovieTape() => Ui("movie_tape", 596, 431, 126, 106);
        public static Sprite Robot() => Ui("robot", 759, 422, 122, 116);
        public static Sprite Mainframe() => Ui("mainframe", 895, 426, 126, 114);
        public static Sprite Corkboard() => Ui("corkboard", 1036, 426, 126, 114);

        static Sprite Ui(string key, float x, float top, float width, float height) =>
            Crop("Interface", "ui_" + key, x, top, width, height);

        static Sprite Crop(string atlas, string key, float x, float top, float width, float height)
            => Crop(atlas, key, x, top, width, height, Vector4.zero);

        static Sprite CropSliced(string atlas, string key, float x, float top, float width, float height, Vector4 border)
            => Crop(atlas, key, x, top, width, height, border);

        static Sprite Crop(string atlas, string key, float x, float top, float width, float height, Vector4 border)
        {
            if (Cache.TryGetValue(key, out var sprite)) return sprite;

            var texture = Resources.Load<Texture2D>("Atlases/" + atlas);
            if (texture == null) return Theme.Solid;
            texture.filterMode = FilterMode.Point;

            var rect = new Rect(x, texture.height - top - height, width, height);
            sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
