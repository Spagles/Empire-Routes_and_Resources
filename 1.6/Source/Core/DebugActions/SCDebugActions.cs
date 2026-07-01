using System;
using System.Text;
using LudeonTK;
using Verse;
using RimWorld;

namespace FactionColonies.SupplyChain
{
    public static class SCDebugActions
    {
        /// <summary>
        /// Iterates all stockpiles (one in Simple mode, per-settlement in Complex mode).
        /// Calls action with (stockpile, label) for each.
        /// </summary>
        private static void ForEachStockpile(Action<IStockpile, string> action)
        {
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp == null) return;

            if (comp.Mode == SupplyChainMode.Simple)
            {
                IStockpile stockpile = comp.Stockpile;
                if (stockpile != null)
                    action(stockpile, "Faction");
            }
            else
            {
                FactionFC faction = FindFC.FactionComp;
                if (faction == null) return;
                foreach (WorldSettlementFC settlement in faction.settlements)
                {
                    WorldObjectComp_SupplyChain sc = SupplyChainCache.GetSettlementComp(settlement);
                    if (sc == null) continue;
                    IStockpile stockpile = sc.GetStockpile();
                    if (stockpile != null)
                        action(stockpile, settlement.Name);
                }
            }
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Fill all stockpiles", allowedGameStates = AllowedGameStates.Playing)]
        private static void FillAllStockpiles()
        {
            ForEachStockpile((stockpile, label) =>
            {
                foreach (ResourceTypeDef rtd in SupplyChainCache.AllResourceTypeDefs)
                {
                    double cap = stockpile.GetCap(rtd);
                    double current = stockpile.GetAmount(rtd);
                    if (cap > current)
                        stockpile.Credit(rtd, cap - current);
                }
            });
            Log.Message("[Empire-SupplyChain] Debug: All stockpiles filled to cap.");
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Empty all stockpiles", allowedGameStates = AllowedGameStates.Playing)]
        private static void EmptyAllStockpiles()
        {
            ForEachStockpile((stockpile, label) =>
            {
                foreach (ResourceTypeDef rtd in SupplyChainCache.AllResourceTypeDefs)
                {
                    double current = stockpile.GetAmount(rtd);
                    if (current > 0)
                    {
                        double drawn;
                        stockpile.TryDraw(rtd, current, out drawn);
                    }
                }
            });
            Log.Message("[Empire-SupplyChain] Debug: All stockpiles emptied.");
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Force resolve needs", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceResolveNeeds()
        {
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp == null) return;

            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            if (comp.Mode == SupplyChainMode.Simple)
            {
                IStockpile stockpile = comp.Stockpile;
                if (stockpile == null) return;
                NeedResolver.ResolveSettlementNeedsFair(faction, stockpile);
            }
            else
            {
                foreach (WorldSettlementFC settlement in faction.settlements)
                {
                    WorldObjectComp_SupplyChain sc = SupplyChainCache.GetSettlementComp(settlement);
                    WorldObjectComp_SettlementNeeds needsComp = SupplyChainCache.GetNeedsComp(settlement);
                    if (sc == null || needsComp == null) continue;
                    IStockpile stockpile = sc.GetStockpile();
                    if (stockpile == null) continue;
                    NeedResolver.ResolveSettlementNeeds(settlement, stockpile, needsComp);
                }
            }
            Log.Message("[Empire-SupplyChain] Debug: Needs resolved.");
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Force dispatch all due routes", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceDispatchAllRoutes()
        {
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp == null || comp.Mode != SupplyChainMode.Complex) return;

            comp.DebugForceDispatchAllRoutes();
            Log.Message("[Empire-SupplyChain] Debug: Dispatched all routes now; "
                + comp.PendingDeliveries.Count + " deliveries in transit.");
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Force arrive all deliveries", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceArriveAllDeliveries()
        {
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp == null || comp.Mode != SupplyChainMode.Complex) return;

            int count = comp.PendingDeliveries.Count;
            comp.DebugForceArriveAllDeliveries();
            Log.Message("[Empire-SupplyChain] Debug: Forced arrival of " + count + " deliveries.");
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Force execute sell orders", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceExecuteSellOrders()
        {
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp == null) return;

            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            comp.PreTaxResolution(faction);
            Log.Message("[Empire-SupplyChain] Debug: PreTaxResolution executed (includes sell orders).");
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Force post-tax cleanup", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForcePostTaxCleanup()
        {
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp == null) return;

            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            comp.PostTaxResolution(faction);
            Log.Message("[Empire-SupplyChain] Debug: PostTaxResolution executed (tithe injection cleanup).");
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Print stockpile state", allowedGameStates = AllowedGameStates.Playing)]
        private static void PrintStockpileState()
        {
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[Empire-SupplyChain] === Stockpile State (" + comp.Mode + " mode) ===");

            ForEachStockpile((stockpile, label) =>
            {
                sb.AppendLine("  " + label + ":");
                foreach (ResourceTypeDef rtd in SupplyChainCache.AllResourceTypeDefs)
                {
                    double amount = stockpile.GetAmount(rtd);
                    double cap = stockpile.GetCap(rtd);
                    if (cap > 0 || amount > 0)
                        sb.AppendLine("    " + rtd.label + ": " + amount.ToString("F1") + " / " + cap.ToString("F1"));
                }
            });

            if (comp.Mode == SupplyChainMode.Complex)
            {
                sb.AppendLine("  Routes: " + comp.SupplyRoutes.Count);
                foreach (SupplyRoute route in comp.SupplyRoutes)
                {
                    if (!route.IsValid()) continue;
                    route.RecacheIfDirty();
                    sb.AppendLine("    " + route.source.Name + " -> " + route.destination.Name
                        + " (" + route.resource.label + " x" + route.amountPerPeriod.ToString("F1")
                        + ", every " + route.frequencyDays + "d, eff=" + route.CachedEfficiency.ToString("F2") + ")");
                }

                sb.AppendLine("  In-transit deliveries: " + comp.PendingDeliveries.Count);
                foreach (PendingDelivery d in comp.PendingDeliveries)
                {
                    string src = d.source != null ? d.source.Name : "?";
                    string dst = d.destination != null ? d.destination.Name : "?";
                    string res = d.resource != null ? d.resource.label : "?";
                    sb.AppendLine("    " + src + " -> " + dst + " (" + res + " x" + d.amount.ToString("F1")
                        + ", eff=" + d.efficiency.ToString("F2") + ", ETA " + d.TicksRemaining.ToStringTicksToPeriod() + ")");
                }
            }

            Log.Message(sb.ToString());
        }

        [DebugAction("Empire Refactored: Routes & Resources", "Force full tax cycle", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceFullTaxCycle()
        {
            WorldComponent_SupplyChain comp = SupplyChainCache.Comp;
            if (comp == null) return;

            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            comp.PreTaxResolution(faction);
            comp.PostTaxResolution(faction);
            Log.Message("[Empire-SupplyChain] Debug: Full tax cycle executed (Pre + Post + cleanup).");
        }
    }
}
