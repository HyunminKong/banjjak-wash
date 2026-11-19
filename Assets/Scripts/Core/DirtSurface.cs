using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    /// <summary>
    /// Voxel dirt mask for one mesh, in the mesh's local space (ported from the web game).
    /// RGBA texture: R = dirt, G = wet, B = foam, A = detergent id. R8 texture: dirt type.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DirtSurface : MonoBehaviour
    {
        public CleanJob Job;
        public Comp Comp;
        public bool IsGround;
        public int Seed;

        [System.NonSerialized] public int Nx, Ny, Nz, N;
        [System.NonSerialized] public float CellSize;
        [System.NonSerialized] public Vector3 G0;
        [System.NonSerialized] public byte[] Data;
        [System.NonSerialized] public byte[] TData;
        [System.NonSerialized] public float[] DirtAmt;
        [System.NonSerialized] public byte[] Type;
        [System.NonSerialized] public byte[] Surf;
        [System.NonSerialized] public float[] Foam, Soak;
        [System.NonSerialized] public byte[] AgentId;
        [System.NonSerialized] public int Total, Remaining, Allow;
        [System.NonSerialized] public int ContactHidden;   // cells left out because something sits right in front of them
        [System.NonSerialized] public bool Done;

        /// <summary>A face with another collider closer than this along its normal can't be reached by the spray.</summary>
        public const float ReachGap = .08f;
        [System.NonSerialized] public bool TexDirty;

        Texture3D tex, ttex;
        MaterialPropertyBlock mpb;
        MeshRenderer rend;
        Mesh mesh;
        Color pc1, pc2;

        static readonly int idMask = Shader.PropertyToID("_Mask3");
        static readonly int idType = Shader.PropertyToID("_Type3");
        static readonly int idG0 = Shader.PropertyToID("_G0");
        static readonly int idGS = Shader.PropertyToID("_GS");
        static readonly int idInv = Shader.PropertyToID("_InvDims");
        static readonly int idPC1 = Shader.PropertyToID("_PC1");
        static readonly int idPC2 = Shader.PropertyToID("_PC2");
        static readonly int idHas = Shader.PropertyToID("_HasDirt");

        public void Setup(CleanJob job, Comp comp, bool ground, float cell, int seed)
        {
            Job = job; Comp = comp; IsGround = ground; Seed = seed;
            mesh = GetComponent<MeshFilter>().sharedMesh;
            rend = GetComponent<MeshRenderer>();
            var bb = mesh.bounds;
            var size = bb.size; var ctr = bb.center;
            float c = cell;
            int nx, ny, nz;
            void Calc() { nx = Mathf.Max(1, Mathf.CeilToInt(size.x / c) + 1); ny = Mathf.Max(1, Mathf.CeilToInt(size.y / c) + 1); nz = Mathf.Max(1, Mathf.CeilToInt(size.z / c) + 1); }
            nx = ny = nz = 1;
            Calc();
            while ((long)nx * ny * nz > G.MaxVoxels) { c *= 1.12f; Calc(); }
            Nx = nx; Ny = ny; Nz = nz; N = nx * ny * nz; CellSize = c;
            G0 = new Vector3(ctr.x - nx * c / 2, ctr.y - ny * c / 2, ctr.z - nz * c / 2);
            Data = new byte[N * 4];
            TData = new byte[N];
            DirtAmt = new float[N];
            Type = new byte[N];
            Surf = new byte[N];
            var rng = job.Rng;
            int pi = rng.Int(Dirt.PaintPairs.GetLength(0));
            pc1 = G.Hex(Dirt.PaintPairs[pi, 0]); pc2 = G.Hex(Dirt.PaintPairs[pi, 1]);
        }

        public Vector3 GridSize => new Vector3(Nx * CellSize, Ny * CellSize, Nz * CellSize);

        /// <summary>Sample the mesh surface, mark visible voxels and generate dirt.</summary>
        public void Init(List<Occluder> occ, bool highOK)
        {
            var mw = transform.localToWorldMatrix;
            var nmat = mw.inverse.transpose;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var wb = rend.bounds;
            float y0 = wb.min.y, ys = Mathf.Max(.05f, wb.size.y);
            var wbx = wb; wbx.Expand(.12f);
            var rel = new List<Occluder>();
            int myId = gameObject.GetInstanceID();
            foreach (var o in occ)
                if (o.Owner != myId && (!o.GroundOnly || IsGround) && o.Bounds.Intersects(wbx)) rel.Add(o);

            // Faces pressed against something else (a wall, a neighbour, a bush) can never be sprayed, so they get no
            // dirt. Only colliders near this surface are worth a ray test.
            var near = new List<Bounds>();
            if (!IsGround)
                foreach (var c in Physics.OverlapBox(wbx.center, wbx.extents, Quaternion.identity, Layers.SprayMask, QueryTriggerInteraction.Ignore))
                    if (c.gameObject != gameObject) { var b = c.bounds; b.Expand(ReachGap * 2 + .02f); near.Add(b); }
            bool Blocked(Vector3 p, Vector3 n)
            {
                bool close = false;
                foreach (var b in near) if (b.Contains(p)) { close = true; break; }
                if (!close) return false;
                return Physics.Raycast(p + n * .004f, n, out var hit, ReachGap, Layers.SprayMask, QueryTriggerInteraction.Ignore)
                    && hit.collider.gameObject != gameObject;
            }
            var contact = new List<int>();

            var rng = new Rng(Noise.HashStr(Job.Id) + (uint)(Seed * 7919));
            // pattern seed per object, so neighbouring zones of one floor join without a seam
            int seed = (int)(Noise.HashStr(Job.Id + Comp.Obj.Name) % 997);
            var spec = Comp.Spec;
            float c2 = CellSize * CellSize;
            var vn = mesh.normals;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 A = verts[tris[t]], B = verts[tris[t + 1]], C = verts[tris[t + 2]];
                Vector3 AB = B - A, AC = C - A;
                var Nn = Vector3.Cross(AB, AC);
                float area = Nn.magnitude / 2;
                if (area < 1e-9f) continue;
                Nn.Normalize();
                // vertex normals decide which side is "outside"
                var avg = vn[tris[t]] + vn[tris[t + 1]] + vn[tris[t + 2]];
                if (Vector3.Dot(avg, Nn) < 0) Nn = -Nn;
                var NW = nmat.MultiplyVector(Nn).normalized;
                int cnt = Mathf.Min(30000, Mathf.Max(1, Mathf.RoundToInt(area / c2 * 1.8f)));
                for (int s = 0; s < cnt; s++)
                {
                    float r1 = rng.Next(), r2 = rng.Next();
                    if (r1 + r2 > 1) { r1 = 1 - r1; r2 = 1 - r2; }
                    var P = A + AB * r1 + AC * r2;
                    int x = Mathf.Clamp(Mathf.FloorToInt((P.x - G0.x) / CellSize), 0, Nx - 1);
                    int y = Mathf.Clamp(Mathf.FloorToInt((P.y - G0.y) / CellSize), 0, Ny - 1);
                    int z = Mathf.Clamp(Mathf.FloorToInt((P.z - G0.z) / CellSize), 0, Nz - 1);
                    int k = x + Nx * (y + Ny * z);
                    if (Surf[k] == 2) continue;
                    var PW = mw.MultiplyPoint3x4(P);
                    bool hidden = (!IsGround && PW.y < .03f) || (NW.y < -.6f && PW.y < 1.05f) || (NW.y > .5f && PW.y > G.HighY && !highOK);
                    if (!hidden) foreach (var o in rel) if (o.Contains(PW)) { hidden = true; break; }
                    if (!hidden && near.Count > 0 && Blocked(PW, NW)) { hidden = true; if (Surf[k] == 0) contact.Add(k); }
                    if (hidden) { if (Surf[k] == 0) Surf[k] = 1; continue; }
                    Surf[k] = 2;
                    var gd = DirtPatterns.Gen(spec, PW, NW, (PW.y - y0) / ys, seed);
                    DirtAmt[k] = gd.amt; Type[k] = gd.type;
                }
            }
            ContactHidden = 0;
            foreach (int k in contact) if (Surf[k] == 1) ContactHidden++;
            Dilate();
            RecountFromDirt();
            CreateTextures();
        }

        /// <summary>Copy dirt into the one-voxel border so trilinear sampling never bleeds clean edges.</summary>
        public void Dilate()
        {
            var add = new List<int>();
            for (int z = 0; z < Nz; z++)
                for (int y = 0; y < Ny; y++)
                    for (int x = 0; x < Nx; x++)
                    {
                        int k = x + Nx * (y + Ny * z);
                        if (Surf[k] == 2) continue;
                        int best = -1; float bd = -1;
                        void Chk(int kk) { if (Surf[kk] == 2 && DirtAmt[kk] > bd) { bd = DirtAmt[kk]; best = kk; } }
                        if (x > 0) Chk(k - 1);
                        if (x < Nx - 1) Chk(k + 1);
                        if (y > 0) Chk(k - Nx);
                        if (y < Ny - 1) Chk(k + Nx);
                        if (z > 0) Chk(k - Nx * Ny);
                        if (z < Nz - 1) Chk(k + Nx * Ny);
                        if (best >= 0) { add.Add(k); add.Add(best); }
                    }
            for (int i = 0; i < add.Count; i += 2)
            {
                int k = add[i], b = add[i + 1];
                DirtAmt[k] = DirtAmt[b]; Type[k] = Type[b];
                if (Surf[k] == 0) Surf[k] = 1;
            }
        }

        public void RecountFromDirt()
        {
            int total = 0, rem = 0;
            for (int k = 0; k < N; k++)
            {
                Data[k * 4] = (byte)(DirtAmt[k] * 255);
                Data[k * 4 + 3] = 25;
                TData[k] = Type[k];
                if (Surf[k] == 2 && DirtAmt[k] >= G.CleanT) rem++;
            }
            total = Total == 0 ? rem : Total;
            Total = total;
            Remaining = rem;
            Allow = Mathf.FloorToInt(total * .035f) + (total >= 8 ? 2 : 0);
            Done = total == 0;
            TexDirty = true;
        }

        void CreateTextures()
        {
            tex = new Texture3D(Nx, Ny, Nz, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name + "_mask" };
            ttex = new Texture3D(Nx, Ny, Nz, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, name = name + "_type" };
            ttex.SetPixelData(TData, 0);
            ttex.Apply(false, false);
            mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetTexture(idMask, tex);
            mpb.SetTexture(idType, ttex);
            mpb.SetVector(idG0, G0);
            mpb.SetVector(idGS, GridSize);
            mpb.SetVector(idInv, new Vector3(1f / Nx, 1f / Ny, 1f / Nz));
            mpb.SetColor(idPC1, pc1.linear);
            mpb.SetColor(idPC2, pc2.linear);
            mpb.SetFloat(idHas, 1f);
            rend.SetPropertyBlock(mpb);
            Upload();
        }

        public void Upload()
        {
            if (tex == null) return;
            tex.SetPixelData(Data, 0);
            tex.Apply(false, false);
            TexDirty = false;
        }

        public int VoxIndex(Vector3 pw)
        {
            var lp = transform.InverseTransformPoint(pw);
            int x = Mathf.Clamp(Mathf.FloorToInt((lp.x - G0.x) / CellSize), 0, Nx - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((lp.y - G0.y) / CellSize), 0, Ny - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((lp.z - G0.z) / CellSize), 0, Nz - 1);
            return x + Nx * (y + Ny * z);
        }

        public float DirtAt(Vector3 pw) => DirtAmt[VoxIndex(pw)];

        /// <summary>Best (dirtiest) voxel near a point, for the target read-out.</summary>
        public int SampleNear(Vector3 pw)
        {
            var lp = transform.InverseTransformPoint(pw);
            int cx = Mathf.FloorToInt((lp.x - G0.x) / CellSize), cy = Mathf.FloorToInt((lp.y - G0.y) / CellSize), cz = Mathf.FloorToInt((lp.z - G0.z) / CellSize);
            int best = -1; float bd = -1;
            for (int z = cz - 1; z <= cz + 1; z++)
                for (int y = cy - 1; y <= cy + 1; y++)
                    for (int x = cx - 1; x <= cx + 1; x++)
                    {
                        if (x < 0 || y < 0 || z < 0 || x >= Nx || y >= Ny || z >= Nz) continue;
                        int k = x + Nx * (y + Ny * z);
                        if (Surf[k] == 0) continue;
                        if (DirtAmt[k] > bd) { bd = DirtAmt[k]; best = k; }
                    }
            return best;
        }

        void EnsureFoam()
        {
            if (Foam != null) return;
            Foam = new float[N]; Soak = new float[N]; AgentId = new byte[N];
        }

        /// <summary>Spray water: removes dirt (faster when soaked), clears foam, wets the surface.</summary>
        public void StampWater(Vector3 pw, float radius, float pwr, float dt)
        {
            var lp = transform.InverseTransformPoint(pw);
            float R = Mathf.Max(.75f, radius * 1.5f / CellSize), r2 = R * R;
            float cx = (lp.x - G0.x) / CellSize - .5f, cy = (lp.y - G0.y) / CellSize - .5f, cz = (lp.z - G0.z) / CellSize - .5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - R)), x1 = Mathf.Min(Nx - 1, Mathf.CeilToInt(cx + R));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - R)), y1 = Mathf.Min(Ny - 1, Mathf.CeilToInt(cy + R));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(cz - R)), z1 = Mathf.Min(Nz - 1, Mathf.CeilToInt(cz + R));
            int cleaned = 0;
            var agents = Agents.All;
            for (int z = z0; z <= z1; z++)
            {
                float dz2 = (z - cz) * (z - cz); if (dz2 > r2) continue;
                for (int y = y0; y <= y1; y++)
                {
                    float dyz = (y - cy) * (y - cy) + dz2; if (dyz > r2) continue;
                    int k = x0 + Nx * (y + Ny * z);
                    for (int x = x0; x <= x1; x++, k++)
                    {
                        byte s = Surf[k]; if (s == 0) continue;
                        float e = ((x - cx) * (x - cx) + dyz) / r2; if (e > 1) continue;
                        if (Foam != null && Foam[k] > 0)
                        {
                            float nf = Foam[k] * Mathf.Exp(-10f * (1 - e * .5f) * dt);
                            Foam[k] = nf < .04f ? 0 : nf;
                            Data[k * 4 + 2] = (byte)(Foam[k] * 255);
                        }
                        float ec = e * 2.25f;
                        if (ec <= 1)
                        {
                            float d = DirtAmt[k];
                            if (d > 0)
                            {
                                float mult = 1;
                                if (Soak != null) { float so = Soak[k]; if (so > 0) mult = 1 + (agents[AgentId[k]].Eff[Type[k]] - 1) * so; }
                                float nd = d * Mathf.Exp(-pwr / Dirt.All[Type[k]].Tough * mult * (1 - ec * .3f) * dt);
                                float val = nd < .004f ? 0 : nd;
                                if (s == 2 && d >= G.CleanT && nd < G.CleanT) cleaned++;
                                DirtAmt[k] = val;
                                Data[k * 4] = (byte)(val * 255 + .5f);
                            }
                        }
                        Data[k * 4 + 1] = 255;
                    }
                }
            }
            TexDirty = true;
            Job.MarkWet(this);
            if (cleaned > 0 && !Done) Job.OnCleaned(this, cleaned);
        }

        public void StampFoam(Vector3 pw, float radius, float amount, int ai)
        {
            EnsureFoam();
            var lp = transform.InverseTransformPoint(pw);
            float R = Mathf.Max(.75f, radius / CellSize), r2 = R * R;
            float cx = (lp.x - G0.x) / CellSize - .5f, cy = (lp.y - G0.y) / CellSize - .5f, cz = (lp.z - G0.z) / CellSize - .5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - R)), x1 = Mathf.Min(Nx - 1, Mathf.CeilToInt(cx + R));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - R)), y1 = Mathf.Min(Ny - 1, Mathf.CeilToInt(cy + R));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(cz - R)), z1 = Mathf.Min(Nz - 1, Mathf.CeilToInt(cz + R));
            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                {
                    int k = x0 + Nx * (y + Ny * z);
                    for (int x = x0; x <= x1; x++, k++)
                    {
                        if (Surf[k] == 0) continue;
                        float e = ((x - cx) * (x - cx) + (y - cy) * (y - cy) + (z - cz) * (z - cz)) / r2; if (e > 1) continue;
                        if (AgentId[k] != ai) { if (Foam[k] > .05f) Foam[k] *= .5f; Soak[k] *= .5f; }
                        Foam[k] = Mathf.Min(1, Foam[k] + amount * (1 - e * .4f));
                        AgentId[k] = (byte)ai;
                        Data[k * 4 + 2] = (byte)(Foam[k] * 255);
                        Data[k * 4 + 3] = (byte)(ai * 51 + 25);
                    }
                }
            TexDirty = true;
            Job.MarkFoamy(this);
        }

        /// <summary>Dry out a little. Returns true while still wet.</summary>
        public bool TickWet()
        {
            bool any = false;
            for (int k = 1; k < Data.Length; k += 4)
            {
                int g = Data[k];
                if (g != 0) { int nv = g > 9 ? g - 9 : 0; Data[k] = (byte)nv; if (nv != 0) any = true; }
            }
            TexDirty = true;
            return any;
        }

        /// <summary>Foam slowly fades while soaking the dirt underneath. Returns true while active.</summary>
        public bool TickFoam()
        {
            if (Foam == null) return false;
            bool any = false;
            var agents = Agents.All;
            for (int k = 0; k < N; k++)
            {
                float v = Foam[k], s = Soak[k];
                if (v > 0)
                {
                    v -= .0025f; if (v < .04f) v = 0;
                    Foam[k] = v; Data[k * 4 + 2] = (byte)(v * 255);
                    if (v > .1f)
                    {
                        s = Mathf.Min(1, s + .04f);
                        float dk = DirtAmt[k];
                        if (dk > .22f)
                        {
                            float nd = Mathf.Max(.22f, dk * Mathf.Exp(-.014f * agents[AgentId[k]].Eff[Type[k]] * v));
                            DirtAmt[k] = nd; Data[k * 4] = (byte)(nd * 255 + .5f);
                        }
                    }
                }
                else if (s > 0) s = Mathf.Max(0, s - .004f);
                Soak[k] = s;
                if (v > 0 || s > 0) any = true;
            }
            TexDirty = true;
            return any;
        }

        /// <summary>Wipe the remaining specks once the surface is within tolerance.</summary>
        public void Finish()
        {
            Done = true;
            for (int k = 0; k < N; k++)
            {
                DirtAmt[k] = 0; Data[k * 4] = 0;
                if (Surf[k] != 0) Data[k * 4 + 1] = 255;
            }
            Remaining = 0;
            TexDirty = true;
            Job.MarkWet(this);
        }

        void OnDestroy()
        {
            if (tex != null) Destroy(tex);
            if (ttex != null) Destroy(ttex);
        }
    }
}
