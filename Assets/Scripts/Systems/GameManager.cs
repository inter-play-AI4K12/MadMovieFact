using System;
using UnityEngine;

namespace MadFact
{
    public enum Phase { Boot, Storefront, Level1, Level2, Level3, Level4, Level5, Win }

    /// <summary>Central game state: money, current phase, the shared matrix model.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public int Money { get; private set; }
        public Phase Current { get; private set; } = Phase.Boot;
        public MfModel Matrix { get; private set; }
        public RunState Run { get; private set; }
        public bool HasActiveRun { get; private set; }

        // Community trust in the store (0-100). Mishandled customers and shady data
        // practices push it down; low trust visibly thins the customer line.
        public const int StartTrust = 70;
        public int Trust { get; private set; } = StartTrust;
        public event Action<int, int> OnTrustChanged;   // (newTotal, delta)

        public void AddTrust(int delta)
        {
            int before = Trust;
            Trust = Mathf.Clamp(Trust + delta, 0, 100);
            if (Trust != before) OnTrustChanged?.Invoke(Trust, Trust - before);
        }

        /// <summary>How many customers actually show up, given current trust.</summary>
        public int TrustScaledCustomers(int nominal)
        {
            if (Trust >= 60) return nominal;
            if (Trust >= 30) return Mathf.Max(1, Mathf.RoundToInt(nominal * 0.75f));
            return Mathf.Max(1, Mathf.RoundToInt(nominal * 0.5f));
        }

        // Highest level the player has unlocked (lets them revisit the hub).
        public int HighestUnlocked = 1;

        // Monetary thresholds that gate progression. Tuned so Level 1 takes ~8-10
        // customers and Level 2 takes 2-3 full batches even with strong rules.
        public const int Level1Goal = 100;
        public const int Level2Goal = 300;

        public event Action<int, int> OnMoneyChanged;   // (newTotal, delta)
        public event Action<Phase> OnPhaseChanged;
        public event Action<SaleTier, int, Vector2> OnSale; // tier, amount, screen pos for popup

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            // Scene composition groups managers under _SceneCommon for readability.
            // Persistent objects must be roots before Unity can move them to the
            // DontDestroyOnLoad scene.
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            Matrix = new MfModel();
            Run = new RunState();
        }

        /// <summary>
        /// Start a clean playthrough from the menu or full-game scene. This intentionally
        /// recreates the learning model and run memory while keeping the persistent manager.
        /// </summary>
        public void ResetForNewGame()
        {
            Money = 0;
            Current = Phase.Boot;
            Matrix = new MfModel();
            Run = new RunState();
            HighestUnlocked = 1;
            HasActiveRun = true;
            Trust = StartTrust;
            OnMoneyChanged?.Invoke(Money, 0);
            OnTrustChanged?.Invoke(Trust, 0);
            OnPhaseChanged?.Invoke(Current);
        }

        /// <summary>
        /// Seed enough state for opening an individual level scene directly in the editor.
        /// If the player arrived from the menu/full game, the existing run is preserved.
        /// </summary>
        public void PrepareStandaloneLevel(int level)
        {
            if (HasActiveRun)
            {
                HighestUnlocked = Mathf.Max(HighestUnlocked, Mathf.Clamp(level, 1, 5));
                return;
            }

            Money = level <= 1 ? 0 : level == 2 ? Level1Goal : Level2Goal;
            Matrix = new MfModel();
            Run = new RunState();
            HighestUnlocked = Mathf.Clamp(level, 1, 5);
            HasActiveRun = true;
            Trust = StartTrust;
            OnMoneyChanged?.Invoke(Money, 0);
            OnTrustChanged?.Invoke(Trust, 0);
        }

        public void SetMoney(int value)
        {
            int delta = value - Money;
            Money = value;
            OnMoneyChanged?.Invoke(Money, delta);
        }

        public void AddMoney(int delta)
        {
            Money += delta;
            OnMoneyChanged?.Invoke(Money, delta);
        }

        /// <summary>Record a recommendation sale with the given match error. Returns the tier.</summary>
        public SaleTier RecordSale(float error, Vector2 screenPos = default)
        {
            var tier = Economy.Tier(error);
            int pay = Economy.Pay(tier);
            Money += pay;
            OnMoneyChanged?.Invoke(Money, pay);
            OnSale?.Invoke(tier, pay, screenPos);

            var au = AudioTension.I;
            if (au != null)
            {
                switch (tier)
                {
                    case SaleTier.Perfect: au.ChaChing(); break;
                    case SaleTier.Close: au.Coin(); break;
                    default: au.Buzzer(); break;
                }
            }
            return tier;
        }

        public void GoTo(Phase p)
        {
            Current = p;
            if (p == Phase.Level2) HighestUnlocked = Mathf.Max(HighestUnlocked, 2);
            if (p == Phase.Level3) HighestUnlocked = Mathf.Max(HighestUnlocked, 3);
            if (p == Phase.Level4) HighestUnlocked = Mathf.Max(HighestUnlocked, 4);
            if (p == Phase.Level5) HighestUnlocked = Mathf.Max(HighestUnlocked, 5);
            OnPhaseChanged?.Invoke(p);
        }

        public bool Level2Unlocked => Money >= Level1Goal || HighestUnlocked >= 2;
        public bool Level3Unlocked => Money >= Level2Goal || HighestUnlocked >= 3;
        public bool Level4Unlocked => HighestUnlocked >= 4;
        public bool Level5Unlocked => HighestUnlocked >= 5;
    }
}
