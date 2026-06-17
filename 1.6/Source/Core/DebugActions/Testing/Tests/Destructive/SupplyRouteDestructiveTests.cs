using System.Linq;

namespace FactionColonies.SupplyChain
{
    /* DESTRUCTIVE: needs two live settlements (may create transient ones) to compute a real route
       efficiency from travel distance. The stockpiles are throwaway fixtures, but settlement
       creation mutates the world. Not reverted. */
    public static class SupplyRouteDestructiveTests
    {
        [EmpireDestructiveTest("SC.Destructive.Routes")]
        public static void Execute_TransfersWithEfficiencyLoss()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            ResourceTypeDef r = SupplyChainCache.AllResourceTypeDefs.FirstOrDefault();
            if (r is null) TestAssert.Skip("No resource types defined");

            WorldSettlementFC src = SCDestructiveTestUtil.SettlementAt(f, 0);
            WorldSettlementFC dst = SCDestructiveTestUtil.SettlementAt(f, 1);
            if (src is null || dst is null) TestAssert.Skip("Could not obtain two settlements");
            if (src == dst) TestAssert.Skip("Only one settlement available; need a distinct source and destination");

            var route = new SupplyRoute(src, dst, r, 50.0);
            TestAssert.DoesNotThrow(() => route.RecacheIfDirty(), "RecacheIfDirty threw");
            double eff = route.CachedEfficiency;
            TestAssert.GreaterThan(eff, 0.0, "Route efficiency should be positive between valid settlements");
            TestAssert.LessThanOrEqual(eff, 1.0, "Route efficiency must not exceed 1.0");

            DictionaryStockpile sourceSp = SCTestHelper.MakeStockpile(r, 100.0, 100.0);
            DictionaryStockpile destSp = SCTestHelper.MakeStockpile(r, 0.0, 1000.0);

            double received = route.Execute(sourceSp, destSp);

            TestAssert.AreEqual(50.0 * eff, received, 0.01,
                "Received should equal drawn * efficiency when the destination has room");
            TestAssert.AreEqual(50.0, sourceSp.GetAmount(r), 0.01, "Source should have 100 - 50 = 50 left");
            TestAssert.AreEqual(received, destSp.GetAmount(r), 0.01,
                "Destination should hold exactly the received amount");

            DestructiveTestUtil.AssertEmpireInvariants(f, "SupplyRoute_Execute");
        }

        [EmpireDestructiveTest("SC.Destructive.Routes")]
        public static void Execute_DestinationFull_ExcessLost()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            ResourceTypeDef r = SupplyChainCache.AllResourceTypeDefs.FirstOrDefault();
            if (r is null) TestAssert.Skip("No resource types defined");

            WorldSettlementFC src = SCDestructiveTestUtil.SettlementAt(f, 0);
            WorldSettlementFC dst = SCDestructiveTestUtil.SettlementAt(f, 1);
            if (src is null || dst is null) TestAssert.Skip("Could not obtain two settlements");
            if (src == dst) TestAssert.Skip("Only one settlement available; need a distinct source and destination");

            var route = new SupplyRoute(src, dst, r, 50.0);
            route.RecacheIfDirty();
            double eff = route.CachedEfficiency;
            if (eff <= 0.0) TestAssert.Skip("Route resolved to zero efficiency");

            DictionaryStockpile sourceSp = SCTestHelper.MakeStockpile(r, 100.0, 100.0);
            DictionaryStockpile destSp = SCTestHelper.MakeStockpile(r, 0.0, 1.0); // tiny cap forces overflow

            double received = route.Execute(sourceSp, destSp);

            TestAssert.LessThanOrEqual(received, 1.0, "Received cannot exceed the destination cap");
            TestAssert.AreEqual(received, destSp.GetAmount(r), 0.01,
                "Destination should hold exactly what fit; the rest is lost");

            DestructiveTestUtil.AssertEmpireInvariants(f, "SupplyRoute_Overflow");
        }
    }
}
