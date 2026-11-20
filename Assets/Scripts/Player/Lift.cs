using UnityEngine;
using UnityEngine.InputSystem;

namespace Banjjak
{
    /// <summary>Scissor lift: L to place / pick up, stand on it and hold Space to raise, C to lower.</summary>
    public class Lift : MonoBehaviour
    {
        public static Lift Active;
        public float H;
        const float MaxH = 4.2f, Base = .42f, HalfX = .78f, HalfZ = .53f;
        public float Top => Base + H;

        PlayerController player;
        CharacterController cc;
        Transform plat;
        readonly Transform[] bars = new Transform[12];
        BoxCollider platCol;
        readonly BoxCollider[] rails = new BoxCollider[4];

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public static void Toggle(PlayerController p, CleanJob job)
        {
            var gm = GameManager.I;
            if (Active != null)
            {
                if (Active.PlayerOn) { gm.Toast("작업대에서 내려온 뒤 회수하세요", "", ToastKind.Info); return; }
                var dp = p.transform.position - Active.transform.position; dp.y = 0;
                if (dp.magnitude > 4) { gm.Toast("작업대 가까이에서 L을 누르세요", "", ToastKind.Info); return; }
                Destroy(Active.gameObject); Active = null;
                gm.Toast("작업대 회수", "", ToastKind.Info);
                return;
            }
            var fwd = Quaternion.Euler(0, p.Yaw, 0) * Vector3.forward;
            var pos = p.transform.position + fwd * 1.7f;
            var b = job.Bounds;
            pos.x = Mathf.Clamp(pos.x, b.xMin + .8f, b.xMax - .8f);
            pos.z = Mathf.Clamp(pos.z, b.yMin + .6f, b.yMax - .6f);
            pos.y = p.transform.position.y;
            var rot = Quaternion.Euler(0, p.Yaw, 0);
            int mask = (1 << Layers.Default) | (1 << Layers.PlayerOnly);
            if (Physics.CheckBox(pos + V(0, 1.05f, 0), V(HalfX, .95f, HalfZ), rot, mask, QueryTriggerInteraction.Ignore))
            {
                gm.Toast("여기는 작업대를 놓을 공간이 없어요", "", ToastKind.Info);
                return;
            }
            var go = new GameObject("Lift");
            go.transform.SetPositionAndRotation(pos, rot);
            Active = go.AddComponent<Lift>();
            Active.Build(p);
            gm.Toast("작업대 설치", "올라타서 Space로 올리고 C로 내려요", ToastKind.Info);
        }

        public static void Remove()
        {
            if (Active != null) { Destroy(Active.gameObject); Active = null; }
        }

        /// <summary>Co-op: a friend's lift, shown at the place and height they report (no colliders, no controls).</summary>
        public static Lift Ghost()
        {
            var go = new GameObject("FriendLift");
            var l = go.AddComponent<Lift>();
            l.ghost = true;
            l.Build(null);
            return l;
        }
        bool ghost;

        void Build(PlayerController p)
        {
            player = p;
            cc = p != null ? p.GetComponent<CharacterController>() : null;
            var yellow = Mats.Yellow;
            Part(MeshKit.RoundedBox(1.6f, .22f, 1.1f, .04f), Mats.DarkMetal, V(0, .17f, 0));
            foreach (var w in new[] { V(-.62f, .08f, -.5f), V(.62f, .08f, -.5f), V(-.62f, .08f, .5f), V(.62f, .08f, .5f) })
                Part(MeshKit.Cylinder(.08f, .08f, .06f, 14).Xf(0, 0, 0, 90), Mats.Rubber, w);
            for (int i = 0; i < 12; i++) bars[i] = Part(MeshKit.RoundedBox(1.4f, .05f, .04f, .015f), yellow, Vector3.zero).transform;
            plat = new GameObject("platform").transform;
            plat.SetParent(transform, false);
            var deck = Part(MeshKit.RoundedBox(1.56f, .07f, 1.06f, .02f), Mats.Diamond, Vector3.zero);
            deck.transform.SetParent(plat, false);
            var rail = new MeshData();
            foreach (var c in new[] { V(-.76f, 0, -.51f), V(.76f, 0, -.51f), V(.76f, 0, .51f), V(-.76f, 0, .51f) }) rail.Add(MeshKit.Cylinder(.02f, .02f, 1.0f, 8).Xf(c.x, .5f, c.z));
            rail.Add(MeshKit.LineTube(V(-.76f, 1, -.51f), V(.76f, 1, -.51f), .022f));
            rail.Add(MeshKit.LineTube(V(.76f, 1, -.51f), V(.76f, 1, .51f), .022f));
            rail.Add(MeshKit.LineTube(V(.76f, 1, .51f), V(-.76f, 1, .51f), .022f));
            rail.Add(MeshKit.LineTube(V(-.76f, .5f, -.51f), V(.76f, .5f, -.51f), .018f));
            var rg = Part(rail, yellow, Vector3.zero);
            rg.transform.SetParent(plat, false);
            if (ghost) { UpdateVisual(); return; }

            var colGo = new GameObject("platformCollider");
            colGo.transform.SetParent(plat, false);
            colGo.layer = Layers.PlayerOnly;
            platCol = colGo.AddComponent<BoxCollider>();
            platCol.center = V(0, -.05f, 0);
            platCol.size = V(HalfX * 2, .1f, HalfZ * 2);
            var railDefs = new[] { (V(0, .55f, -HalfZ), V(HalfX * 2, 1.1f, .06f)), (V(0, .55f, HalfZ), V(HalfX * 2, 1.1f, .06f)), (V(-HalfX, .55f, 0), V(.06f, 1.1f, HalfZ * 2)), (V(HalfX, .55f, 0), V(.06f, 1.1f, HalfZ * 2)) };
            for (int i = 0; i < 4; i++)
            {
                var r = new GameObject("rail" + i);
                r.transform.SetParent(plat, false);
                r.layer = Layers.PlayerOnly;
                rails[i] = r.AddComponent<BoxCollider>();
                rails[i].center = railDefs[i].Item1;
                rails[i].size = railDefs[i].Item2;
                rails[i].enabled = false;
            }
            UpdateVisual();
        }

        GameObject Part(MeshData md, Material m, Vector3 pos)
        {
            var go = new GameObject("part");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = md.ToMesh("lift");
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        void UpdateVisual()
        {
            plat.localPosition = V(0, Top, 0);
            float y0 = .28f, span = Top - .04f - y0, st = span / 3;
            float ang = Mathf.Asin(Mathf.Clamp(st / 1.4f, 0, .999f));
            int i = 0;
            foreach (float sz in new[] { -.45f, .45f })
                for (int k = 0; k < 3; k++)
                    foreach (int s in new[] { 1, -1 })
                    {
                        var b = bars[i++];
                        b.localPosition = V(0, y0 + st * (k + .5f), sz);
                        b.localRotation = Quaternion.Euler(0, 0, s * ang * Mathf.Rad2Deg);
                    }
        }

        public void SetHeight(float h) { H = Mathf.Clamp(h, 0, MaxH); UpdateVisual(); }

        public bool PlayerOn
        {
            get
            {
                if (player == null) return false;
                var lp = transform.InverseTransformPoint(player.transform.position);
                return Mathf.Abs(lp.x) < HalfX + .1f && Mathf.Abs(lp.z) < HalfZ + .1f && Mathf.Abs(lp.y - Top) < .2f;
            }
        }

        void Update()
        {
            if (player == null) return;
            bool on = PlayerOn;
            foreach (var r in rails) r.enabled = on && H > .3f;
            var kb = Keyboard.current;
            if (!on || kb == null || !player.InputEnabled || Cursor.lockState != CursorLockMode.Locked) return;
            float dt = Mathf.Min(Time.deltaTime, .05f), before = H;
            if (kb.spaceKey.isPressed) H = Mathf.Min(MaxH, H + 1.1f * dt);
            if (kb.cKey.isPressed) H = Mathf.Max(0, H - 1.1f * dt);
            if (H != before)
            {
                float dh = H - before;
                if (dh > 0) cc.Move(Vector3.up * dh);
                UpdateVisual();
            }
        }
    }
}
