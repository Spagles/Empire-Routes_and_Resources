using RimWorld;
using UnityEngine;
using Verse;

namespace FactionColonies.SupplyChain
{
    /// <summary>
    /// A single in-transit route delivery. Fully self-contained: it snapshots the resource,
    /// the amount already drawn from the source, and the route efficiency at dispatch time, so
    /// it still arrives correctly even if the originating <see cref="SupplyRoute"/> is edited or
    /// deleted while the goods are en route. On arrival only the destination still needs to exist.
    /// </summary>
    public class PendingDelivery : IExposable
    {
        public WorldSettlementFC source;       // for UI labelling + source-removal handling
        public WorldSettlementFC destination;  // credit target on arrival
        public ResourceTypeDef resource;
        public double amount;                  // raw in-transit amount already drawn from source
        public double efficiency;              // route efficiency snapshot; applied on arrival
        public int dispatchTick;               // absolute tick the delivery left the source
        public int arrivalTick;                // absolute tick the delivery lands

        /// <summary>Fraction of the journey completed (0 at dispatch, 1 at arrival).</summary>
        public float Progress
        {
            get
            {
                if (arrivalTick <= dispatchTick) return 1f;
                return Mathf.Clamp01((Find.TickManager.TicksGame - dispatchTick) / (float)(arrivalTick - dispatchTick));
            }
        }

        /// <summary>Ticks until arrival (never negative).</summary>
        public int TicksRemaining => Mathf.Max(0, arrivalTick - Find.TickManager.TicksGame);

        public void ExposeData()
        {
            Scribe_References.Look(ref source, "source");
            Scribe_References.Look(ref destination, "destination");
            Scribe_Defs.Look(ref resource, "resource");
            Scribe_Values.Look(ref amount, "amount", 0.0);
            Scribe_Values.Look(ref efficiency, "efficiency", 0.0);
            Scribe_Values.Look(ref dispatchTick, "dispatchTick", 0);
            Scribe_Values.Look(ref arrivalTick, "arrivalTick", 0);
        }
    }
}
