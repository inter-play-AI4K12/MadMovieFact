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
        [SerializeField] List<GameObject> _line = new List<GameObject>();
        [SerializeField] Button _enter;
        [SerializeField] Text _enterLabel, _subtitle;
        [SerializeField] RectTransform _lineRoot;
        [SerializeField] Image _background;
        int _era = 1;

        public Button Enter => _enter;

        public static StorefrontView Create(Transform canvas)
        {
            var root = UIFactory.Image(canvas, "Storefront", Theme.Wall);
            UIFactory.Fill(UIFactory.RT(root.gameObject), 0, 46, 0, 0); // below HUD
            var view = root.gameObject.AddComponent<StorefrontView>();
            view.Build(root.transform);
            return view;
        }

        void Build(Transform root)
        {
            _background = UIFactory.Image(root, "StoreInterior", Color.white,
                ArtSprites.StorefrontBackground(), Image.Type.Simple, false);
            _background.preserveAspect = false;
            UIFactory.Fill(UIFactory.RT(_background.gameObject));

            // line of customers (spawns to the right of the counter, trailing off-screen)
            _lineRoot = UIFactory.RT(UIFactory.Node(root, "Line"));
            _lineRoot.anchorMin = new Vector2(0, 0); _lineRoot.anchorMax = new Vector2(0, 0);
            // Baseline sits on the carpet in front of the counter; x avoids the VHS display.
            _lineRoot.pivot = new Vector2(0, 0); _lineRoot.anchoredPosition = new Vector2(180, 128);
            for (int i = 0; i < 16; i++)
            {
                var fig = MakeCustomerFigure(_lineRoot, i);
                fig.SetActive(false);
                _line.Add(fig);
            }

            _subtitle = UIFactory.Text(root, "Subtitle", "", 15, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleCenter, true);
            _subtitle.color = Theme.TitleText;
            var outline = _subtitle.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);          // readable on bright era backdrops
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            UIFactory.Place(UIFactory.RT(_subtitle.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(700, 42), new Vector2(0, 326));

            _enter = UIFactory.Button(root, "Enter", "APPROACH THE COUNTER", null, Theme.Manila, 18, Theme.SystemSans);
            UIFactory.Place(UIFactory.RT(_enter.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(340, 48), new Vector2(0, 274));
            UIFactory.ButtonIcon(_enter, ArtSprites.Play(), 32f);
            _enterLabel = _enter.GetComponentInChildren<Text>();
        }

        GameObject MakeCustomerFigure(Transform parent, int index)
        {
            var go = UIFactory.Node(parent, "Cust" + index);
            var rt = (RectTransform)go.transform;
            // Slight overlap makes a growing line readable without shrinking the characters.
            rt.sizeDelta = new Vector2(96, 170);
            rt.anchoredPosition = new Vector2(index * 64, (index % 2) * 3);

            var back = UIFactory.Image(go.transform, "Back", Color.white, ArtSprites.QueueBack(index), Image.Type.Simple, false);
            back.preserveAspect = true;
            UIFactory.Fill(UIFactory.RT(back.gameObject));
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

        /// <summary>
        /// The store itself levels up: 1 = the tired original shop, 2 = computerized
        /// storefront, 3 = the Quantum Networks office, 4 = Aethelred Global HQ.
        /// </summary>
        public void SetEra(int era)
        {
            era = Mathf.Clamp(era, 1, 4);
            // Authored prefab instances never ran Build(), and their interior art may live
            // in the separate Environment prefab — search the whole canvas by name.
            if (_background == null) _background = UIFactory.FindDeep<Image>(transform.root, "StoreInterior");
            if (era == _era || _background == null) return;
            _era = era;
            _background.sprite = era == 1 ? ArtSprites.StorefrontBackground() : ArtSprites.LevelBackground(era);
            // the queue art belongs to the original shop's floor; hide it in later eras
            if (_lineRoot != null) _lineRoot.gameObject.SetActive(era <= 2);
        }

        /// <summary>Staging-branch compatibility alias: levels map straight onto eras.</summary>
        public void SetBackgroundForLevel(int level) => SetEra(Mathf.Max(1, level));
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
