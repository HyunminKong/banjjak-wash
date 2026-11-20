using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Banjjak.Net
{
    /// <summary>
    /// Another player in co-op (and the character preview in 꾸미기): an animal-headed worker built from code.
    /// Both hands hold the tool through two-bone IK (right hand on the gun grip, left on the lance handle, both on the
    /// surface cleaner's bar), the torso turns to the aim and leans with it while the head takes the rest of the pitch,
    /// feet stay planted on a walk cycle, and the water/foam streams come out of the nozzle along the real aim.
    /// </summary>
    public class Avatar : MonoBehaviour
    {
        public string Key, DisplayName;
        public Skins.Look Look;
        public bool Preview;   // menu preview: no network, no label, no spray rays

        // ------------------------------------------------------------------ network state
        struct Snap { public float t; public Vector3 feet; public float yaw, pitch; public bool crouch; }
        readonly List<Snap> snaps = new List<Snap>();
        public int Tool, Deg, Agent;
        public bool Spraying, Turbo, FanV;
        public float LastHeard;

        public void Push(Vector3 feet, float yaw, float pitch, bool crouch)
        {
            snaps.Add(new Snap { t = Time.time, feet = feet, yaw = yaw, pitch = pitch, crouch = crouch });
            if (snaps.Count > 12) snaps.RemoveAt(0);
            LastHeard = Time.time;
            if (snaps.Count == 1) { transform.position = feet; bodyYaw = yaw; curYaw = yaw; curPitch = pitch; lastFeet = feet; }
        }

        // ------------------------------------------------------------------ rig
        Transform hips, chest, headPivot, gun, gunTip, foamBottle, device, deviceSpin, label;
        Seg upperArmL, foreArmL, upperArmR, foreArmR, thighL, shinL, thighR, shinR;
        Transform handL, handR, footL, footR, kneeL, kneeR, elbowL, elbowR;
        ParticleSystem stream, splash, foamPs;
        Material nozzleMat, bottleMat;
        float bodyYaw, curYaw, curPitch, walkPhase, speedSm, crouchSm, deviceSpinA;
        Vector3 lastFeet;
        Text labelText;

        // chubby, big-headed proportions: short legs, thick limbs, round mitts
        const float UpperArm = .27f, ForeArm = .26f, Thigh = .34f, Shin = .33f;
        const float HipY = .72f, CrouchHipY = .48f, HeadScale = 1.25f;

        class Seg { public Transform T; public float R; }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public static Avatar Create(string key, string name, Skins.Look look, bool preview = false)
        {
            var go = new GameObject("Player_" + name);
            var a = go.AddComponent<Avatar>();
            a.Key = key; a.DisplayName = name; a.Look = look; a.Preview = preview;
            a.Build();
            return a;
        }

        public void SetLook(Skins.Look look)
        {
            if (look.ToString() == Look.ToString()) return;
            Look = look;
            foreach (Transform c in transform) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            Build();
            if (Layer >= 0) SetLayer(transform, Layer);
        }

        /// <summary>The 꾸미기 preview lives on its own layer, seen only by the preview camera.</summary>
        public int Layer = -1;
        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }

        GameObject Part(Transform parent, MeshData md, Material m, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject("p");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = md.ToMesh("avatar");
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        Transform Node(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        Seg MakeSeg(Material m, float r)
        {
            var t = new GameObject("seg").transform;
            t.SetParent(transform, false);
            // unit-length tube along +Z, stretched each frame to the bone length
            Part(t, MeshKit.Cylinder(r, r * .92f, 1f, 14, true).Xf(0, 0, .5f, 90), m, Vector3.zero);
            return new Seg { T = t, R = r };
        }

        Transform Ball(Material m, float r, Vector3 scale)
        {
            var t = new GameObject("ball").transform;
            t.SetParent(transform, false);
            Part(t, MeshKit.Sphere(1, 16, 12).Scale(scale * r), m, Vector3.zero);
            return t;
        }

        void Build()
        {
            var an = Skins.Animals[Look.Animal];
            var suitC = Skins.Suits[Look.Suit];
            var suit = Mats.Solid(suitC, .32f);
            var pants = Mats.Solid(suitC * .78f, .28f);
            var band = Mats.Solid(new Color(.93f, .95f, .92f), .75f);
            var fur = Mats.Solid(Look.FurColor, .18f);
            var face = Mats.Solid(an.Face, .2f);
            var noseM = Mats.Solid(an.Nose, .6f);
            var inner = Mats.Solid(an.Inner, .2f);
            var eyeM = Mats.Solid(new Color(.05f, .05f, .06f), .85f);
            var white = Mats.Solid(Color.white, .6f);
            var cheek = Mats.Solid(G.Hex(0xf2a0a8), .2f);
            var boots = Mats.Rubber;

            // hips and legs
            hips = Node("hips", transform, V(0, HipY, 0));
            Part(hips, MeshKit.RoundedBox(.44f, .22f, .33f, .1f, 3), pants, V(0, .02f, 0));
            Part(hips, MeshKit.RoundedBox(.455f, .05f, .34f, .02f, 2), Mats.GunDark, V(0, .12f, 0));   // belt
            Part(hips, MeshKit.RoundedBox(.07f, .06f, .02f, .01f), Mats.Steel, V(0, .12f, .172f));      // buckle
            thighL = MakeSeg(pants, .092f); shinL = MakeSeg(pants, .082f);
            thighR = MakeSeg(pants, .092f); shinR = MakeSeg(pants, .082f);
            kneeL = Ball(pants, .086f, Vector3.one); kneeR = Ball(pants, .086f, Vector3.one);
            footL = Node("footL", transform, Vector3.zero); footR = Node("footR", transform, Vector3.zero);
            foreach (var f in new[] { footL, footR })
            {
                Part(f, MeshKit.RoundedBox(.16f, .13f, .29f, .06f, 3), boots, V(0, .065f, .045f));
                Part(f, MeshKit.Cylinder(.085f, .088f, .13f, 16), boots, V(0, .17f, 0));
            }

            // torso: short and round, pivoting at the waist so it can lean with the aim
            chest = Node("chest", hips, V(0, .08f, 0));
            Part(chest, MeshKit.RoundedBox(.5f, .46f, .37f, .17f, 4), suit, V(0, .23f, 0));
            Part(chest, MeshKit.RoundedBox(.505f, .045f, .375f, .02f, 2), band, V(0, .12f, 0));
            Part(chest, MeshKit.RoundedBox(.48f, .045f, .355f, .02f, 2), band, V(0, .31f, 0));
            Part(chest, MeshKit.RoundedBox(.15f, .16f, .02f, .03f, 2), Mats.Solid(suitC * .85f, .3f), V(.11f, .26f, .185f));   // chest pocket
            upperArmL = MakeSeg(suit, .074f); foreArmL = MakeSeg(suit, .068f);
            upperArmR = MakeSeg(suit, .074f); foreArmR = MakeSeg(suit, .068f);
            elbowL = Ball(suit, .07f, Vector3.one); elbowR = Ball(suit, .07f, Vector3.one);
            handL = Ball(Mats.Glove, .07f, V(1, .88f, 1.15f)); handR = Ball(Mats.Glove, .07f, V(1, .88f, 1.15f));
            foreach (var s in new[] { -1, 1 }) Part(chest, MeshKit.Sphere(.09f, 16, 12), suit, V(s * .25f, .37f, 0));   // shoulders

            // head: big and round
            headPivot = Node("head", chest, V(0, .44f, 0));
            var hd = Node("headMesh", headPivot, V(0, .2f, .01f));
            hd.localScale = Vector3.one * HeadScale;
            Part(hd, MeshKit.Sphere(.205f, 28, 20).Scale(V(1, .94f, .95f)), fur, Vector3.zero);
            BuildFace(hd, an, face, noseM, inner, eyeM, white, cheek, fur);
            BuildHat(hd, suitC);

            BuildGun();
            BuildDevice();
            BuildFx();
            if (!Preview) BuildLabel();
        }

        void BuildFace(Transform hd, Skins.Animal an, Material face, Material noseM, Material inner, Material eyeM, Material white, Material cheek, Material fur)
        {
            // eyes (round, glossy, with a highlight) and blush
            float ey = an.BigEyes ? .14f : .03f, ex = an.BigEyes ? .09f : .072f, ez = an.BigEyes ? .1f : .178f;
            foreach (var s in new[] { -1, 1 })
            {
                if (an.BigEyes)
                {
                    Part(hd, MeshKit.Sphere(.072f, 18, 12), fur, V(s * ex, ey, ez - .02f));
                    Part(hd, MeshKit.Sphere(.052f, 16, 12), white, V(s * ex, ey + .005f, ez + .03f));
                    Part(hd, MeshKit.Sphere(.026f, 12, 8), eyeM, V(s * ex, ey + .005f, ez + .075f));
                }
                else
                {
                    if (an.Patches) Part(hd, MeshKit.Sphere(1, 14, 10).Scale(V(.05f, .065f, .02f)), inner, V(s * ex, ey - .005f, ez - .01f), V(0, 0, s * 25));
                    Part(hd, MeshKit.Sphere(.026f, 12, 8).Scale(V(1, 1.15f, .7f)), an.Patches ? white : eyeM, V(s * ex, ey, ez));
                    if (an.Patches) Part(hd, MeshKit.Sphere(.016f, 10, 8), eyeM, V(s * ex, ey, ez + .012f));
                }
                Part(hd, MeshKit.Sphere(.008f, 8, 6), white, V(s * ex + .008f, ey + .012f, (an.BigEyes ? ez + .095f : ez + .02f)));
                Part(hd, MeshKit.Sphere(1, 12, 8).Scale(V(.034f, .018f, .01f)), cheek, V(s * .118f, -.045f, .16f));
            }
            if (an.Muzzle)
            {
                Part(hd, MeshKit.Sphere(1, 18, 12).Scale(V(.085f, .06f, .055f)), face, V(0, -.055f, .165f));
                Part(hd, MeshKit.Sphere(1, 12, 8).Scale(V(.026f, .019f, .018f)), noseM, V(0, -.032f, .218f));
            }
            if (an.Beak)
            {
                Part(hd, MeshKit.Sphere(1, 18, 12).Scale(V(.15f, .13f, .07f)), face, V(0, -.01f, .135f));
                Part(hd, MeshKit.Cone(.032f, .07f, 14).Xf(0, 0, 0, 90), noseM, V(0, -.05f, .2f));
            }
            switch (an.Ears)
            {
                case "round":
                    foreach (var s in new[] { -1, 1 })
                    {
                        Part(hd, MeshKit.Sphere(.068f, 14, 10).Scale(V(1, 1, .7f)), fur, V(s * .135f, .155f, -.01f));
                        Part(hd, MeshKit.Sphere(.04f, 12, 8).Scale(V(1, 1, .5f)), inner, V(s * .135f, .155f, .025f));
                    }
                    break;
                case "point":
                    foreach (var s in new[] { -1, 1 })
                    {
                        Part(hd, MeshKit.Cone(.06f, .12f, 14).Scale(V(1, 1, .55f)), fur, V(s * .11f, .2f, -.01f), V(0, 0, s * -18));
                        Part(hd, MeshKit.Cone(.036f, .08f, 12).Scale(V(1, 1, .4f)), inner, V(s * .108f, .195f, .02f), V(0, 0, s * -18));
                    }
                    break;
                case "long":
                    foreach (var s in new[] { -1, 1 })
                    {
                        Part(hd, MeshKit.Sphere(1, 14, 12).Scale(V(.045f, .16f, .032f)), fur, V(s * .075f, .3f, -.02f), V(0, 0, s * -9));
                        Part(hd, MeshKit.Sphere(1, 12, 10).Scale(V(.024f, .12f, .012f)), inner, V(s * .077f, .3f, .006f), V(0, 0, s * -9));
                    }
                    break;
                case "flop":
                    foreach (var s in new[] { -1, 1 })
                        Part(hd, MeshKit.Sphere(1, 14, 12).Scale(V(.05f, .11f, .032f)), inner, V(s * .19f, .0f, -.01f), V(0, 0, s * 18));
                    break;
            }
        }

        void BuildHat(Transform hd, Color suitC)
        {
            switch (Look.Hat)
            {
                case 1:   // hard hat
                    {
                        var hat = Mats.Solid(Look.Suit == 0 ? new Color(.95f, .95f, .93f) : G.Hex(0xf5c518), .7f);
                        Part(hd, MeshKit.Sphere(.214f, 24, 12, Mathf.PI / 2).Scale(V(1, .82f, 1)), hat, V(0, .06f, 0));
                        Part(hd, MeshKit.Cylinder(.24f, .24f, .016f, 28), hat, V(0, .062f, .015f));
                        Part(hd, MeshKit.RoundedBox(.03f, .02f, .36f, .008f), hat, V(0, .232f, 0));
                        break;
                    }
                case 2:   // beanie with a pompom
                    {
                        var knit = Mats.Solid(suitC * .9f, .1f);
                        Part(hd, MeshKit.Sphere(.214f, 24, 12, Mathf.PI / 2).Scale(V(1, .78f, 1)), knit, V(0, .055f, 0));
                        Part(hd, MeshKit.Cylinder(.218f, .218f, .055f, 28), Mats.Solid(suitC * .7f, .1f), V(0, .07f, 0));
                        Part(hd, MeshKit.Sphere(.045f, 14, 10), Mats.Solid(Color.white, .1f), V(0, .23f, 0));
                        break;
                    }
                case 3:   // cap
                    {
                        var cap = Mats.Solid(suitC, .3f);
                        Part(hd, MeshKit.Sphere(.21f, 24, 12, Mathf.PI / 2).Scale(V(1, .72f, 1)), cap, V(0, .07f, 0));
                        Part(hd, MeshKit.RoundedBox(.2f, .016f, .15f, .007f), cap, V(0, .075f, .21f), V(-8, 0, 0));
                        break;
                    }
            }
        }

        void BuildGun()
        {
            gun = Node("gun", transform, Vector3.zero);
            Part(gun, MeshKit.RoundedBox(.052f, .075f, .22f, .02f, 3), Mats.GunYellow, Vector3.zero);
            Part(gun, MeshKit.RoundedBox(.046f, .135f, .058f, .018f, 3), Mats.GunDark, V(0, -.085f, -.07f), V(16, 0, 0));
            Part(gun, MeshKit.Cylinder(.0085f, .0085f, .64f, 10).Xf(0, 0, .45f, 90), Mats.Steel, Vector3.zero);
            Part(gun, MeshKit.Cylinder(.019f, .017f, .17f, 14).Xf(0, 0, .25f, 90), Mats.Rubber, Vector3.zero);
            Part(gun, MeshKit.Tube(new[] { V(0, -.17f, -.1f), V(.02f, -.4f, -.15f), V(.05f, -.85f, -.1f) }, .013f, 16, 8, false), Mats.Hose, Vector3.zero);
            nozzleMat = new Material(Mats.NozzleTip);
            Part(gun, MeshKit.Cylinder(.012f, .015f, .045f, 12).Xf(0, 0, .785f, 90), nozzleMat, Vector3.zero);
            gunTip = Node("tip", gun, V(0, 0, .81f));
            foamBottle = Node("bottle", gun, V(0, -.09f, .66f));
            bottleMat = new Material(Mats.FoamBottle);
            Part(foamBottle, MeshKit.Merge(MeshKit.Cylinder(.04f, .04f, .12f, 18), MeshKit.Sphere(.04f, 18, 6, Mathf.PI / 2).Xf(0, .06f, 0), MeshKit.Sphere(.04f, 18, 6, Mathf.PI / 2).Xf(0, -.06f, 0, 180)).Xf(0, 0, 0, 90), bottleMat, Vector3.zero);
        }

        void BuildDevice()
        {
            device = Node("cleaner", transform, Vector3.zero);
            Part(device, MeshKit.Lathe(new float[,] { { .5f, 0 }, { .5f, .06f }, { .44f, .12f }, { .2f, .16f }, { .001f, .17f } }, 32), Mats.DarkMetal, Vector3.zero);
            deviceSpin = Part(device, MeshKit.RoundedBox(.85f, .02f, .06f, .01f), Mats.Steel, V(0, .02f, 0)).transform;
            Part(device, MeshKit.Tube(new[] { V(0, .16f, 0), V(0, .5f, .25f), V(0, .95f, .55f) }, .02f, 14, 8), Mats.Steel, Vector3.zero);
            Part(device, MeshKit.LineTube(V(-.22f, .95f, .55f), V(.22f, .95f, .55f), .022f), Mats.Rubber, Vector3.zero);
            device.gameObject.SetActive(false);
        }

        void BuildFx()
        {
            stream = Fx("stream", .25f, ParticleSystemRenderMode.Stretch, .012f, new Color(.86f, .94f, 1f, .5f));
            splash = Fx("splash", 1f, ParticleSystemRenderMode.Stretch, .015f, new Color(.88f, .95f, 1f, .32f));
            foamPs = Fx("foam", .6f, ParticleSystemRenderMode.Billboard, 0, new Color(1, 1, 1, .9f));
        }

        ParticleSystem Fx(string name, float gravity, ParticleSystemRenderMode mode, float velScale, Color col)
        {
            var go = new GameObject("fx_" + name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0; main.startLifetime = 1; main.startColor = col; main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle;
            r.renderMode = mode; r.velocityScale = velScale; r.lengthScale = 1;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        void BuildLabel()
        {
            var go = new GameObject("label", typeof(RectTransform));
            label = go.transform;
            label.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)label;
            rt.sizeDelta = new Vector2(360, 64);
            label.localScale = Vector3.one * .0028f;
            var bg = new GameObject("bg", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(label, false);
            ((RectTransform)bg.transform).sizeDelta = new Vector2(300, 56);
            bg.color = new Color(0, 0, 0, .45f);
            labelText = new GameObject("t", typeof(RectTransform)).AddComponent<Text>();
            labelText.transform.SetParent(label, false);
            ((RectTransform)labelText.transform).sizeDelta = new Vector2(300, 56);
            labelText.font = Resources.Load<Font>("Fonts/NanumGothic-Bold");
            labelText.fontSize = 34;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = DisplayName;
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            Vector3 feet; float yaw, pitch; bool crouch;
            Sample(out feet, out yaw, out pitch, out crouch);
            transform.position = feet;

            // walking speed from how far the feet moved
            var mv = feet - lastFeet; mv.y = 0;
            float spd = dt > 0 ? mv.magnitude / dt : 0;
            lastFeet = feet;
            speedSm = Mathf.Lerp(speedSm, Mathf.Min(spd, 6), dt * 8);
            crouchSm = Mathf.MoveTowards(crouchSm, crouch ? 1 : 0, dt * 4);

            // the body faces the aim (catching up quickly), so the gun and head stay in front of the chest
            bodyYaw = Mathf.LerpAngle(bodyYaw, yaw, dt * 10);
            transform.rotation = Quaternion.Euler(0, bodyYaw, 0);
            float relYaw = Mathf.DeltaAngle(bodyYaw, yaw);

            // hips: crouch lowers them, walking bobs them
            walkPhase += speedSm * dt * Mathf.PI / .62f;
            bool walking = speedSm > .25f;
            float bob = walking ? Mathf.Abs(Mathf.Sin(walkPhase)) * .035f : 0;
            hips.localPosition = V(0, Mathf.Lerp(HipY, CrouchHipY, crouchSm) + bob, crouchSm * -.06f);
            hips.localRotation = Quaternion.Euler(crouchSm * 8, 0, 0);

            // torso leans with a third of the pitch, the head takes the rest and the leftover yaw
            float lean = Mathf.Clamp(pitch * .3f, -18, 25) + crouchSm * 10;
            chest.localRotation = Quaternion.Euler(lean, relYaw * .4f, 0);
            float headPitch = Mathf.Clamp(pitch - lean - crouchSm * 8, -50, 55);
            headPivot.localRotation = Quaternion.Euler(headPitch, relYaw * .6f, 0);

            // legs: feet planted on a walk cycle, knees forward
            float stride = walking ? Mathf.Min(.2f, speedSm * .055f) : 0;
            PlaceLeg(-1, walkPhase, stride, thighL, shinL, kneeL, footL);
            PlaceLeg(1, walkPhase + Mathf.PI, stride, thighR, shinR, kneeR, footR);

            // tool
            var aim = Quaternion.Euler(pitch, yaw, 0);
            bool cleaner = Tool == 2;
            gun.gameObject.SetActive(!cleaner);
            device.gameObject.SetActive(cleaner);
            foamBottle.gameObject.SetActive(Tool == 1);
            nozzleMat.SetColor("_BaseColor", Turbo ? G.Hex(0x4a4f57) : WasherTool.NozzleColor(Deg));
            if (Tool == 1) bottleMat.SetColor("_BaseColor", Agents.All[Mathf.Clamp(Agent, 0, Agents.All.Count - 1)].Foam);

            Vector3 rTarget, lTarget, rHint, lHint;
            var shoulderC = chest.TransformPoint(V(0, .37f, 0));
            if (!cleaner)
            {
                // the gun sits at the right of the chest and points where the player aims; the right mitt holds the
                // grip, the left one the rubber handle on the lance, elbows out and down
                gun.position = shoulderC + aim * V(.1f, -.17f, .22f) + (Spraying ? aim * V(0, 0, -.01f) : Vector3.zero);
                gun.rotation = aim;
                rTarget = gun.TransformPoint(V(0, -.075f, -.065f));
                lTarget = gun.TransformPoint(V(0, -.005f, .17f));
                rHint = chest.TransformPoint(V(.55f, .0f, -.25f));
                lHint = chest.TransformPoint(V(-.5f, -.1f, .15f));
            }
            else
            {
                var fwd = Quaternion.Euler(0, bodyYaw, 0) * Vector3.forward;
                device.position = feet + fwd * 1.15f + Vector3.up * .005f;
                device.rotation = Quaternion.Euler(0, bodyYaw + 180, 0);
                if (Spraying) { deviceSpinA += dt * 2300; deviceSpin.localRotation = Quaternion.Euler(0, deviceSpinA, 0); }
                rTarget = device.TransformPoint(V(-.17f, .95f, .55f));
                lTarget = device.TransformPoint(V(.17f, .95f, .55f));
                rHint = chest.TransformPoint(V(.5f, -.1f, -.1f));
                lHint = chest.TransformPoint(V(-.5f, -.1f, -.1f));
            }
            PlaceArm(chest.TransformPoint(V(.25f, .37f, 0)), rTarget, rHint, upperArmR, foreArmR, elbowR, handR);
            PlaceArm(chest.TransformPoint(V(-.25f, .37f, 0)), lTarget, lHint, upperArmL, foreArmL, elbowL, handL);

            if (label != null && Camera.main != null)
            {
                label.position = headPivot.position + Vector3.up * .72f;
                label.rotation = Quaternion.LookRotation(label.position - Camera.main.transform.position);
            }
            if (!Preview) SprayFx(dt, aim, feet, crouch);
        }

        void Sample(out Vector3 feet, out float yaw, out float pitch, out bool crouch)
        {
            if (Preview || snaps.Count == 0)
            {
                feet = transform.position; yaw = curYaw; pitch = curPitch; crouch = curCrouch;
                return;
            }
            // play back 0.3 s behind the newest update so there is always a pair to blend between
            float rt = Time.time - .3f;
            int i = snaps.Count - 1;
            while (i > 0 && snaps[i - 1].t > rt) i--;
            Snap a = snaps[Mathf.Max(0, i - 1)], b = snaps[i];
            float f = b.t > a.t ? Mathf.Clamp01((rt - a.t) / (b.t - a.t)) : 1;
            if (rt >= snaps[snaps.Count - 1].t) { a = b = snaps[snaps.Count - 1]; f = 1; }
            feet = Vector3.Lerp(a.feet, b.feet, f);
            yaw = Mathf.LerpAngle(a.yaw, b.yaw, f);
            pitch = Mathf.Lerp(a.pitch, b.pitch, f);
            crouch = f < .5f ? a.crouch : b.crouch;
            curYaw = yaw; curPitch = pitch;
        }

        /// <summary>Pose without network updates (꾸미기 preview, tests).</summary>
        public void Pose(float yaw, float pitch, bool crouch = false) { curYaw = yaw; curPitch = pitch; bodyYaw = yaw; curCrouch = crouch; }
        bool curCrouch;

        void PlaceLeg(int side, float phase, float stride, Seg thigh, Seg shin, Transform knee, Transform foot)
        {
            var hip = hips.TransformPoint(V(side * .12f, -.05f, 0));
            float lift = stride > 0 ? Mathf.Max(0, Mathf.Sin(phase)) * .07f : 0;
            var fLocal = V(side * .13f, lift, Mathf.Cos(phase) * stride + crouchSm * .08f);
            var footPos = transform.TransformPoint(fLocal);
            var hint = hips.TransformPoint(V(side * .12f, -.45f, .8f));
            Solve(hip, footPos + Vector3.up * .1f, hint, Thigh, Shin, out var kneeP, out var ankle);
            Stretch(thigh, hip, kneeP);
            Stretch(shin, kneeP, ankle);
            knee.position = kneeP;
            foot.position = ankle - Vector3.up * .1f;
            foot.rotation = Quaternion.Euler(0, bodyYaw, 0);
        }

        void PlaceArm(Vector3 shoulder, Vector3 target, Vector3 hint, Seg upper, Seg fore, Transform elbow, Transform hand)
        {
            Solve(shoulder, target, hint, UpperArm, ForeArm, out var el, out var wrist);
            Stretch(upper, shoulder, el);
            Stretch(fore, el, wrist);
            elbow.position = el;
            hand.position = wrist;
            hand.rotation = Quaternion.LookRotation(wrist - el, Vector3.up);
        }

        static void Stretch(Seg s, Vector3 a, Vector3 b)
        {
            var d = b - a;
            float len = d.magnitude;
            s.T.position = a;
            if (len > 1e-4f) s.T.rotation = Quaternion.LookRotation(d / len, Mathf.Abs(d.y) / len > .95f ? Vector3.forward : Vector3.up);
            s.T.localScale = new Vector3(1, 1, Mathf.Max(.001f, len));
        }

        /// <summary>Two-bone IK: elbow/knee bends toward the hint; the chain straightens (without stretching) when out of reach.</summary>
        static void Solve(Vector3 a, Vector3 target, Vector3 hint, float l1, float l2, out Vector3 mid, out Vector3 end)
        {
            var d = target - a;
            float len = Mathf.Clamp(d.magnitude, .02f, l1 + l2 - .002f);
            var dir = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.down;
            float cosA = Mathf.Clamp((l1 * l1 + len * len - l2 * l2) / (2 * l1 * len), -1, 1);
            float sinA = Mathf.Sqrt(1 - cosA * cosA);
            var h = hint - a;
            var bend = h - Vector3.Dot(h, dir) * dir;
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.Cross(dir, Vector3.right);
            bend.Normalize();
            mid = a + dir * (cosA * l1) + bend * (sinA * l1);
            end = a + dir * len;
        }

        // ------------------------------------------------------------------ spray visuals (no cleaning: that arrives as cell updates)
        void SprayFx(float dt, Quaternion aim, Vector3 feet, bool crouch)
        {
            if (!Spraying) return;
            var eye = feet + Vector3.up * (crouch ? 1.0f : 1.62f);
            if (Tool == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    float a = Random.value * Mathf.PI * 2;
                    var c = device.position + new Vector3(Mathf.Cos(a), .03f, Mathf.Sin(a)) * .53f;
                    splash.Emit(new ParticleSystem.EmitParams { position = c, velocity = new Vector3(Mathf.Cos(a) * 1.4f, .6f + Random.value, Mathf.Sin(a) * 1.4f), startSize = .012f, startLifetime = .35f }, 1);
                }
                return;
            }
            if (Tool == 1)
            {
                var col = Agents.All[Mathf.Clamp(Agent, 0, Agents.All.Count - 1)].Foam; col.a = .85f;
                for (int k = 0; k < 4; k++)
                {
                    var d = (aim * new Vector3((Random.value - .5f) * .35f, (Random.value - .5f) * .22f, 1)).normalized;
                    var end = Physics.Raycast(eye, d, out var h, 6.5f, Layers.SprayMask, QueryTriggerInteraction.Ignore) ? h.point : eye + d * 6.5f;
                    var delta = end - gunTip.position;
                    float dist = delta.magnitude;
                    foamPs.Emit(new ParticleSystem.EmitParams { position = gunTip.position, velocity = delta / Mathf.Max(.01f, dist) * 11f + Random.insideUnitSphere * .5f, startSize = Random.Range(.03f, .07f), startLifetime = dist / 11f, startColor = col }, 1);
                }
                return;
            }
            int n = Turbo ? 4 : Deg == 0 ? 1 : Mathf.Clamp(Mathf.RoundToInt(Deg / 6f) + 2, 3, 8);
            float sp = Deg * Mathf.Deg2Rad, phase = Time.time * Mathf.PI * 2 * 14, cone = 3.2f * Mathf.Deg2Rad;
            for (int k = 0; k < n; k++)
            {
                Vector3 dl;
                if (Turbo) { float a = phase + k * Mathf.PI * 2 / n; dl = new Vector3(Mathf.Sin(cone) * Mathf.Cos(a), Mathf.Sin(cone) * Mathf.Sin(a), Mathf.Cos(cone)); }
                else { float th = n == 1 ? 0 : -sp / 2 + sp * k / (n - 1); dl = FanV ? new Vector3(0, Mathf.Sin(th), Mathf.Cos(th)) : new Vector3(Mathf.Sin(th), 0, Mathf.Cos(th)); }
                var d = (aim * dl).normalized;
                Vector3 end;
                if (Physics.Raycast(eye, d, out var hit, G.BaseRange, Layers.SprayMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    if (Random.value < .5f)
                        splash.Emit(new ParticleSystem.EmitParams { position = hit.point + hit.normal * .02f, velocity = (Vector3.Reflect(d, hit.normal) * .6f + hit.normal * .5f + Random.insideUnitSphere * .7f).normalized * Random.Range(1.2f, 3f), startSize = Random.Range(.006f, .016f), startLifetime = Random.Range(.2f, .45f) }, 1);
                }
                else end = eye + d * G.BaseRange;
                var from = gunTip.position;
                var delta2 = end - from;
                float dist2 = delta2.magnitude;
                if (dist2 < .05f) continue;
                for (int i = 0; i < 2; i++)
                {
                    float f = Random.value * .25f;
                    stream.Emit(new ParticleSystem.EmitParams { position = from + delta2 * f, velocity = delta2 / dist2 * 42f + Random.insideUnitSphere * .25f, startSize = .022f * Random.Range(.7f, 1.3f), startLifetime = dist2 * (1 - f) / 42f }, 1);
                }
            }
        }
    }
}
