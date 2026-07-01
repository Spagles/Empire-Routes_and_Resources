using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionColonies.SupplyChain
{
    public class SupplyRoute : IExposable
    {
        public WorldSettlementFC source;
        public WorldSettlementFC destination;
        public ResourceTypeDef resource;
        public double amountPerPeriod;
        public int priority;

        // How often (in days) this route dispatches a delivery, and when its next dispatch is due.
        // nextDispatchTick = -1 is a sentinel meaning "not yet scheduled" — a new route dispatches
        // immediately on the first daily tick after creation, then every frequencyDays thereafter.
        public int frequencyDays = SupplyChainSettings.defaultRouteFrequencyDays;
        public int nextDispatchTick = -1;

        // Cached (not saved)
        private int cachedTravelTicks;
        private double cachedEfficiency;
        private bool dirty = true;

        public int CachedTravelTicks => cachedTravelTicks;
        public double CachedEfficiency => cachedEfficiency;

        public SupplyRoute()
        {
        }

        public SupplyRoute(WorldSettlementFC source, WorldSettlementFC destination,
            ResourceTypeDef resource, double amountPerPeriod, int priority = 0)
        {
            this.source = source;
            this.destination = destination;
            this.resource = resource;
            this.amountPerPeriod = amountPerPeriod;
            this.priority = priority;

            dirty = true;
        }

        /// <summary>
        /// Sets the delivery frequency (clamped to the configured bounds) and reschedules the next
        /// dispatch relative to now, so a shortened frequency takes effect promptly.
        /// </summary>
        public void SetFrequencyDays(int days)
        {
            int clamped = Mathf.Clamp(days, SupplyChainSettings.minRouteFrequencyDays,
                SupplyChainSettings.maxRouteFrequencyDays);
            if (clamped == frequencyDays) return;
            frequencyDays = clamped;
            nextDispatchTick = Find.TickManager.TicksGame + frequencyDays * GenDate.TicksPerDay;
        }

        /// <summary>
        /// Returns true if both source and destination settlements still exist and are valid.
        /// </summary>
        public bool IsValid()
        {
            return source != null && destination != null && resource != null
                && !source.Destroyed && !destination.Destroyed;
        }

        public void SetDirty()
        {
            dirty = true;
        }

        public void RecacheIfDirty()
        {
            if (!dirty) return;
            dirty = false;

            if (!IsValid())
            {
                cachedTravelTicks = 0;
                cachedEfficiency = 0.0;
                return;
            }

            cachedTravelTicks = TravelUtil.ReturnTicksToArrive(source.Tile, destination.Tile);
            double travelDays = cachedTravelTicks / (double)GenDate.TicksPerDay;
            double baseEfficiency = FormulaUtil.RouteEfficiency(travelDays);

            // Apply route efficiency bonus stat from source settlement
            FCStatDef routeEffStat = SCStatDefOf.SC_RouteEfficiencyBonus;
            if (routeEffStat != null)
            {
                double bonus = FindFC.FactionComp.GetStatValue(routeEffStat, source);
                baseEfficiency += bonus;
            }

            // Apply modifier hooks
            foreach (ISupplyRouteModifier mod in SupplyRouteModifierRegistry.Modifiers)
            {
                try
                {
                    baseEfficiency = mod.ModifyRouteEfficiency(this, baseEfficiency);
                }
                catch (Exception e)
                {
                    LogSC.Error($"ISupplyRouteModifier {mod.GetType().Name} threw: {e}");
                }
            }

            cachedEfficiency = Math.Max(0.0, Math.Min(1.0, baseEfficiency));
        }

        /// <summary>
        /// Dispatch a delivery: draw up to <see cref="amountPerPeriod"/> from the source stockpile
        /// (shipping whatever is available if short) and return a self-contained
        /// <see cref="PendingDelivery"/> that arrives after this route's travel time, with efficiency
        /// applied on arrival. Returns null if the route is invalid or nothing could be drawn.
        /// </summary>
        public PendingDelivery TryDispatch(IStockpile sourceStockpile)
        {
            if (!IsValid() || amountPerPeriod <= 0 || cachedEfficiency <= 0)
                return null;

            if (!sourceStockpile.TryDraw(resource, amountPerPeriod, out double drawn) || drawn <= 0)
                return null;

            int now = Find.TickManager.TicksGame;
            return new PendingDelivery
            {
                source = source,
                destination = destination,
                resource = resource,
                amount = drawn,                // amountPerPeriod is a target; ship whatever we could draw
                efficiency = cachedEfficiency, // snapshot; the route may change before arrival
                dispatchTick = now,
                arrivalTick = now + cachedTravelTicks
            };
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref source, "source");
            Scribe_References.Look(ref destination, "destination");
            Scribe_Defs.Look(ref resource, "resource");
            Scribe_Values.Look(ref amountPerPeriod, "amountPerPeriod", 0.0);
            Scribe_Values.Look(ref priority, "priority", 0);
            Scribe_Values.Look(ref frequencyDays, "frequencyDays", SupplyChainSettings.defaultRouteFrequencyDays);
            Scribe_Values.Look(ref nextDispatchTick, "nextDispatchTick", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                dirty = true;
            }
        }
    }
}
