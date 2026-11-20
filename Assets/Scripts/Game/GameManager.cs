using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Banjjak
{
    [Serializable]
    public class SaveData
    {
        public float Money;
        public int Washer;
        public int Agent;
        public List<string> Done = new List<string>();
        public string Current = "patio";
        public List<string> Owned = new List<string>();
        public int Robots, TrashBots, Drones, MowBots;
        public int Tool;
        public List<JobProg> Prog = new List<JobProg>();
    }

    [Serializable]
    public class JobProg
    {
        public string Id;
        public float P;
    }

    public enum GameMode { Menu, Playing, Paused, Result }

    /// <summary>Boots the game: environment, jobs, player, HUD, menu flow, saving and the automated test run.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public SaveData Save = new SaveData();
        public CleanJob Job;
        public int JobIndex = -1;
        public Hud Hud;
        public PlayerController Player;
        public WasherTool Washer;
        public Camera Cam;
        public GameMode Mode = GameMode.Menu;

        ReflectionProbe probe;
        float saveT;
        public bool Highlight;

        /// <summary>F: show remaining dirt glowing orange plus on-screen markers (toggle).</summary>
        public void ToggleHighlight()
        {
            Highlight = !Highlight;
            Shader.SetGlobalFloat("_DirtHL", Highlight ? 1 : 0);
            Toast(Highlight ? "남은 때 표시 켬" : "남은 때 표시 끔", Highlight ? "주황색으로 빛나는 곳이 아직 더러운 곳이에요" : "", ToastKind.Info);
        }
        string autotestDir, reachBakeDir;
        readonly StringBuilder testLog = new StringBuilder();

        void Awake()
        {
            I = this;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // browser: let requestAnimationFrame pace frames and go a little easier on the GPU
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                {
                    urp.msaaSampleCount = 2;
                    urp.shadowDistance = 32;
                }
            }
            else
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = 144;
            }
            LoadSave();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-autotest") autotestDir = args[i + 1];
            // -bakereach <dir>: work out every job's unreachable dirt cells and write them there (Assets/Resources/Reach)
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-bakereach") reachBakeDir = args[i + 1];
            if (autotestDir != null && reachBakeDir != null) CleanJob.UseReachBake = false;
            if (autotestDir != null) Application.logMessageReceived += (msg, stack, type) => { if (type == LogType.Error || type == LogType.Exception) testLog.AppendLine("[" + type + "] " + msg + "\n" + stack); };

            Physics.IgnoreLayerCollision(Layers.Player, Layers.SprayOnly, true);
            Physics.IgnoreLayerCollision(Layers.Player, Layers.Rig, true);

            float t0 = Time.realtimeSinceStartup;
            Mats.Init();
            Dirt.PushShaderGlobals();
            testLog.AppendLine($"materials {Time.realtimeSinceStartup - t0:0.00}s");
            SetupEnvironment();
            SpawnPlayer();
            var hudGo = new GameObject("HudRoot");
            Hud = hudGo.AddComponent<Hud>();
            Hud.TestMode = autotestDir != null;
            int start = Mathf.Max(0, JobDefs.IndexOf(Save.Current));
            StartJob(start);
            Hud.Build(this);
            if (Save.Tool > 0 && Save.Tool < 3 && Owned(Shop.Tools[Save.Tool].Id)) Washer.SelectTool(Save.Tool);
            if (!Owned(Agents.All[Save.Agent].Id)) Save.Agent = 0;
            Washer.Refresh();
            SetMode(GameMode.Menu);
            if (autotestDir != null) StartCoroutine(AutoTest());
            else if (CloudConfig.Enabled && PlayerPrefs.HasKey("banjjak-refresh")) StartCoroutine(AutoLogin());
        }

        // ------------------------------------------------------------------ shop
        public bool Owned(string id) => Shop.CostOf(id) == 0 || Save.Owned.Contains(id);

        public int BotCount(string key) => key == "robots" ? Save.Robots : key == "trashBots" ? Save.TrashBots : key == "drones" ? Save.Drones : Save.MowBots;

        /// <summary>Buy an item: "washer", "bot:&lt;key&gt;", or a tool/upgrade/detergent id.</summary>
        public bool Buy(string id)
        {
            if (id == "washer")
            {
                if (Save.Washer + 1 >= Washers.All.Length) return false;
                var nx = Washers.All[Save.Washer + 1];
                if (Save.Money < nx.Cost) return false;
                Save.Money -= nx.Cost; Save.Washer++;
            }
            else if (id.StartsWith("bot:"))
            {
                string key = id.Substring(4);
                var b = System.Array.Find(Shop.Bots, x => x.Key == key);
                if (b == null || BotCount(key) >= b.Max || Save.Money < b.Cost) return false;
                Save.Money -= b.Cost;
                if (key == "robots") Save.Robots++; else if (key == "trashBots") Save.TrashBots++; else if (key == "drones") Save.Drones++; else Save.MowBots++;
            }
            else
            {
                int cost = Shop.CostOf(id);
                if (Owned(id) || Save.Money < cost) return false;
                Save.Money -= cost; Save.Owned.Add(id);
            }
            PersistSave();
            Ding(false);
            Washer.Refresh();
            return true;
        }

        public void CycleAgent()
        {
            var list = new List<int>();
            for (int i = 0; i < Agents.All.Count; i++) if (Owned(Agents.All[i].Id)) list.Add(i);
            int k = list.IndexOf(Save.Agent);
            Save.Agent = list[(k + 1) % list.Count];
            Washer.Refresh();
            var a = Agents.All[Save.Agent];
            Toast("세제: " + a.Name, a.Desc, ToastKind.Info);
        }

        // ------------------------------------------------------------------ trash, weeds, bins (E)
        public int Bag;
        public int BagCap => Owned("bigbag") ? 35 : 15;

        Vector3? NearestBin()
        {
            var p = Player.transform.position;
            foreach (var b in Job.Bins)
                if (new Vector2(b.x - p.x, b.z - p.z).magnitude < 2.3f && Mathf.Abs(b.y - p.y) < 1.2f) return b;
            return null;
        }

        public (string kind, int index) FindUse()
        {
            if (Job == null) return (null, -1);
            var cp = Cam.transform.position; var dir = Cam.transform.forward;
            int best = -1; float bt = 9;
            if (Job.Trash != null)
                for (int i = 0; i < Job.Trash.N; i++)
                {
                    var it = Job.Trash.Items[i];
                    if (it.Picked) continue;
                    var o = it.P + Vector3.up * .06f - cp; float t = Vector3.Dot(o, dir);
                    if (t < .3f || t > 2.9f) continue;
                    if ((o - dir * t).magnitude < .3f && t < bt) { bt = t; best = i; }
                }
            if (best >= 0) return ("trash", best);
            if (Job.Weeds != null)
                for (int i = 0; i < Job.Weeds.N; i++)
                {
                    if (Job.Weeds.Pulled[i]) continue;
                    var o = Job.Weeds.Items[i].p + Vector3.up * .06f - cp; float t = Vector3.Dot(o, dir);
                    if (t < .3f || t > 2.9f) continue;
                    if ((o - dir * t).magnitude < .32f && t < bt) { bt = t; best = i; }
                }
            if (best >= 0) return ("weed", best);
            if (Bag > 0 && NearestBin().HasValue) return ("bin", 0);
            return (null, -1);
        }

        public string UseHint()
        {
            var (kind, _) = FindUse();
            return kind == "trash" ? (Bag >= BagCap ? "봉투가 가득 찼어요 · 쓰레기통에서 E" : "E 쓰레기 줍기") : kind == "weed" ? "E 잡초 뽑기" : kind == "bin" ? $"E 쓰레기 버리기 ({Bag}개)" : "";
        }

        public void DoUse()
        {
            var (kind, i) = FindUse();
            if (kind == "trash")
            {
                if (Bag >= BagCap) { Toast("봉투가 가득 찼어요", "쓰레기통 앞에서 E로 비우세요", ToastKind.Info); return; }
                var p = Job.Trash.Items[i].P;
                if (!Job.Trash.Pick(i)) return;
                Bag++; Job.Changed = true;
                for (int q = 0; q < 4; q++) Robots.Splash(p + Vector3.up * .1f, .8f, true);
                Blip();
                if (Bag >= BagCap) Toast("봉투가 가득 찼어요", "쓰레기통 앞에서 E로 비우세요", ToastKind.Info);
            }
            else if (kind == "weed")
            {
                var p = Job.Weeds.Items[i].p;
                if (!Job.Weeds.Pull(i)) return;
                for (int q = 0; q < 10; q++) Robots.Clippings(p + Vector3.up * .06f);
                int pay = Mathf.RoundToInt(Shop.WeedPay * Job.Mul);
                Job.Earned += pay; Job.Changed = true; AddMoney(pay);
                Toast($"잡초 뽑기 ({Job.Weeds.Count}/{Job.Weeds.N})", "+" + G.Money(pay), ToastKind.Normal);
                Ding(false);
            }
            else if (kind == "bin")
            {
                int pay = Mathf.RoundToInt(Bag * Shop.TrashPay * Job.Mul);
                Job.Earned += pay; AddMoney(pay);
                Toast($"쓰레기 {Bag}개 버리기", "+" + G.Money(pay), ToastKind.Big);
                Ding(false);
                Bag = 0;
            }
        }

        // ------------------------------------------------------------------ jobs
        public bool Unlocked(int i) => i == 0 || Save.Done.Contains(JobDefs.All[i - 1].Id) || Save.Done.Contains(JobDefs.All[i].Id);
        public bool IsDone(int i) => Save.Done.Contains(JobDefs.All[i].Id);
        public float SavedProgress(string id)
        {
            var e = Save.Prog.Find(x => x.Id == id);
            if (e != null) return e.P;
            return UseCloud ? 0 : PlayerPrefs.GetFloat("banjjak-prog-" + id, 0);
        }

        void SetProg(string id, float p)
        {
            var e = Save.Prog.Find(x => x.Id == id);
            if (e == null) Save.Prog.Add(e = new JobProg { Id = id });
            e.P = p;
        }

        /// <summary>Tear down the current job (saving it) and build another one.</summary>
        public float StartJob(int index, bool fresh = false)
        {
            if (Job != null)
            {
                if (Job.Changed && !Job.Completed && autotestDir == null) SaveJob();
                Destroy(Job.Root.gameObject);
                Job = null;
            }
            Lift.Remove();
            Robots.RemoveAll();
            Bag = 0;
            float t0 = Time.realtimeSinceStartup;
            JobIndex = index;
            Job = JobDefs.Create(index, IsDone(index) ? .5f : 1f);
            Job.Finish();
            if (fresh) ClearJobSave(Job.Id);
            bool loaded = autotestDir == null && !fresh && Job.Apply(ReadJobSave(Job.Id));
            Job.CompDone += (c, pay) => { AddMoney(pay); if (!c.Obj.Done) Toast($"{c.Obj.Name} · {c.Name}", "+" + G.Money(pay), ToastKind.Normal); Ding(false); };
            Job.ObjectDone += (o, pay) => { Toast($"{o.Name} 완료!", "+" + G.Money(pay), ToastKind.Big); Ding(true); };
            Job.JobDone += bonus => OnJobDone(bonus);
            // parts that only finished on loading still pay, and a job with nothing left gets finished
            if (loaded) Job.SettleAfterLoad();
            Player.SetPose(Job.Spawn, Job.SpawnYaw, 12);
            probe.RenderProbe();
            Save.Current = Job.Id;
            PersistSave();
            if (Hud != null) Hud.OnJobChanged();
            if (loaded && !Job.Completed) Toast("이어서 하기", $"진행도 {Mathf.FloorToInt(Job.Progress * 100)}%", ToastKind.Info);
            return Time.realtimeSinceStartup - t0;
        }

        void OnJobDone(float bonus)
        {
            AddMoney(bonus);
            ClearJobSave(Job.Id);
            SetProg(Job.Id, 0);
            if (!Save.Done.Contains(Job.Id)) Save.Done.Add(Job.Id);
            PersistSave();
            StartCoroutine(ShowResultLater(bonus));
        }

        /// <summary>Menu card clicked.</summary>
        public void PickJob(int index)
        {
            if (!Unlocked(index)) return;
            bool same = index == JobIndex && Job != null && !Job.Completed;
            if (!same) StartJob(index, IsDone(index) && SavedProgress(JobDefs.All[index].Id) <= 0);
            SetMode(GameMode.Playing);
        }

        public void SetMode(GameMode m)
        {
            Mode = m;
            bool play = m == GameMode.Playing;
            if (autotestDir == null)
            {
                Cursor.lockState = play ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !play;
                Player.InputEnabled = play;
            }
            if (!play) SaveAll();
            Hud?.OnModeChanged();
        }

        // ------------------------------------------------------------------ environment
        void SetupEnvironment()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .955f, .885f);
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .92f;
            sun.shadowBias = .04f;
            sun.shadowNormalBias = .25f;
            sunGo.transform.rotation = Quaternion.Euler(46, -35, 0);
            RenderSettings.sun = sun;

            var sky = new Material(ShaderBank.I.skybox);
            sky.SetFloat("_SunSize", .03f);
            sky.SetFloat("_SunSizeConvergence", 6);
            sky.SetFloat("_AtmosphereThickness", .8f);
            sky.SetColor("_SkyTint", new Color(.46f, .54f, .66f));
            sky.SetColor("_GroundColor", new Color(.42f, .44f, .4f));
            sky.SetFloat("_Exposure", 1.2f);
            RenderSettings.skybox = sky;

            ApplyAmbient();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 45;
            RenderSettings.fogEndDistance = 240;
            RenderSettings.fogColor = new Color(.72f, .79f, .86f);

            probe = new GameObject("Reflections").AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(400, 120, 400);
            probe.transform.position = new Vector3(0, 1.8f, 0);
            probe.resolution = 256;
            probe.hdr = true;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;

            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.isGlobal = true;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(.35f); ca.contrast.Override(6); ca.saturation.Override(6);
            var bl = prof.Add<Bloom>(true); bl.threshold.Override(1.05f); bl.intensity.Override(.3f); bl.scatter.Override(.6f);
            var vg = prof.Add<Vignette>(true); vg.intensity.Override(.17f); vg.smoothness.Override(.45f);
            vol.profile = prof;
        }

        /// <summary>Ambient light as explicit spherical harmonics: blue sky from above, green-grey bounce from below.</summary>
        public static void ApplyAmbient()
        {
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(.34f, .38f, .43f));
            sh.AddDirectionalLight(Vector3.up, new Color(.42f, .52f, .68f), .55f);
            sh.AddDirectionalLight(Vector3.down, new Color(.3f, .33f, .24f), .25f);
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = sh;
        }

        void SpawnPlayer()
        {
            var p = new GameObject("Player");
            p.AddComponent<CharacterController>();
            Player = p.AddComponent<PlayerController>();
            var head = new GameObject("Head").transform;
            head.SetParent(p.transform, false);
            Player.Head = head;

            Cam = new GameObject("MainCamera").AddComponent<Camera>();
            Cam.tag = "MainCamera";
            Cam.transform.SetParent(head, false);
            Cam.fieldOfView = 70;
            Cam.nearClipPlane = .05f;
            Cam.farClipPlane = 420;
            Cam.cullingMask = ~(1 << Layers.Rig);
            Cam.gameObject.AddComponent<AudioListener>();
            var cd = Cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true;
            cd.renderShadows = true;
            cd.antialiasing = AntialiasingMode.None;
            Player.Cam = Cam;

            var rigCam = new GameObject("RigCamera").AddComponent<Camera>();
            rigCam.transform.SetParent(Cam.transform, false);
            rigCam.fieldOfView = 62;
            rigCam.nearClipPlane = .01f;
            rigCam.farClipPlane = 4;
            rigCam.cullingMask = 1 << Layers.Rig;
            var rd = rigCam.GetUniversalAdditionalCameraData();
            rd.renderType = CameraRenderType.Overlay;
            rd.renderShadows = false;
            cd.cameraStack.Add(rigCam);

            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(rigCam.transform, false);
            var rig = rigGo.AddComponent<HandRig>();
            rig.Build(Player);

            Washer = p.AddComponent<WasherTool>();
            Washer.Init(Player, rig);
            Player.SetPose(Vector3.zero, 180, 12);
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            float dt = Time.deltaTime;
            Job?.Tick(dt);
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (autotestDir == null && kb != null && mouse != null)
            {
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                switch (Mode)
                {
                    case GameMode.Playing:
                        if (kb.escapeKey.wasPressedThisFrame) SetMode(GameMode.Paused);
                        if (kb.lKey.wasPressedThisFrame)
                        {
                            if (Owned("lift") || Job.FreeLift || Lift.Active != null) Lift.Toggle(Player, Job);
                            else Toast("고소 작업대가 없어요", "장비 가게에서 살 수 있어요", ToastKind.Info);
                        }
                        if (kb.eKey.wasPressedThisFrame) DoUse();
                        if (kb.gKey.wasPressedThisFrame) Robots.Toggle(Player);
                        if (kb.tabKey.wasPressedThisFrame) Hud.ToggleList();
                        if (kb.hKey.wasPressedThisFrame) Hud.ToggleHelp();
                        // focus lost (alt-tab) also unlocks the cursor: treat it as pause
                        if (Cursor.lockState != CursorLockMode.Locked && Application.isFocused) SetMode(GameMode.Paused);
                        break;
                    case GameMode.Paused:
                        if (kb.escapeKey.wasPressedThisFrame || (mouse.leftButton.wasPressedThisFrame && !overUI)) SetMode(GameMode.Playing);
                        break;
                    case GameMode.Menu:
                        if (kb.escapeKey.wasPressedThisFrame && Job != null && !Hud.LoginBlocksEsc) SetMode(GameMode.Paused);
                        break;
                    case GameMode.Result:
                        if (kb.escapeKey.wasPressedThisFrame) SetMode(GameMode.Menu);
                        break;
                }
                if (Mode == GameMode.Playing && kb.fKey.wasPressedThisFrame) ToggleHighlight();
            }
            saveT += dt;
            if (saveT > 15 && Job != null && Job.Changed && autotestDir == null) { saveT = 0; SaveAll(); }
            if (profileDirty && UseCloud && Time.realtimeSinceStartup - profilePushT > 3)
            {
                profileDirty = false; profilePushT = Time.realtimeSinceStartup;
                StartCoroutine(Acct.PushProfile(JsonUtility.ToJson(Save)));
            }
        }

        void LateUpdate()
        {
            Job?.UploadDirty();
            Job?.Grass?.Flush();
        }

        void Blip()
        {
            if (sfx == null) { sfx = gameObject.AddComponent<AudioSource>(); sfx.volume = .35f; }
            int rate = 44100, n = rate / 6;
            var data = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)rate, f = Mathf.Lerp(520, 880, t / .08f); data[i] = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 18) * .25f; }
            var clip = AudioClip.Create("blip", n, 1, rate, false);
            clip.SetData(data, 0);
            sfx.PlayOneShot(clip);
        }

        void OnApplicationQuit() { if (autotestDir == null) SaveAll(); }
        void OnApplicationFocus(bool f) { if (!f && autotestDir == null) SaveAll(); }

        public void AddMoney(float v) { Save.Money += v; PersistSave(); }

        public void Toast(string title, string sub, ToastKind kind) { if (Hud != null) Hud.Toast(title, sub, kind); }

        IEnumerator ShowResultLater(float bonus)
        {
            yield return new WaitForSeconds(1.6f);
            Hud.SetResult(bonus);
            if (autotestDir == null) SetMode(GameMode.Result);
        }

        // ------------------------------------------------------------------ sound
        AudioSource sfx;
        void Ding(bool big)
        {
            if (sfx == null) { sfx = gameObject.AddComponent<AudioSource>(); sfx.volume = .35f; }
            int rate = 44100; float len = big ? .9f : .45f;
            int n = (int)(rate * len);
            var data = new float[n];
            float[] notes = big ? new[] { 880f, 1108.7f, 1318.5f } : new[] { 1318.5f, 1760f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate, v = 0;
                for (int k = 0; k < notes.Length; k++)
                {
                    float st = k * (big ? .11f : .08f);
                    if (t < st) continue;
                    float tt = t - st;
                    v += Mathf.Sin(2 * Mathf.PI * notes[k] * tt) * Mathf.Exp(-tt * 7) * .3f;
                }
                data[i] = v;
            }
            var clip = AudioClip.Create("ding", n, 1, rate, false);
            clip.SetData(data, 0);
            sfx.PlayOneShot(clip);
        }

        // ------------------------------------------------------------------ saving
        const string SaveKey = "banjjak-save-v1";

        void LoadSave()
        {
            Save = new SaveData();
            var s = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(s)) { try { Save = JsonUtility.FromJson<SaveData>(s) ?? new SaveData(); } catch { Save = new SaveData(); } }
            FixSave();
        }

        void FixSave()
        {
            if (Save.Done == null) Save.Done = new List<string>();
            if (Save.Owned == null) Save.Owned = new List<string>();
            if (Save.Prog == null) Save.Prog = new List<JobProg>();
            if (string.IsNullOrEmpty(Save.Current)) Save.Current = "patio";
            Save.Washer = Mathf.Clamp(Save.Washer, 0, Washers.All.Length - 1);
            Save.Agent = Mathf.Clamp(Save.Agent, 0, Agents.All.Count - 1);
        }

        bool profileDirty;
        float profilePushT;

        void PersistSave()
        {
            if (autotestDir != null) return;
            if (UseCloud) { profileDirty = true; return; }   // pushed by Update, a few seconds apart
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Save));
            PlayerPrefs.Save();
        }

        void SaveJob()
        {
            var raw = Job.Serialize();
            if (UseCloud) StartCoroutine(Acct.PushJob(Job.Id, Convert.ToBase64String(raw)));
            else Job.SaveLocal(raw);
            Job.Changed = false;
            SetProg(Job.Id, Job.Progress);
            PersistSave();
        }

        byte[] ReadJobSave(string id)
        {
            if (UseCloud) return Acct.JobCache.TryGetValue(id, out var b64) && !string.IsNullOrEmpty(b64) ? Convert.FromBase64String(b64) : null;
            return CleanJob.ReadLocal(id);
        }

        void ClearJobSave(string id)
        {
            if (UseCloud) StartCoroutine(Acct.DeleteJob(id));
            else CleanJob.ClearLocal(id);
        }

        // ------------------------------------------------------------------ accounts (Supabase)
        public readonly Account Acct = new Account();
        /// <summary>True once the account's data is in memory. Until then (while signing in) everything still saves to this device.</summary>
        bool acctReady;
        public bool UseCloud => CloudConfig.Enabled && Acct.LoggedIn && acctReady;

        /// <summary>Called by the login panel. report(ok, message).</summary>
        public void SubmitLogin(string name, string pw, bool signUp, Action<bool, string> report)
        {
            if (!CloudConfig.Enabled) { report(false, "아직 서버가 연결되지 않았어요"); return; }
            if (Acct.Busy) return;
            name = (name ?? "").Trim();
            if (!Account.ValidName(name)) { report(false, "아이디는 영문·숫자·_ 3~20자예요"); return; }
            if ((pw ?? "").Length < 6) { report(false, "비밀번호는 6자 이상이어야 해요"); return; }
            // keep the guest's current job safe on this device before switching to the account
            if (!UseCloud && Job != null && Job.Changed && !Job.Completed) SaveJob();
            System.Action<bool, string> after = (ok, msg) =>
            {
                if (!ok) { report(false, msg); return; }
                StartCoroutine(AfterLogin(signUp, report));
            };
            StartCoroutine(signUp ? Acct.SignUp(name, pw, after) : Acct.Login(name, pw, after));
        }

        IEnumerator AfterLogin(bool signUp, Action<bool, string> report)
        {
            acctReady = false;
            string profile = null; bool ok = false;
            yield return Acct.Pull((k, p) => { ok = k; profile = p; });
            if (!Acct.LoggedIn) yield break;   // logged out while loading
            if (!ok) { report?.Invoke(false, "계정 정보를 불러오지 못했어요"); Acct.Logout(); Hud?.RefreshAccount(); yield break; }
            // still the guest here (acctReady is false): keep the guest's job on this device, never in the account
            if (Job != null && Job.Changed && !Job.Completed) SaveJob();
            if (!string.IsNullOrEmpty(profile) && profile != "{}")
            {
                try { Save = JsonUtility.FromJson<SaveData>(profile) ?? new SaveData(); } catch { Save = new SaveData(); }
                FixSave();
                report?.Invoke(true, $"{Acct.Username} 님, 어서 오세요");
            }
            else
            {
                // brand-new account: carry this device's guest progress into it
                foreach (var d in JobDefs.All)
                {
                    var raw = CleanJob.ReadLocal(d.Id);
                    if (raw != null) yield return Acct.PushJob(d.Id, Convert.ToBase64String(raw));
                }
                yield return Acct.PushProfile(JsonUtility.ToJson(Save));
                report?.Invoke(true, signUp ? "가입 완료! 이 기기의 진행도를 계정에 옮겼어요" : "계정에 진행도를 저장했어요");
            }
            if (!Acct.LoggedIn) { LoadSave(); yield break; }   // logged out while migrating: back to the guest save
            if (Job != null) Job.Changed = false;   // the guest job is already stored on this device; don't push it to the account
            profileDirty = false;
            acctReady = true;
            ReloadAfterProfileChange();
            Toast($"{Acct.Username} 계정으로 저장돼요", "다른 컴퓨터에서도 로그인하면 이어서 할 수 있어요", ToastKind.Info);
        }

        public void Logout()
        {
            if (!Acct.LoggedIn) return;
            if (Job != null && Job.Changed && !Job.Completed) SaveJob();
            if (profileDirty) StartCoroutine(Acct.PushProfile(JsonUtility.ToJson(Save)));
            profileDirty = false;
            acctReady = false;
            Acct.Logout();
            if (Job != null) Job.Changed = false;   // just saved to the account; don't copy it into this device's guest save
            LoadSave();
            ReloadAfterProfileChange();
            Toast("로그아웃했어요", "지금부터는 이 기기에만 저장돼요", ToastKind.Info);
        }

        void ReloadAfterProfileChange()
        {
            Robots.RemoveAll();
            StartJob(Mathf.Max(0, JobDefs.IndexOf(Save.Current)));
            Washer.Refresh();
            Hud?.OnModeChanged();
            Hud?.RefreshAccount();
        }

        IEnumerator AutoLogin()
        {
            bool ok = false;
            yield return Acct.Refresh(k => ok = k);
            if (ok) yield return AfterLogin(false, null);
            Hud?.RefreshAccount();
        }

        void SaveAll()
        {
            if (autotestDir != null) return;
            PersistSave();
            if (Job != null && !Job.Completed && Job.Changed) SaveJob();
        }

        // ------------------------------------------------------------------ automated test
        IEnumerator AutoTest()
        {
            Directory.CreateDirectory(autotestDir);
            Player.InputEnabled = false;   // keystrokes meant for other windows must not move the test player
            yield return new WaitForSeconds(1.2f);
            yield return ShotWait("00_menu.png");
            Mode = GameMode.Playing; Hud.OnModeChanged();

            DirtSurface.KeepSamples = true;   // the reach check needs each dirt cell's point and normal
            int stuckJobs = 0;
            for (int i = 0; i < JobDefs.All.Length; i++)
            {
                float bt = StartJob(i);
                yield return new WaitForSeconds(.8f);
                yield return ShotWait($"job{i}_{Job.Id}.png");
                var zero = new List<string>();
                foreach (var o in Job.Objects) foreach (var c in o.Comps) if (c.Total == 0) zero.Add(o.Name + "/" + c.Name);
                testLog.AppendLine($"{Job.Id}: build {bt:0.00}s, objects {Job.Objects.Count}, surfaces {Job.Surfs.Count}, cells {Job.Total}, zero-total comps: {(zero.Count == 0 ? "none" : string.Join(", ", zero))}");
                // faces left out because something sits right in front of them (unreachable by the spray)
                var hid = new List<(string n, int h, int t)>();
                int hidAll = 0;
                foreach (var o in Job.Objects) foreach (var c in o.Comps)
                    {
                        int h = 0; foreach (var s in c.Surfs) h += s.ContactHidden;
                        hidAll += h;
                        if (h > 0) hid.Add((o.Name + "/" + c.Name, h, c.Total));
                    }
                hid.Sort((a, b) => (b.h / (float)(b.h + b.t)).CompareTo(a.h / (float)(a.h + a.t)));
                var top = new List<string>();
                for (int k = 0; k < Mathf.Min(5, hid.Count); k++) top.Add($"{hid[k].n} {hid[k].h * 100f / (hid[k].h + hid[k].t):0}% ({hid[k].h})");
                testLog.AppendLine($"  unreachable left out: {hidAll} cells; most: {(top.Count == 0 ? "none" : string.Join(", ", top))}");
                // the lift is free only in the tower; elsewhere it's a $2,400 purchase, so every other job has to be
                // finishable without it (crouching, standing, jumping, steps and platforms only)
                if (ReachCheck.Run(Job, testLog, reachBakeDir, Job.FreeLift).Count > 0) stuckJobs++;
                yield return null;
            }
            if (reachBakeDir != null) testLog.AppendLine($"reach bake written to {reachBakeDir}" + (stuckJobs > 0 ? $" ({stuckJobs} job(s) FAILED)" : ""));
            else testLog.AppendLine($"reach check: {(stuckJobs == 0 ? $"all {JobDefs.All.Length} jobs can reach 100%" : stuckJobs + " job(s) have surfaces that can't be finished")}");
            DirtSurface.KeepSamples = false;
            CleanJob.UseReachBake = true;

            // a save where every part is clean but none was marked finished (what an update that leaves out
            // unreachable cells produces) must finish the job on loading, so the next job unlocks
            {
                StartJob(0);
                foreach (var s in Job.Surfs) { for (int k = 0; k < s.N; k++) if (s.Surf[k] == 2) s.DirtAmt[k] = 0; s.RecountFromDirt(); }
                var raw = Job.Serialize();
                var j2 = JobDefs.Create(0, 1f);
                j2.Finish();
                bool applied = j2.Apply(raw);
                int paid = 0; bool fired = false;
                j2.CompDone += (c, pay) => paid += pay;
                j2.JobDone += b => fired = true;
                j2.SettleAfterLoad();
                testLog.AppendLine($"load settle: applied {applied}, completed {j2.Completed}, job-done event {fired}, parts paid ${paid}");
                Destroy(j2.Root.gameObject);
                // the tests below expect the tower, the last job of the loop above
                StartJob(JobDefs.IndexOf("tower"));
                yield return new WaitForSeconds(.8f);
            }

            // tower: inside the 2nd floor, then the lift against the front wall
            Player.SetPose(new Vector3(-1, 3.52f, -5.2f), 200, 8);
            yield return new WaitForSeconds(.6f);
            yield return ShotWait("tower_2f.png");
            Player.SetPose(new Vector3(-3.25f, 0, 1.2f), 180, 0);
            yield return null;
            Lift.Toggle(Player, Job);
            yield return null;
            testLog.AppendLine($"lift placed: {Lift.Active != null}");
            if (Lift.Active != null)
            {
                Lift.Active.SetHeight(3.3f);
                var lp = Lift.Active.transform.position;
                Player.SetPose(new Vector3(lp.x, Lift.Active.Top + .05f, lp.z), 180, 0);
                yield return new WaitForSeconds(.5f);
                Washer.SelectTool(0);
                Washer.ForceSpray = true;
                yield return new WaitForSeconds(2.5f);
                yield return ShotWait("tower_lift_spray.png");
                Washer.ForceSpray = false;
                var f2 = Job.Objects.Find(o => o.Name == "2층");
                testLog.AppendLine($"after lift spray: player y {Player.transform.position.y:0.00}, 2F walls {f2.Comps[0].Progress * 100:0.0}%");
            }

            // ---- shop, tools, litter and robots on the patio
            Lift.Remove();
            Save.Money = 50000;
            Mode = GameMode.Menu; Hud.OnModeChanged(); Hud.ShowShop(true);
            yield return new WaitForSeconds(.3f);
            yield return ShotWait("shop.png");
            bool bought = Buy("surface") & Buy("turbo") & Buy("bot:robots") & Buy("bot:trashBots") & Buy("bot:drones") & Buy("bot:mowBots") & Buy("ag_bio") & Buy("washer");
            testLog.AppendLine($"purchases ok: {bought}, money left {Save.Money}, washer {Washers.All[Save.Washer].Psi}");
            Hud.ShowShop(false);
            Mode = GameMode.Playing; Hud.OnModeChanged();
            StartJob(0);
            yield return new WaitForSeconds(.5f);
            testLog.AppendLine($"patio litter: trash {Job.Trash.N}, weeds {Job.Weeds.N}, lawn blades {Job.Grass.Lawn}, bins {Job.Bins.Count}");

            // pick up a piece of trash and empty the bag at the bin
            var item = Job.Trash.Items.Find(t => !t.Picked && t.P.y < .1f);
            var stand = item.P + new Vector3(0, 0, 1.3f);
            Player.SetPose(new Vector3(stand.x, 0, stand.z), 180, 0);
            yield return null;
            var look = item.P + Vector3.up * .06f - Cam.transform.position;
            Player.SetPose(new Vector3(stand.x, 0, stand.z), 180, Mathf.Atan2(-look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
            yield return null;
            var use1 = FindUse().kind; DoUse();
            var bin = Job.Bins[0];
            Player.SetPose(new Vector3(bin.x + 1.2f, 0, bin.z), 270, 10);
            yield return null;
            float m0 = Save.Money; var use2 = FindUse().kind; DoUse();
            testLog.AppendLine($"use: aim at trash -> {use1}, picked {Job.Trash.Count}; at bin -> {use2}, paid {Save.Money - m0}");

            // auto mower: deploy with slot 4, let it work, recall with slot 4
            Player.SetPose(new Vector3(-9, 0, 8), 90, 25);
            Washer.SelectTool(3);
            bool deployed = Robots.AutoMower != null;
            yield return new WaitForSeconds(6f);
            yield return ShotWait("mower.png");
            int cutAuto = Job.Grass.CutCount;
            Washer.SelectTool(3);
            testLog.AppendLine($"auto mower: deployed {deployed}, cut {cutAuto} blades in 6s, recalled {Robots.AutoMower == null}");

            // F: remaining dirt highlight + markers, and the remaining list
            Player.SetPose(new Vector3(0, 0, 6.2f), 180, 14);
            ToggleHighlight();
            Hud.ToggleList();
            yield return new WaitForSeconds(.6f);
            yield return ShotWait("remaining.png");
            ToggleHighlight();
            Hud.ToggleList();
            testLog.AppendLine($"highlight toggled back off: {!Highlight}");
            Washer.SelectTool(2);
            Player.SetPose(new Vector3(1.5f, 0, 2.5f), 180, 30);
            var floorB2 = Job.Objects.Find(o => o.Name == "테라스 바닥");
            Washer.ForceSpray = true;
            yield return new WaitForSeconds(2f);
            yield return ShotWait("surface.png");
            Washer.ForceSpray = false;
            testLog.AppendLine($"surface cleaner: floor {string.Join(", ", floorB2.Comps.ConvertAll(c => c.Name + " " + (c.Progress * 100).ToString("0") + "%"))}");
            Washer.SelectTool(0);

            // robots
            int trash0 = Job.Trash.Count, cut0 = Job.Grass.CutCount; float floor0 = Job.Progress;
            Player.SetPose(new Vector3(0, 0, 5), 180, 15);
            Robots.Toggle(Player);
            yield return new WaitForSeconds(9f);
            yield return ShotWait("robots.png");
            var drone = Robots.Active.Find(r => r.Type == "drone");
            testLog.AppendLine($"robots {Robots.Active.Count}: trash picked +{Job.Trash.Count - trash0}, grass cut +{Job.Grass.CutCount - cut0}, job progress {floor0 * 100:0.0}% -> {Job.Progress * 100:0.0}%, drone at {drone.transform.position}");
            Robots.RemoveAll();

            File.WriteAllText(Path.Combine(autotestDir, "autotest.log"), testLog.ToString());
            yield return new WaitForSeconds(.3f);
            Application.Quit();
        }

        void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(autotestDir, name));

        IEnumerator ShotWait(string name)
        {
            Shot(name);
            yield return null;
            yield return null;
        }
    }
}
