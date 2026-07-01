using System.Linq;
using Verse;

namespace FactionColonies.SupplyChain
{
    /* DESTRUCTIVE: needs two live settlements (may create transient ones) to compute a real route
       efficiency from travel distance. The stockpiles are throwaway fixtures, but settlement
       creation mutates the world. Not reverted. */
    public static class SupplyRouteDestructiveTests
    {
        [EmpireDestructiveTest("SC.Destructive.Routes")]
        public static void Dispatch_DrawsFromSource_ArrivesWithEfficiencyLoss()
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

            // Dispatch draws the full amount from the source immediately (in-transit).
            PendingDelivery d = route.TryDispatch(sourceSp);
            TestAssert.IsNotNull(d, "TryDispatch should produce a delivery when the source has stock");
            TestAssert.AreEqual(50.0, d.amount, 0.01, "Delivery carries the drawn amount");
            TestAssert.AreEqual(eff, d.efficiency, 0.01, "Delivery snapshots the route efficiency");
            TestAssert.AreEqual(50.0, sourceSp.GetAmount(r), 0.01, "Source should have 100 - 50 = 50 left at dispatch");
            TestAssert.LessThan((double)Find.TickManager.TicksGame, d.arrivalTick + 1.0, "Arrival must be in the future");

            // Simulate arrival (mirrors WorldComponent_SupplyChain.ProcessArrivals): efficiency applied here.
            double credited = d.amount * d.efficiency;
            double excess = destSp.Credit(r, credited);
            double received = credited - excess;

            TestAssert.AreEqual(50.0 * eff, received, 0.01,
                "Received should equal drawn * efficiency when the destination has room");
            TestAssert.AreEqual(received, destSp.GetAmount(r), 0.01,
                "Destination should hold exactly the received amount");

            DestructiveTestUtil.AssertEmpireInvariants(f, "SupplyRoute_Dispatch");
        }

        [EmpireDestructiveTest("SC.Destructive.Routes")]
        public static void Dispatch_DestinationFullOnArrival_ExcessLost()
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
            DictionaryStockpile destSp = SCTestHelper.MakeStockpile(r, 0.0, 1.0); // tiny cap forces overflow on arrival

            PendingDelivery d = route.TryDispatch(sourceSp);
            TestAssert.IsNotNull(d, "TryDispatch should produce a delivery when the source has stock");

            // Simulate arrival into a nearly-full destination.
            double credited = d.amount * d.efficiency;
            double excess = destSp.Credit(r, credited);
            double received = credited - excess;

            TestAssert.LessThanOrEqual(received, 1.0, "Received cannot exceed the destination cap");
            TestAssert.AreEqual(received, destSp.GetAmount(r), 0.01,
                "Destination should hold exactly what fit; the rest is lost");

            DestructiveTestUtil.AssertEmpireInvariants(f, "SupplyRoute_Overflow");
        }
    }
}
