using UnityEngine;

namespace MadFact
{
    public enum SaleTier { Perfect, Close, Terrible }

    /// <summary>Revenue is the loss function. Money earned is tied to the matrix error.</summary>
    public static class Economy
    {
        // GDD bands: Perfect (~0) $20 ; Close (0.5-1.5) $5 ; Terrible (2.0+) refund -$5.
        public const float PerfectMax = 0.5f;
        public const float CloseMax = 2.0f;

        public const int PerfectPay = 20;
        public const int ClosePay = 5;
        public const int Refund = -5;

        public static SaleTier Tier(float error)
        {
            if (error < PerfectMax) return SaleTier.Perfect;
            if (error < CloseMax) return SaleTier.Close;
            return SaleTier.Terrible;
        }

        public static int Pay(SaleTier tier)
        {
            switch (tier)
            {
                case SaleTier.Perfect: return PerfectPay;
                case SaleTier.Close: return ClosePay;
                default: return Refund;
            }
        }

        public static Color TierColor(SaleTier t)
        {
            switch (t)
            {
                case SaleTier.Perfect: return Theme.Cash;
                case SaleTier.Close: return Theme.Coin;
                default: return Theme.ErrorRed;
            }
        }

        public static string TierLabel(SaleTier t)
        {
            switch (t)
            {
                case SaleTier.Perfect: return "PERFECT MATCH";
                case SaleTier.Close: return "CLOSE ENOUGH";
                default: return "REFUND DEMANDED";
            }
        }
    }
}
