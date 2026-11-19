using UnityEngine;
using UnityEngine.InputSystem;

namespace Banjjak
{
    /// <summary>Pressure-washer gun (wheel = spray angle) and foam cannon, with water particles and sound.</summary>
    public class WasherTool : MonoBehaviour
    {
        public PlayerController Player;
        public HandRig Rig;
        public int Tool;            // 0 = gun, 1 = foam, 2 = surface cleaner, 3 = mower
        public int GunStep = 6;     // angle = step * 5 degrees, -1 = turbo
        public bool FanVertical;
        public float Soap = 100;
        public bool Spraying;
        public bool ForceSpray;     // used by the automated test
        public float LastNearest = 99;

        ParticleSystem stream, splash, mist, foamPs;
        AudioSource sprayAudio;
        float soapWarnT;
        static readonly Vector3[] dirs = new Vector3[16];

        public float Range => G.BaseRange + (GameManager.I.Owned("ext") ? 2.5f : 0);
        public float SoapMax => GameManager.I.Owned("tank") ? 200 : 100;
        public int Deg => Mathf.Max(0, GunStep) * 5;
        public bool Turbo => GunStep < 0;

        Transform surfaceGo, surfaceSpin;
        static readonly int SolidMask = (1 << Layers.Default) | (1 << Layers.PlayerOnly);

        public static Color NozzleColor(int deg) =>
            deg < 8 ? G.Hex(0xe5484d) : deg < 20 ? G.Hex(0xf5c518) : deg < 33 ? G.Hex(0x30a46c) : G.Hex(0xf2f2f2);

        public void Init(PlayerController p, HandRig rig)
        {
            Player = p; Rig = rig;
            stream = MakePS("stream", Mats.Particle, 0.25f, ParticleSystemRenderMode.Stretch, .012f, new Color(.86f, .94f, 1f, .5f));
            splash = MakePS("splash", Mats.Particle, 1f, ParticleSystemRenderMode.Stretch, .015f, new Color(.88f, .95f, 1f, .32f));
            mist = MakePS("mist", Mats.Mist, -.02f, ParticleSystemRenderMode.Billboard, 0, new Color(.95f, .97f, 1f, .035f));
            foamPs = MakePS("foam", Mats.Particle, .6f, ParticleSystemRenderMode.Billboard, 0, new Color(1, 1, 1, .9f));
            sprayAudio = gameObject.AddComponent<AudioSource>();
            sprayAudio.clip = MakeNoiseClip();
            sprayAudio.loop = true;
            sprayAudio.volume = 0;
            BuildDevices();
            sprayAudio.Play();
            Refresh();
        }

        ParticleSystem MakePS(string name, Material mat, float gravity, ParticleSystemRenderMode mode, float velScale, Color col)
        {
            var go = new GameObject("ps_" + name);
            go.transform.SetParent(transform.parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0;
            main.startLifetime = 1;
            main.startColor = col;
            main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .6f), new GradientAlphaKey(0, 1) });
            col2.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.velocityScale = velScale;
            r.lengthScale = 1;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static AudioClip MakeNoiseClip()
        {
            int rate = 44100, n = rate * 2;
            var data = new float[n];
            float b0 = 0, b1 = 0, lp = 0;
            var rng = new Rng(99u);
            for (int i = 0; i < n; i++)
            {
                float w = rng.Next() * 2 - 1;
                b0 = .99f * b0 + w * .06f;
                b1 = .9f * b1 + w * .3f;
                lp += (w - lp) * .35f;
                data[i] = (b0 * .6f + b1 * .25f + lp * .35f) * .6f;
            }
            // crossfade ends for a seamless loop
            int fade = 2000;
            for (int i = 0; i < fade; i++) { float t = i / (float)fade; data[i] = data[i] * t + data[n - fade + i] * (1 - t); }
            var clip = AudioClip.Create("spray", n - fade, 1, rate, false);
            var trimmed = new float[n - fade];
            System.Array.Copy(data, trimmed, n - fade);
            clip.SetData(trimmed, 0);
            return clip;
        }

        public void SelectTool(int t)
        {
            var def = Shop.Tools[t];
            if (!GameManager.I.Owned(def.Id)) { GameManager.I.Toast($"{def.Name}이(가) 아직 없어요", "장비 가게에서 살 수 있어요", ToastKind.Info); return; }
            if (t == 3) { Robots.ToggleAutoMower(Player); return; }   // slot 4 deploys / recalls the auto mower
            if (t == Tool) return;
            Tool = t;
            GameManager.I.Save.Tool = t;
            if (t == 1 && !foamTip) { foamTip = true; GameManager.I.Toast("거품을 뿌리고 2~3초 기다리세요", "불린 뒤 물로 헹구면 훨씬 빨라요", ToastKind.Info); }
            Refresh();
        }
        bool foamTip;

        public void AdjustAngle(int dir)
        {
            int prev = GunStep;
            int min = GameManager.I.Owned("turbo") ? -1 : 0;
            GunStep = Mathf.Clamp(GunStep + dir, min, 9);
            if (GunStep != prev)
            {
                if (GunStep < 0) GameManager.I.Toast("터보 모드", "물줄기가 원을 그리며 돌아요", ToastKind.Info);
                Refresh();
            }
        }

        public void Refresh()
        {
            var agent = Agents.All[GameManager.I.Save.Agent];
            Rig.SetTool(Tool, Turbo ? G.Hex(0x4a4f57) : NozzleColor(Deg), agent.Foam);
            Rig.gameObject.SetActive(Tool < 2);
            if (surfaceGo != null) surfaceGo.gameObject.SetActive(Tool == 2);
            GameManager.I.Hud?.RefreshTools();
        }

        // ------------------------------------------------------------------ surface cleaner + mower
        GameObject DevPart(Transform parent, MeshData md, Material m, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject("dev");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = md.ToMesh("device");
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        void BuildDevices()
        {
            surfaceGo = new GameObject("SurfaceCleaner").transform;
            DevPart(surfaceGo, MeshKit.Lathe(new float[,] { { .5f, 0 }, { .5f, .06f }, { .44f, .12f }, { .2f, .16f }, { .001f, .17f } }, 40), Mats.DarkMetal, Vector3.zero);
            DevPart(surfaceGo, MeshKit.Cylinder(.505f, .505f, .035f, 40, true).Xf(0, .015f, 0), Mats.Rubber, Vector3.zero);
            surfaceSpin = DevPart(surfaceGo, MeshKit.RoundedBox(.85f, .02f, .06f, .01f), Mats.Steel, V(0, .02f, 0)).transform;
            DevPart(surfaceGo, MeshKit.Tube(new[] { V(0, .16f, 0), V(0, .5f, .25f), V(0, .95f, .55f) }, .02f, 16, 8), Mats.Steel, Vector3.zero);
            DevPart(surfaceGo, MeshKit.LineTube(V(-.22f, .95f, .55f), V(.22f, .95f, .55f), .022f), Mats.Rubber, Vector3.zero);
            DevPart(surfaceGo, MeshKit.Tube(new[] { V(.1f, .16f, .05f), V(.3f, .1f, .6f), V(.5f, .05f, 1.4f) }, .015f, 16, 8, false), Mats.Hose, Vector3.zero);
            surfaceGo.gameObject.SetActive(false);

        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>Place a pushed device on the ground in front of the player; returns the ground hit (if any).</summary>
        bool PlaceDevice(Transform dev, float fwd, out RaycastHit hit)
        {
            var p = Player.transform.position;
            var dir = Quaternion.Euler(0, Player.Yaw, 0) * Vector3.forward;
            var o = p + dir * fwd + Vector3.up * 1.0f;
            bool ok = Physics.Raycast(o, Vector3.down, out hit, 1.35f, Layers.SprayMask | (1 << Layers.PlayerOnly), QueryTriggerInteraction.Ignore);
            float gy = ok ? hit.point.y : p.y;
            dev.position = new Vector3(o.x, gy + .005f, o.z);
            dev.rotation = Quaternion.Euler(0, Player.Yaw + 180, 0);
            return ok;
        }

        void UpdateSurface(float dt, bool active)
        {
            bool big = GameManager.I.Owned("bigdisc");
            float rad = big ? .75f : .5f, fwd = big ? 1.45f : 1.2f;
            surfaceGo.localScale = new Vector3(rad / .5f, 1, rad / .5f);
            bool ok = PlaceDevice(surfaceGo, fwd, out var hit);
            if (!active) return;
            surfaceSpin.Rotate(0, dt * 2300, 0, Space.Self);
            if (ok)
            {
                var S = hit.collider.GetComponent<DirtSurface>();
                if (S != null && S.N > 0 && hit.normal.y > .7f) S.StampWater(hit.point, rad * .85f, Washers.All[GameManager.I.Save.Washer].Power * 6, dt);
                for (int i = 0; i < 3; i++)
                {
                    float a = Random.value * Mathf.PI * 2;
                    var c = surfaceGo.position + new Vector3(Mathf.Cos(a), .03f, Mathf.Sin(a)) * (rad + .03f);
                    splash.Emit(new ParticleSystem.EmitParams { position = c, velocity = new Vector3(Mathf.Cos(a) * 1.4f, .6f + Random.value, Mathf.Sin(a) * 1.4f), startSize = .012f, startLifetime = .35f }, 1);
                }
            }
        }


        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (locked && kb != null && Player.InputEnabled)
            {
                if (kb.digit1Key.wasPressedThisFrame) SelectTool(0);
                if (kb.digit2Key.wasPressedThisFrame) SelectTool(1);
                if (kb.digit3Key.wasPressedThisFrame) SelectTool(2);
                if (kb.digit4Key.wasPressedThisFrame) SelectTool(3);
                if (kb.rKey.wasPressedThisFrame) { FanVertical = !FanVertical; Refresh(); }
                if (kb.qKey.wasPressedThisFrame) GameManager.I.CycleAgent();
            }
            if (locked && mouse != null && Player.InputEnabled)
            {
                float sc = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(sc) > .01f && Tool == 0) AdjustAngle(sc > 0 ? -1 : 1);
            }

            bool fire = ForceSpray || (locked && Player.InputEnabled && mouse != null && mouse.leftButton.isPressed);
            Spraying = false;
            if (GameManager.I.Mode != GameMode.Playing && !ForceSpray) fire = false;
            if (GameManager.I.Job != null)
            {
                if (Tool == 0 && fire) { SprayWater(dt); Spraying = true; }
                else if (Tool == 1 && fire) Spraying = SprayFoam(dt);
                else if (Tool == 2) { UpdateSurface(dt, fire); Spraying = fire; }
            }
            if (!(fire && Tool == 1)) Soap = Mathf.Min(SoapMax, Soap + 7 * dt);

            float targetVol = Spraying ? (Tool == 0 ? .32f : Tool == 3 ? .22f : .2f) : 0;
            sprayAudio.volume = Mathf.MoveTowards(sprayAudio.volume, targetVol, dt * 3);
            float pitch = Tool == 1 ? .55f : Tool == 2 ? .45f : Tool == 3 ? .3f : Turbo ? 1.05f : Deg == 0 ? 1.25f : 1.1f - Deg * .007f;
            if (LastNearest < 2) pitch *= 1.12f;
            sprayAudio.pitch = Mathf.Lerp(sprayAudio.pitch, pitch, dt * 6);
            Rig.Animate(dt, Spraying);
            soapWarnT -= dt;
        }

        float PowerAt(float dist, float mul)
        {
            float R = Range;
            float df = dist <= 2 ? 1 : Mathf.Clamp(1 - (dist - 2) / (R - 2) * .65f, .35f, 1);
            return Washers.All[GameManager.I.Save.Washer].Power * mul * df;
        }

        void SprayWater(float dt)
        {
            var cam = Player.Cam.transform;
            var origin = cam.position;
            var q = cam.rotation;
            int deg = Deg;
            bool turbo = Turbo;
            int n = turbo ? 6 : deg == 0 ? 1 : Mathf.Clamp(Mathf.RoundToInt(deg / 4f) + 2, 3, 13);
            float sp = deg * Mathf.Deg2Rad, mul = turbo ? 2.5f : 3.2f * 7f / (7f + deg);
            var tip = Rig.Tip.position;
            float nearest = 99;
            float phase = Time.time * Mathf.PI * 2 * 14, cone = 3.2f * Mathf.Deg2Rad;
            for (int k = 0; k < n; k++)
            {
                if (turbo)
                {
                    float a = phase + k * Mathf.PI * 2 / n;
                    dirs[k] = new Vector3(Mathf.Sin(cone) * Mathf.Cos(a), Mathf.Sin(cone) * Mathf.Sin(a), Mathf.Cos(cone));
                }
                else
                {
                    float th = n == 1 ? 0 : -sp / 2 + sp * k / (n - 1);
                    th += (Random.value - .5f) * (n == 1 ? .004f : sp / (n - 1) * .6f);
                    float jit = (Random.value - .5f) * (n == 1 ? .004f : .012f);
                    dirs[k] = FanVertical ? new Vector3(jit, Mathf.Sin(th), Mathf.Cos(th)) : new Vector3(Mathf.Sin(th), jit, Mathf.Cos(th));
                }
                var d = (q * dirs[k]).normalized;
                Vector3 end;
                bool hitSomething = Physics.Raycast(origin, d, out var hit, Range, Layers.SprayMask, QueryTriggerInteraction.Ignore);
                if (hitSomething)
                {
                    end = hit.point;
                    nearest = Mathf.Min(nearest, hit.distance);
                    var S = hit.collider.GetComponent<DirtSurface>();
                    if (S != null && S.N > 0)
                    {
                        float dist = hit.distance;
                        float r = turbo ? .07f + dist * .035f : n == 1 ? .05f + dist * .01f : Mathf.Max(.06f, dist * sp / (n - 1) * 1.05f);
                        S.StampWater(hit.point, r, PowerAt(dist, mul), dt);
                    }
                    var nrm = hit.normal;
                    if (Vector3.Dot(nrm, d) > 0) nrm = -nrm;
                    var refl = Vector3.Reflect(d, nrm);
                    if (Random.value < (n == 1 ? 1f : .6f))
                    {
                        var v = (refl * .6f + nrm * .5f + Random.insideUnitSphere * .7f).normalized * Random.Range(1.2f, 3.2f);
                        splash.Emit(new ParticleSystem.EmitParams { position = hit.point + nrm * .02f, velocity = v, startSize = Random.Range(.006f, .016f), startLifetime = Random.Range(.2f, .45f) }, 1);
                    }
                    if (Random.value < .12f)
                        mist.Emit(new ParticleSystem.EmitParams { position = hit.point + nrm * .08f, velocity = nrm * .25f + Random.insideUnitSphere * .2f, startSize = Random.Range(.12f, .28f), startLifetime = Random.Range(.5f, .9f) }, 1);
                }
                else end = origin + d * Range;
                EmitStream(tip, end, n == 1 ? 4 : 2, .022f);
            }
            LastNearest = nearest;
        }

        void EmitStream(Vector3 from, Vector3 to, int count, float size)
        {
            var delta = to - from;
            float dist = delta.magnitude;
            if (dist < .05f) return;
            var dir = delta / dist;
            const float speed = 42f;
            for (int i = 0; i < count; i++)
            {
                float f = Random.value * .25f;
                var p = from + delta * f + Random.insideUnitSphere * .004f;
                float life = dist * (1 - f) / speed;
                stream.Emit(new ParticleSystem.EmitParams { position = p, velocity = dir * speed + Random.insideUnitSphere * .25f, startSize = size * Random.Range(.7f, 1.3f), startLifetime = life }, 1);
            }
        }

        bool SprayFoam(float dt)
        {
            if (Soap <= 0)
            {
                if (soapWarnT <= 0) { GameManager.I.Toast("세제가 다 떨어졌어요", "잠시 쉬면 다시 차요", ToastKind.Info); soapWarnT = 4; }
                return false;
            }
            Soap = Mathf.Max(0, Soap - 8 * dt);
            float R = Mathf.Min(6.5f, Range);
            int ai = GameManager.I.Save.Agent;
            var cam = Player.Cam.transform;
            var tip = Rig.Tip.position;
            var foamCol = Agents.All[ai].Foam;
            foreach (float a in new[] { -11f, 0, 11f })
                foreach (float b in new[] { -7f, 0, 7f })
                {
                    float ax = (a + (Random.value - .5f) * 5) * Mathf.Deg2Rad, by = (b + (Random.value - .5f) * 5) * Mathf.Deg2Rad;
                    var d = (cam.rotation * new Vector3(Mathf.Sin(ax), Mathf.Sin(by), 1)).normalized;
                    Vector3 end;
                    if (Physics.Raycast(cam.position, d, out var hit, R, Layers.SprayMask, QueryTriggerInteraction.Ignore))
                    {
                        end = hit.point;
                        var S = hit.collider.GetComponent<DirtSurface>();
                        if (S != null && S.N > 0) S.StampFoam(hit.point, Mathf.Max(.15f, hit.distance * .14f), 3.5f * dt, ai);
                    }
                    else end = cam.position + d * R;
                    var delta = end - tip;
                    float dist = delta.magnitude;
                    float speed = 11f;
                    var v = delta / Mathf.Max(.01f, dist) * speed;
                    var c = foamCol; c.a = .85f;
                    foamPs.Emit(new ParticleSystem.EmitParams { position = tip + Random.insideUnitSphere * .01f, velocity = v + Random.insideUnitSphere * .5f, startSize = Random.Range(.03f, .07f), startLifetime = dist / speed, startColor = c }, 1);
                }
            return true;
        }
    }
}
