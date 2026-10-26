using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBlast
{
    /// <summary>
    /// Builds the whole game at runtime (camera, board, tray, UI, audio) and runs input + scoring.
    /// It boots itself via RuntimeInitializeOnLoadMethod, so any scene (even an empty one) works.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        // ---------- Layout (world units, portrait ~ 10 x 18) ----------
        const int N = Board.Size;
        static readonly Vector3 BoardOrigin = new Vector3(-3.5f, -2f, 0f);   // world centre of cell (0,0)
        const float TrayY = -6.3f;
        const float SlotSpacing = 3.4f;
        const float TrayScale = 0.55f;
        const float DragLift = 2.5f;      // piece floats above the finger so it stays visible

        static readonly Color[] Palette =
        {
            new Color(0.95f, 0.30f, 0.35f), new Color(1.00f, 0.60f, 0.20f),
            new Color(1.00f, 0.85f, 0.25f), new Color(0.40f, 0.85f, 0.40f),
            new Color(0.30f, 0.80f, 0.90f), new Color(0.35f, 0.55f, 1.00f),
            new Color(0.65f, 0.45f, 0.95f), new Color(0.95f, 0.45f, 0.75f),
        };
        static readonly Color PanelColor = new Color(0.15f, 0.18f, 0.33f);
        static readonly Color EmptyCellColor = new Color(0.21f, 0.25f, 0.43f);

        class Slot
        {
            public Shape shape;
            public int color;
            public Transform root;
            public Vector3 home;
            public bool busy;
        }

        // ---------- State ----------
        readonly Board board = new Board();
        readonly SpriteRenderer[,] blocks = new SpriteRenderer[N, N];
        readonly SpriteRenderer[] ghosts = new SpriteRenderer[9];
        readonly Slot[] slots = new Slot[3];
        readonly List<int> tmpRows = new List<int>();
        readonly List<int> tmpCols = new List<int>();
        readonly List<ClearedCell> cleared = new List<ClearedCell>();

        Camera cam;
        Transform fxRoot;
        Font font;
        Text scoreText, bestText, popupText, overText;
        GameObject overPanel;
        AudioSource audioSrc;
        AudioClip sfxPlace, sfxClear, sfxOver;
        Coroutine popupRoutine;

        int score, best, combo, movesSinceClear;
        bool gameOver;
        float gameOverTime;
        int dragSlot = -1;
        Vector3 dragTarget;
        float lastAspect;

        // ---------- Boot ----------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<GameController>() != null) return;
            new GameObject("BlockBlast").AddComponent<GameController>();
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            SetupCamera();
            font = LoadFont();
            best = PlayerPrefs.GetInt("best", 0);

            BuildWorld();
            BuildUI();
            BuildAudio();
            NewGame();
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            if (FindObjectOfType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.11f, 0.20f);
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            FitCamera();
        }

        void FitCamera()
        {
            // keep a 10 x 18 unit area visible on any aspect ratio
            cam.orthographicSize = Mathf.Max(9f, 5f / cam.aspect);
        }

        static Font LoadFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 32);
            return f;
        }

        // ---------- Build ----------
        static SpriteRenderer MakeSprite(string name, Sprite sprite, Transform parent, Vector3 localPos, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static Vector3 CellToWorld(int x, int y)
        {
            return new Vector3(BoardOrigin.x + x, BoardOrigin.y + y, 0f);
        }

        void BuildWorld()
        {
            var root = new GameObject("World").transform;
            fxRoot = new GameObject("FX").transform;

            var panel = MakeSprite("BoardPanel", Art.Square, root, CellToWorld(3, 3) + new Vector3(0.5f, 0.5f, 0f), PanelColor, 0);
            panel.transform.localScale = new Vector3(8.5f, 8.5f, 1f);

            for (int x = 0; x < N; x++)
            {
                for (int y = 0; y < N; y++)
                {
                    MakeSprite("Bg", Art.Cell, root, CellToWorld(x, y), EmptyCellColor, 1);
                    var b = MakeSprite("Block", Art.Cell, root, CellToWorld(x, y), Color.white, 2);
                    b.enabled = false;
                    blocks[x, y] = b;
                }
            }

            for (int i = 0; i < ghosts.Length; i++)
            {
                ghosts[i] = MakeSprite("Ghost", Art.Cell, root, Vector3.zero, Color.white, 3);
                ghosts[i].enabled = false;
            }
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            scoreText = MakeText(canvasGo.transform, "Score", 130, FontStyle.Bold, Color.white);
            bestText = MakeText(canvasGo.transform, "Best", 46, FontStyle.Bold, new Color(1f, 0.85f, 0.3f));
            popupText = MakeText(canvasGo.transform, "Popup", 100, FontStyle.Bold, Color.white);
            popupText.gameObject.SetActive(false);

            overPanel = new GameObject("GameOver", typeof(RectTransform), typeof(Image));
            overPanel.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)overPanel.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = overPanel.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.78f);
            img.raycastTarget = false;
            overText = MakeText(overPanel.transform, "OverText", 80, FontStyle.Bold, Color.white);
            overText.rectTransform.anchoredPosition = Vector2.zero;
            overPanel.SetActive(false);
        }

        Text MakeText(Transform parent, string name, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(900f, size * 1.4f);
            return t;
        }

        void PlaceUI(Graphic g, Vector3 world)
        {
            var sp = cam.WorldToScreenPoint(world);
            g.rectTransform.position = new Vector3(sp.x, sp.y, 0f);
        }

        void LayoutUI()
        {
            PlaceUI(scoreText, new Vector3(0f, 7.7f, 0f));
            PlaceUI(bestText, new Vector3(0f, 6.5f, 0f));
            PlaceUI(popupText, new Vector3(0f, 1.5f, 0f));
        }

        void BuildAudio()
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            sfxPlace = Art.Tone(320f, 0.08f, 0.5f, 200f);
            sfxClear = Art.Tone(520f, 0.30f, 0.4f, 1040f);
            sfxOver = Art.Tone(330f, 0.70f, 0.4f, 90f);
        }

        void Sfx(AudioClip clip, float pitch)
        {
            audioSrc.pitch = pitch;
            audioSrc.PlayOneShot(clip);
        }

        // ---------- Game flow ----------
        void NewGame()
        {
            board.Reset();
            foreach (var b in blocks) b.enabled = false;
            score = 0;
            combo = 0;
            movesSinceClear = 0;
            gameOver = false;
            dragSlot = -1;
            overPanel.SetActive(false);
            HideGhosts();
            UpdateScoreUI();
            SpawnTray();
        }

        void SpawnTray()
        {
            var picked = new Shape[3];
            for (int attempt = 0; attempt < 40; attempt++)
            {
                bool anyFits = false;
                for (int i = 0; i < 3; i++)
                {
                    picked[i] = ShapeLibrary.Next();
                    anyFits |= board.CanPlaceAnywhere(picked[i]);
                }
                if (anyFits) break;   // never hand the player a hand where nothing can be placed (if avoidable)
            }

            for (int i = 0; i < 3; i++)
            {
                if (slots[i] != null && slots[i].root != null) Destroy(slots[i].root.gameObject);
                slots[i] = CreateSlot(i, picked[i], Random.Range(0, Palette.Length));
            }
            CheckGameOver();
        }

        Slot CreateSlot(int index, Shape shape, int color)
        {
            var s = new Slot { shape = shape, color = color };
            s.home = new Vector3((index - 1) * SlotSpacing, TrayY, 0f);
            s.root = new GameObject("Piece" + index).transform;
            s.root.position = s.home;
            s.root.localScale = Vector3.zero;

            foreach (var c in shape.Cells)
            {
                var pos = new Vector3(c.x - (shape.Width - 1) / 2f, c.y - (shape.Height - 1) / 2f, 0f);
                MakeSprite("c", Art.Cell, s.root, pos, Palette[color], 6);
            }
            StartCoroutine(PopIn(s));
            return s;
        }

        void Update()
        {
            if (!Mathf.Approximately(lastAspect, cam.aspect))
            {
                lastAspect = cam.aspect;
                FitCamera();
                LayoutUI();
            }

            if (gameOver)
            {
                float since = Time.unscaledTime - gameOverTime;
                if (!overPanel.activeSelf && since >= 0f) ShowGameOver();
                else if (overPanel.activeSelf && since > 0.5f && Input.GetMouseButtonDown(0)) NewGame();
                return;
            }

            Vector3 p = PointerWorld();
            if (Input.GetMouseButtonDown(0)) BeginDrag(p);

            if (dragSlot >= 0)
            {
                if (Input.GetMouseButtonUp(0)) EndDrag(p);
                else if (Input.GetMouseButton(0)) UpdateDrag(p);
                else CancelDrag();
            }
        }

        Vector3 PointerWorld()
        {
            var sp = Input.mousePosition;
            sp.z = -cam.transform.position.z;
            var w = cam.ScreenToWorldPoint(sp);
            w.z = 0f;
            return w;
        }

        // ---------- Dragging ----------
        void BeginDrag(Vector3 p)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null || s.busy) continue;
                if (Mathf.Abs(p.x - s.home.x) < SlotSpacing * 0.5f && Mathf.Abs(p.y - s.home.y) < 2.2f)
                {
                    dragSlot = i;
                    UpdateDrag(p);
                    return;
                }
            }
        }

        void UpdateDrag(Vector3 p)
        {
            var s = slots[dragSlot];
            dragTarget = new Vector3(p.x, p.y + DragLift, 0f);
            float k = 1f - Mathf.Exp(-30f * Time.deltaTime);
            s.root.position = Vector3.Lerp(s.root.position, dragTarget, k);
            s.root.localScale = Vector3.Lerp(s.root.localScale, Vector3.one, k);
            UpdatePreview(s);
        }

        void EndDrag(Vector3 p)
        {
            var s = slots[dragSlot];
            int idx = dragSlot;
            dragSlot = -1;
            HideGhosts();
            ResetBlockTints();

            dragTarget = new Vector3(p.x, p.y + DragLift, 0f);
            int gx, gy;
            if (TryGetGridPos(s, dragTarget, out gx, out gy)) PlacePiece(idx, s, gx, gy);
            else StartCoroutine(ReturnHome(s));
        }

        void CancelDrag()
        {
            var s = slots[dragSlot];
            dragSlot = -1;
            HideGhosts();
            ResetBlockTints();
            StartCoroutine(ReturnHome(s));
        }

        bool TryGetGridPos(Slot s, Vector3 target, out int gx, out int gy)
        {
            // world position of the piece's bottom-left cell, converted to grid coordinates
            float ox = target.x - (s.shape.Width - 1) / 2f;
            float oy = target.y - (s.shape.Height - 1) / 2f;
            gx = Mathf.RoundToInt(ox - BoardOrigin.x);
            gy = Mathf.RoundToInt(oy - BoardOrigin.y);
            return board.CanPlace(s.shape, gx, gy);
        }

        void UpdatePreview(Slot s)
        {
            ResetBlockTints();
            HideGhosts();

            int gx, gy;
            if (!TryGetGridPos(s, dragTarget, out gx, out gy)) return;

            var col = Palette[s.color];
            int gi = 0;
            foreach (var c in s.shape.Cells)
            {
                var g = ghosts[gi++];
                g.enabled = true;
                g.transform.position = CellToWorld(gx + c.x, gy + c.y);
                g.color = new Color(col.r, col.g, col.b, 0.6f);
            }

            // light up the lines this drop would complete
            board.PreviewLines(s.shape, gx, gy, tmpRows, tmpCols);
            foreach (int y in tmpRows) for (int x = 0; x < N; x++) Glow(x, y);
            foreach (int x in tmpCols) for (int y = 0; y < N; y++) Glow(x, y);
        }

        void Glow(int x, int y)
        {
            var b = blocks[x, y];
            if (b.enabled) b.color = Color.Lerp(b.color, Color.white, 0.55f);
        }

        void ResetBlockTints()
        {
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                    if (blocks[x, y].enabled) blocks[x, y].color = Palette[board.Cells[x, y] - 1];
        }

        void HideGhosts()
        {
            foreach (var g in ghosts) g.enabled = false;
        }

        // ---------- Placing / scoring ----------
        void PlacePiece(int idx, Slot s, int gx, int gy)
        {
            board.Place(s.shape, gx, gy, s.color + 1);
            foreach (var c in s.shape.Cells)
            {
                var b = blocks[gx + c.x, gy + c.y];
                b.enabled = true;
                b.color = Palette[s.color];
            }
            Destroy(s.root.gameObject);
            slots[idx] = null;
            Sfx(sfxPlace, 1f);

            int gained = s.shape.Cells.Length;
            string popup = null;

            int lines = board.ClearFullLines(cleared);
            if (lines > 0)
            {
                combo++;
                movesSinceClear = 0;
                gained += 10 * lines * lines * Mathf.Max(1, combo);

                foreach (var cc in cleared) blocks[cc.X, cc.Y].enabled = false;
                StartCoroutine(ClearFx(new List<ClearedCell>(cleared)));
                Sfx(sfxClear, 1f + 0.06f * Mathf.Min(combo, 8));

                popup = Praise(lines);
                if (combo >= 2) popup += "\nCombo x" + combo;
            }
            else
            {
                movesSinceClear++;
                if (movesSinceClear >= 3) combo = 0;   // combo breaks after 3 moves without a clear
            }

            if (board.IsEmpty())
            {
                gained += 300;
                popup = "PERFECT CLEAR!";
            }

            score += gained;
            UpdateScoreUI();
            if (popup != null) ShowPopup(popup + "\n+" + gained);

            bool trayEmpty = true;
            foreach (var sl in slots) if (sl != null) trayEmpty = false;
            if (trayEmpty) SpawnTray();
            else CheckGameOver();
        }

        static string Praise(int lines)
        {
            switch (lines)
            {
                case 1: return "Nice!";
                case 2: return "Great!";
                case 3: return "Awesome!";
                default: return "INCREDIBLE!";
            }
        }

        void UpdateScoreUI()
        {
            scoreText.text = score.ToString();
            bestText.text = "BEST  " + Mathf.Max(best, score);
        }

        void CheckGameOver()
        {
            foreach (var s in slots)
                if (s != null && board.CanPlaceAnywhere(s.shape)) return;

            gameOver = true;
            gameOverTime = Time.unscaledTime + 0.8f;   // short pause so the player sees the final board
            if (score > best)
            {
                best = score;
                PlayerPrefs.SetInt("best", best);
                PlayerPrefs.Save();
            }
            UpdateScoreUI();
        }

        void ShowGameOver()
        {
            overText.text = "GAME OVER\n\nScore  " + score + "\nBest  " + best + "\n\nTap to play again";
            overPanel.SetActive(true);
            Sfx(sfxOver, 1f);
        }

        // ---------- Animations ----------
        IEnumerator PopIn(Slot s)
        {
            s.busy = true;
            const float dur = 0.2f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (s.root == null) yield break;
                s.root.localScale = Vector3.one * (TrayScale * Mathf.SmoothStep(0f, 1f, t / dur));
                yield return null;
            }
            if (s.root != null) s.root.localScale = Vector3.one * TrayScale;
            s.busy = false;
        }

        IEnumerator ReturnHome(Slot s)
        {
            s.busy = true;
            Vector3 p0 = s.root.position, sc0 = s.root.localScale;
            const float dur = 0.15f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (s.root == null) yield break;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                s.root.position = Vector3.Lerp(p0, s.home, k);
                s.root.localScale = Vector3.Lerp(sc0, Vector3.one * TrayScale, k);
                yield return null;
            }
            if (s.root != null)
            {
                s.root.position = s.home;
                s.root.localScale = Vector3.one * TrayScale;
            }
            s.busy = false;
        }

        IEnumerator ClearFx(List<ClearedCell> cells)
        {
            var objs = new List<Transform>();
            foreach (var c in cells)
            {
                var col = Color.Lerp(Palette[c.ColorIndex], Color.white, 0.6f);
                objs.Add(MakeSprite("fx", Art.Cell, fxRoot, CellToWorld(c.X, c.Y), col, 4).transform);
            }
            const float dur = 0.3f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float sc = Mathf.Lerp(1.15f, 0f, t / dur);
                foreach (var tr in objs) tr.localScale = new Vector3(sc, sc, 1f);
                yield return null;
            }
            foreach (var tr in objs) Destroy(tr.gameObject);
        }

        void ShowPopup(string msg)
        {
            if (popupRoutine != null) StopCoroutine(popupRoutine);
            popupRoutine = StartCoroutine(PopupRoutine(msg));
        }

        IEnumerator PopupRoutine(string msg)
        {
            popupText.text = msg;
            popupText.gameObject.SetActive(true);
            const float dur = 0.95f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                float sc = k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f);
                popupText.rectTransform.localScale = new Vector3(sc, sc, 1f);
                var c = popupText.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                popupText.color = c;
                yield return null;
            }
            popupText.gameObject.SetActive(false);
        }
    }
}
