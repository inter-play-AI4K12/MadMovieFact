using System;
using UnityEngine;

namespace MadFact
{
    public enum Phase { Boot, Storefront, Level1, Level2, Level3, Level4, Win }

    /// <summary>Central game state: money, current phase, the shared matrix model.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public int Money { get; private set; }
        public Phase Current { get; private set; } = Phase.Boot;
        public MfModel Matrix { get; private set; }

        // Highest level the player has unlocked (lets them revisit the hub).
        public int HighestUnlocked = 1;

        // Monetary thresholds that gate progression.
        public const int Level1Goal = 40;
        public const int Level2Goal = 110;

        public event Action<int, int> OnMoneyChanged;   // (newTotal, delta)
        public event Action<Phase> OnPhaseChanged;
        public event Action<SaleTier, int, Vector2> OnSale; // tier, amount, screen pos for popup

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Matrix = new MfModel();
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
            OnPhaseChanged?.Invoke(p);
        }

        public bool Level2Unlocked => Money >= Level1Goal || HighestUnlocked >= 2;
        public bool Level3Unlocked => Money >= Level2Goal || HighestUnlocked >= 3;
    }
}
