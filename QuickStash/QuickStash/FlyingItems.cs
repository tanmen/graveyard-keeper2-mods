using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using QuickStash.Core;
using UnityEngine;
using UnityEngine.UI;

namespace QuickStash
{
    /// <summary>
    /// しまった品のアイコンを、自分の体から宝箱へ飛ばす（しまった品 1 種類・宝箱 1 つにつき 1 つ。多いときは先頭の 16 個）。
    ///
    /// ゲーム自身の「技術点が飛ぶ」演出（FlyingTechPoint）と同じく、世界の位置を CameraSystem.WorldToScreenPoint で画面の位置に直し、
    /// UI の部品として飛ばす。体と宝箱の画面の位置は毎フレーム直すので、歩いて画面が動いても宝箱からずれない。
    /// 置き場はゲームの UI の根の下の自前の Canvas（並び順 60: HUD の 50 より上、窓の 400〜・ツールチップの 700 より下）。
    /// 品はもう宝箱に入っているので、演出は見た目だけ（途中で消えても在庫は変わらない）。
    /// </summary>
    internal sealed class FlyingItems : MonoBehaviour
    {
        private const int SortingOrder = 60;

        // 体のどの高さから飛ばすか（足元 0〜吹き出しの位置 1）
        private const float BodyHeight = 0.45f;

        // 所持品のセルの地の絵（アイコンの青い縁を置き換える材質と色を、このセルから写す）
        private const string InventoryCellBack = "widget_items_cell-inventory_item_cell";

        private static readonly Color FallbackOutline = new Color(0.1216f, 0.0986f, 0.0941f, 1f);

        private static FlyingItems instance;
        private static Material iconMaterial;
        private static Color iconOutline = FallbackOutline;
        private static bool iconResolved;
        private static bool loggedFirst;

        private readonly List<Flight> flights = new List<Flight>();
        private RectTransform layer;
        private Canvas canvas;
        private float referencePixelsPerUnit = 50f;

        public static void Launch(List<StashMove> moves)
        {
            if (moves == null || moves.Count == 0)
            {
                return;
            }

            try
            {
                FlyingItems fly = Ensure();
                if (fly == null)
                {
                    Plugin.Log.LogWarning("飛ぶ演出: ゲームの UI の根が見つからないので飛ばさない");
                    return;
                }

                fly.Add(moves);
            }
            catch (Exception e)
            {
                Hooks.LogError("飛ぶ演出の準備", e);
            }
        }

        private static FlyingItems Ensure()
        {
            if (instance != null && instance.layer != null)
            {
                return instance;
            }

            Transform parent = GameBridge.FindUiRoot();
            if (parent == null)
            {
                return null;
            }

            var go = new GameObject("QuickStash.Flying", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.rootCanvas != null)
            {
                canvas.referencePixelsPerUnit = parentCanvas.rootCanvas.referencePixelsPerUnit;
            }

            instance = go.AddComponent<FlyingItems>();
            instance.layer = rect;
            instance.canvas = canvas;
            instance.referencePixelsPerUnit = canvas.referencePixelsPerUnit;
            return instance;
        }

        private void Add(List<StashMove> moves)
        {
            if (!TryBodyPoint(out Vector3 body) || !TryToLocal(body, out Vector2 from))
            {
                Plugin.Log.LogWarning("飛ぶ演出: プレイヤーの位置を画面に直せなかったので飛ばさない");
                return;
            }

            ResolveIconLook();
            float now = Time.unscaledTime;
            int launched = 0;
            int skipped = 0;
            foreach (StashMove move in moves)
            {
                if (launched >= FlightCurve.MaxFlights)
                {
                    break;
                }

                Sprite sprite = IconOf(move.Item);
                if (sprite == null || move.Chest == null || !TryChestPoint(move.Chest, out Vector3 chest))
                {
                    skipped++;
                    continue;
                }

                Image image = CreateIcon(sprite);
                image.gameObject.SetActive(false);
                flights.Add(new Flight(image, move.Chest, chest, now + FlightCurve.Delay(launched)));
                launched++;
            }

            if (!loggedFirst)
            {
                loggedFirst = true;
                string target = string.Empty;
                if (flights.Count > 0 && TryToLocal(flights[flights.Count - 1].LastChestPoint, out Vector2 to))
                {
                    WgoData wgo = flights[flights.Count - 1].Chest;
                    target = $"・最後の宝箱 {wgo.id} の的 {flights[flights.Count - 1].LastChestPoint}（置き場所 {wgo.Position}）→ UI {to}";
                }

                Plugin.Log.LogInfo($"飛ぶ演出を初めて出した: {launched} 個（飛ばせなかった {skipped} 個）・体 {body} → UI {from}{target}"
                    + $"・アイコンの材質 {(iconMaterial == null ? "なし（青い縁のまま）" : iconMaterial.shader.name)}");
            }
        }

        private void Update()
        {
            if (flights.Count == 0)
            {
                return;
            }

            try
            {
                Step();
            }
            catch (Exception e)
            {
                Hooks.LogError("飛ぶ演出", e);
                Clear();
            }
        }

        private void Step()
        {
            float now = Time.unscaledTime;
            bool hasBody = TryBodyPoint(out Vector3 body);
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight flight = flights[i];
                if (flight.Image == null)
                {
                    flights.RemoveAt(i);
                    continue;
                }

                float t = (now - flight.StartAt) / FlightCurve.Duration;
                if (t < 0f)
                {
                    continue;
                }

                if (t >= 1f || !hasBody)
                {
                    Destroy(flight.Image.gameObject);
                    flights.RemoveAt(i);
                    continue;
                }

                if (TryChestPoint(flight.Chest, out Vector3 chest))
                {
                    flight.LastChestPoint = chest;
                }

                if (!TryToLocal(body, out Vector2 from) || !TryToLocal(flight.LastChestPoint, out Vector2 to))
                {
                    flight.Image.gameObject.SetActive(false);
                    continue;
                }

                float arc = FlightCurve.ArcHeight(Vector2.Distance(from, to));
                Vector2 position = Vector2.Lerp(from, to, FlightCurve.Progress(t)) + Vector2.up * FlightCurve.Lift(t, arc);
                RectTransform rect = flight.Image.rectTransform;
                rect.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
                rect.localScale = Vector3.one * FlightCurve.Scale(t);
                flight.Group.alpha = FlightCurve.Alpha(t);
                if (!flight.Image.gameObject.activeSelf)
                {
                    flight.Image.gameObject.SetActive(true);
                }
            }
        }

        private void Clear()
        {
            foreach (Flight flight in flights)
            {
                if (flight.Image != null)
                {
                    Destroy(flight.Image.gameObject);
                }
            }

            flights.Clear();
        }

        private Image CreateIcon(Sprite sprite)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.layer = layer.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(layer, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float perPixel = sprite.pixelsPerUnit > 0f ? referencePixelsPerUnit / sprite.pixelsPerUnit : 1f;
            rect.sizeDelta = sprite.rect.size * perPixel;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            if (iconMaterial != null)
            {
                // ゲームのアイコンは純粋な青の縁を持ち、専用の材質で縁の色を置き換えて描く（UIItemCell と同じ呼び方）
                image.material = iconMaterial;
                image.BlueColorReplace(iconOutline);
            }

            CanvasGroup group = go.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return image;
        }

        /// <summary>体の真ん中あたり（足元と吹き出しの位置の間）。</summary>
        private static bool TryBodyPoint(out Vector3 point)
        {
            point = Vector3.zero;
            PlayerController controller = GameBridge.Controller;
            if (controller == null)
            {
                return false;
            }

            Vector3 feet = controller.MovablePosition;
            Transform bubble = controller.BubblePoint;
            point = bubble == null ? feet : Vector3.Lerp(feet, bubble.position, BodyHeight);
            return true;
        }

        /// <summary>宝箱の見た目の真ん中（見た目が読み込まれていなければ置き場所）。</summary>
        private static bool TryChestPoint(WgoData wgo, out Vector3 point)
        {
            point = Vector3.zero;
            if (wgo == null)
            {
                return false;
            }

            point = wgo.Position;
            Wgo view = GameScene.GetWgoViewGlobal(wgo.UniqueId);
            if (view == null || !view.isActiveAndEnabled)
            {
                return true;
            }

            bool any = false;
            Bounds bounds = default;
            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
            {
                // 粒子・軌跡・線は見た目の真ん中をずらすので外す（粒子は型の DLL を参照しないよう名前で見る）
                if (renderer == null || !renderer.enabled || renderer is TrailRenderer || renderer is LineRenderer
                    || renderer.GetType().Name == "ParticleSystemRenderer")
                {
                    continue;
                }

                if (any)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    any = true;
                }
            }

            if (any)
            {
                point = bounds.center;
            }

            return true;
        }

        private bool TryToLocal(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            if (layer == null || canvas == null)
            {
                return false;
            }

            Vector3 screen = CameraSystem.WorldToScreenPoint(world);
            Canvas root = canvas.rootCanvas;
            Camera uiCamera = root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, uiCamera, out local);
        }

        private static Sprite IconOf(ItemDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.iconId))
            {
                return null;
            }

            try
            {
                EasySpritesCollection sprites = LazySingletonSO<EasySpritesCollection>.Instance;
                return sprites == null ? null : sprites.GetSprite(def.iconId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>所持品のセルのアイコンの材質と、縁の色（ImageColors.NormalColor）を写す（Craft Todo の MenuParts と同じ探し方）。</summary>
        private static void ResolveIconLook()
        {
            if (iconResolved)
            {
                return;
            }

            iconResolved = true;
            try
            {
                foreach (UIItemCell cell in Resources.FindObjectsOfTypeAll<UIItemCell>())
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    Traverse t = Traverse.Create(cell);
                    Image back = t.Field("background").GetValue<Image>();
                    if (back == null || back.sprite == null || back.sprite.name != InventoryCellBack)
                    {
                        continue;
                    }

                    Image icon = t.Field("icon").GetValue<Image>();
                    Material material = icon == null ? null : icon.material;
                    // 材質が UI の既定のもの（色を掛けるだけ）なら、縁の置き換えをすると全体が暗くなる。そのときは使わない
                    if (material != null && material.shader != null && material != Canvas.GetDefaultCanvasMaterial() && material.shader.name != "UI/Default")
                    {
                        iconMaterial = material;
                    }

                    ImageColors colors = t.Field("colors").GetValue<ImageColors>();
                    if (colors != null)
                    {
                        iconOutline = colors.NormalColor;
                    }

                    return;
                }

                Plugin.Log.LogWarning("飛ぶ演出: 所持品のセルが見つからないので、アイコンの青い縁を置き換えられない");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"飛ぶ演出: アイコンの材質を引けなかった: {e.GetBaseException().Message}");
            }
        }

        private sealed class Flight
        {
            public Flight(Image image, WgoData chest, Vector3 chestPoint, float startAt)
            {
                Image = image;
                Group = image.GetComponent<CanvasGroup>();
                Chest = chest;
                LastChestPoint = chestPoint;
                StartAt = startAt;
            }

            public Image Image { get; }

            public CanvasGroup Group { get; }

            public WgoData Chest { get; }

            /// <summary>宝箱の的（見た目が消えても、最後に分かった所へ飛ぶ）。</summary>
            public Vector3 LastChestPoint { get; set; }

            public float StartAt { get; }
        }
    }
}
