namespace FactionColonies.SupplyChain
{
    /* DESTRUCTIVE: queries founding-cost logic against live world/faction state. CanFoundSettlement
       is read-only, but obtaining a settlement may create a transient one, and a settings field is
       temporarily overridden (and restored). Not fully reverted. */
    public static class FoundingCostDestructiveTests
    {
        [EmpireDestructiveTest("SC.Destructive.Founding")]
        public static void ComputeDistanceMultiplier_AtExistingSettlement_IsAboutOne()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = SCDestructiveTestUtil.FirstOrTransient(f);
            if (s is null || !s.Tile.Valid) TestAssert.Skip("No settlement with a valid tile");

            FoundingCostUtil.InvalidateCache();
            double mult = FoundingCostUtil.ComputeDistanceMultiplier(s.Tile);

            // The nearest settlement to a settlement's own tile is itself (travel ~0) -> multiplier ~1.0.
            TestAssert.GreaterThan(mult, 0.999, "Distance multiplier should be at least ~1.0");
            TestAssert.LessThan(mult, 1.05, "At an existing settlement tile travel is ~0, so multiplier ~1.0");

            DestructiveTestUtil.AssertEmpireInvariants(f, "ComputeDistanceMultiplier_AtSettlement");
        }

        [EmpireDestructiveTest("SC.Destructive.Founding")]
        public static void CanFoundSettlement_BelowFreeThreshold_AlwaysAllowed()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp is null) TestAssert.Skip("No SupplyChain world component");
            WorldSettlementFC s = SCDestructiveTestUtil.FirstOrTransient(f);
            if (s is null || !s.Tile.Valid) TestAssert.Skip("No settlement with a valid tile");

            int origThreshold = SupplyChainSettings.freeSettlementThreshold;
            try
            {
                SupplyChainSettings.freeSettlementThreshold = 9999; // force the "below threshold" branch
                var validator = new FoundingCostValidator(comp);
                string reason;
                bool ok = validator.CanFoundSettlement(s.Tile, s.settlementDef, out reason, 1f);
                TestAssert.IsTrue(ok, "Founding below the free-settlement threshold should always be allowed");
            }
            finally
            {
                SupplyChainSettings.freeSettlementThreshold = origThreshold;
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "CanFoundSettlement_BelowThreshold");
        }
    }
}
