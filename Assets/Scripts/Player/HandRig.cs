using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    /// <summary>First-person gloved hands holding the pressure-washer gun, rendered by an overlay camera.</summary>
    public class HandRig : MonoBehaviour
    {
        public Transform Gun, Tip, FoamBottle;
        public PlayerController Player;
        MeshRenderer nozzleRend, bottleRend;
        Material nozzleMat, bottleMat;
        Vector3 basePos = new Vector3(.185f, -.23f, .56f);
        Quaternion baseRot;
        float kick;

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public void Build(PlayerController player)
        {
            Player = player;
            Gun = new GameObject("Gun").transform;
            Gun.SetParent(transform, false);
            // lance points at the crosshair roughly 3 m ahead
            baseRot = Quaternion.LookRotation((new Vector3(-.02f, -.12f, 4f) - basePos).normalized, Vector3.up) * Quaternion.Euler(0, 0, -6);
            Gun.localPosition = basePos;
            Gun.localRotation = baseRot;

            // --- gun body ---
            Part(Gun, MeshKit.RoundedBox(.052f, .075f, .22f, .02f, 3), Mats.GunYellow, V(0, 0, 0));
            Part(Gun, MeshKit.RoundedBox(.056f, .02f, .2f, .008f), Mats.GunDark, V(0, .042f, 0));
            Part(Gun, MeshKit.RoundedBox(.046f, .135f, .058f, .018f, 3), Mats.GunDark, V(0, -.085f, -.07f), V(16, 0, 0));
            Part(Gun, MeshKit.RoundedBox(.016f, .06f, .022f, .007f), Mats.GunDark, V(0, -.058f, -.012f), V(12, 0, 0));
            Part(Gun, MeshKit.Tube(new[] { V(0, -.035f, .03f), V(0, -.085f, .02f), V(0, -.12f, -.03f) }, .006f, 12), Mats.GunDark, Vector3.zero);
            // hose from the bottom of the grip, sagging out of view
            Part(Gun, MeshKit.Cylinder(.014f, .014f, .04f, 14).Xf(0, -.16f, -.1f, 16), Mats.Steel, Vector3.zero);
            Part(Gun, MeshKit.Tube(new[] { V(0, -.18f, -.105f), V(.01f, -.3f, -.12f), V(.06f, -.5f, -.05f), V(.12f, -.75f, .1f) }, .013f, 24, 10, false), Mats.Hose, Vector3.zero);

            // --- lance ---
            Part(Gun, MeshKit.Cylinder(.016f, .016f, .05f, 16).Xf(0, 0, .13f, 90), Mats.GunDark, Vector3.zero);
            Part(Gun, MeshKit.Cylinder(.0085f, .0085f, .64f, 12).Xf(0, 0, .45f, 90), Mats.Steel, Vector3.zero);
            Part(Gun, MeshKit.Cylinder(.019f, .017f, .17f, 18).Xf(0, 0, .25f, 90), Mats.Rubber, Vector3.zero);
            var noz = Part(Gun, MeshKit.Cylinder(.012f, .015f, .045f, 16).Xf(0, 0, .785f, 90), Mats.NozzleTip, Vector3.zero);
            nozzleRend = noz.GetComponent<MeshRenderer>();
            nozzleMat = new Material(Mats.NozzleTip);
            nozzleRend.sharedMaterial = nozzleMat;
            Tip = new GameObject("Tip").transform;
            Tip.SetParent(Gun, false);
            Tip.localPosition = V(0, 0, .81f);

            // foam cannon bottle (visible with the foam tool)
            FoamBottle = new GameObject("FoamBottle").transform;
            FoamBottle.SetParent(Gun, false);
            FoamBottle.localPosition = V(0, -.055f, .66f);
            var bottle = MeshKit.Merge(MeshKit.Cylinder(.04f, .04f, .12f, 24), MeshKit.Sphere(.04f, 24, 8, Mathf.PI / 2).Xf(0, .06f, 0), MeshKit.Sphere(.04f, 24, 8, Mathf.PI / 2).Xf(0, -.06f, 0, 180));
            var b = Part(FoamBottle, bottle.Xf(0, 0, 0, 90), Mats.FoamBottle, V(0, -.035f, 0));
            bottleRend = b.GetComponent<MeshRenderer>();
            bottleMat = new Material(Mats.FoamBottle);
            bottleRend.sharedMaterial = bottleMat;
            Part(FoamBottle, MeshKit.Cylinder(.012f, .012f, .03f, 12), Mats.GunDark, V(0, -.005f, .02f));

            // --- hands ---
            BuildRightHand();
            BuildLeftHand();

            SetLayerRecursive(transform, Layers.Rig);
        }

        void BuildRightHand()
        {
            var h = new GameObject("RightHand").transform;
            h.SetParent(Gun, false);
            h.localPosition = V(0, -.085f, -.07f);
            h.localRotation = Quaternion.Euler(16, 0, 0);
            var glove = new MeshData();
            // palm wraps the grip from the right side and behind
            glove.Add(MeshKit.Sphere(1, 20, 14).Scale(V(.034f, .058f, .044f)).Xf(.03f, .005f, -.012f));
            glove.Add(MeshKit.Sphere(1, 16, 12).Scale(V(.03f, .034f, .036f)).Xf(.018f, -.035f, -.03f));
            // four fingers curl around the front of the grip (from right side to left side)
            for (int i = 0; i < 4; i++)
            {
                float y = .042f - i * .024f;
                float r = .0105f - i * .0006f;
                glove.Add(MeshKit.Tube(new[] { V(.034f, y, .008f), V(.026f, y - .002f, .04f), V(-.004f, y - .004f, .05f), V(-.03f, y - .004f, .03f), V(-.034f, y - .002f, .006f) }, r, 16, 10));
            }
            // thumb over the top-left of the grip
            glove.Add(MeshKit.Tube(new[] { V(.03f, .045f, -.03f), V(.01f, .07f, -.02f), V(-.02f, .075f, .0f), V(-.03f, .066f, .02f) }, .012f, 14, 10));
            // back of the hand on the outside of the grip
            glove.Add(MeshKit.Sphere(1, 18, 12).Scale(V(.022f, .05f, .04f)).Xf(.04f, .01f, .0f));
            Part(h, glove, Mats.Glove, Vector3.zero);
            Arm(h, V(.03f, -.07f, -.035f), V(.36f, -.62f, .22f), .036f);
        }

        /// <summary>Glove cuff + sleeve from the wrist to a point off screen (given in camera space).</summary>
        void Arm(Transform hand, Vector3 wrist, Vector3 camTarget, float r)
        {
            var target = hand.InverseTransformPoint(transform.TransformPoint(camTarget));
            var dir = (target - wrist).normalized;
            var cuffEnd = wrist + dir * .05f;
            Part(hand, MeshKit.Tube(new[] { wrist - dir * .01f, cuffEnd }, r * 1.08f, 4, 18, true), Mats.Glove, Vector3.zero);
            var mid = Vector3.Lerp(cuffEnd, target, .45f) + Vector3.down * .02f;
            Part(hand, MeshKit.Tube(new[] { cuffEnd, mid, target }, r * 1.3f, 12, 18, false), Mats.Sleeve, Vector3.zero);
        }

        void BuildLeftHand()
        {
            var h = new GameObject("LeftHand").transform;
            h.SetParent(Gun, false);
            h.localPosition = V(0, 0, .25f);
            var glove = new MeshData();
            // palm under/left of the lance handle
            glove.Add(MeshKit.Sphere(1, 20, 14).Scale(V(.03f, .042f, .055f)).Xf(-.026f, -.026f, -.005f));
            // fingers wrap over the top from the left side to the right
            for (int i = 0; i < 4; i++)
            {
                float z = .036f - i * .024f;
                float r = .0105f - i * .0006f;
                glove.Add(MeshKit.Tube(new[] { V(-.03f, -.004f, z), V(-.024f, .022f, z), V(.0f, .03f, z - .002f), V(.022f, .018f, z - .004f), V(.026f, -.002f, z - .004f) }, r, 16, 10));
            }
            // thumb along the left-bottom
            glove.Add(MeshKit.Tube(new[] { V(-.03f, -.04f, -.03f), V(-.018f, -.03f, .01f), V(-.004f, -.024f, .04f) }, .012f, 12, 10));
            // back of the hand on the left of the lance
            glove.Add(MeshKit.Sphere(1, 18, 12).Scale(V(.02f, .036f, .05f)).Xf(-.036f, -.012f, 0));
            Part(h, glove, Mats.Glove, Vector3.zero);
            Arm(h, V(-.035f, -.055f, -.05f), V(-.04f, -.62f, .36f), .034f);
        }

        static GameObject Part(Transform parent, MeshData md, Material m, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject("p");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = md.ToMesh("rig");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayerRecursive(c, layer);
        }

        public void SetTool(int tool, Color nozzleColor, Color foamColor)
        {
            FoamBottle.gameObject.SetActive(tool == 1);
            nozzleMat.SetColor("_BaseColor", nozzleColor);
            bottleMat.SetColor("_BaseColor", foamColor);
        }

        public void Animate(float dt, bool spraying)
        {
            float t = Time.time;
            float bob = Player != null && Player.Moving ? Player.BobPhase : 0;
            kick = Mathf.MoveTowards(kick, spraying ? 1 : 0, dt * 8);
            var sway = new Vector3(Mathf.Sin(t * .9f) * .002f + Mathf.Sin(bob * .5f) * .006f, Mathf.Sin(t * 1.3f) * .0015f + Mathf.Abs(Mathf.Cos(bob * .5f)) * .005f, 0);
            var jitter = spraying ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * .0008f : Vector3.zero;
            Gun.localPosition = basePos + sway + jitter + new Vector3(0, 0, -.012f * kick);
            Gun.localRotation = baseRot * Quaternion.Euler(-1.5f * kick, 0, 0);
        }
    }
}
