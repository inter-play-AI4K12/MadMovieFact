using System;
using UnityEngine;

namespace MadFact
{
    public enum Phase
    {
        Boot,
        Storefront,
        Level1,
        Level2,
        Level3,
        Level4,
        Level5,
        Level6,
        Level7,
        Level8,
        Win
    }

    /// <summary>Central game state: money, current phase, the shared matrix model.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public int Money { get; private set; }
        public Phase Current { get; private set; } = Phase.Boot;
        public MfModel Matrix { get; private set; }
        public RunState Run { get; private set; }
        public bool HasActiveRun { get; private set; }
        public bool CanContinue { get; private set; }
        public int CurrentLevel { get; private set; }

        // The money balance at the moment the player most recently opened the current
        // level. Bankruptcy restarts the level by rolling Money back to this baseline
        // rather than wiping the whole run.
        public int LevelEntryMoney { get; set; }
        public event Action OnBankrupt;
        bool _bankrupt;

        // Community trust in the store (0-100). Mishandled customers and shady data
        // practices push it down; low trust visibly thins the customer line.
        public const int StartTrust = 70;
        public int Trust { get; private set; } = StartTrust;
        public event Action<int, int> OnTrustChanged;   // (newTotal, delta)

        // Mirrors LevelEntryMoney/OnBankrupt: trust bottoming out at 0 restarts the
        // level too — an empty-trust store can't sell anything, same as a negative till.
        public int LevelEntryTrust { get; set; }
        public event Action OnTrustCollapsed;
        bool _trustCollapsed;

        public void AddTrust(int delta)
        {
            int before = Trust;
            Trust = Mathf.Clamp(Trust + delta, 0, 100);
            if (Trust != before) OnTrustChanged?.Invoke(Trust, Trust - before);
            CheckTrustCollapse();
        }

        public void SetTrust(int value)
        {
            int before = Trust;
            Trust = Mathf.Clamp(value, 0, 100);
            if (Trust != before) OnTrustChanged?.Invoke(Trust, Trust - before);
            CheckTrustCollapse();
        }

        /// <summary>
        /// Fires OnTrustCollapsed once per zero crossing. Listeners (MadFactBootstrap) restart
        /// the current level on the next frame — never synchronously, since this can be called
        /// from deep inside a level's own batch/sale coroutine.
        /// </summary>
        void CheckTrustCollapse()
        {
            if (Trust <= 0 && !_trustCollapsed) { _trustCollapsed = true; OnTrustCollapsed?.Invoke(); }
            else if (Trust > 0) { _trustCollapsed = false; }
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
            CanContinue = false;
            CurrentLevel = 0;
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
            level = Mathf.Clamp(level, 1, LevelSceneCatalog.MaxPlayableLevel);
            if (HasActiveRun)
            {
                HighestUnlocked = Mathf.Max(HighestUnlocked, level);
                CurrentLevel = level;
                CanContinue = true;
                return;
            }

            Money = level <= 1 ? 0 : level == 2 ? Level1Goal : Level2Goal;
            Matrix = new MfModel();
            Run = new RunState();
            HighestUnlocked = level;
            HasActiveRun = true;
            CurrentLevel = level;
            CanContinue = true;
            Trust = StartTrust;
            OnMoneyChanged?.Invoke(Money, 0);
            OnTrustChanged?.Invoke(Trust, 0);
        }

        /// <summary>Dismiss the current run and seed a clean run at the selected level.</summary>
        public void StartNewAtLevel(int level)
        {
            level = Mathf.Clamp(level, 1, LevelSceneCatalog.MaxPlayableLevel);
            Money = level <= 1 ? 0 : level == 2 ? Level1Goal : Level2Goal;
            Current = Phase.Boot;
            Matrix = new MfModel();
            Run = new RunState();
            HighestUnlocked = level;
            HasActiveRun = true;
            CanContinue = true;
            CurrentLevel = level;
            Trust = StartTrust;
            OnMoneyChanged?.Invoke(Money, 0);
            OnTrustChanged?.Invoke(Trust, 0);
            OnPhaseChanged?.Invoke(Current);
        }

        public void MarkLevelCompleted(int level)
        {
            HighestUnlocked = Mathf.Max(HighestUnlocked,
                Mathf.Clamp(level + 1, 1, LevelSceneCatalog.MaxPlayableLevel));
            CurrentLevel = 0;
            CanContinue = false;
        }

        public void SetMoney(int value)
        {
            int delta = value - Money;
            Money = value;
            OnMoneyChanged?.Invoke(Money, delta);
            CheckBankruptcy();
        }

        public void AddMoney(int delta)
        {
            Money += delta;
            OnMoneyChanged?.Invoke(Money, delta);
            CheckBankruptcy();
        }

        /// <summary>
        /// Fires OnBankrupt once per negative crossing. Listeners (MadFactBootstrap) restart
        /// the current level on the next frame — never synchronously, since this can be
        /// called from deep inside a level's own batch/sale coroutine.
        /// </summary>
        void CheckBankruptcy()
        {
            if (Money < 0 && !_bankrupt) { _bankrupt = true; OnBankrupt?.Invoke(); }
            else if (Money >= 0) { _bankrupt = false; }
        }

        /// <summary>Record a recommendation sale with the given match error. Returns the tier.</summary>
        public SaleTier RecordSale(float error, Vector2 screenPos = default)
        {
            var tier = Economy.Tier(error);
            int pay = Economy.Pay(tier);
            Money += pay;
            OnMoneyChanged?.Invoke(Money, pay);
            OnSale?.Invoke(tier, pay, screenPos);
            CheckBankruptcy();

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
            if (p == Phase.Level6) HighestUnlocked = Mathf.Max(HighestUnlocked, 6);
            if (p == Phase.Level7) HighestUnlocked = Mathf.Max(HighestUnlocked, 7);
            if (p == Phase.Level8) HighestUnlocked = Mathf.Max(HighestUnlocked, 8);
            OnPhaseChanged?.Invoke(p);
        }

        public bool Level2Unlocked => Money >= Level1Goal || HighestUnlocked >= 2;
        public bool Level3Unlocked => Money >= Level2Goal || HighestUnlocked >= 3;
        public bool Level4Unlocked => HighestUnlocked >= 4;
        public bool Level5Unlocked => HighestUnlocked >= 5;
        public bool Level6Unlocked => HighestUnlocked >= 6;
        public bool Level7Unlocked => HighestUnlocked >= 7;
        public bool Level8Unlocked => HighestUnlocked >= 8;
    }
}
