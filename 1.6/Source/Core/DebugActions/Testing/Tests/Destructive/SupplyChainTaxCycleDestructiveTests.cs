namespace FactionColonies.SupplyChain
{
    /* DESTRUCTIVE: runs the real SupplyChain tax-resolution pass against live settlements. It moves
       resources between stockpiles, resolves needs, and overwrites need-states. Not reverted. */
    public static class SupplyChainTaxCycleDestructiveTests
    {
        [EmpireDestructiveTest("SC.Destructive.Tax")]
        public static void FullTaxCycle_DoesNotThrow_AndStockpilesNonNegative()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp is null) TestAssert.Skip("No SupplyChain world component");

            if (f.settlements.Count == 0)
            {
                WorldSettlementFC created = DestructiveTestUtil.CreateTransientSettlement();
                if (created is null) TestAssert.Skip("No settlements and no valid tile to create one");
            }

            TestAssert.DoesNotThrow(() => comp.PreTaxResolution(f), "PreTaxResolution threw");
            TestAssert.DoesNotThrow(() => comp.PostTaxResolution(f), "PostTaxResolution threw");

            AssertStockpilesNonNegative(f, comp, "FullTaxCycle");
            DestructiveTestUtil.AssertEmpireInvariants(f, "FullTaxCycle");
        }

        [EmpireDestructiveTest("SC.Destructive.Tax")]
        public static void PostTaxCleanup_PerSettlement_DoesNotThrow()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp is null) TestAssert.Skip("No SupplyChain world component");
            if (f.settlements.Count == 0) TestAssert.Skip("No settlements");

            foreach (WorldSettlementFC s in f.settlements)
            {
                WorldObjectComp_SupplyChain sc = SupplyChainCache.GetSettlementComp(s);
                if (sc is null) continue;
                TestAssert.DoesNotThrow(() => sc.PostTaxCleanup(),
                    "PostTaxCleanup threw for " + s.Name);
            }
            DestructiveTestUtil.AssertEmpireInvariants(f, "PostTaxCleanup");
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
            {
                // amount must never go negative; -0.001 tolerance for float noise.
                TestAssert.GreaterThan(sp.GetAmount(r), -0.001, ctx + ": negative stockpile amount for " + r.defName);
            }
        }
    }
}
