using System;
using System.Collections.Generic;
using System.Threading;
using RimWorld.Planet;
using Verse;

namespace FactionColonies.SupplyChain
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* Recomputes supply-route travel times / paths (the expensive A* world pathfind) OFF the main    */
    /* thread, mirroring the base mod's FCRoadQueue: a generation-numbered background Thread computes  */
    /* results from an immutable snapshot and publishes them for the main thread to merge on its next  */
    /* tick. Safe because WorldPathPool access is globally locked (WorldPathPoolPatches) and the       */
    /* worker only READS world state and returns plain data — all route mutation happens on the main  */
    /* thread in the merge. A user setting forces synchronous (main-thread) computation instead.       */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public class SupplyRouteWarmer
    {
        private struct Job
        {
            public SupplyRoute route;
            public PlanetTile from;
            public PlanetTile to;
        }

        private struct Result
        {
            public int travelTicks;
            public List<PlanetTile> path;
        }

        // currentGeneration advances on every kick and on every world-change Invalidate(); a worker's
        // results are discarded unless its captured generation still matches when it finishes.
        private volatile int currentGeneration;
        private volatile int completedGeneration = -1;
        private volatile bool workerActive;

        // Written by the worker, read/cleared by the main thread. Guarded by the volatile
        // completedGeneration (release on write / acquire on read), same as FCRoadQueue's pendingNewEdges.
        private Dictionary<SupplyRoute, Result> pendingResults;

        /// <summary>Discard any in-flight computation — call after a world change (roads/research) re-dirties routes.</summary>
        public void Invalidate()
        {
            currentGeneration++;
        }

        /// <summary>
        /// Main thread, once per WorldComponentTick: merge any finished results, then (if idle) snapshot the
        /// path-dirty routes and start a new generation — on a background thread, or synchronously if the
        /// user disabled threading.
        /// </summary>
        public void Tick(List<SupplyRoute> routes)
        {
            MergeCompleted();

            if (workerActive || routes == null) return;

            List<Job> jobs = null;
            for (int i = 0; i < routes.Count; i++)
            {
                SupplyRoute r = routes[i];
                if (r.PathReady || !r.IsValid()) continue;
                if (jobs is null) jobs = new List<Job>();
                jobs.Add(new Job { route = r, from = r.source.Tile, to = r.destination.Tile });
            }
            if (jobs is null) return;

            int gen = ++currentGeneration;
            workerActive = true;

            if (SupplyChainSettings.useThreadedRouteComputation)
            {
                Thread thread = new Thread(() => Compute(jobs, gen)) { IsBackground = true };
                thread.Start();
            }
            else
            {
                Compute(jobs, gen);
                MergeCompleted();
            }
        }

        private void Compute(List<Job> jobs, int gen)
        {
            try
            {
                Dictionary<SupplyRoute, Result> results = new Dictionary<SupplyRoute, Result>(jobs.Count);
                for (int i = 0; i < jobs.Count; i++)
                {
                    // Bail if superseded by a newer generation or the game is being torn down.
                    if (gen != currentGeneration || Current.Game is null) return;

                    Job job = jobs[i];
                    try
                    {
                        List<PlanetTile> path;
                        int ticks = TravelUtil.ReturnTicksToArrive(job.from, job.to, out path);
                        results[job.route] = new Result { travelTicks = ticks, path = path };
                    }
                    catch (Exception e)
                    {
                        // Leave this route dirty so it retries next generation.
                        LogSC.Error($"SupplyRouteWarmer pathfind threw (route left dirty): {e}");
                    }
                }

                if (gen == currentGeneration)
                {
                    pendingResults = results;      // non-volatile write...
                    completedGeneration = gen;     // ...published by this volatile write (release)
                }
            }
            finally
            {
                workerActive = false;
            }
        }

        private void MergeCompleted()
        {
            if (completedGeneration != currentGeneration) return;  // nothing fresh, or superseded
            Dictionary<SupplyRoute, Result> results = pendingResults;
            if (results is null) return;
            pendingResults = null;

            foreach (KeyValuePair<SupplyRoute, Result> kv in results)
                kv.Key.ApplyPathResult(kv.Value.travelTicks, kv.Value.path);
        }
    }
}
