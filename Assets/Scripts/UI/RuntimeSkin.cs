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
                // Authored prefabs use Unity's built-in font only as an Edit Mode preview.
                // Replace that preview at runtime too; otherwise it remains visibly softer
                // than the larger OS-font atlases used by dynamically created controls.
                if (text.font == null || text.font == Theme.Fallback)
                    text.font = text.GetComponentInParent<Button>() != null ? Theme.SystemSans : Theme.Typewriter;
            }

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite != null) continue;

                // Procedural sprites cannot be serialized into authored prefabs. Restore
                // only artwork whose role is unambiguous. Generic bevel panels must stay
                // flat: Theme.Raised uses a low-PPU 9-slice whose borders expand into
                // large dark rectangles at runtime even though the prefab looks clean.
                bool isWindowFrame = image.transform.Find("WindowBody") != null;
                if (image.GetComponent<Button>() != null)
                {
                    // Matrix cells are clickable data, not action buttons. Keep the
                    // simple solid rectangle authored in the prefab so known black
                    // ratings and colour-coded guesses remain crisp in Play Mode.
                    if (IsMatrixCell(image.gameObject.name))
                    {
                        image.sprite = Theme.Solid;
                        image.type = Image.Type.Simple;
                    }
                    else
                    {
                        image.sprite = image.GetComponent<CommsBox>() != null || isWindowFrame
                            ? ArtSprites.PanelChrome()
                            : ArtSprites.ButtonChrome();
                        image.type = Image.Type.Sliced;
                    }
                }
                else if (isWindowFrame)
                {
                    image.sprite = ArtSprites.PanelChrome();
                    image.type = Image.Type.Sliced;
                }
                else if (image.type == Image.Type.Sliced)
                {
                    image.sprite = Theme.Solid;
                    image.type = Image.Type.Simple;
                }
                else
                    image.sprite = Theme.Solid;
            }

            Set(root, "StoreInterior", ArtSprites.StorefrontBackground());
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

        static bool IsMatrixCell(string name)
        {
            if (name.StartsWith("SCell_")) return true;
            if (!name.StartsWith("C")) return false;
            int underscore = name.IndexOf('_');
            if (underscore <= 1 || underscore >= name.Length - 1) return false;
            return int.TryParse(name.Substring(1, underscore - 1), out _) &&
                   int.TryParse(name.Substring(underscore + 1), out _);
        }

        static void SkinButtons(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var icon = UIFactory.FindDeep<Image>(button.transform, "Icon");
                if (icon == null) continue;
                string name = button.gameObject.name;
                if (name == "Leave") icon.sprite = ArtSprites.Close();
                else if (name == "Enter" || name == "Run" || name == "Generate") icon.sprite = ArtSprites.Play();
                else if (name == "GPrev")
                {
                    icon.sprite = ArtSprites.Back();
                    icon.color = Theme.Ink;
                }
                else if (name == "GNext")
                {
                    icon.sprite = ArtSprites.Next();
                    icon.color = Theme.Ink;
                }
                else if (name == "Add") icon.sprite = ArtSprites.Add();
                else if (name == "Clr") icon.sprite = ArtSprites.Clear();
                else if (name == "Reset") icon.sprite = ArtSprites.Reset();
                else if (name == "Optimize") icon.sprite = ArtSprites.Optimize();
                else if (name == "Green" || name == "UsePoster") icon.sprite = ArtSprites.Confirm();
                else if (name.StartsWith("M") && int.TryParse(name.Substring(1), out int movie)) icon.sprite = ArtSprites.MovieCover(movie);
                else if (name.StartsWith("Q") && int.TryParse(name.Substring(1), out int question))
                    icon.sprite = Level1Counter.QuestionIcon(question);
                else if (name.StartsWith("S") && int.TryParse(name.Substring(1), out int sticker)) icon.sprite = ArtSprites.Sticker(sticker);
                else if (name.StartsWith("Col") && int.TryParse(name.Substring(3), out int col)) icon.sprite = ArtSprites.MatrixCover(col);
                else if (name.StartsWith("Row") && int.TryParse(name.Substring(3), out int row))
                    icon.sprite = ArtSprites.CustomerPortrait(GameData.Customers[GameData.MatrixCustomers[row]].Name);

            }
        }

        static void SkinCatalog(Transform root)
        {
            for (int i = 0; i < 4; i++) Set(root, "SI" + i, ArtSprites.VibeIcon(i));

            // PosterBrowser cells are authored in the prefab so designers can edit
            // their layout. Their cover sprites are generated from the source sheet
            // at runtime, so reconnect those transient sprites for both Play Mode and
            // the editor preview.
            foreach (var browser in root.GetComponentsInChildren<PosterBrowser>(true))
            {
                browser.ApplySimpleNavigationStyle();
                foreach (Transform child in browser.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.name.StartsWith("Poster") ||
                        !int.TryParse(child.name.Substring("Poster".Length), out int movieIndex) ||
                        movieIndex < 0 || movieIndex >= GameData.Movies.Count)
                        continue;

                    var poster = UIFactory.FindDeep<Image>(child, "Img");
                    if (poster != null) poster.sprite = ArtSprites.MovieCover(movieIndex);
                }
            }

            if (root.GetComponent<Level2Robot>() != null)
            {
                var g = UIFactory.FindDeep<Button>(root, "GSel");
                var m = UIFactory.FindDeep<Button>(root, "MSel");
                if (g != null) UIFactory.FindDeep<Image>(g.transform, "Icon").sprite = ArtSprites.GenreIcon(Genre.SciFi);
                if (m != null) UIFactory.FindDeep<Image>(m.transform, "Icon").sprite = ArtSprites.MovieCover(0);
                var browse = UIFactory.FindDeep<Image>(root, "Browse");
                if (browse != null)
                {
                    browse.sprite = ArtSprites.Next();
                    browse.color = Theme.TitleText;
                }

                var pickerWindow = UIFactory.FindDeep<Transform>(root, "PickerWindow");
                if (pickerWindow != null)
                {
                    var pickerTitle = UIFactory.FindDeep<Text>(pickerWindow, "T");
                    if (pickerTitle != null) pickerTitle.color = Theme.Ink;
                    var close = UIFactory.FindDeep<Button>(pickerWindow, "Close");
                    if (close != null)
                    {
                        var icon = UIFactory.FindDeep<Image>(close.transform, "Icon");
                        if (icon != null) icon.gameObject.SetActive(false);
                        var label = close.GetComponentInChildren<Text>(true);
                        if (label != null)
                        {
                            label.text = "CLOSE";
                            label.color = Theme.Ink;
                            label.rectTransform.offsetMin = new Vector2(4, 2);
                            label.rectTransform.offsetMax = new Vector2(-4, -2);
                        }
                    }
                }
            }
        }

        static void SetButtonIcon(Transform root, string buttonName, Sprite sprite)
        {
            var button = UIFactory.FindDeep<Button>(root, buttonName);
            var icon = button != null ? UIFactory.FindDeep<Image>(button.transform, "Icon") : null;
            if (icon != null) icon.sprite = sprite;
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
    }
}
