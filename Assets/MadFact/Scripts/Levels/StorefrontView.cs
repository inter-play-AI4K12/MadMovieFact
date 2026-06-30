using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// The Storefront hub. A top-down-ish video store whose customer line visibly grows
    /// to represent the scaling bottleneck. Acts as the backdrop behind every level.
    /// </summary>
    public class StorefrontView : MonoBehaviour
    {
        readonly List<GameObject> _line = new List<GameObject>();
        Button _enter;
        Text _enterLabel, _subtitle;
        RectTransform _lineRoot;

        public Button Enter => _enter;

        public static StorefrontView Create(Transform canvas)
        {
            var root = UIFactory.Image(canvas, "Storefront", Theme.Wall);
            UIFactory.Fill(UIFactory.RT(root.gameObject), 0, 40, 0, 0); // below HUD
            var view = root.gameObject.AddComponent<StorefrontView>();
            view.Build(root.transform);
            return view;
        }

        void Build(Transform root)
        {
            // fluorescent ceiling band
            var ceil = UIFactory.Image(root, "Ceiling", Theme.WallDark);
            UIFactory.Place(UIFactory.RT(ceil.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(2000, 26), new Vector2(0, 0));
            for (int i = 0; i < 5; i++)
            {
                var tube = UIFactory.Image(ceil.transform, "Tube", new Color(0.95f, 0.96f, 0.85f));
                UIFactory.Place(UIFactory.RT(tube.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120, 8), new Vector2(-360 + i * 180, 0));
            }

            // floor
            var floor = UIFactory.Image(root, "Floor", Theme.Lino);
            var frt = UIFactory.RT(floor.gameObject);
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0);
            frt.pivot = new Vector2(0.5f, 0); frt.sizeDelta = new Vector2(0, 150); frt.anchoredPosition = Vector2.zero;

            // shelves (top area). Note the empty HORROR / COMEDY sections: the market gap, foreshadowed.
            string[] shelves = { "SCI-FI", "SCI-FI", "ACTION", "ACTION", "HORROR\n(empty)", "COMEDY\n(empty)" };
            for (int i = 0; i < shelves.Length; i++)
            {
                bool empty = shelves[i].Contains("empty");
                var sh = UIFactory.Bevel(root, "Shelf", empty ? Theme.WallDark : new Color(0.45f, 0.38f, 0.30f));
                UIFactory.Place(UIFactory.RT(sh.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, 84), new Vector2(-415 + i * 166, -52));
                // tapes
                if (!empty)
                    for (int t = 0; t < 10; t++)
                    {
                        var tape = UIFactory.Image(sh.transform, "Tape", new Color(0.1f + 0.5f * (t % 3) / 2f, 0.1f, 0.12f));
                        UIFactory.Place(UIFactory.RT(tape.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(11, 56), new Vector2(8 + t * 13, 0));
                    }
                var lbl = UIFactory.Text(sh.transform, "L", shelves[i], 13, empty ? Theme.ErrorRed : Theme.TitleText, Theme.SystemSans, TextAnchor.LowerCenter, false, FontStyle.Bold);
                UIFactory.Fill(UIFactory.RT(lbl.gameObject), 2, 2, 2, 2);
            }

            // counter
            var counter = UIFactory.Bevel(root, "Counter", Theme.Counter);
            UIFactory.Place(UIFactory.RT(counter.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(420, 70), new Vector2(0, 86));
            var cashreg = UIFactory.Bevel(counter.transform, "Register", Theme.Face);
            UIFactory.Place(UIFactory.RT(cashreg.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(70, 46), new Vector2(120, 22));

            // line of customers (spawns to the right of the counter, trailing off-screen)
            _lineRoot = UIFactory.RT(UIFactory.Node(root, "Line"));
            _lineRoot.anchorMin = new Vector2(0, 0); _lineRoot.anchorMax = new Vector2(0, 0);
            _lineRoot.pivot = new Vector2(0, 0); _lineRoot.anchoredPosition = new Vector2(40, 96);
            for (int i = 0; i < 16; i++)
            {
                var fig = MakeCustomerFigure(_lineRoot, i);
                fig.SetActive(false);
                _line.Add(fig);
            }

            _subtitle = UIFactory.Text(root, "Subtitle", "", 15, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, true);
            UIFactory.Place(UIFactory.RT(_subtitle.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(620, 40), new Vector2(0, 196));

            _enter = UIFactory.Button(root, "Enter", "APPROACH THE COUNTER", null, Theme.Manila, 18, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(_enter.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(320, 48), new Vector2(0, 150));
            _enterLabel = _enter.GetComponentInChildren<Text>();
        }

        GameObject MakeCustomerFigure(Transform parent, int index)
        {
            var go = UIFactory.Node(parent, "Cust" + index);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(34, 70);
            rt.anchoredPosition = new Vector2(index * 40, 0);
            var rnd = new System.Random(index * 7 + 3);
            Color shirt = new Color((float)rnd.NextDouble() * 0.6f + 0.2f, (float)rnd.NextDouble() * 0.6f + 0.2f, (float)rnd.NextDouble() * 0.6f + 0.2f);
            Color skin = new Color(0.95f, 0.78f + (float)rnd.NextDouble() * 0.1f, 0.62f);
            var body = UIFactory.Image(go.transform, "Body", shirt);
            UIFactory.Place(UIFactory.RT(body.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(28, 40), new Vector2(0, 0));
            var head = UIFactory.Image(go.transform, "Head", skin, Theme.Disc);
            UIFactory.Place(UIFactory.RT(head.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(22, 22), new Vector2(0, 40));
            go.AddComponent<Bob>().offset = index * 0.5f;
            return go;
        }

        public void SetLine(int n)
        {
            n = Mathf.Clamp(n, 0, _line.Count);
            for (int i = 0; i < _line.Count; i++) _line[i].SetActive(i < n);
        }

        public void SetEnter(string label, Action onClick)
        {
            _enterLabel.text = label;
            _enter.onClick.RemoveAllListeners();
            if (onClick != null) _enter.onClick.AddListener(() => onClick());
        }

        public void SetEnterVisible(bool v) => _enter.gameObject.SetActive(v);
        public void SetSubtitle(string s) => _subtitle.text = s;
    }

    /// <summary>Gentle idle bob for customer figures so the line feels alive.</summary>
    public class Bob : MonoBehaviour
    {
        public float offset;
        Vector2 _home; bool _init;
        void Update()
        {
            var rt = (RectTransform)transform;
            if (!_init) { _home = rt.anchoredPosition; _init = true; }
            rt.anchoredPosition = _home + new Vector2(0, Mathf.Sin(Time.unscaledTime * 2.5f + offset) * 2f);
        }
    }
}
