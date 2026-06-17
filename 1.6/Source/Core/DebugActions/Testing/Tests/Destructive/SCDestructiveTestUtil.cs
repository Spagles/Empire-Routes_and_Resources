using System.Collections.Generic;

namespace FactionColonies.SupplyChain
{
    /// <summary>
    /// SupplyChain-specific helpers for the destructive test tier. Builds on the base mod's
    /// <see cref="DestructiveTestUtil"/> (transient-settlement create/teardown + invariant battery)
    /// and adds the fixtures the Routes &amp; Resources destructive tests share.
    /// </summary>
    public static class SCDestructiveTestUtil
    {
        /// <summary>First live settlement, or a freshly created transient one (null if neither possible).</summary>
        public static WorldSettlementFC FirstOrTransient(FactionFC f)
        {
            if (f?.settlements != null && f.settlements.Count > 0) return f.settlements[0];
            return DestructiveTestUtil.CreateTransientSettlement();
        }

        /// <summary>Settlement at <paramref name="index"/>, or a freshly created transient one.</summary>
        public static WorldSettlementFC SettlementAt(FactionFC f, int index)
        {
            if (f?.settlements != null && f.settlements.Count > index) return f.settlements[index];
            return DestructiveTestUtil.CreateTransientSettlement();
        }

        /// <summary>
        /// A stockpile seeded with every resource type at a very large amount and cap — used to
        /// drive need resolution into the "fully supplied" regime.
        /// </summary>
        public static DictionaryStockpile AbundantStockpile()
        {
            var amounts = new Dictionary<ResourceTypeDef, double>();
            var caps = new Dictionary<ResourceTypeDef, double>();
            foreach (ResourceTypeDef r in SupplyChainCache.AllResourceTypeDefs)
            {
                amounts[r] = 1e9;
                caps[r] = 1e9;
            }
            return new DictionaryStockpile(amounts, caps);
        }
    }
}
