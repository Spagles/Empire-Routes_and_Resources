using System.Collections.Generic;

namespace FactionColonies.SupplyChain
{
    /* DESTRUCTIVE: resolves needs against a real settlement, overwriting its live need-states and
       invalidating its stat cache. Stockpiles passed in are throwaway fixtures. Not reverted. */
    public static class NeedResolverDestructiveTests
    {
        [EmpireDestructiveTest("SC.Destructive.Needs")]
        public static void ResolveSettlementNeeds_AbundantStockpile_AllNeedsMet()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = SCDestructiveTestUtil.FirstOrTransient(f);
            if (s is null) TestAssert.Skip("No settlement available");
            WorldObjectComp_SettlementNeeds needsComp = SupplyChainCache.GetNeedsComp(s);
            if (needsComp is null) TestAssert.Skip("Settlement has no needs comp");

            DictionaryStockpile sp = SCDestructiveTestUtil.AbundantStockpile();
            TestAssert.DoesNotThrow(() => NeedResolver.ResolveSettlementNeeds(s, sp, needsComp),
                "ResolveSettlementNeeds threw");

            List<NeedState> states = needsComp.NeedStates;
            if (states is null || states.Count == 0)
                TestAssert.Skip("Settlement has no active needs to satisfy");

            foreach (NeedState ns in states)
                TestAssert.GreaterThan(ns.Satisfaction, 0.999,
                    "Need '" + ns.label + "' should be fully met from an abundant stockpile");

            DestructiveTestUtil.AssertEmpireInvariants(f, "ResolveSettlementNeeds_Abundant");
        }

        [EmpireDestructiveTest("SC.Destructive.Needs")]
        public static void ResolveSettlementNeeds_EmptyStockpile_NothingFulfilled()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = SCDestructiveTestUtil.FirstOrTransient(f);
            if (s is null) TestAssert.Skip("No settlement available");
            WorldObjectComp_SettlementNeeds needsComp = SupplyChainCache.GetNeedsComp(s);
            if (needsComp is null) TestAssert.Skip("Settlement has no needs comp");

            DictionaryStockpile sp = SCTestHelper.MakeEmptyStockpile();
            TestAssert.DoesNotThrow(() => NeedResolver.ResolveSettlementNeeds(s, sp, needsComp),
                "ResolveSettlementNeeds threw");

            List<NeedState> states = needsComp.NeedStates;
            bool anyPositiveDemand = false;
            if (states != null)
            {
                foreach (NeedState ns in states)
                {
                    if (ns.demanded <= 0) continue;
                    anyPositiveDemand = true;
                    TestAssert.AreEqual(0.0, ns.fulfilled, 0.001,
                        "Empty stockpile should fulfill nothing for '" + ns.label + "'");
                }
            }
            if (!anyPositiveDemand)
                TestAssert.Skip("Settlement has no positive-demand needs to test");

            DestructiveTestUtil.AssertEmpireInvariants(f, "ResolveSettlementNeeds_Empty");
        }

        [EmpireDestructiveTest("SC.Destructive.Needs")]
        public static void StatusBarFlow_BaseNeeds_MatchesResolvedBaseNeeds()
        {
            // Regression: the status-bar flow accumulator and the needs resolver must apply the
            // same activation filter. A settlement-type-restricted need (e.g. UrbanRural's city-only
            // food) that the resolver excludes must not leak into flow.baseNeeds, or the bottom bar
            // reports more demand than the settlement actually has.
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = SCDestructiveTestUtil.FirstOrTransient(f);
            if (s is null) TestAssert.Skip("No settlement available");
            WorldObjectComp_SettlementNeeds needsComp = SupplyChainCache.GetNeedsComp(s);
            if (needsComp is null) TestAssert.Skip("Settlement has no needs comp");
            WorldObjectComp_SupplyChain dataComp = WorldComponent_SupplyChain.GetComp(s);
            if (dataComp is null) TestAssert.Skip("Settlement has no supply-chain comp");
            WorldComponent_SupplyChain worldComp = SupplyChainCache.Comp;
            if (worldComp is null) TestAssert.Skip("No supply-chain world component");

            // Resolve fresh base need-states for this settlement.
            DictionaryStockpile sp = SCDestructiveTestUtil.AbundantStockpile();
            NeedResolver.ResolveSettlementNeeds(s, sp, needsComp);

            List<NeedState> states = needsComp.NeedStates;
            if (states is null || states.Count == 0)
                TestAssert.Skip("Settlement has no active needs to compare");

            foreach (ResourceTypeDef def in SupplyChainCache.AllResourceTypeDefs)
            {
                double resolvedBase = 0.0;
                foreach (NeedState ns in states)
                {
                    if (ns.category == NeedCategory.Base && ns.resource == def)
                        resolvedBase += ns.demanded;
                }

                WorldComponent_SupplyChain.FlowBreakdown flow = worldComp.CalculateFlow(s, dataComp, def);
                TestAssert.AreEqual(resolvedBase, flow.baseNeeds, 0.001,
                    "flow.baseNeeds for '" + def.defName + "' must match the resolved base needs "
                    + "(restricted needs must be filtered identically in both paths)");
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "StatusBarFlow_BaseNeeds");
        }

        [EmpireDestructiveTest("SC.Destructive.Needs")]
        public static void ResolveSettlementNeedsFair_DoesNotThrow()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (f.settlements.Count == 0)
            {
                WorldSettlementFC created = DestructiveTestUtil.CreateTransientSettlement();
                if (created is null) TestAssert.Skip("No settlements and no valid tile to create one");
            }

            DictionaryStockpile sp = SCDestructiveTestUtil.AbundantStockpile();
            TestAssert.DoesNotThrow(() => NeedResolver.ResolveSettlementNeedsFair(f, sp),
                "ResolveSettlementNeedsFair threw");
            DestructiveTestUtil.AssertEmpireInvariants(f, "ResolveSettlementNeedsFair");
        }
    }
}
