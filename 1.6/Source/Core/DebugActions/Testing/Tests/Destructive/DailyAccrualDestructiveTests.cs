using System.Collections.Generic;

namespace FactionColonies.SupplyChain
{
    /* DESTRUCTIVE: runs the real daily-accrual consume pass (PostDailyAccrual) and the per-building
       dormancy driver against live settlements. Deposits via Realize, draws needs/inputs/tithe, and
       toggles BuildingFC.active. Not reverted. */
    public static class DailyAccrualDestructiveTests
    {
        [EmpireDestructiveTest("SC.Destructive.Daily")]
        public static void PostDailyAccrual_DoesNotThrow_AndStockpilesNonNegative()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp is null) TestAssert.Skip("No SupplyChain world component");

            if (f.settlements.Count == 0)
            {
                WorldSettlementFC created = DestructiveTestUtil.CreateTransientSettlement();
                if (created is null) TestAssert.Skip("No settlements and no valid tile to create one");
            }

            TestAssert.DoesNotThrow(() => comp.PostDailyAccrual(f), "PostDailyAccrual threw");

            AssertStockpilesNonNegative(f, comp, "PostDailyAccrual");
            DestructiveTestUtil.AssertEmpireInvariants(f, "PostDailyAccrual");
        }

        [EmpireDestructiveTest("SC.Destructive.Daily")]
        public static void Realize_Deposits_AndStaysNonNegative()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp is null) TestAssert.Skip("No SupplyChain world component");

            WorldSettlementFC s = SCDestructiveTestUtil.FirstOrTransient(f);
            if (s is null) TestAssert.Skip("No settlement available");
            WorldObjectComp_SupplyChain sc = SupplyChainCache.GetSettlementComp(s);
            if (sc is null) TestAssert.Skip("No settlement comp");

            ResourceTypeDef r = null;
            foreach (ResourceTypeDef def in SupplyChainCache.AllResourceTypeDefs) { r = def; break; }
            if (r is null) TestAssert.Skip("No resource defs");

            // Deposit a modest per-day amount the way the base mod's realize callback would.
            TestAssert.DoesNotThrow(() => sc.Realize(r, 5.0, 5.0), "Realize threw");

            AssertStockpilesNonNegative(f, comp, "Realize");
            DestructiveTestUtil.AssertEmpireInvariants(f, "Realize");
        }

        [EmpireDestructiveTest("SC.Destructive.Daily")]
        public static void ResolveBuildingDormancy_DoesNotThrow()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp is null) TestAssert.Skip("No SupplyChain world component");
            if (f.settlements.Count == 0) TestAssert.Skip("No settlements");

            foreach (WorldSettlementFC s in f.settlements)
            {
                WorldObjectComp_SupplyChain sc = SupplyChainCache.GetSettlementComp(s);
                IStockpile sp = comp.Mode == SupplyChainMode.Simple ? comp.Stockpile : sc?.GetStockpile();
                if (sp is null) continue;
                WorldSettlementFC captured = s;
                TestAssert.DoesNotThrow(
                    () => NeedResolver.ResolveBuildingDormancy(captured, sp),
                    "ResolveBuildingDormancy threw for " + s.Name);
            }
            DestructiveTestUtil.AssertEmpireInvariants(f, "ResolveBuildingDormancy");
        }

        [EmpireDestructiveTest("SC.Destructive.Daily")]
        public static void BuildingDormancy_StarvedInputBuilding_GoesDormant()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp is null) TestAssert.Skip("No SupplyChain world component");

            // Find a settlement with an input-requiring building.
            foreach (WorldSettlementFC s in f.settlements)
            {
                if (s.BuildingsComp is null) continue;

                List<BuildingFC> buildings = s.BuildingsComp.Buildings;
                for (int slot = 0; slot < buildings.Count; slot++)
                {
                    BuildingFC b = buildings[slot];
                    if (b.def is null || b.def == BuildingFCDefOf.Empty) continue;
                    BuildingNeedExtension ext = SupplyChainCache.GetBuildingNeedExt(b.def);
                    if (ext?.inputs is null || ext.inputs.Count == 0) continue;

                    WorldObjectComp_SupplyChain sc = SupplyChainCache.GetSettlementComp(s);
                    IStockpile sp = comp.Mode == SupplyChainMode.Simple ? comp.Stockpile : sc?.GetStockpile();
                    if (sp is null) continue;

                    // Drain this building's inputs from the stockpile so it cannot be afforded.
                    foreach (BuildingResourceInput input in ext.inputs)
                    {
                        if (input.resource is null) continue;
                        sp.TryDraw(input.resource, sp.GetAmount(input.resource), out _);
                    }

                    NeedResolver.ResolveBuildingDormancy(s, sp);
                    TestAssert.IsFalse(buildings[slot].active,
                        "A starved input building (" + b.def.defName + ") should be dormant");
                    DestructiveTestUtil.AssertEmpireInvariants(f, "BuildingDormancy_Starved");
                    return;
                }
            }
            TestAssert.Skip("No input-requiring building available to starve");
        }

        private static void AssertStockpilesNonNegative(FactionFC f, WorldComponent_SupplyChain comp, string ctx)
        {
            if (comp.Mode == SupplyChainMode.Simple)
            {
                AssertOneStockpile(comp.Stockpile, ctx + ":Faction");
                return;
            }
            foreach (WorldSettlementFC s in f.settlements)
            {
                WorldObjectComp_SupplyChain sc = SupplyChainCache.GetSettlementComp(s);
                if (sc is null) continue;
                AssertOneStockpile(sc.GetStockpile(), ctx + ":" + s.Name);
            }
        }

        private static void AssertOneStockpile(IStockpile sp, string ctx)
        {
            if (sp is null) return;
            foreach (ResourceTypeDef r in SupplyChainCache.AllResourceTypeDefs)
                TestAssert.GreaterThan(sp.GetAmount(r), -0.001, ctx + ": negative stockpile amount for " + r.defName);
        }
    }
}
