using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Banjjak
{
    public enum ToastKind { Normal, Big, Info }

    /// <summary>Heads-up display built entirely in code (uGUI + OS Korean font).</summary>
    public class Hud : MonoBehaviour
    {
        GameManager gm;
        Font font, fontBold;
        CanvasScaler scaler;
        readonly List<(RectTransform rt, Text text)> markers = new List<(RectTransform, Text)>();
        readonly Dictionary<Comp, (Vector3 p, float t)> anchors = new Dictionary<Comp, (Vector3, float)>();
        Sprite round, roundSmall;
        RectTransform root;

        Text jobName, pctText, timeText, moneyText, psiText;
        Image progressFill;
        RectTransform toastBox; Text toastTitle, toastSub; Image toastBg; float toastT;
        readonly Queue<(string, string, ToastKind)> toasts = new Queue<(string, string, ToastKind)>();
        RectTransform targetBox; Text targetName, targetDirt, targetPct; Image targetChip, targetFill;
        Text hintText;
        Image[] slots = new Image[4]; Text[] slotLabels = new Text[4];
        Text bagText, gadgetText, useTip; RectTransform useTipBox;
        GameObject cardsRoot, shopRoot;
        Image tabJobs, tabShop; Text tabJobsT, tabShopT;
        readonly List<(string id, Text name, Button btn, Text btnText)> shopRows = new List<(string, Text, Button, Text)>();
        Text washerPsi, washerName; Button washerBtn; Text washerBtnText;
        Text angleTitle, angleValue; Image soapFill; Text agentName;
        RectTransform crossFan; Image crossDot;
        GameObject helpPanel, pausePanel, listPanel, resultPanel, menuPanel;
        Text listText, resultText, pauseTitle, pauseDesc, menuMoney;
        readonly List<(Text title, Text state, Image btn, Text btnText, Button button)> cards = new List<(Text, Text, Image, Text, Button)>();
        readonly List<GameObject> gameplayUi = new List<GameObject>();
        bool helpOn = true, listOn;
        public bool TestMode;

        static readonly Color Ink = G.Hex(0x1c2430);
        static readonly Color Sub = G.Hex(0x5b6573);
        static readonly Color Card = new Color(1, 1, 1, .93f);
        static readonly Color Accent = G.Hex(0x2f80ed);
        static readonly Color Good = G.Hex(0x30a46c);

        public void Build(GameManager g)
        {
            gm = g;
            // bundled Nanum Gothic (OFL) so Korean renders everywhere, including the browser build
            font = Resources.Load<Font>("Fonts/NanumGothic-Regular");
            fontBold = Resources.Load<Font>("Fonts/NanumGothic-Bold");
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 32);
            if (fontBold == null) fontBold = font;
            round = RoundSprite(64, 22);
            roundSmall = RoundSprite(32, 10);

            var canvasGo = new GameObject("HUD");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            root = canvasGo.GetComponent<RectTransform>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            BuildJobCard();
            BuildMoney();
            BuildToast();
            BuildCrosshair();
            BuildTarget();
            BuildToolbar();
            BuildHelp();
            BuildList();
            foreach (Transform c in root)
                if (c.gameObject != toastBox.gameObject && c.gameObject != helpPanel && c.gameObject != listPanel) gameplayUi.Add(c.gameObject);
            BuildPause();
            BuildResult();
            BuildMenu();
            BuildLogin();
            BuildFps();
            BuildCoop();
            BuildSkin();
            RefreshTools();
            OnJobChanged();
            OnModeChanged();
            RefreshAccount();
        }

        Button Btn(RectTransform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color bg, Color fg, UnityEngine.Events.UnityAction onClick, int fontSize = 16)
        {
            var rt = Box("btn", parent, anchor, pivot, pos, size);
            var img = Panel(rt, bg);
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(.9f, .93f, .97f);
            cb.pressedColor = new Color(.78f, .82f, .88f);
            cb.disabledColor = new Color(.85f, .85f, .85f, .7f);
            b.colors = cb;
            b.onClick.AddListener(onClick);
            Label(rt, label, fontSize, fg, TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, 0), size, true);
            return b;
        }

        void BuildMenu()
        {
            var p = Box("menu", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(0, 0));
            p.anchorMin = A(0, 0); p.anchorMax = A(1, 1); p.sizeDelta = A(0, 0);
            menuPanel = p.gameObject;
            var bg = p.gameObject.AddComponent<Image>(); bg.color = new Color(.05f, .08f, .12f, .62f); bg.raycastTarget = true;
            var head = Box("head", p, A(.5f, 1), A(.5f, 1), A(0, -34), A(1136, 96));
            Label(head, "혼자 하는 고압세척", 14, G.Hex(0x9cc7ff), TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(0, 0), A(400, 20), true);
            Label(head, "반짝 워시", 40, Color.white, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(0, -20), A(400, 52), true);
            Label(head, "의뢰를 골라 시작하세요. 끝낸 의뢰 다음 현장이 열려요.", 14, new Color(1, 1, 1, .8f), TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(2, -72), A(600, 22));
            menuMoney = Label(head, "", 26, Color.white, TextAnchor.MiddleRight, A(1, 1), A(1, 1), A(0, -6), A(300, 40), true);
            var tj = Btn(head, "의뢰", A(1, 1), A(1, 1), A(-128, -56), A(120, 36), Accent, Color.white, () => ShowShop(false), 15);
            var ts = Btn(head, "장비 가게", A(1, 1), A(1, 1), A(0, -56), A(120, 36), G.Hex(0xeef2f7), Ink, () => ShowShop(true), 15);
            coopBtn = Btn(head, "같이 하기", A(1, 1), A(1, 1), A(-256, -56), A(120, 36), G.Hex(0xffd166), Ink, () => ShowCoop(true), 15);
            Btn(head, "꾸미기", A(1, 1), A(1, 1), A(-384, -56), A(120, 36), G.Hex(0xf7c6d9), Ink, () => ShowSkin(true), 15);
            accountBtn = Btn(head, "", A(1, 1), A(1, 1), A(-312, -8), A(230, 34), new Color(1, 1, 1, .16f), Color.white, OnAccountButton, 14);
            accountText = accountBtn.GetComponentInChildren<Text>();
            tabJobs = (Image)tj.targetGraphic; tabShop = (Image)ts.targetGraphic;
            tabJobsT = tj.GetComponentInChildren<Text>(); tabShopT = ts.GetComponentInChildren<Text>();
            var cr = Box("cards", p, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(0, 0));
            cr.anchorMin = A(0, 0); cr.anchorMax = A(1, 1); cr.sizeDelta = A(0, 0);
            cardsRoot = cr.gameObject;
            var sr = Box("shop", p, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(0, 0));
            sr.anchorMin = A(0, 0); sr.anchorMax = A(1, 1); sr.sizeDelta = A(0, 0);
            shopRoot = sr.gameObject;
            BuildShop(sr);
            shopRoot.SetActive(false);
            p = cr;
            const float cw = 560, ch = 164, gap = 12;
            for (int i = 0; i < JobDefs.All.Length; i++)
            {
                int idx = i;
                var d = JobDefs.All[i];
                int col = i % 2, row = i / 2;
                var cardRt = Box("card" + i, p, A(.5f, 1), A(.5f, 1), A((col == 0 ? -1 : 1) * (cw + gap) / 2, -150 - row * (ch + gap)), A(cw, ch));
                Panel(cardRt, new Color(1, 1, 1, .96f));
                var title = Label(cardRt, $"{d.Name}  <size=13><color=#2f80ed>{d.Size}</color></size>", 20, Ink, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(20, -14), A(380, 28), true);
                Label(cardRt, G.Money(d.Reward), 18, Ink, TextAnchor.UpperRight, A(1, 1), A(1, 1), A(-20, -15), A(140, 26), true);
                var desc = Label(cardRt, d.Desc, 13, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(20, -46), A(cw - 40, 40));
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                var sb = new StringBuilder();
                foreach (var k in d.DirtKeys)
                {
                    var dt = Dirt.All[Dirt.Index(k)];
                    sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(dt.Ui)}>●</color> {dt.Name}   ");
                }
                Label(cardRt, sb.ToString(), 12, Sub, TextAnchor.MiddleLeft, A(0, 0), A(0, 0), A(20, 18), A(370, 24));
                var state = Label(cardRt, "", 12, Sub, TextAnchor.MiddleRight, A(1, 0), A(1, 0), A(-18, 58), A(280, 20), true);
                var b = Btn(cardRt, "작업 시작", A(1, 0), A(1, 0), A(-16, 14), A(160, 38), Accent, Color.white, () => gm.PickJob(idx), 15);
                var btnText = b.GetComponentInChildren<Text>();
                cards.Add((title, state, (Image)b.targetGraphic, btnText, b));
            }
        }

        // ------------------------------------------------------------------ shop
        void BuildShop(RectTransform sr)
        {
            const float cw = 560, rowH = 52, gap = 6, top = -150;
            float x1 = -(cw + 12) / 2, x2 = (cw + 12) / 2;
            float y = top;
            RectTransform Section(float x, string title)
            {
                var t = Label(sr, title, 15, Color.white, TextAnchor.MiddleLeft, A(.5f, 1), A(.5f, 1), A(x, y - 2), A(cw, 22), true);
                y -= 28;
                return t.rectTransform;
            }
            void Row(float x, string id, string name, string desc)
            {
                var r = Box("row", sr, A(.5f, 1), A(.5f, 1), A(x, y), A(cw, rowH));
                r.pivot = A(.5f, 1); r.anchoredPosition = A(x, y);
                Panel(r, new Color(1, 1, 1, .96f));
                var nm = Label(r, name, 14, Ink, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(16, -7), A(380, 20), true);
                Label(r, desc, 12, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(16, -28), A(390, 18));
                string iid = id;
                var b = Btn(r, "", A(1, .5f), A(1, .5f), A(-10, 0), A(138, 36), Accent, Color.white, () => { if (gm.Buy(iid)) RefreshShop(); }, 13);
                shopRows.Add((id, nm, b, b.GetComponentInChildren<Text>()));
                y -= rowH + gap;
            }
            // column 1: washer, tools, upgrades
            var wbox = Box("washer", sr, A(.5f, 1), A(.5f, 1), A(x1, y), A(cw, 92));
            wbox.pivot = A(.5f, 1); wbox.anchoredPosition = A(x1, y);
            Panel(wbox, new Color(1, 1, 1, .96f));
            Label(wbox, "고압세척기", 12, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(16, -10), A(200, 18), true);
            washerPsi = Label(wbox, "", 28, Ink, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(16, -28), A(260, 36), true);
            washerName = Label(wbox, "", 12, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(16, -66), A(260, 18));
            washerBtn = Btn(wbox, "", A(1, .5f), A(1, .5f), A(-12, 0), A(250, 44), G.Hex(0xf08a3c), Color.white, () => { if (gm.Buy("washer")) RefreshShop(); }, 14);
            washerBtnText = washerBtn.GetComponentInChildren<Text>();
            y -= 92 + 14;
            Section(x1, "도구");
            Row(x1, "surface", "원형 바닥 세척기", "3번 · 걸어 다니며 넓은 바닥을 한 번에");
            Row(x1, "mower", "자동 잔디 깎기", "4번 · 누르면 혼자 돌아다니며 잔디를 깎아요 (보너스 수입)");
            y -= 8;
            Section(x1, "장비");
            foreach (var u in Shop.Upgrades) Row(x1, u.Id, u.Name, u.Desc);
            // column 2: robots, detergents
            y = top;
            Section(x2, "자동 청소 로봇 (G로 출동·회수)");
            foreach (var b in Shop.Bots) Row(x2, "bot:" + b.Key, b.Name, b.Desc);
            y -= 8;
            Section(x2, "세제 (거품 분사기에 넣어 써요 · Q로 바꾸기)");
            foreach (var a in Agents.All) Row(x2, a.Id, a.Name, a.Desc);
        }

        public void ShowShop(bool on)
        {
            shopRoot.SetActive(on);
            cardsRoot.SetActive(!on);
            tabShop.color = on ? Accent : G.Hex(0xeef2f7); tabShopT.color = on ? Color.white : Ink;
            tabJobs.color = on ? G.Hex(0xeef2f7) : Accent; tabJobsT.color = on ? Ink : Color.white;
            if (on) RefreshShop(); else RefreshMenu();
        }

        void RefreshShop()
        {
            menuMoney.text = G.Money(gm.Save.Money);
            var w = Washers.All[gm.Save.Washer];
            washerPsi.text = w.Psi + " <size=14>PSI</size>";
            washerName.text = w.Name;
            if (gm.Save.Washer + 1 < Washers.All.Length)
            {
                var nx = Washers.All[gm.Save.Washer + 1];
                washerBtnText.text = $"{nx.Psi} PSI로 업그레이드 · {G.Money(nx.Cost)}";
                washerBtn.interactable = gm.Save.Money >= nx.Cost;
            }
            else { washerBtnText.text = "최고 등급"; washerBtn.interactable = false; }
            foreach (var r in shopRows)
            {
                if (r.id.StartsWith("bot:"))
                {
                    var b = System.Array.Find(Shop.Bots, x => "bot:" + x.Key == r.id);
                    int have = gm.BotCount(b.Key);
                    r.name.text = $"{b.Name} <color=#5b6573>({have}/{b.Max}대)</color>";
                    bool full = have >= b.Max;
                    r.btnText.text = full ? "최대" : have > 0 ? $"한 대 더 · {G.Money(b.Cost)}" : G.Money(b.Cost);
                    r.btn.interactable = !full && gm.Save.Money >= b.Cost;
                    ((Image)r.btn.targetGraphic).color = full ? Good : Accent;
                }
                else
                {
                    bool own = gm.Owned(r.id);
                    int cost = Shop.CostOf(r.id);
                    r.btnText.text = own ? "보유" : G.Money(cost);
                    r.btn.interactable = !own && gm.Save.Money >= cost;
                    ((Image)r.btn.targetGraphic).color = own ? Good : Accent;
                }
            }
        }

        void RefreshMenu()
        {
            menuMoney.text = G.Money(gm.Save.Money);
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                var d = JobDefs.All[i];
                bool unlocked = gm.Unlocked(i), done = gm.IsDone(i);
                float prog = i == gm.JobIndex && gm.Job != null ? gm.Job.Progress : gm.SavedProgress(d.Id);
                bool inProgress = !(i == gm.JobIndex && gm.Job != null && gm.Job.Completed) && prog > .001f;
                var coop = Banjjak.Net.Coop.I;
                bool guest = coop != null && coop.IsGuest;
                bool coopJob = coop != null && coop.Active && i == gm.JobIndex && gm.Job != null;
                c.button.interactable = unlocked || (guest && coopJob);
                // co-op: the host's job is the one everyone plays; guests can't pick others
                if (coopJob) { c.btnText.text = "같이 닦기"; c.btn.color = G.Hex(0xf0a020); c.state.text = $"같이 하는 현장 · {Mathf.FloorToInt(prog * 100)}%"; }
                else if (guest) { c.btnText.text = "방장이 골라요"; c.btn.color = G.Hex(0xc3cad3); c.state.text = unlocked ? "" : "이전 의뢰를 먼저 끝내세요"; }
                else if (!unlocked) { c.btnText.text = "잠김"; c.btn.color = G.Hex(0xc3cad3); c.state.text = "이전 의뢰를 먼저 끝내세요"; }
                else if (inProgress) { c.btnText.text = "이어서 하기"; c.btn.color = Accent; c.state.text = $"진행 {Mathf.FloorToInt(prog * 100)}%" + (done ? " · 완료한 적 있음" : ""); }
                else if (done) { c.btnText.text = "다시 하기"; c.btn.color = Good; c.state.text = "완료"; }
                else { c.btnText.text = "작업 시작"; c.btn.color = Accent; c.state.text = ""; }
            }
        }

        public void OnJobChanged()
        {
            if (jobName == null || gm == null || gm.Job == null) return;
            jobName.text = gm.Job.Name;
            if (pauseDesc != null) pauseDesc.text = gm.Job.Name + " — " + gm.Job.Desc;
            resultPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ account
        Button accountBtn; Text accountText;
        GameObject loginPanel;
        InputField idField, pwField;
        Text loginMsg;
        Button loginBtn, signupBtn;
        int loginClosedFrame = -1;
        public bool LoginBlocksEsc => (loginPanel != null && loginPanel.activeSelf) || (coopPanel != null && coopPanel.activeSelf)
                                      || (skinPanel != null && skinPanel.activeSelf) || loginClosedFrame == Time.frameCount;

        // ------------------------------------------------------------------ 꾸미기 (character look)
        const int PreviewLayer = 12;
        GameObject skinPanel;
        Camera previewCam;
        Banjjak.Net.Avatar previewAvatar;
        RenderTexture previewRT;
        readonly List<(Image img, Text txt, System.Func<bool> on, bool swatch)> skinBtns = new List<(Image, Text, System.Func<bool>, bool)>();
        float previewT;

        void BuildSkin()
        {
            var p = Box("skin", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(0, 0));
            p.anchorMin = A(0, 0); p.anchorMax = A(1, 1); p.sizeDelta = A(0, 0);
            skinPanel = p.gameObject;
            var bg = p.gameObject.AddComponent<Image>(); bg.color = new Color(.03f, .05f, .08f, .55f); bg.raycastTarget = true;
            var card = Box("card", p, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(820, 580));
            Panel(card, new Color(1, 1, 1, .98f));
            Label(card, "꾸미기", 13, Accent, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(32, -20), A(200, 20), true);
            Label(card, "내 캐릭터", 24, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(32, -40), A(300, 32), true);

            // live preview: the character on its own layer, filmed by its own camera into a texture
            var pv = Box("preview", card, A(0, 1), A(0, 1), A(32, -84), A(300, 400));
            Panel(pv, G.Hex(0xe8eef6));
            previewRT = new RenderTexture(600, 800, 24) { name = "skinPreview" };
            var raw = new GameObject("rt", typeof(RectTransform)).AddComponent<RawImage>();
            raw.transform.SetParent(pv, false);
            ((RectTransform)raw.transform).sizeDelta = A(300, 400);
            raw.texture = previewRT;
            raw.raycastTarget = false;

            float y = -84;
            void Row(string name, int count, System.Func<int, string> label, System.Func<int, Color?> swatch, System.Action<int> pick, System.Func<int, bool> on, int perRow)
            {
                Label(card, name, 15, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(360, y), A(110, 38), true);
                float w = 92, gap = 8;
                for (int i = 0; i < count; i++)
                {
                    int idx = i, r = i / perRow, c = i % perRow;
                    var sw = swatch(i);
                    var b = Btn(card, sw.HasValue ? "" : label(i), A(0, 1), A(0, 1), A(450 + c * (w + gap), y - r * 44), A(w, 38),
                        sw ?? G.Hex(0xeef2f7), Ink, () => { pick(idx); OnLookChanged(); }, 14);
                    skinBtns.Add(((Image)b.targetGraphic, b.GetComponentInChildren<Text>(), () => on(idx), sw.HasValue));
                }
                y -= 44 * ((count + perRow - 1) / perRow) + 14;
            }
            var a = Skins.Animals;
            Row("캐릭터", a.Length, i => a[i].Name, i => null, i => SetLook(l => { l.Animal = i; return l; }), i => CurLook().Animal == i, 3);
            Row("털 색", Skins.FurNames.Length, i => Skins.FurNames[i], i => i == 0 ? (Color?)null : Skins.Furs[i], i => SetLook(l => { l.Fur = i; return l; }), i => CurLook().Fur == i, 3);
            Row("작업복", Skins.Suits.Length, i => Skins.SuitNames[i], i => Skins.Suits[i], i => SetLook(l => { l.Suit = i; return l; }), i => CurLook().Suit == i, 3);
            Row("모자", Skins.HatNames.Length, i => Skins.HatNames[i], i => null, i => SetLook(l => { l.Hat = i; return l; }), i => CurLook().Hat == i, 4);
            Btn(card, "닫기", A(.5f, 0), A(.5f, 0), A(0, 18), A(160, 42), Accent, Color.white, () => ShowSkin(false), 15);
            skinPanel.SetActive(false);
        }

        Skins.Look CurLook() => Skins.Look.Parse(gm.Save.Look);

        void SetLook(System.Func<Skins.Look, Skins.Look> change) => gm.SetLook(change(CurLook()));

        void OnLookChanged()
        {
            RefreshSkin();
            if (previewAvatar != null) previewAvatar.SetLook(CurLook());
        }

        void RefreshSkin()
        {
            foreach (var (img, txt, on, swatch) in skinBtns)
            {
                bool sel = on();
                if (swatch) txt.text = sel ? "●" : "";
                else { img.color = sel ? Accent : G.Hex(0xeef2f7); txt.color = sel ? Color.white : Ink; }
                if (swatch) txt.color = Color.white;
            }
        }

        public void ShowSkin(bool on)
        {
            if (skinPanel == null) return;
            skinPanel.SetActive(on);
            if (!on) { loginClosedFrame = Time.frameCount; if (previewCam != null) previewCam.enabled = false; return; }
            if (previewAvatar == null)
            {
                previewAvatar = Banjjak.Net.Avatar.Create("preview", "", CurLook(), true);
                previewAvatar.Layer = PreviewLayer;
                previewAvatar.transform.position = new Vector3(0, -300, 0);
                Banjjak.Net.Avatar.SetLayer(previewAvatar.transform, PreviewLayer);
                var cg = new GameObject("PreviewCam");
                previewCam = cg.AddComponent<Camera>();
                previewCam.cullingMask = 1 << PreviewLayer;
                previewCam.clearFlags = CameraClearFlags.SolidColor;
                previewCam.backgroundColor = G.Hex(0xe8eef6);
                previewCam.fieldOfView = 30;
                previewCam.targetTexture = previewRT;
                // whole body, hat to boots, with a little room around
                cg.transform.position = new Vector3(0, -300 + 1.15f, 4.3f);
                cg.transform.LookAt(new Vector3(0, -300 + .98f, 0));
                if (gm.Player != null && gm.Player.Cam != null) gm.Player.Cam.cullingMask &= ~(1 << PreviewLayer);
            }
            else previewAvatar.SetLook(CurLook());
            previewCam.enabled = true;
            RefreshSkin();
        }

        void UpdateSkin()
        {
            if (skinPanel == null || !skinPanel.activeSelf || previewAvatar == null) return;
            previewT += Time.unscaledDeltaTime;
            // turn slowly back and forth so the face, the hat and the tool are all seen
            previewAvatar.Pose(Mathf.Sin(previewT * .6f) * 40, 6);
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) ShowSkin(false);
            if (gm.Mode != GameMode.Menu) ShowSkin(false);
        }

        // ------------------------------------------------------------------ co-op (같이 하기)
        Button coopBtn;
        GameObject coopPanel, coopIdle, coopRoom;
        InputField coopCode;
        Text coopStatus, coopCodeBig, coopLink, coopNames;
        RectTransform coopBadge; Text coopBadgeText;
        float coopRefreshT, coopMenuT;

        void BuildCoop()
        {
            var p = Box("coop", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(0, 0));
            p.anchorMin = A(0, 0); p.anchorMax = A(1, 1); p.sizeDelta = A(0, 0);
            coopPanel = p.gameObject;
            var bg = p.gameObject.AddComponent<Image>(); bg.color = new Color(.03f, .05f, .08f, .55f); bg.raycastTarget = true;
            var card = Box("card", p, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(460, 430));
            Panel(card, new Color(1, 1, 1, .98f));
            Label(card, "같이 하기", 13, Accent, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(36, -20), A(200, 20), true);
            Label(card, "친구와 같은 현장 닦기", 24, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(36, -40), A(390, 32), true);
            var d = Label(card, "최대 4명이에요. 방장이 고른 현장으로 다 같이 이동하고, 누가 닦든 모두의 화면에서 지워져요. 보상은 각자 받아요.", 13, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(36, -80), A(390, 40));
            d.horizontalOverflow = HorizontalWrapMode.Wrap;

            var idle = Box("idle", card, A(.5f, 1), A(.5f, 1), A(0, -128), A(460, 200));
            coopIdle = idle.gameObject;
            Btn(idle, "방 만들기", A(.5f, 1), A(.5f, 1), A(0, 0), A(388, 46), Accent, Color.white, () => { Banjjak.Net.Coop.I.Host(); RefreshCoop(); }, 16);
            Label(idle, "또는 친구의 방 코드로 참가", 13, Sub, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(36, -64), A(390, 20));
            var f = Box("code", idle, A(0, 1), A(0, 1), A(36, -92), A(250, 46));
            var fimg = Panel(f, G.Hex(0xf1f4f8)); fimg.raycastTarget = true;
            var ft = Label(f, "", 22, Ink, TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(240, 42), true);
            var fph = Label(f, "숫자 4자리", 15, new Color(Sub.r, Sub.g, Sub.b, .7f), TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(240, 42));
            coopCode = f.gameObject.AddComponent<InputField>();
            coopCode.textComponent = ft; coopCode.placeholder = fph; coopCode.targetGraphic = fimg;
            coopCode.characterLimit = 4; coopCode.contentType = InputField.ContentType.IntegerNumber;
            Btn(idle, "참가", A(0, 1), A(0, 1), A(298, -92), A(126, 46), Good, Color.white, () => { Banjjak.Net.Coop.I.Join(coopCode.text); RefreshCoop(); }, 16);

            var room = Box("room", card, A(.5f, 1), A(.5f, 1), A(0, -128), A(460, 220));
            coopRoom = room.gameObject;
            Label(room, "방 코드", 13, Sub, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(36, 0), A(200, 18));
            coopCodeBig = Label(room, "", 44, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(36, -18), A(200, 54), true);
            coopLink = Label(room, "", 12, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(36, -74), A(390, 34));
            coopLink.horizontalOverflow = HorizontalWrapMode.Wrap;
            coopNames = Label(room, "", 15, Ink, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(236, -6), A(200, 90));
            Btn(room, "방 나가기", A(0, 1), A(0, 1), A(36, -116), A(150, 40), G.Hex(0xeef2f7), Ink, () => { Banjjak.Net.Coop.I.Leave(); RefreshCoop(); }, 14);

            coopStatus = Label(card, "", 13, Sub, TextAnchor.MiddleLeft, A(0, 0), A(0, 0), A(36, 70), A(390, 22));
            Btn(card, "닫기", A(.5f, 0), A(.5f, 0), A(0, 18), A(140, 40), Accent, Color.white, () => ShowCoop(false), 15);
            coopPanel.SetActive(false);

            // in-game badge (top left, under the FPS readout)
            coopBadge = Box("coopBadge", root, A(0, 1), A(0, 1), A(24, -152), A(210, 26));
            Panel(coopBadge, new Color(1, .82f, .4f, .92f), true);
            coopBadgeText = Label(coopBadge, "", 13, Ink, TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(210, 26), true);
            coopBadge.gameObject.SetActive(false);
        }

        public void ShowCoop(bool on)
        {
            if (coopPanel == null) return;
            coopPanel.SetActive(on);
            if (!on) loginClosedFrame = Time.frameCount;
            RefreshCoop();
        }

        public void RefreshCoop()
        {
            var c = Banjjak.Net.Coop.I;
            if (coopPanel == null || c == null) return;
            coopBtn.gameObject.SetActive(CloudConfig.Enabled);
            coopIdle.SetActive(!c.Active);
            coopRoom.SetActive(c.Active);
            coopStatus.text = c.Status;
            if (gm != null && gm.Mode == GameMode.Menu && menuPanel != null) RefreshMenu();
            if (c.Active)
            {
                coopCodeBig.text = c.Code;
                coopLink.text = "친구에게 코드를 알려 주거나 이 주소를 보내세요\nhyunminkong.github.io/banjjak-wash/?room=" + c.Code;
                coopNames.text = string.Join("\n", c.Names());
            }
        }

        void UpdateCoop()
        {
            var c = Banjjak.Net.Coop.I;
            if (c == null || coopBadge == null || gm == null) return;
            bool show = c.Active && gm.Mode != GameMode.Menu;
            coopBadge.gameObject.SetActive(show);
            // the job cards follow the room (the host's job turns orange, progress moves) while the menu is open
            if (c.Active && gm.Mode == GameMode.Menu)
            {
                coopMenuT += Time.unscaledDeltaTime;
                if (coopMenuT > 1) { coopMenuT = 0; RefreshMenu(); }
            }
            if (show) coopBadgeText.text = c.Connected ? $"같이 하기 {c.Code} · {c.Names().Count}명" : c.Status;
            if (coopPanel.activeSelf)
            {
                coopRefreshT += Time.unscaledDeltaTime;
                if (coopRefreshT > .5f) { coopRefreshT = 0; RefreshCoop(); }
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) ShowCoop(false);
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !c.Active) { c.Join(coopCode.text); RefreshCoop(); }
                if (gm.Mode != GameMode.Menu) coopPanel.SetActive(false);
            }
        }

        void OnAccountButton()
        {
            if (gm.Acct.LoggedIn) { gm.Logout(); RefreshAccount(); return; }
            loginMsg.text = "";
            loginPanel.SetActive(true);
            idField.ActivateInputField();
        }

        public void RefreshAccount()
        {
            if (accountBtn == null) return;
            accountBtn.gameObject.SetActive(CloudConfig.Enabled);
            accountText.text = gm.Acct.LoggedIn ? $"{gm.Acct.Username} · 로그아웃" : "로그인 / 회원가입";
        }

        InputField Field(RectTransform parent, float y, string placeholder, bool password)
        {
            var rt = Box("field", parent, A(.5f, 1), A(.5f, 1), A(0, y), A(340, 44));
            var img = Panel(rt, G.Hex(0xf1f4f8));
            img.raycastTarget = true;
            var text = Label(rt, "", 16, Ink, TextAnchor.MiddleLeft, A(0, .5f), A(0, .5f), A(14, 0), A(312, 40));
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var ph = Label(rt, placeholder, 14, new Color(Sub.r, Sub.g, Sub.b, .75f), TextAnchor.MiddleLeft, A(0, .5f), A(0, .5f), A(14, 0), A(312, 40));
            var input = rt.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = img;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.characterLimit = password ? 64 : 20;
            return input;
        }

        void BuildLogin()
        {
            var p = Box("login", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(0, 0));
            p.anchorMin = A(0, 0); p.anchorMax = A(1, 1); p.sizeDelta = A(0, 0);
            loginPanel = p.gameObject;
            var bg = p.gameObject.AddComponent<Image>(); bg.color = new Color(.03f, .05f, .08f, .55f); bg.raycastTarget = true;
            var card = Box("card", p, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(420, 392));
            Panel(card, new Color(1, 1, 1, .98f));
            Label(card, "계정", 13, Accent, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(40, -20), A(200, 20), true);
            Label(card, "로그인 · 회원가입", 24, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(40, -40), A(340, 32), true);
            var d = Label(card, "로그인하면 진행도가 계정에 저장돼서 다른 컴퓨터에서도 이어서 할 수 있어요. 처음이면 아이디와 비밀번호를 정하고 회원가입을 누르세요.", 13, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(40, -82), A(340, 56));
            d.horizontalOverflow = HorizontalWrapMode.Wrap;
            idField = Field(card, -146, "아이디 (영문·숫자·_ 3~20자)", false);
            pwField = Field(card, -200, "비밀번호 (6자 이상)", true);
            loginMsg = Label(card, "", 13, Sub, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(40, -254), A(340, 22), true);
            loginBtn = Btn(card, "로그인", A(.5f, 0), A(.5f, 0), A(-87, 56), A(166, 46), Accent, Color.white, () => SubmitLogin(false), 16);
            signupBtn = Btn(card, "회원가입", A(.5f, 0), A(.5f, 0), A(87, 56), A(166, 46), G.Hex(0x30a46c), Color.white, () => SubmitLogin(true), 16);
            Btn(card, "닫기", A(.5f, 0), A(.5f, 0), A(0, 14), A(120, 32), new Color(1, 1, 1, 0), Sub, () => loginPanel.SetActive(false), 13);
            loginPanel.SetActive(false);
        }

        void SubmitLogin(bool signUp)
        {
            if (gm.Acct.Busy) return;
            loginMsg.color = Sub;
            loginMsg.text = signUp ? "가입하는 중…" : "로그인하는 중…";
            loginBtn.interactable = signupBtn.interactable = false;
            gm.SubmitLogin(idField.text, pwField.text, signUp, (ok, msg) =>
            {
                loginBtn.interactable = signupBtn.interactable = true;
                loginMsg.color = ok ? Good : G.Hex(0xe5484d);
                loginMsg.text = msg;
                if (ok) { pwField.text = ""; StartCoroutine(CloseLoginSoon()); }
                RefreshAccount();
            });
        }

        System.Collections.IEnumerator CloseLoginSoon()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            loginPanel.SetActive(false);
        }

        public void OnModeChanged()
        {
            if (menuPanel == null) return;
            var m = gm.Mode;
            bool inGame = m != GameMode.Menu;
            foreach (var g in gameplayUi) g.SetActive(inGame);
            helpPanel.SetActive(inGame && helpOn);
            listPanel.SetActive(inGame && listOn);
            if (!inGame) targetBox.gameObject.SetActive(false);
            menuPanel.SetActive(m == GameMode.Menu);
            pausePanel.SetActive(m == GameMode.Paused && !TestMode);
            if (m != GameMode.Paused) ShowSettings(false);
            resultPanel.SetActive(m == GameMode.Result);
            if (m == GameMode.Menu) RefreshMenu();
        }

        public void SetResult(float bonus)
        {
            var j = gm.Job;
            resultText.text = $"걸린 시간 {G.Clock(j.Elapsed)}\n번 돈 {G.Money(j.Earned)} (완료 보너스 {G.Money(bonus)} 포함)";
        }

        // ------------------------------------------------------------------ building blocks
        static Sprite RoundSprite(int n, int r)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + .5f, r, n - r), cy = Mathf.Clamp(y + .5f, r, n - r);
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(r - d + .5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        Image Panel(RectTransform rt, Color c, bool small = false, float radiusMul = 1)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = small ? roundSmall : round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = radiusMul;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        Text Label(Transform parent, string text, int size, Color c, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sz, bool bold = false)
        {
            var rt = Box("t", parent, anchor, pivot, pos, sz);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? fontBold : font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = bold && fontBold == font ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        static Vector2 A(float x, float y) => new Vector2(x, y);

        void BuildJobCard()
        {
            var card = Box("job", root, A(0, 1), A(0, 1), A(18, -18), A(300, 92));
            Panel(card, Card);
            jobName = Label(card, gm.Job.Name, 20, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(18, -12), A(200, 28), true);
            Label(card, "자동 저장", 13, Good, TextAnchor.MiddleRight, A(1, 1), A(1, 1), A(-16, -14), A(100, 24), true);
            var bar = Box("bar", card, A(0, 1), A(0, 1), A(18, -48), A(264, 8));
            Panel(bar, G.Hex(0xe6ebf1), true, 3);
            var fill = Box("fill", bar, A(0, .5f), A(0, .5f), A(0, 0), A(0, 8));
            progressFill = Panel(fill, Accent, true, 3);
            pctText = Label(card, "0%", 20, Ink, TextAnchor.MiddleLeft, A(0, 0), A(0, 0), A(18, 8), A(120, 28), true);
            timeText = Label(card, "00:00", 14, Sub, TextAnchor.MiddleRight, A(1, 0), A(1, 0), A(-16, 10), A(100, 24));
        }

        void BuildMoney()
        {
            var card = Box("money", root, A(1, 1), A(1, 1), A(-18, -18), A(210, 64));
            Panel(card, Card);
            var coin = Box("coin", card, A(0, .5f), A(0, .5f), A(16, 0), A(30, 30));
            Panel(coin, G.Hex(0xf5b82e), true, 1.6f);
            Label(coin, "₩", 16, Color.white, TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, 1), A(30, 30), true).text = "$";
            moneyText = Label(card, "$0", 24, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(56, -8), A(150, 30), true);
            psiText = Label(card, "", 12, Sub, TextAnchor.MiddleLeft, A(0, 0), A(0, 0), A(57, 6), A(150, 20));
        }

        void BuildToast()
        {
            toastBox = Box("toast", root, A(.5f, 1), A(.5f, 1), A(0, -22), A(420, 64));
            toastBg = Panel(toastBox, Card);
            toastTitle = Label(toastBox, "", 18, Ink, TextAnchor.MiddleCenter, A(.5f, 1), A(.5f, 1), A(0, -8), A(400, 26), true);
            toastSub = Label(toastBox, "", 14, Good, TextAnchor.MiddleCenter, A(.5f, 0), A(.5f, 0), A(0, 8), A(400, 22), true);
            toastBox.gameObject.SetActive(false);
        }

        void BuildCrosshair()
        {
            var c = Box("cross", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(6, 6));
            crossDot = Panel(c, new Color(1, 1, 1, .9f), true, 6);
            crossFan = Box("fan", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(60, 2));
            Panel(crossFan, new Color(1, 1, 1, .55f), true, 6);
        }

        void BuildTarget()
        {
            targetBox = Box("target", root, A(.5f, 0), A(.5f, 0), A(0, 112), A(380, 50));
            Panel(targetBox, new Color(1, 1, 1, .9f));
            targetName = Label(targetBox, "", 15, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(16, -6), A(250, 22), true);
            var chip = Box("chip", targetBox, A(0, 0), A(0, 0), A(16, 9), A(10, 10));
            targetChip = Panel(chip, Color.gray, true, 3);
            targetDirt = Label(targetBox, "", 13, Sub, TextAnchor.MiddleLeft, A(0, 0), A(0, 0), A(32, 4), A(200, 20));
            targetPct = Label(targetBox, "", 15, Ink, TextAnchor.MiddleRight, A(1, .5f), A(1, .5f), A(-16, 0), A(80, 24), true);
            var bar = Box("bar", targetBox, A(1, 0), A(1, 0), A(-16, 8), A(90, 5));
            Panel(bar, G.Hex(0xe6ebf1), true, 4);
            targetFill = Panel(Box("fill", bar, A(0, .5f), A(0, .5f), A(0, 0), A(0, 5)), Good, true, 4);
            targetBox.gameObject.SetActive(false);
            hintText = Label(root, "", 14, Color.white, TextAnchor.MiddleCenter, A(.5f, 0), A(.5f, 0), A(0, 172), A(800, 24));
            var sh = hintText.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .6f); sh.effectDistance = A(1, -1);
        }

        void BuildToolbar()
        {
            var bar = Box("tools", root, A(.5f, 0), A(.5f, 0), A(-172, 18), A(300, 76));
            Panel(bar, Card);
            string[] names = { "고압 건", "거품", "바닥\n세척기", "자동\n잔디" };
            for (int i = 0; i < 4; i++)
            {
                var s = Box("slot" + i, bar, A(0, .5f), A(0, .5f), A(10 + i * 71, 0), A(64, 60));
                slots[i] = Panel(s, G.Hex(0xf1f4f8), false, 1.4f);
                Label(s, (i + 1).ToString(), 11, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(6, -3), A(20, 16), true);
                slotLabels[i] = Label(s, names[i], 12, Ink, TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, -5), A(64, 36), true);
                slotLabels[i].lineSpacing = .9f;
            }
            var ang = Box("angle", root, A(.5f, 0), A(.5f, 0), A(55, 18), A(130, 76));
            Panel(ang, Card);
            angleTitle = Label(ang, "분사 각도", 12, Sub, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(14, -10), A(110, 20));
            angleValue = Label(ang, "30°", 26, Ink, TextAnchor.MiddleLeft, A(0, 0), A(0, 0), A(14, 8), A(110, 34), true);
            // trash bag + gadgets (bottom left)
            var bagBox = Box("bag", root, A(0, 0), A(0, 0), A(18, 18), A(210, 64));
            Panel(bagBox, Card);
            Label(bagBox, "쓰레기 봉투", 12, Sub, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(14, -8), A(120, 20));
            bagText = Label(bagBox, "0/15", 20, Ink, TextAnchor.MiddleLeft, A(0, 0), A(0, 0), A(14, 6), A(80, 30), true);
            gadgetText = Label(bagBox, "", 12, Sub, TextAnchor.MiddleRight, A(1, .5f), A(1, .5f), A(-12, -4), A(110, 44));
            // E prompt under the crosshair
            useTipBox = Box("useTip", root, A(.5f, .5f), A(.5f, 1), A(0, -26), A(220, 30));
            var tipBg = Panel(useTipBox, new Color(.06f, .09f, .13f, .72f), true, 2);
            useTip = Label(useTipBox, "", 14, Color.white, TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(220, 30), true);
            useTipBox.gameObject.SetActive(false);
            var ag = Box("agent", root, A(.5f, 0), A(.5f, 0), A(227, 18), A(190, 76));
            Panel(ag, Card);
            Label(ag, "세제", 12, Sub, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(14, -10), A(100, 20));
            agentName = Label(ag, "", 15, Ink, TextAnchor.MiddleLeft, A(0, .5f), A(0, .5f), A(14, -2), A(170, 24), true);
            var sb = Box("soap", ag, A(0, 0), A(0, 0), A(14, 12), A(160, 6));
            Panel(sb, G.Hex(0xe6ebf1), true, 4);
            soapFill = Panel(Box("fill", sb, A(0, .5f), A(0, .5f), A(0, 0), A(160, 6)), Accent, true, 4);
        }

        void BuildHelp()
        {
            var p = Box("help", root, A(1, 0), A(1, 0), A(-18, 18), A(282, 196));
            helpPanel = p.gameObject;
            Panel(p, new Color(1, 1, 1, .86f));
            var sb = new StringBuilder();
            sb.AppendLine("<b>조작</b>");
            sb.AppendLine("WASD 이동 · Shift 달리기 · Space 점프");
            sb.AppendLine("Ctrl 앉기/일어서기");
            sb.AppendLine("왼쪽 클릭 분사 · 휠 분사 각도");
            sb.AppendLine("1~4 도구 · Q 세제 바꾸기 · R 세로/가로");
            sb.AppendLine("E 쓰레기 줍기·잡초 뽑기·버리기");
            sb.AppendLine("4 자동 잔디 깎기 · G 로봇 · F 남은 때 표시");
            sb.AppendLine("L 작업대 · 작업대 위 Space 올리기 · C 내리기");
            sb.AppendLine("Tab 체크리스트 · H 도움말 · Esc 일시정지");
            Label(p, sb.ToString(), 13, Ink, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(16, -14), A(270, 220)).lineSpacing = 1.15f;
            p.sizeDelta = A(300, 234);
        }

        void BuildList()
        {
            var p = Box("list", root, A(0, 1), A(0, 1), A(18, -122), A(300, 520));
            listPanel = p.gameObject;
            Panel(p, new Color(1, 1, 1, .93f));
            listText = Label(p, "", 14, Ink, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(18, -16), A(270, 490));
            listText.lineSpacing = 1.1f;
            listPanel.SetActive(false);
        }

        void BuildPause()
        {
            var p = Box("pause", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(4000, 4000));
            pausePanel = p.gameObject;
            var bg = p.gameObject.AddComponent<Image>(); bg.color = new Color(.05f, .08f, .12f, .45f); bg.raycastTarget = false;
            var card = Box("card", p, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(460, 326));
            Panel(card, new Color(1, 1, 1, .96f));
            Label(card, "일시정지", 14, Accent, TextAnchor.MiddleCenter, A(.5f, 1), A(.5f, 1), A(0, -26), A(420, 20), true);
            pauseTitle = Label(card, "반짝 워시", 34, Ink, TextAnchor.MiddleCenter, A(.5f, 1), A(.5f, 1), A(0, -64), A(420, 44), true);
            pauseDesc = Label(card, "", 14, Sub, TextAnchor.UpperCenter, A(.5f, 1), A(.5f, 1), A(0, -102), A(400, 60));
            pauseDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Btn(card, "설정 · 화질과 FPS", A(.5f, 0), A(.5f, 0), A(0, 86), A(398, 42), G.Hex(0xeef2f7), Ink, () => ShowSettings(true), 15);
            Btn(card, "계속하기", A(.5f, 0), A(.5f, 0), A(-104, 26), A(190, 48), Accent, Color.white, () => gm.SetMode(GameMode.Playing), 17);
            Btn(card, "의뢰 목록", A(.5f, 0), A(.5f, 0), A(104, 26), A(190, 48), G.Hex(0xeef2f7), Ink, () => gm.SetMode(GameMode.Menu), 17);
            BuildSettings(p);
        }

        // ------------------------------------------------------------------ settings (Esc → 설정)
        GameObject settingsPanel;
        readonly List<(Image img, Text txt, System.Func<bool> on)> settingBtns = new List<(Image, Text, System.Func<bool>)>();
        public bool SettingsOpen => settingsPanel != null && settingsPanel.activeInHierarchy;

        public void ShowSettings(bool on)
        {
            if (settingsPanel == null) return;
            settingsPanel.SetActive(on);
            if (on) RefreshSettings();
        }

        /// <summary>Test only: the pause screen is hidden in test mode, so show the settings on their own.</summary>
        public void TestShowSettings(bool on) { pausePanel.SetActive(on); ShowSettings(on); }

        void BuildSettings(RectTransform parent)
        {
            var card = Box("settings", parent, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(560, 440));
            settingsPanel = card.gameObject;
            var cardBg = Panel(card, new Color(1, 1, 1, .98f));
            cardBg.raycastTarget = true;   // clicks on the card never reach the game
            Label(card, "설정", 14, Accent, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(36, -22), A(200, 20), true);
            Label(card, "화질과 FPS", 26, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(36, -46), A(400, 34), true);
            var hint = Label(card, "게임이 끊기면 화질을 낮추거나 해상도를 줄여 보세요. 이 기기에만 저장돼요.", 13, Sub, TextAnchor.UpperLeft, A(0, 1), A(0, 1), A(36, -86), A(490, 36));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;

            float y = -134;
            void Row(string name, string[] labels, System.Action<int> pick, System.Func<int, bool> isOn)
            {
                Label(card, name, 15, Ink, TextAnchor.MiddleLeft, A(0, 1), A(0, 1), A(36, y), A(110, 40), true);
                float w = (560 - 36 - 150 - 8f * (labels.Length - 1)) / labels.Length;
                for (int i = 0; i < labels.Length; i++)
                {
                    int idx = i;
                    var b = Btn(card, labels[i], A(0, 1), A(0, 1), A(150 + i * (w + 8), y), A(w, 40), G.Hex(0xeef2f7), Ink, () => { pick(idx); RefreshSettings(); }, 14);
                    settingBtns.Add(((Image)b.targetGraphic, b.GetComponentInChildren<Text>(), () => isOn(idx)));
                }
                y -= 56;
            }
            Row("화질", GfxSettings.QualityNames, i => GfxSettings.SetQuality(i), i => GfxSettings.Quality == i);
            var scaleNames = new string[GfxSettings.Scales.Length];
            for (int i = 0; i < scaleNames.Length; i++) scaleNames[i] = GfxSettings.Scales[i] + "%";
            Row("해상도", scaleNames, i => GfxSettings.SetScale(GfxSettings.Scales[i]), i => GfxSettings.Scale == GfxSettings.Scales[i]);
            Row("FPS 제한", new[] { "30", "60", "제한 없음" }, i => GfxSettings.SetFps(GfxSettings.FpsCaps[i]), i => GfxSettings.Fps == GfxSettings.FpsCaps[i]);
            Row("FPS 표시", new[] { "끄기", "켜기" }, i => GfxSettings.SetShowFps(i == 1), i => GfxSettings.ShowFps == (i == 1));
            Btn(card, "닫기", A(.5f, 0), A(.5f, 0), A(0, 22), A(200, 44), Accent, Color.white, () => ShowSettings(false), 16);
            settingsPanel.SetActive(false);
        }

        void RefreshSettings()
        {
            foreach (var (img, txt, on) in settingBtns)
            {
                bool sel = on();
                img.color = sel ? Accent : G.Hex(0xeef2f7);
                txt.color = sel ? Color.white : Ink;
            }
        }

        // FPS readout (top left, under the job card), on when 설정 → FPS 표시 is on
        RectTransform fpsBox;
        Text fpsText;
        int fpsFrames;
        float fpsT;

        void BuildFps()
        {
            fpsBox = Box("fps", root, A(0, 1), A(0, 1), A(24, -122), A(92, 26));
            Panel(fpsBox, new Color(1, 1, 1, .85f), true);
            fpsText = Label(fpsBox, "", 13, Ink, TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(92, 26), true);
            fpsBox.gameObject.SetActive(false);
        }

        void UpdateFps()
        {
            if (fpsBox == null) return;
            // in play and pause only: on the job menu it would float beside the title
            fpsBox.gameObject.SetActive(GfxSettings.ShowFps && gm != null && gm.Mode != GameMode.Menu);
            fpsFrames++;
            fpsT += Time.unscaledDeltaTime;
            if (fpsT >= .5f)
            {
                fpsText.text = Mathf.RoundToInt(fpsFrames / fpsT) + " FPS";
                fpsFrames = 0; fpsT = 0;
            }
        }

        void BuildResult()
        {
            var p = Box("result", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(440, 250));
            resultPanel = p.gameObject;
            Panel(p, new Color(1, 1, 1, .97f));
            Label(p, "작업 완료!", 30, Ink, TextAnchor.MiddleCenter, A(.5f, 1), A(.5f, 1), A(0, -40), A(400, 40), true);
            resultText = Label(p, "", 16, Sub, TextAnchor.UpperCenter, A(.5f, 1), A(.5f, 1), A(0, -86), A(380, 90));
            Btn(p, "다음 의뢰 고르기", A(.5f, 0), A(.5f, 0), A(-104, 24), A(196, 48), Accent, Color.white, () => gm.SetMode(GameMode.Menu), 16);
            Btn(p, "계속 둘러보기", A(.5f, 0), A(.5f, 0), A(104, 24), A(196, 48), G.Hex(0xeef2f7), Ink, () => gm.SetMode(GameMode.Playing), 16);
            resultPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ runtime
        public void Toast(string title, string sub, ToastKind kind) => toasts.Enqueue((title, sub, kind));

        public void RefreshTools()
        {
            if (gm == null || gm.Washer == null || slots[0] == null) return;
            var w = gm.Washer;
            for (int i = 0; i < 4; i++)
            {
                bool own = gm.Owned(Shop.Tools[i].Id);
                bool on = i == 3 ? Robots.AutoMower != null : i == w.Tool;
                slots[i].color = on ? (i == 3 ? G.Hex(0xdcf3e2) : G.Hex(0xdcecff)) : G.Hex(0xf1f4f8);
                slotLabels[i].color = on ? (i == 3 ? Good : Accent) : own ? Ink : new Color(Ink.r, Ink.g, Ink.b, .3f);
            }
            if (w.Tool == 0)
            {
                angleTitle.text = "분사 각도 · 휠";
                angleValue.text = w.Turbo ? "터보" : w.Deg + "°" + (w.FanVertical ? " <size=14>세로</size>" : "");
            }
            else if (w.Tool == 1) { angleTitle.text = "거품 분사"; angleValue.text = "넓게"; }
            else if (w.Tool == 2) { angleTitle.text = "바닥 세척기"; angleValue.text = gm.Owned("bigdisc") ? "1.5m" : "1m"; }
            else { angleTitle.text = "잔디 깎기"; angleValue.text = "밀기"; }
            var bots = new List<string>();
            int nb = Robots.Owned(gm.Save);
            if (nb > 0) bots.Add(Robots.Active.Count > 0 ? $"로봇 {Robots.Active.Count}대 작업 중" : $"로봇 {nb}대 · G");
            if (gm.Owned("lift") || (gm.Job != null && gm.Job.FreeLift)) bots.Add("작업대 · L");
            if (gadgetText != null) gadgetText.text = string.Join("\n", bots);
            var ag = Agents.All[gm.Save.Agent];
            agentName.text = ag.Name;
            soapFill.color = Color.Lerp(ag.Ui, Accent, .45f);
            psiText.text = Washers.All[gm.Save.Washer].Psi + " PSI · " + Washers.All[gm.Save.Washer].Name;
        }

        public void ToggleHelp() { helpOn = !helpOn; helpPanel.SetActive(helpOn); }
        public void ToggleList() { listOn = !listOn; listPanel.SetActive(listOn); if (listOn) UpdateList(); }

        void Update()
        {
            // narrow / tall windows (e.g. a browser side pane) scale by width so nothing is cut off
            if (scaler != null) scaler.matchWidthOrHeight = (float)Screen.width / Mathf.Max(1, Screen.height) < 1.5f ? 0f : .5f;
            UpdateFps();
            UpdateCoop();
            UpdateSkin();
            if (loginPanel != null && loginPanel.activeSelf)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) SubmitLogin(false);
                if (kb != null && kb.tabKey.wasPressedThisFrame) { if (idField.isFocused) pwField.ActivateInputField(); else idField.ActivateInputField(); }
                if (kb != null && kb.escapeKey.wasPressedThisFrame) { loginPanel.SetActive(false); loginClosedFrame = Time.frameCount; }
                if (gm.Mode != GameMode.Menu) loginPanel.SetActive(false);
            }
            if (gm == null || gm.Job == null) return;
            var j = gm.Job;
            float p = j.Progress;
            pctText.text = Mathf.FloorToInt(p * 100) + "%";
            progressFill.rectTransform.sizeDelta = A(264 * p, 8);
            progressFill.color = p >= 1 ? Good : Accent;
            timeText.text = G.Clock(j.Elapsed);
            moneyText.text = G.Money(gm.Save.Money);
            soapFill.rectTransform.sizeDelta = A(160 * gm.Washer.Soap / gm.Washer.SoapMax, 6);
            bagText.text = $"{gm.Bag}/{gm.BagCap}";
            bagText.color = gm.Bag >= gm.BagCap ? G.Hex(0xe5484d) : Ink;
            if (gm.Mode == GameMode.Playing)
            {
                string tip = gm.UseHint();
                useTipBox.gameObject.SetActive(tip.Length > 0);
                useTip.text = tip;
                useTipBox.sizeDelta = A(Mathf.Max(150, tip.Length * 15 + 30), 30);
            }
            else useTipBox.gameObject.SetActive(false);

            // fan indicator under the crosshair
            var w = gm.Washer;
            float fanPx = w.Tool == 0 ? (w.Deg == 0 ? 0 : w.Deg * 2.6f + 8) : 46;
            crossFan.sizeDelta = w.FanVertical && w.Tool == 0 ? A(2, fanPx) : A(fanPx, 2);
            crossFan.gameObject.SetActive(fanPx > 0);

            if (menuPanel.activeSelf) menuMoney.text = G.Money(gm.Save.Money);

            if (gm.Mode != GameMode.Menu) UpdateTarget();
            UpdateToast();
            if (listPanel.activeSelf && Time.frameCount % 10 == 0) UpdateList();
            UpdateMarkers();
        }

        void UpdateTarget()
        {
            var cam = gm.Cam.transform;
            string hint = "";
            if (Physics.Raycast(cam.position, cam.forward, out var hit, gm.Washer.Range + 1, Layers.SprayMask, QueryTriggerInteraction.Ignore))
            {
                var s = hit.collider.GetComponent<DirtSurface>();
                if (s != null && s.N > 0)
                {
                    var c = s.Comp;
                    targetBox.gameObject.SetActive(true);
                    targetName.text = c.Obj.Name + " · " + c.Name;
                    float pct = c.Done ? 1 : c.Progress;
                    targetPct.text = Mathf.FloorToInt(pct * 100) + "%";
                    targetFill.rectTransform.sizeDelta = A(90 * pct, 5);
                    int k = s.SampleNear(hit.point);
                    if (c.Done) { targetDirt.text = "깨끗해요"; targetChip.color = Good; }
                    else if (k >= 0 && s.DirtAmt[k] >= G.CleanT)
                    {
                        var dt = Dirt.All[s.Type[k]];
                        targetDirt.text = dt.Name;
                        targetChip.color = dt.Ui;
                        if (s.Foam != null && s.Foam[k] >= .1f && s.Soak[k] < 1) hint = $"거품이 때를 불리는 중… {Mathf.RoundToInt(s.Soak[k] * 100)}%";
                        else if (s.Soak != null && s.Soak[k] >= .15f) hint = $"다 불었어요! 지금 헹구면 {1 + (Agents.All[s.AgentId[k]].Eff[s.Type[k]] - 1) * s.Soak[k]:0.0}배 빨리 지워져요";
                        else if (Dirt.Hints.TryGetValue(dt.Key, out var h) && gm.Washer.Spraying) hint = h;
                    }
                    else { targetDirt.text = "여기는 깨끗해요"; targetChip.color = Good; }
                }
                else targetBox.gameObject.SetActive(false);
            }
            else targetBox.gameObject.SetActive(false);
            hintText.text = hint;
        }

        void UpdateToast()
        {
            if (toastT > 0)
            {
                toastT -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(toastT / .3f) * Mathf.Clamp01((2.6f - toastT) / .2f);
                toastBox.anchoredPosition = A(0, -22 + (1 - a) * 20);
                toastBg.color = new Color(1, 1, 1, .95f * a);
                toastTitle.color = new Color(Ink.r, Ink.g, Ink.b, a);
                toastSub.color = new Color(Good.r, Good.g, Good.b, a);
                if (toastT <= 0) toastBox.gameObject.SetActive(false);
                return;
            }
            if (toasts.Count == 0) return;
            var (title, sub, kind) = toasts.Dequeue();
            toastTitle.text = title;
            toastSub.text = sub;
            toastTitle.fontSize = kind == ToastKind.Big ? 22 : 18;
            toastBox.gameObject.SetActive(true);
            toastT = kind == ToastKind.Big ? 3.2f : 2.6f;
        }

        // ------------------------------------------------------------------ remaining dirt: list + markers
        /// <summary>A point on a still-dirty voxel of the component, preferring the one nearest the player.</summary>
        Vector3 Anchor(Comp c)
        {
            if (anchors.TryGetValue(c, out var a) && Time.time - a.t < 1.5f) return a.p;
            var player = gm.Player.transform.position + Vector3.up;
            Vector3 best = Vector3.zero; float bd = float.MaxValue; bool found = false;
            foreach (var s in c.Surfs)
            {
                if (s.Done || s.N == 0) continue;
                for (int k = 0; k < 500; k++)
                {
                    int i = Random.Range(0, s.N);
                    if (s.Surf[i] != 2 || s.DirtAmt[i] < G.CleanT) continue;
                    int x = i % s.Nx, y = (i / s.Nx) % s.Ny, z = i / (s.Nx * s.Ny);
                    var p = s.transform.TransformPoint(s.G0 + new Vector3(x + .5f, y + .5f, z + .5f) * s.CellSize);
                    float d = (p - player).sqrMagnitude;
                    if (d < bd) { bd = d; best = p; found = true; }
                }
            }
            if (!found && c.Surfs.Count > 0) best = c.Surfs[0].GetComponent<Renderer>().bounds.center;
            anchors[c] = (best, Time.time);
            return best;
        }

        List<(Comp c, Vector3 p, float d)> Remaining()
        {
            var list = new List<(Comp, Vector3, float)>();
            var pp = gm.Player.transform.position;
            foreach (var o in gm.Job.Objects)
                foreach (var c in o.Comps)
                    if (!c.Done) { var p = Anchor(c); list.Add((c, p, Vector3.Distance(pp, p))); }
            list.Sort((a, b) => a.Item3.CompareTo(b.Item3));
            return list;
        }

        void UpdateList()
        {
            var j = gm.Job;
            var rem = Remaining();
            int total = 0; foreach (var o in j.Objects) total += o.Comps.Count;
            var pp = gm.Player.transform.position;
            var sb = new StringBuilder();
            sb.AppendLine($"<b><size=17>남은 곳 {rem.Count}곳</size></b>  <color=#8a93a0>가까운 순</color>\n");
            for (int i = 0; i < Mathf.Min(16, rem.Count); i++)
            {
                var (c, p, d) = rem[i];
                string floor = p.y - pp.y > 2.2f ? " · 위층" : p.y - pp.y < -2.2f ? " · 아래층" : "";
                sb.AppendLine($"<color=#e5484d>●</color> {c.Obj.Name} · {c.Name}  <color=#5b6573>{Mathf.FloorToInt(c.Progress * 100)}%</color>  <color=#8a93a0>{d:0}m{floor}</color>");
            }
            if (rem.Count > 16) sb.AppendLine($"<color=#8a93a0>… 외 {rem.Count - 16}곳</color>");
            sb.AppendLine($"\n<b>완료</b> {total - rem.Count}/{total}곳");
            var extras = new List<string>();
            if (j.Weeds != null && j.Weeds.N > 0) extras.Add($"잡초 {j.Weeds.Count}/{j.Weeds.N}");
            if (j.Trash != null && j.Trash.N > 0) extras.Add($"쓰레기 {j.Trash.Count}/{j.Trash.N}");
            if (j.Grass != null && j.Grass.Lawn > 0) extras.Add($"잔디 {Mathf.FloorToInt(100f * j.Grass.CutCount / j.Grass.Lawn)}%");
            if (extras.Count > 0) sb.AppendLine("<color=#5b6573>부수입 · " + string.Join(" · ", extras) + "</color>");
            sb.AppendLine("<color=#8a93a0>F로 남은 때를 화면에 표시 (켜기/끄기)</color>");
            listText.text = sb.ToString();
        }

        void UpdateMarkers()
        {
            bool show = gm.Highlight && gm.Mode == GameMode.Playing && gm.Job != null;
            var rem = show ? Remaining() : null;
            int n = show ? Mathf.Min(8, rem.Count) : 0;
            while (markers.Count < n)
            {
                var rt = Box("marker", root, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(200, 28));
                Panel(rt, new Color(.9f, .33f, .1f, .88f), true, 2);
                var t = Label(rt, "", 13, Color.white, TextAnchor.MiddleCenter, A(.5f, .5f), A(.5f, .5f), A(0, 0), A(200, 28), true);
                rt.SetSiblingIndex(1);
                markers.Add((rt, t));
            }
            var cam = gm.Cam;
            var rootSize = root.rect.size;
            for (int i = 0; i < markers.Count; i++)
            {
                var (rt, t) = markers[i];
                if (i >= n) { rt.gameObject.SetActive(false); continue; }
                rt.gameObject.SetActive(true);
                var (c, p, d) = rem[i];
                var sp = cam.WorldToScreenPoint(p);
                bool behind = sp.z < 0;
                if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(sp.x, sp.y), null, out var local);
                float mx = rootSize.x / 2 - 110, my = rootSize.y / 2 - 60;
                bool off = behind || Mathf.Abs(local.x) > mx || Mathf.Abs(local.y) > my;
                if (behind && Mathf.Abs(local.y) < my) local.y = -my;
                local = new Vector2(Mathf.Clamp(local.x, -mx, mx), Mathf.Clamp(local.y, -my, my));
                string arrow = "";
                if (off) arrow = Mathf.Abs(local.x) >= mx - 1 ? (local.x > 0 ? "▶ " : "◀ ") : (local.y > 0 ? "▲ " : "▼ ");
                rt.anchoredPosition = local + A(0, 18);
                t.text = $"{arrow}{c.Name} {Mathf.FloorToInt(c.Progress * 100)}% · {d:0}m";
                rt.sizeDelta = A(Mathf.Max(120, t.preferredWidth + 24), 28);
                t.rectTransform.sizeDelta = rt.sizeDelta;
            }
        }
    }
}
