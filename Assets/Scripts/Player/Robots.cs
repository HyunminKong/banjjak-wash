using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    /// <summary>Autonomous helpers: floor scrubber, trash collector, robot mower and a wall/window drone.</summary>
    public class Robot : MonoBehaviour
    {
        public string Type;
        public bool Fast;   // the free auto mower: quicker, wider cut
        float x, y, z, h, th, t, pause, trip, chase;
        int load, target = -1;
        readonly HashSet<int> ign = new HashSet<int>();
        // drone state
        string st = "seek";
        float timer;
        DirtSurface tgS;
        Vector3 tgP, hover;
        readonly List<Transform> brushes = new List<Transform>(), rotors = new List<Transform>();
        Material led;

        const int BotLoad = 8;
        static readonly int SolidMask = (1 << Layers.Default) | (1 << Layers.PlayerOnly);
        static CleanJob Job => GameManager.I.Job;
        static Vector3 V(float a, float b, float c) => new Vector3(a, b, c);

        public void Init(string type, Vector3 p, float heading)
        {
            Type = type; x = p.x; y = p.y; z = p.z; h = th = heading; t = Random.value * .4f;
            if (type == "drone") BuildDrone(); else BuildBot();
            transform.position = p;
        }

        // ------------------------------------------------------------------ models
        GameObject Part(MeshData md, Material m, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject("part");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = md.ToMesh("bot");
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        void BuildBot()
        {
            uint accent = Type == "trash" ? 0xff7a3du : Type == "mower" ? 0x2f9d4au : 0x1f86f0u;
            var shell = Mats.Solid(Type == "mower" ? G.Hex(0xdfe9df) : G.Hex(0xf4f6f8), .7f);
            Part(MeshKit.Lathe(new float[,] { { .3f, 0 }, { .32f, .03f }, { .32f, .1f }, { .29f, .14f }, { .14f, .17f }, { .001f, .175f } }, 40), shell, Vector3.zero);
            var ring = new List<Vector3>();
            for (int i = 0; i <= 40; i++) { float a = i / 40f * Mathf.PI * 2; ring.Add(V(Mathf.Cos(a) * .32f, .07f, Mathf.Sin(a) * .32f)); }
            Part(MeshKit.Tube(ring, .025f, 40, 8, false), Mats.Solid(G.Hex(accent), .6f), Vector3.zero);
            if (Type == "trash")
            {
                Part(MeshKit.Lathe(new float[,] { { .1f, .17f }, { .13f, .17f }, { .15f, .36f }, { .13f, .37f } }, 24), Mats.DoubleSided(Mats.Solid(G.Hex(0xff9a5e), .5f)), Vector3.zero);
                Part(MeshKit.Tube(new[] { V(0, .15f, .2f), V(0, .12f, .36f), V(0, .03f, .42f) }, .014f, 8, 8), Mats.Rubber, Vector3.zero);
            }
            if (Type == "mower") Part(MeshKit.RoundedBox(.08f, .12f, .2f, .03f), Mats.Solid(G.Hex(0x2f9d4a), .6f), V(0, .22f, -.05f));
            Part(MeshKit.Cylinder(.07f, .08f, .04f, 20), Mats.ChairDark, V(0, .19f, 0));
            led = Mats.Solid(G.Hex(0x5cff9d), .6f);
            led.SetColor("_Emission", G.Hex(0x2bdc6f) * 2);
            Part(MeshKit.Sphere(.022f, 12, 8), led, V(0, .2f, .2f));
            foreach (int s in new[] { -1, 1 })
                brushes.Add(Part(MeshKit.Cone(.09f, .03f, 12).Xf(0, 0, 0, 180), Mats.Grey, V(s * .22f, .015f, .2f)).transform);
        }

        void BuildDrone()
        {
            var white = Mats.Solid(G.Hex(0xf4f6f8), .7f);
            Part(MeshKit.RoundedBox(.26f, .08f, .26f, .04f, 3), white, Vector3.zero);
            foreach (var c in new[] { V(-.22f, 0, -.22f), V(.22f, 0, -.22f), V(-.22f, 0, .22f), V(.22f, 0, .22f) })
            {
                Part(MeshKit.Tube(new[] { Vector3.zero, V(c.x * .5f, .01f, c.z * .5f), V(c.x, .02f, c.z) }, .012f, 6, 8), Mats.ChairDark, Vector3.zero);
                Part(MeshKit.Cylinder(.025f, .025f, .04f, 12), Mats.ChairDark, V(c.x, .04f, c.z));
                rotors.Add(Part(MeshKit.RoundedBox(.24f, .004f, .025f, .002f, 1), Mats.ChairDark, V(c.x, .065f, c.z)).transform);
            }
            Part(MeshKit.Cylinder(.012f, .018f, .12f, 10).Xf(0, 0, 0, 90), Mats.Yellow, V(0, -.04f, .16f));
            led = Mats.Solid(G.Hex(0x5cd3ff), .6f);
            led.SetColor("_Emission", G.Hex(0x2ab6ff) * 2);
            Part(MeshKit.Sphere(.018f, 10, 8), led, V(0, .05f, -.13f));
        }

        // ------------------------------------------------------------------ helpers
        static bool Blocked(float px, float pz, float py) =>
            Physics.CheckBox(V(px, py + .2f, pz), V(.3f, .12f, .3f), Quaternion.identity, SolidMask, QueryTriggerInteraction.Ignore);

        static DirtSurface GroundHit(float px, float pz, float py, out Vector3 point)
        {
            point = default;
            if (!Physics.Raycast(V(px, py + .7f, pz), Vector3.down, out var hit, 1f, Layers.SprayMask, QueryTriggerInteraction.Ignore)) return null;
            var s = hit.collider.GetComponent<DirtSurface>();
            if (s == null || !s.IsGround || s.N == 0) return null;
            point = hit.point;
            return s;
        }

        float SteerAround(float hd)
        {
            foreach (float o in new[] { 0, .45f, -.45f, .9f, -.9f, 1.35f, -1.35f, 1.8f, -1.8f })
            {
                float a = hd + o;
                if (!Blocked(x + Mathf.Sin(a) * .55f, z + Mathf.Cos(a) * .55f, y) && !Blocked(x + Mathf.Sin(a) * .3f, z + Mathf.Cos(a) * .3f, y)) return a;
            }
            return hd + Mathf.PI * (.6f + Random.value * .8f);
        }

        Vector3? BinFor()
        {
            Vector3? best = null; float bd = 1e9f;
            foreach (var b in Job.Bins)
            {
                if (Mathf.Abs(b.y - y) > 1.2f) continue;
                float d = (b.x - x) * (b.x - x) + (b.z - z) * (b.z - z);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        // ------------------------------------------------------------------ ground robots
        void Update()
        {
            if (Job == null) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            led.SetColor("_Emission", (Type == "drone" ? G.Hex(0x2ab6ff) : G.Hex(0x2bdc6f)) * (1.4f + .8f * Mathf.Sin(Time.time * 6 + x)));
            if (Type == "drone") { UpdateDrone(dt); return; }
            foreach (var b in brushes) b.Rotate(0, dt * (pause > 0 ? 1700 : 1000), 0, Space.Self);
            if (pause > 0) { pause -= dt; transform.position = V(x, y + Mathf.Abs(Mathf.Sin(pause * 9)) * .02f, z); return; }
            var bd = Job.Bounds;
            t -= dt;
            if (t <= 0)
            {
                t = .8f + Random.value * .4f;
                float best = -1e9f, bh = h;
                if (Type == "trash" && Job.Trash != null)
                {
                    if (load >= BotLoad) { var b = BinFor(); if (b.HasValue) { bh = SteerAround(Mathf.Atan2(b.Value.x - x, b.Value.z - z)); best = 1; t = .4f; } }
                    else
                    {
                        int i = Job.Trash.NearestTo(V(x, y, z), ign);
                        if (i >= 0)
                        {
                            var it = Job.Trash.Items[i];
                            if (target != i) { target = i; chase = 0; }
                            if (chase > 9) { ign.Add(i); target = -1; }
                            else
                            {
                                float dh = Mathf.Atan2(it.P.x - x, it.P.z - z);
                                bh = new Vector2(it.P.x - x, it.P.z - z).magnitude < .7f ? dh : SteerAround(dh);
                                best = 1; t = .45f;
                            }
                        }
                    }
                }
                if (best < 0)
                    for (int k = 0; k < 10; k++)
                    {
                        float a = h + k / 10f * Mathf.PI * 2, px = x + Mathf.Sin(a) * 1.1f, pz = z + Mathf.Cos(a) * 1.1f;
                        if (px < bd.xMin || px > bd.xMax || pz < bd.yMin || pz > bd.yMax) continue;
                        float sc;
                        if (Type == "mower") sc = y < .2f && GroundHit(px, pz, 0, out _) == null && Job.Grass != null ? Job.Grass.UncutNear(px, pz, .6f) / 25f : -1;
                        else { var s = GroundHit(px, pz, y, out var pt); sc = s != null ? (Type == "floor" ? s.DirtAt(pt) : 0) : -1; }
                        if (Blocked(px, pz, y)) sc = -1;
                        sc += Random.value * .15f + (k == 0 ? .1f : 0);
                        if (sc > best) { best = sc; bh = a; }
                    }
                th = bh;
            }
            float d = Mathf.DeltaAngle(h * Mathf.Rad2Deg, th * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            h += Mathf.Clamp(d, -2.8f * dt, 2.8f * dt);
            float sp = Fast ? 1.4f : Type == "trash" ? 1.1f : .85f, nx = x + Mathf.Sin(h) * sp * dt, nz = z + Mathf.Cos(h) * sp * dt;
            if (nx < bd.xMin || nx > bd.xMax || nz < bd.yMin || nz > bd.yMax || Blocked(nx, nz, y)) { th = h + Mathf.PI * (.6f + Random.value * .8f); t = .7f; }
            else { x = nx; z = nz; }
            transform.position = V(x, y, z);
            transform.rotation = Quaternion.Euler(0, h * Mathf.Rad2Deg, 0);

            if (Type == "floor")
            {
                var s = GroundHit(x, z, y, out var pt);
                if (s != null) { s.StampWater(pt, .33f, 22, dt); if (Random.value < .5f) Robots.Splash(V(x, y + .05f, z) + Random.insideUnitSphere * .25f, .5f); }
            }
            else if (Type == "mower")
            {
                if (y < .2f && Job.Grass != null) { int n = Job.Grass.Cut(x, z, Fast ? .5f : .34f); if (n > 0 && Random.value < .6f) Robots.Clippings(V(x, .15f, z)); }
            }
            else if (Type == "trash" && Job.Trash != null)
            {
                if (load >= BotLoad)
                {
                    var b = BinFor(); trip += dt;
                    if (!b.HasValue || new Vector2(b.Value.x - x, b.Value.z - z).magnitude < 1.15f || trip > 30)
                    {
                        load = 0; trip = 0; pause = 1.3f; t = 0;
                        if (b.HasValue) for (int q = 0; q < 6; q++) Robots.Splash(b.Value + V(0, 1, 0), 1.2f, true);
                    }
                    return;
                }
                chase += dt;
                if (ign.Count > 0 && Random.value < dt * .05f) ign.Clear();
                int i = Job.Trash.NearestTo(V(x, y, z), ign);
                if (i >= 0)
                {
                    var it = Job.Trash.Items[i];
                    if ((it.P.x - x) * (it.P.x - x) + (it.P.z - z) * (it.P.z - z) < .2f && Job.Trash.Pick(i))
                    {
                        load++; pause = .75f; chase = 0;
                        int pay = Mathf.RoundToInt(Shop.TrashPay * Job.Mul);
                        Job.Earned += pay; Job.Changed = true;
                        GameManager.I.AddMoney(pay);
                        for (int q = 0; q < 4; q++) Robots.Splash(it.P + V(0, .2f, 0), 1f, true);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ drone
        bool ClearPath(Vector3 a, Vector3 b, DirtSurface tgt)
        {
            var d = b - a; float L = d.magnitude;
            if (L < .01f) return true;
            if (!Physics.Raycast(a, d / L, out var hit, L + .2f, Layers.SprayMask, QueryTriggerInteraction.Ignore)) return tgt == null;
            if (tgt != null && hit.collider.gameObject == tgt.gameObject) return true;
            return hit.distance > L - (tgt != null ? .08f : -.1f);
        }

        bool PickTarget()
        {
            var cands = new List<DirtSurface>();
            foreach (var s in Job.Surfs) if (!s.Done && !s.IsGround && s.Remaining > 0) cands.Add(s);
            if (cands.Count == 0) return false;
            var from = V(x, y, z);
            float bestD = 1e9f; bool found = false; int hits = 0;
            for (int tries = 0; tries < 40 && hits < 6; tries++)
            {
                var S = cands[Random.Range(0, cands.Count)];
                for (int k = 0; k < 300; k++)
                {
                    int i = Random.Range(0, S.N);
                    if (S.Surf[i] != 2 || S.DirtAmt[i] < G.CleanT) continue;
                    int vx = i % S.Nx, vy = (i / S.Nx) % S.Ny, vz = i / (S.Nx * S.Ny);
                    var p = S.transform.TransformPoint(S.G0 + new Vector3(vx + .5f, vy + .5f, vz + .5f) * S.CellSize);
                    var mesh = S.GetComponent<MeshFilter>().sharedMesh;
                    var ext = mesh.bounds.size;
                    Vector3 n;
                    if (Mathf.Min(ext.x, Mathf.Min(ext.y, ext.z)) < .02f) n = S.transform.TransformDirection(mesh.normals[0]).normalized;
                    else
                    {
                        n = p - S.GetComponent<Renderer>().bounds.center;
                        if (Mathf.Abs(n.y) < n.magnitude * .6f) n.y = 0;
                        n.Normalize();
                    }
                    var hv = p + n * 1.3f; hv.y = Mathf.Max(hv.y, .9f);
                    if (!ClearPath(hv, p, S))
                    {
                        var flip = p - n * 1.3f;
                        if (flip.y < .9f || !ClearPath(flip, p, S)) break;
                        hv = flip;
                    }
                    if (!ClearPath(from, hv, null)) break;
                    hits++;
                    float dd = Vector3.Distance(from, hv) + Random.value * 2;
                    if (dd < bestD) { bestD = dd; found = true; tgS = S; tgP = p; hover = hv; }
                    break;
                }
            }
            return found;
        }

        void UpdateDrone(float dt)
        {
            foreach (var r in rotors) r.Rotate(0, dt * 2400, 0, Space.Self);
            if (st == "seek") { st = PickTarget() ? "fly" : "idle"; timer = 2; }
            if (st == "idle") { timer -= dt; if (timer <= 0) st = "seek"; }
            else if (st == "fly")
            {
                var dv = hover - V(x, y, z); float L = dv.magnitude;
                if (L < .15f) { st = "spray"; timer = 4; }
                else
                {
                    var step = dv * Mathf.Min(L, 2.6f * dt) / L;
                    x += step.x; y += step.y; z += step.z;
                    h = Mathf.Atan2(dv.x, dv.z);
                }
            }
            else if (st == "spray")
            {
                h = Mathf.Atan2(tgP.x - x, tgP.z - z);
                timer -= dt;
                if (tgS != null) tgS.StampWater(tgP, .32f, 24, dt);
                Robots.Stream(V(x, y - .04f, z), tgP);
                if (timer <= 0 || tgS == null || tgS.Done || tgS.DirtAt(tgP) < .05f) st = "seek";
            }
            transform.position = V(x, y + Mathf.Sin(Time.time * 3 + z) * .03f, z);
            transform.rotation = Quaternion.Euler(st == "fly" ? 8 : 0, h * Mathf.Rad2Deg, 0);
        }
    }

    public static class Robots
    {
        public static readonly List<Robot> Active = new List<Robot>();
        public static Robot AutoMower;   // the free mower from tool slot 4
        static ParticleSystem fx;

        /// <summary>Key 4: send the free mower out on its own, or call it back.</summary>
        public static void ToggleAutoMower(PlayerController p)
        {
            var gm = GameManager.I;
            if (AutoMower != null)
            {
                Object.Destroy(AutoMower.gameObject); AutoMower = null;
                gm.Toast("자동 잔디 깎기 회수", "", ToastKind.Info);
                gm.Hud?.RefreshTools();
                return;
            }
            if (gm.Job.Grass == null || gm.Job.Grass.Lawn == 0) { gm.Toast("이 현장엔 깎을 잔디가 없어요", "", ToastKind.Info); return; }
            var pp = p.transform.position;
            float a = p.Yaw * Mathf.Deg2Rad;
            var go = new GameObject("auto_mower");
            AutoMower = go.AddComponent<Robot>();
            AutoMower.Fast = true;
            AutoMower.Init("mower", new Vector3(pp.x + Mathf.Sin(a) * 1.2f, 0, pp.z + Mathf.Cos(a) * 1.2f), a);
            gm.Toast("자동 잔디 깎기 출발", "알아서 잔디밭을 돌며 깎아요 · 다시 4를 누르면 회수", ToastKind.Info);
            gm.Hud?.RefreshTools();
        }

        public static int Owned(SaveData s) => s.Robots + s.TrashBots + s.Drones + s.MowBots;

        public static void Toggle(PlayerController p)
        {
            var gm = GameManager.I;
            if (Owned(gm.Save) == 0) { gm.Toast("로봇이 없어요", "장비 가게에서 살 수 있어요", ToastKind.Info); return; }
            if (Active.Count > 0) { RemoveAll(); gm.Toast("로봇 회수", "", ToastKind.Info); gm.Hud?.RefreshTools(); return; }
            var pp = p.transform.position;
            bool onLift = Lift.Active != null && Lift.Active.PlayerOn;
            float fy = pp.y > .15f && !onLift ? pp.y : 0;
            int k = 0;
            void Spawn(string type, int count)
            {
                for (int i = 0; i < count; i++, k++)
                {
                    float a = (p.Yaw + (k - 1.5f) * 43f) * Mathf.Deg2Rad;
                    var pos = new Vector3(pp.x + Mathf.Sin(a) * 1.5f, type == "drone" ? fy + 2.2f : fy, pp.z + Mathf.Cos(a) * 1.5f);
                    var go = new GameObject("robot_" + type);
                    var r = go.AddComponent<Robot>();
                    r.Init(type, pos, a);
                    Active.Add(r);
                }
            }
            Spawn("floor", gm.Save.Robots);
            Spawn("trash", gm.Save.TrashBots);
            Spawn("drone", gm.Save.Drones);
            Spawn("mower", gm.Save.MowBots);
            gm.Toast($"로봇 {Active.Count}대 출동!", "알아서 일하는 중", ToastKind.Info);
            gm.Hud?.RefreshTools();
        }

        public static void RemoveAll()
        {
            foreach (var r in Active) if (r != null) Object.Destroy(r.gameObject);
            Active.Clear();
            if (AutoMower != null) { Object.Destroy(AutoMower.gameObject); AutoMower = null; }
        }

        static ParticleSystem Fx()
        {
            if (fx != null) return fx;
            var go = new GameObject("robotFx");
            fx = go.AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fx.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1;
            var em = fx.emission; em.enabled = false;
            var sh = fx.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fx.Play();
            return fx;
        }

        public static void Splash(Vector3 p, float speed, bool debris = false)
        {
            var c = debris ? new Color(1f, .78f, .45f, .9f) : new Color(.88f, .95f, 1f, .5f);
            Fx().Emit(new ParticleSystem.EmitParams { position = p, velocity = new Vector3(Random.Range(-.5f, .5f), Random.Range(.5f, 1.2f), Random.Range(-.5f, .5f)) * speed, startSize = debris ? .04f : .025f, startLifetime = .5f, startColor = c }, 1);
        }

        public static void Clippings(Vector3 p)
        {
            Fx().Emit(new ParticleSystem.EmitParams { position = p, velocity = new Vector3(Random.Range(-1.2f, 1.2f), .8f, Random.Range(-1.2f, 1.2f)), startSize = .03f, startLifetime = .45f, startColor = new Color(.38f, .62f, .2f, 1) }, 1);
        }

        public static void Stream(Vector3 from, Vector3 to)
        {
            var d = to - from; float L = d.magnitude;
            if (L < .05f) return;
            const float speed = 30;
            for (int i = 0; i < 2; i++)
                Fx().Emit(new ParticleSystem.EmitParams { position = from + Random.insideUnitSphere * .01f, velocity = d / L * speed, startSize = .02f, startLifetime = L / speed, startColor = new Color(.86f, .94f, 1f, .5f) }, 1);
        }
    }
}
