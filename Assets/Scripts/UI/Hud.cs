using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>Persistent top bar: store sign, the cash total, and the level goal.</summary>
    public class Hud : MonoBehaviour
    {
        [SerializeField] Text _money, _goal, _level;
        [SerializeField] Image _goalIcon;
        [SerializeField] RectTransform _canvas;
        Text _trust;
        Image _trustFill;
        bool _bound;

        void Start()
        {
            if (_bound || GameManager.I == null || _money == null) return;
            Bind(GameManager.I);
        }

        void Bind(GameManager gm)
        {
            if (_bound) return;
            _bound = true;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>().transform as RectTransform;
            EnsureTrustPlate();
            gm.OnMoneyChanged += OnMoney;
            gm.OnPhaseChanged += OnPhase;
            gm.OnSale += OnSale;
            gm.OnTrustChanged += OnTrust;
            OnMoney(gm.Money, 0);
            OnTrust(gm.Trust, 0);
        }

        /// <summary>
        /// The trust meter is newer than the authored HUD prefab, so it is always built
        /// at bind time — on both the authored and the code-constructed path.
        /// </summary>
        void EnsureTrustPlate()
        {
            if (_trust != null) return;
            if (_goal != null)
            {
                // authored prefabs park the goal text where the plate now lives
                var grt = UIFactory.RT(_goal.gameObject);
                grt.anchoredPosition = new Vector2(-296, grt.anchoredPosition.y);
            }

            var trustPlate = UIFactory.Bevel(transform, "TrustPlate", new Color(0.13f, 0.12f, 0.16f), sunken: true);
            UIFactory.Place(UIFactory.RT(trustPlate.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(118, 30), new Vector2(-168, 0));
            var trustBg = UIFactory.Image(trustPlate.transform, "TrustBg", new Color(0, 0, 0, 0.35f), null, Image.Type.Simple, false);
            UIFactory.Fill(UIFactory.RT(trustBg.gameObject), 5, 6, 5, 6);
            _trustFill = UIFactory.Image(trustBg.transform, "TrustFill", Theme.Cash, null, Image.Type.Simple, false);
            var tfr = UIFactory.RT(_trustFill.gameObject);
            tfr.anchorMin = Vector2.zero; tfr.anchorMax = new Vector2(0.7f, 1f);
            tfr.offsetMin = Vector2.zero; tfr.offsetMax = Vector2.zero;
            _trust = UIFactory.Text(trustPlate.transform, "Trust", "TRUST 70", 11, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(_trust.gameObject));
        }

        public static Hud Create(Transform canvas)
        {
            var bar = UIFactory.Bevel(canvas, "HUD", new Color(0.09f, 0.12f, 0.13f));
            var rt = UIFactory.RT(bar.gameObject);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, 46); rt.anchoredPosition = Vector2.zero;

            var hud = bar.gameObject.AddComponent<Hud>();
            hud._canvas = (RectTransform)canvas;

            var avatar = UIFactory.Image(bar.transform, "PlayerAvatar", Color.white, ArtSprites.MovieFanAvatar(), Image.Type.Simple, false);
            avatar.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(avatar.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 34), new Vector2(8, 0));

            var logo = UIFactory.Image(bar.transform, "StoreLogo", Color.white, ArtSprites.StoreLogo(), Image.Type.Simple, false);
            logo.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(logo.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 34), new Vector2(46, 0));

            var sign = UIFactory.Text(bar.transform, "Sign", "PELLINGS VIDEO", 18, Theme.TitleText, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(sign.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(240, 34), new Vector2(84, 0));

            hud._level = UIFactory.Text(bar.transform, "Level", "", 14, Theme.CrtAmber, Theme.SystemSans, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(hud._level.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(280, 30), new Vector2(0, -6));

            var moneyPlate = UIFactory.Bevel(bar.transform, "MoneyPlate", new Color(0.10f, 0.16f, 0.10f), sunken: true);
            UIFactory.Place(UIFactory.RT(moneyPlate.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(150, 30), new Vector2(-12, 0));
            var cashIcon = UIFactory.Image(moneyPlate.transform, "CashIcon", Color.white, ArtSprites.CashRegister(), Image.Type.Simple, false);
            cashIcon.preserveAspect = true;
            UIFactory.Place(UIFactory.RT(cashIcon.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 24), new Vector2(5, 0));
            hud._money = UIFactory.Text(moneyPlate.transform, "Money", "$0", 20, Theme.CrtGreen, Theme.Typewriter, TextAnchor.MiddleRight, false, FontStyle.Bold);
            UIFactory.Fill(UIFactory.RT(hud._money.gameObject), 34, 2, 10, 2);

            // goal text; the trust meter is added by EnsureTrustPlate() during Bind
            hud._goal = UIFactory.Text(bar.transform, "Goal", "", 12, Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleRight, false);
            UIFactory.Place(UIFactory.RT(hud._goal.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(160, 16), new Vector2(-296, 0));
            hud._goalIcon = UIFactory.Image(bar.transform, "GoalIcon", Color.white, ArtSprites.Goal(), Image.Type.Simple, false);
            hud._goalIcon.preserveAspect = true;
            hud._goalIcon.gameObject.SetActive(false);

            hud.Bind(GameManager.I);
            return hud;
        }

        void OnMoney(int total, int delta)
        {
            _money.text = "$" + total;
            _money.color = total < 0 ? Theme.ErrorRed : Theme.CrtGreen;
            RefreshGoal();
        }

        void OnTrust(int total, int delta)
        {
            if (_trust == null) return;
            _trust.text = "TRUST " + total;
            var c = total >= 60 ? Theme.Cash : total >= 30 ? Theme.Coin : Theme.ErrorRed;
            _trustFill.color = c;
            var rt = UIFactory.RT(_trustFill.gameObject);
            rt.anchorMax = new Vector2(total / 100f, 1f);
            if (delta != 0)
            {
                // float a +/- trust popup near the meter
                var go = UIFactory.Node(_canvas, "TrustPop");
                var t = go.AddComponent<Text>();
                t.font = Theme.SystemSans; t.fontSize = 18; t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                t.color = delta > 0 ? Theme.Cash : Theme.ErrorRed;
                t.text = (delta > 0 ? "+" : "") + delta + " TRUST";
                var rt2 = (RectTransform)go.transform;
                rt2.anchorMin = rt2.anchorMax = new Vector2(1f, 1f);
                rt2.sizeDelta = new Vector2(160, 30);
                rt2.anchoredPosition = new Vector2(-220, -70);
                go.AddComponent<FloatAway>();
            }
        }

        void OnPhase(Phase p)
        {
            switch (p)
            {
                case Phase.Storefront: _level.text = "— THE STOREFRONT —"; break;
                case Phase.Level1: _level.text = "LEVEL 1 · MANUAL RECOMMENDATION"; break;
                case Phase.Level2: _level.text = "LEVEL 2 · RULE-BASED RECOMMENDATION"; break;
                case Phase.Level3: _level.text = "LEVEL 3 · CONTENT-BASED RECOMMENDATION"; break;
                case Phase.Level4: _level.text = "LEVEL 4 · COLLABORATIVE FILTERING"; break;
                case Phase.Level5: _level.text = "LEVEL 5 · MARKET GAP RESEARCH"; break;
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
                case Phase.Level3: _goal.text = "goal: serve the line by the box"; break;
                default: _goal.text = ""; break;
            }
            _goalIcon.gameObject.SetActive(false);
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
