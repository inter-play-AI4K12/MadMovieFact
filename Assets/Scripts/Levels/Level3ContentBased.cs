using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Level 3 draft — Content-Based Recommendation. This exists so the game arc,
    /// scene list, and progression can move to five levels now, while the actual mechanic
    /// can be designed later without disturbing the other levels.
    /// </summary>
    public class Level3ContentBased : MonoBehaviour
    {
        [SerializeField] GameObject _root;

        void Awake()
        {
            if (_root == null) return;
            Bind("Continue", Complete);
            Bind("Leave", () => MadFactBootstrap.I.GoStorefront());
        }

        void Bind(string name, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.FindDeep<Button>(transform, name);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static Level3ContentBased Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level3ContentBased");
            UIFactory.Fill(UIFactory.RT(go));
            var lvl = go.AddComponent<Level3ContentBased>();
            lvl.Build(go.transform);
            lvl._root.SetActive(false);
            return lvl;
        }

        void Build(Transform parent)
        {
            _root = UIFactory.Image(parent, "Level3ContentBased", new Color(0.05f, 0.08f, 0.11f, 0.92f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 40, 40, 40, 0);

            var bg = UIFactory.Image(_root.transform, "FeatureWall", new Color(0.07f, 0.10f, 0.13f));
            UIFactory.Fill(UIFactory.RT(bg.gameObject));

            var window = UIFactory.DialogWindow(_root.transform, "Window", Theme.Face);
            UIFactory.Place(UIFactory.RT(window.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(820, 420), Vector2.zero);

            var title = UIFactory.Text(window.transform, "Title", "LEVEL 3 · CONTENT-BASED RECOMMENDATION", 18, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(720, 34), new Vector2(0, -14));

            var body = UIFactory.Text(window.transform, "Body",
                "CONTENT FEATURE LAB\n\nThis level will compare movie/item features directly:\n\n" +
                "• read a movie's explicit attributes\n" +
                "• match those attributes to a customer's stated need\n" +
                "• expose why content-based systems miss hidden collaborative taste\n\n" +
                "For now this slot preserves the real five-level structure and routes into collaborative filtering.",
                16, Theme.Ink, Theme.Typewriter, TextAnchor.UpperLeft, true);
            UIFactory.Place(UIFactory.RT(body.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(690, 240), new Vector2(0, -68));

            var featureStrip = UIFactory.Bevel(window.transform, "FeatureStrip", new Color(0.06f, 0.09f, 0.10f), sunken: true);
            UIFactory.Place(UIFactory.RT(featureStrip.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(620, 70), new Vector2(0, 78));
            string[] labels = { "SPACE", "SPOOKY", "FUNNY", "EXPLOSIONS" };
            for (int i = 0; i < labels.Length; i++)
            {
                var chip = UIFactory.Bevel(featureStrip.transform, "Feature" + i, Latent.Colors[i]);
                UIFactory.Place(UIFactory.RT(chip.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 34), new Vector2(24 + i * 148, 0));
                var text = UIFactory.Text(chip.transform, "T", labels[i], 13, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                UIFactory.Fill(UIFactory.RT(text.gameObject));
            }

            var cont = UIFactory.Button(window.transform, "Continue", "CONTINUE TO COLLABORATIVE FILTERING", Complete, Theme.Cash, 15, Theme.SystemSans, Theme.TitleText);
            UIFactory.Place(UIFactory.RT(cont.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(360, 38), new Vector2(0, 24));
            UIFactory.ButtonIcon(cont, ArtSprites.Next(), 26f);

            var leave = UIFactory.Button(window.transform, "Leave", "", () => MadFactBootstrap.I.GoStorefront(), Theme.Face, 16);
            UIFactory.Place(UIFactory.RT(leave.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(26, 22), new Vector2(-10, -10));
            UIFactory.ButtonIcon(leave, ArtSprites.Close(), 18f, true);
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            MadFactBootstrap.I.Storefront.SetLine(12);
        }

        public void Close() => _root.SetActive(false);

        void Complete()
        {
            if (MadFactBootstrap.I.Level3Cleared) return;
            MadFactBootstrap.I.Level3Cleared = true;
            Close();
            MadFactBootstrap.I.OnContentBasedGoal();
        }
    }
}
