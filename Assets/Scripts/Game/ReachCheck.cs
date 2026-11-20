using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Banjjak
{
    /// <summary>
    /// Can every dirt cell of a job actually be sprayed? Runs in the automated test only.
    /// 1. Finds every spot the player can stand on: a fine grid over the job, every floor height in each column,
    ///    flood-filled from the spawn (up a step or a jump, down any drop, nothing solid in between).
    /// 2. Adds crouching, standing and scissor-lift eye heights at each spot.
    /// 3. A cell is reachable when a straight, unblocked line from one of those eyes, within the spray range, lands
    ///    on that surface right at the cell (on any face the cell spans).
    /// Check mode reports surfaces whose unreachable dirty cells outnumber their finish allowance (stuck below 100%).
    /// Bake mode writes every unreachable cell to Resources/Reach/&lt;job id&gt;.bytes; CleanJob leaves them out on load.
    /// </summary>
    public static class ReachCheck
    {
        public const int BakeMagic = 0x31484352;   // "RCH1"
        const float Step = .25f;          // grid spacing for standing spots (fine enough for 40 cm stair treads)
        const float Climb = .7f;          // a step or a jump (jump apex is about .75 m)
        const float Range = 7.4f;         // base spray range, a little under G.BaseRange
        const float HitTol = .08f;        // the jet cleans cells this close to where it lands
        const int MaxRays = 800;          // per cell point, before giving up

        static readonly int Solid = (1 << Layers.Default) | (1 << Layers.PlayerOnly);

        public struct Stuck { public string Name; public int Unreached, Allow, Total; }

        /// <param name="bakeDir">null: check dirty cells only and report; otherwise test every dirt cell and write the bake there.</param>
        public static List<Stuck> Run(CleanJob job, StringBuilder log, string bakeDir = null, bool lift = true)
        {
            float t0 = Time.realtimeSinceStartup;
            Physics.SyncTransforms();
            var eyes = Eyes(job, lift, out int spots);
            var stuck = new List<Stuck>();
            if (spots < 50)
            {
                log.AppendLine($"  reach check FAILED: only {spots} standing spots found from the spawn");
                stuck.Add(new Stuck { Name = "(no standing spots)" });
                return stuck;
            }
            // eyes bucketed in 1 m cubes, so each cell tries the nearest ones first (same floor before other floors)
            var buckets = new Dictionary<(int, int, int), List<Vector3>>();
            foreach (var e in eyes)
            {
                var key = (Mathf.FloorToInt(e.x), Mathf.FloorToInt(e.y), Mathf.FloorToInt(e.z));
                if (!buckets.TryGetValue(key, out var l)) buckets[key] = l = new List<Vector3>();
                l.Add(e);
            }
            int ringMax = Mathf.CeilToInt(Range) + 1;
            int checkedCells = 0, rays = 0, unreachedAll = 0;
            var bake = bakeDir != null ? new List<(int n, int dirt, List<int> cells)>() : null;

            bool Sees(DirtSurface s, Vector3 p, Vector3 n)
            {
                int tries = 0, bx = Mathf.FloorToInt(p.x), by = Mathf.FloorToInt(p.y), bz = Mathf.FloorToInt(p.z);
                for (int ring = 0; ring <= ringMax; ring++)
                    for (int a = -ring; a <= ring; a++)
                        for (int b = -ring; b <= ring; b++)
                        {
                            // only the shell of this ring: c runs through when a or b is on it, else just its two faces
                            bool edge = a == -ring || a == ring || b == -ring || b == ring;
                            for (int c = -ring; c <= ring; c += edge || ring == 0 ? 1 : 2 * ring)
                        {
                            if (!buckets.TryGetValue((bx + a, by + b, bz + c), out var list)) continue;
                            foreach (var e in list)
                            {
                                var v = p - e;
                                float d = v.magnitude;
                                if (d > Range || d < .05f) continue;
                                if (Vector3.Dot(-v, n) < .1f * d) continue;      // eye behind or edge-on to the face
                                rays++;
                                if (++tries > MaxRays) return false;
                                if (!Physics.Raycast(e, v / d, out var hit, d + .15f, Layers.SprayMask, QueryTriggerInteraction.Ignore)) continue;
                                if (hit.collider.gameObject != s.gameObject) continue;
                                if ((hit.point - p).sqrMagnitude > HitTol * HitTol) continue;
                                return true;
                            }
                        }
                        }
                return false;
            }

            foreach (var s in job.Surfs)
            {
                var hide = bake != null ? new List<int>() : null;
                int unreached = 0;
                if (s.Samples != null)
                    foreach (var kv in s.Samples)
                    {
                        int k = kv.Key;
                        if (s.Surf[k] != 2) continue;
                        if (bake == null && (s.Done || s.DirtAmt[k] < G.CleanT)) continue;
                        checkedCells++;
                        bool ok = false;
                        foreach (var (p, n) in kv.Value) if (Sees(s, p, n)) { ok = true; break; }
                        if (ok) continue;
                        unreached++;
                        hide?.Add(k);
                    }
                unreachedAll += unreached;
                if (bake != null) bake.Add((s.N, s.DirtCellCount(), hide));
                else if (unreached > s.Allow)
                    stuck.Add(new Stuck { Name = s.Comp.Obj.Name + "/" + s.Comp.Name, Unreached = unreached, Allow = s.Allow, Total = s.Total });
                if (bake != null && hide.Count > s.DirtCellCount() * .6f)
                    log.AppendLine($"  reach bake: {s.Comp.Obj.Name}/{s.Comp.Name} loses {hide.Count * 100 / Mathf.Max(1, s.DirtCellCount())}% of its cells");
            }
            log.AppendLine($"  reach {(bake != null ? "bake" : "check")}{(lift ? "" : " without the lift")}: {spots} standing spots, {eyes.Count} eyes, {checkedCells} cells, {rays} rays, {unreachedAll} unreachable, {Time.realtimeSinceStartup - t0:0.0}s");
            if (bake != null)
            {
                Directory.CreateDirectory(bakeDir);
                using (var w = new BinaryWriter(File.Create(Path.Combine(bakeDir, job.Id + ".bytes"))))
                {
                    w.Write(BakeMagic);
                    w.Write(bake.Count);
                    foreach (var (n, dirt, cells) in bake)
                    {
                        w.Write(n); w.Write(dirt); w.Write(cells.Count);
                        foreach (int k in cells) w.Write(k);
                    }
                }
            }
            else
            {
                string tag = lift ? "" : " (no lift)";
                if (stuck.Count == 0) log.AppendLine($"  reach check{tag}: every surface can reach 100%");
                foreach (var st in stuck) log.AppendLine($"  STUCK{tag} {st.Name}: {st.Unreached} unreachable dirty cells (allowance {st.Allow}, total {st.Total})");
            }
            return stuck;
        }

        /// <summary>Eye positions: standing spots reachable on foot from the spawn, with crouch, stand and lift heights.</summary>
        static List<Vector3> Eyes(CleanJob job, bool lift, out int spotCount)
        {
            var r = job.Bounds;
            int nx = Mathf.CeilToInt(r.width / Step), nz = Mathf.CeilToInt(r.height / Step);
            Vector3 At(int i, int j, float y) => new Vector3(r.xMin + (i + .5f) * Step, y, r.yMin + (j + .5f) * Step);
            // every height the player fits standing on, per grid column (several floors in the tower)
            var floors = new List<float>[nx * nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var list = new List<float>();
                    var top = At(i, j, 40);
                    foreach (var h in Physics.RaycastAll(top, Vector3.down, 45, Solid, QueryTriggerInteraction.Ignore))
                    {
                        if (h.normal.y < .6f) continue;
                        var p = h.point;
                        // room for the body and head; starts .6 m up so the next stair tread (rises up to .4 m) doesn't count
                        if (Physics.CheckCapsule(p + Vector3.up * .6f, p + Vector3.up * 1.5f, .15f, Solid, QueryTriggerInteraction.Ignore)) continue;
                        list.Add(p.y);
                    }
                    floors[i + nx * j] = list;
                }
            // start from the standing spot nearest the spawn
            var seen = new HashSet<(int, int, float)>();
            var q = new Queue<(int, int, float)>();
            float best = float.MaxValue; (int, int, float) seed = default;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    foreach (var y in floors[i + nx * j])
                    {
                        float d = (At(i, j, y) - job.Spawn).sqrMagnitude;
                        if (d < best) { best = d; seed = (i, j, y); }
                    }
            if (best < 16) { q.Enqueue(seed); seen.Add(seed); }
            var eyes = new List<Vector3>();
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                var (i, j, y) = q.Dequeue();
                var p = At(i, j, y);
                // the fine grid is for walking up narrow steps; on open floors every other spot is enough for eyes
                bool eyeHere = (i % 2 == 0 && j % 2 == 0) || (y > .3f && y < 1.9f);
                if (eyeHere)
                {
                eyes.Add(p + Vector3.up * .95f);
                eyes.Add(p + Vector3.up * 1.6f);
                // spraying at the top of a jump (apex about .75 m)
                if (!Physics.Linecast(p + Vector3.up * 1.6f, p + Vector3.up * 2.4f, Solid, QueryTriggerInteraction.Ignore)) eyes.Add(p + Vector3.up * 2.35f);
                // the scissor lift: platform .42 m plus up to 4.2 m, wherever there is head room
                if (lift) foreach (float lh in new[] { 1.4f, 2.8f, 4.2f })
                {
                    var e = p + Vector3.up * (.42f + lh + 1.6f);
                    if (Physics.Linecast(p + Vector3.up * 1.6f, e, Solid, QueryTriggerInteraction.Ignore)) break;
                    eyes.Add(e);
                }
                }
                for (int d = 0; d < 4; d++)
                {
                    int a = i + dx[d], c = j + dz[d];
                    if (a < 0 || c < 0 || a >= nx || c >= nz) continue;
                    foreach (var y2 in floors[a + nx * c])
                    {
                        if (y2 - y > Climb || seen.Contains((a, c, y2))) continue;   // any drop is fine, climbing is not
                        var p2 = At(a, c, y2);
                        float top = Mathf.Max(y, y2);
                        if (Physics.Linecast(new Vector3(p.x, top + 1.0f, p.z), new Vector3(p2.x, top + 1.0f, p2.z), Solid, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.Linecast(new Vector3(p.x, top + .5f, p.z), new Vector3(p2.x, top + .5f, p2.z), Solid, QueryTriggerInteraction.Ignore)) continue;
                        seen.Add((a, c, y2)); q.Enqueue((a, c, y2));
                    }
                }
            }
            spotCount = seen.Count;
            return eyes;
        }
    }
}
