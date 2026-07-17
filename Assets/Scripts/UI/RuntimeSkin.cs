using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Restores transient runtime-created sprites and OS fonts on authored prefabs.
    /// Layout, hierarchy, colors and anchors live in the prefab; this class only
    /// reconnects art that Unity cannot serialize because it is sliced at runtime.
    /// </summary>
    public static class RuntimeSkin
    {
        public static void Apply(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                // Authored prefabs cannot serialize the OS fonts used by the final skin.
                // The editor preview supplies Unity's built-in font, which is replaced
                // here when gameplay begins.
                if (text.font == null || (Application.isPlaying && text.font == Theme.Fallback))
                    text.font = text.GetComponentInParent<Button>() != null ? Theme.SystemSans : Theme.Typewriter;
            }

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite != null) continue;
                if (image.GetComponent<Button>() != null)
                {
                    image.sprite = image.GetComponent<CommsBox>() != null ? ArtSprites.PanelChrome() : ArtSprites.ButtonChrome();
                    image.type = Image.Type.Sliced;
                }
                else if (image.type == Image.Type.Sliced)
                    image.sprite = Theme.Raised;
                else
                    image.sprite = Theme.Solid;
            }

            Set(root, "StoreInterior", ArtSprites.StorefrontBackground());
            Set(root, "StorefrontGhost", ArtSprites.StorefrontBackground());
            SetSliced(root, "MainMenuPanel", ArtSprites.DialogWindow());
            Set(root, "PlayerAvatar", ArtSprites.MovieFanAvatar());
            Set(root, "StoreLogo", ArtSprites.StoreLogo());
            Set(root, "CashIcon", ArtSprites.CashRegister());
            Set(root, "GoalIcon", ArtSprites.Goal());
            Set(root, "Reel", ArtSprites.FilmReel());
            Set(root, "Scanlines", Theme.Scanlines);
            Set(root, "Vignette", Theme.Vignette);

            foreach (var glow in NamedImages(root, "Glow")) glow.sprite = Theme.Glow;
            Set(root, "P", ArtSprites.CustomerPortrait("WENDELL"));

            SkinButtons(root);
            SkinCatalog(root);
        }

        static void SkinButtons(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                string name = button.gameObject.name;
                if (name.StartsWith("M") && int.TryParse(name.Substring(1), out int posterIndex))
                {
                    // Level 1 deliberately uses a taller Poster child instead of the
                    // standard square Icon. Runtime-created crop sprites cannot be
                    // serialized into the prefab, so reconnect the cover explicitly.
                    var poster = UIFactory.FindDeep<Image>(button.transform, "Poster");
                    if (poster != null) poster.sprite = ArtSprites.MovieCover(posterIndex);
                }

                var icon = UIFactory.FindDeep<Image>(button.transform, "Icon");
                if (icon == null) continue;
                if (name == "Leave") icon.sprite = ArtSprites.Close();
                else if (name == "Menu_0") icon.sprite = ArtSprites.Play();
                else if (name == "Menu_1") icon.sprite = ArtSprites.StoreLogo();
                else if (name == "Menu_2") icon.sprite = ArtSprites.CustomerPortrait("WENDELL");
                else if (name == "Menu_3") icon.sprite = ArtSprites.Robot();
                else if (name == "Menu_4") icon.sprite = ArtSprites.MovieCover(0);
                else if (name == "Menu_5") icon.sprite = ArtSprites.Optimize();
                else if (name == "Menu_6") icon.sprite = ArtSprites.Goal();
                else if (name == "Quit") icon.sprite = ArtSprites.Stop();
                else if (name == "Enter" || name == "Run") icon.sprite = ArtSprites.Play();
                else if (name == "Next" || name == "Continue") icon.sprite = ArtSprites.Next();
                else if (name == "Add") icon.sprite = ArtSprites.Add();
                else if (name == "Clr") icon.sprite = ArtSprites.Clear();
                else if (name == "Reset") icon.sprite = ArtSprites.Reset();
                else if (name == "Optimize") icon.sprite = ArtSprites.Optimize();
                else if (name == "Green") icon.sprite = ArtSprites.Confirm();
                else if (name.StartsWith("M") && int.TryParse(name.Substring(1), out int movie)) icon.sprite = ArtSprites.MovieCover(movie);
                else if (name.StartsWith("Q") && int.TryParse(name.Substring(1), out int question)) icon.sprite = ArtSprites.VibeIcon(question);
                else if (name.StartsWith("S") && int.TryParse(name.Substring(1), out int sticker)) icon.sprite = ArtSprites.Sticker(sticker);
                else if (name.StartsWith("Col") && int.TryParse(name.Substring(3), out int col)) icon.sprite = ArtSprites.MatrixCover(col);
                else if (name.StartsWith("Row") && int.TryParse(name.Substring(3), out int row))
                    icon.sprite = ArtSprites.CustomerPortrait(GameData.Customers[GameData.MatrixCustomers[row]].Name);
            }
        }

        static void SkinCatalog(Transform root)
        {
            for (int i = 0; i < 4; i++) Set(root, "SI" + i, ArtSprites.VibeIcon(i));
            var level2 = root.GetComponent<Level2Robot>();
            if (level2 == null) level2 = root.GetComponentInChildren<Level2Robot>(true);
            if (level2 != null)
            {
                var g = UIFactory.FindDeep<Button>(level2.transform, "GSel");
                var m = UIFactory.FindDeep<Button>(level2.transform, "MSel");
                if (g != null) UIFactory.FindDeep<Image>(g.transform, "Icon").sprite = ArtSprites.GenreIcon(Genre.SciFi);
                if (m != null) UIFactory.FindDeep<Image>(m.transform, "Icon").sprite = ArtSprites.MovieCover(0);
            }
        }

        static Image[] NamedImages(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Image>(true);
            int count = 0;
            foreach (var image in all) if (image.gameObject.name == name) count++;
            var result = new Image[count];
            int index = 0;
            foreach (var image in all) if (image.gameObject.name == name) result[index++] = image;
            return result;
        }

        static void Set(Transform root, string name, Sprite sprite)
        {
            var image = UIFactory.FindDeep<Image>(root, name);
            if (image != null) image.sprite = sprite;
        }

        static void SetSliced(Transform root, string name, Sprite sprite)
        {
            var image = UIFactory.FindDeep<Image>(root, name);
            if (image == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
    }
}
