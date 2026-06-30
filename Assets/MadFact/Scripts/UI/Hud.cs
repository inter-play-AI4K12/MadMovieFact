using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>Persistent top bar: store sign, the cash total, and the level goal.</summary>
    public class Hud : MonoBehaviour
    {
        Text _money, _goal, _level;
        RectTransform _canvas;

        public static Hud Create(Transform canvas)
        {
            var bar = UIFactory.Bevel(canvas, "HUD", Theme.Face);
            var rt = UIFactory.RT(bar.gameObject);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, 40); rt.anchoredPosition = Vector2.zero;

            var hud = bar.gameObject.AddComponent<Hud>();
            hud._canvas = (RectTransform)canvas;

            var sign = UIFactory.Text(bar.transform, "Sign", "📼 PELLINGS VIDEO", 18, Theme.Ink, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(sign.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(320, 34), new Vector2(14, 0));

            hud._level = UIFactory.Text(bar.transform, "Level", "", 14, Theme.InkSoft, Theme.SystemSans, TextAnchor.MiddleCenter, false);
            UIFactory.Place(UIFactory.RT(hud._level.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(340, 30), new Vector2(0, -6));

            var moneyPlate = UIFactory.Bevel(bar.transform, "MoneyPlate", new Color(0.10f, 0.16f, 0.10f), sunken: true);
            UIFactory.Place(UIFactory.RT(moneyPlate.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(150, 30), new Vector2(-12, 0));
            hud._money = UIFactory.Text(moneyPlate.transform, "Money", "$0", 20, Theme.CrtGreen, Theme.Typewriter, TextAnchor.MiddleRight, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(hud._money.gameObject), 8, 2, 10, 2);

            hud._goal = UIFactory.Text(bar.transform, "Goal", "", 12, Theme.InkSoft, Theme.SystemSans, TextAnchor.MiddleRight, false);
            UIFactory.Place(UIFactory.RT(hud._goal.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(200, 16), new Vector2(-170, 0));

            var gm = GameManager.I;
            gm.OnMoneyChanged += hud.OnMoney;
            gm.OnPhaseChanged += hud.OnPhase;
            gm.OnSale += hud.OnSale;
            hud.OnMoney(gm.Money, 0);
            return hud;
        }

        void OnMoney(int total, int delta)
        {
            _money.text = "$" + total;
            _money.color = total < 0 ? Theme.ErrorRed : Theme.CrtGreen;
            RefreshGoal();
        }

        void OnPhase(Phase p)
        {
            switch (p)
            {
                case Phase.Storefront: _level.text = "— THE STOREFRONT —"; break;
                case Phase.Level1: _level.text = "LEVEL 1 · THE MANUAL ERA"; break;
                case Phase.Level2: _level.text = "LEVEL 2 · THE AUTOMATION ERA"; break;
                case Phase.Level3: _level.text = "LEVEL 3 · THE ALGORITHM ERA"; break;
                case Phase.Level4: _level.text = "LEVEL 4 · THE MARKET GAP"; break;
                case Phase.Win: _level.text = "★ BLOCKBUSTER ★"; break;
            }
            RefreshGoal();
        }

        void RefreshGoal()
        {
            var gm = GameManager.I;
            switch (gm.Current)
            {
                case Phase.Level1: _goal.text = $"goal: ${GameManager.Level1Goal} to upgrade"; break;
                case Phase.Level2: _goal.text = $"goal: ${GameManager.Level2Goal} to automate"; break;
                default: _goal.text = ""; break;
            }
        }

        void OnSale(SaleTier tier, int amount, Vector2 screenPos)
        {
            var go = UIFactory.Node(_canvas, "SalePop");
            var t = go.AddComponent<Text>();
            t.font = Theme.Typewriter; t.fontSize = 30; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = Economy.TierColor(tier);
            t.text = (amount >= 0 ? "+$" + amount : "-$" + Mathf.Abs(amount));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(200, 50);
            Vector2 local = screenPos == default ? new Vector2(0, 40) : ScreenToCanvas(screenPos);
            rt.anchoredPosition = local;
            go.AddComponent<FloatAway>();
        }

        Vector2 ScreenToCanvas(Vector2 screen)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out local);
            return local;
        }
    }

    /// <summary>Floats a transient label upward while fading out, then self-destructs.</summary>
    public class FloatAway : MonoBehaviour
    {
        public float life = 1.1f, rise = 70f;
        float _t;
        Text _text;
        Vector2 _start;
        void Start() { _text = GetComponent<Text>(); _start = ((RectTransform)transform).anchoredPosition; }
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = _t / life;
            ((RectTransform)transform).anchoredPosition = _start + new Vector2(0, rise * k);
            if (_text != null) { var c = _text.color; c.a = Mathf.Clamp01(1f - k); _text.color = c; }
            if (_t >= life) Destroy(gameObject);
        }
    }
}
