using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Донастройка фазы 1. Запуск: меню AimEgo > Setup > "Фаза 1.5: оружие в руках и HUD".
///
/// Делает:
///  1. Нормализует вьюмодели VM_Deagle / VM_AK74: масштаб под реальный размер,
///     ствол вдоль +Z (вперёд), рукоять у хештей, Muzzle на конце ствола.
///  2. Собирает uGUI HUD на префабе игрока: прицел, хитмаркер, перезарядка,
///     HP, патроны, слоты оружия. Убирает временный DebugHud.
///
/// Повторный запуск безопасен. Если после нормализации ствол смотрит НАЗАД —
/// запусти AimEgo > Tools > "Развернуть вьюмодели на 180°".
/// </summary>
public static class AimEgoPhase15Setup
{
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("AimEgo/Setup/Фаза 1.5: оружие в руках и HUD")]
    public static void Run()
    {
        NormalizeViewModels(flip180: false);
        BuildHud();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("AimEgo Фаза 1.5: готово. Если ствол смотрит не туда — AimEgo > Tools > Развернуть на 180°.");
    }

    [MenuItem("AimEgo/Tools/Развернуть вьюмодели на 180°")]
    public static void FlipViewmodels()
    {
        NormalizeViewModels(flip180: true);
        AssetDatabase.SaveAssets();
        Debug.Log("AimEgo: вьюмодели развёрнуты на 180°.");
    }

    // ===== 1. НОРМАЛИЗАЦИЯ ВЬЮМОДЕЛЕЙ =====
    internal static void NormalizeViewModels(bool flip180)
    {
        NormalizeViewModel("Assets/Prefabs/Weapons/VM_Deagle.prefab", 0.34f, flip180);
        NormalizeViewModel("Assets/Prefabs/Weapons/VM_AK74.prefab", 0.88f, flip180);
    }

    internal static void NormalizeViewModel(string path, float targetLength, bool flip180)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("AimEgo: не найден " + path + " — запусти Фазу 1.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var model = root.transform.Find("Model");
            var muzzle = root.transform.Find("Muzzle");
            if (model == null)
            {
                Debug.LogWarning("AimEgo: в " + path + " нет Model — нормализация пропущена.");
                return;
            }

            // Исходные границы модели в мировых координатах (корень в нуле)
            Bounds bounds = GetBounds(model.gameObject);
            if (bounds.extents == Vector3.zero)
            {
                Debug.LogWarning("AimEgo: у " + path + " нет рендереров — нормализация пропущена.");
                return;
            }

            // 1. Поворот: самая длинная ось модели становится вперёд (+Z)
            Vector3 size = bounds.size;
            Quaternion rotation = Quaternion.identity;
            if (size.y >= size.x && size.y >= size.z)
                rotation = Quaternion.Euler(90f, 0f, 0f);          // ствол смотрел вверх/вниз
            else if (size.x >= size.z)
                rotation = Quaternion.Euler(0f, -90f, 0f);         // ствол вдоль X

            if (flip180) rotation *= Quaternion.Euler(0f, 180f, 0f);
            model.localRotation = rotation;

            // 2. Масштаб: длина ствола = targetLength
            float dominant = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float scale = targetLength / dominant;
            model.localScale = Vector3.one * scale;

            // 3. Позиция: центр ствола на оси хештя, рукоять (зад) у точки крепления
            bounds = GetBounds(model.gameObject); // пересчёт после поворота и масштаба
            Vector3 pos = model.localPosition;
            pos -= new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            model.localPosition = pos;
            bounds = GetBounds(model.gameObject); // финальный пересчёт

            // 4. Дуло — на конец ствола
            if (muzzle != null)
                muzzle.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + 0.02f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("AimEgo: нормализована вьюмодель " + path + " (длина " + targetLength + " м, дуло z=" +
                      (muzzle != null ? muzzle.localPosition.z.ToString("F2") : "?") + ")");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return default;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // ===== 2. HUD =====
    internal static void BuildHud()
    {
        if (!System.IO.File.Exists(PlayerPrefabPath))
        {
            Debug.LogError("AimEgo: нет " + PlayerPrefabPath + " — запусти Фазу 0.");
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            // Временный IMGUI-HUD больше не нужен
            var oldHud = contents.GetComponent<DebugHud>();
            if (oldHud != null) Object.DestroyImmediate(oldHud);

            var existing = contents.transform.Find("HUD");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // ===== Canvas =====
            var canvasGO = new GameObject("HUD");
            canvasGO.transform.SetParent(contents.transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var hud = canvasGO.AddComponent<Hud>();

            // ===== Прицел (центр экрана) =====
            var crossRoot = CreateAnchor(canvasGO.transform, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            hud.crosshairBars = new RectTransform[4];
            hud.crosshairBars[0] = MakeBar(crossRoot, "Up", Vector2.zero, new Vector2(2f, 8f), Color.white).rectTransform;
            hud.crosshairBars[1] = MakeBar(crossRoot, "Down", Vector2.zero, new Vector2(2f, 8f), Color.white).rectTransform;
            hud.crosshairBars[2] = MakeBar(crossRoot, "Left", Vector2.zero, new Vector2(8f, 2f), Color.white).rectTransform;
            hud.crosshairBars[3] = MakeBar(crossRoot, "Right", Vector2.zero, new Vector2(8f, 2f), Color.white).rectTransform;

            // ===== Хитмаркер: крестик 45° в центре =====
            var hitRoot = CreateAnchor(canvasGO.transform, "Hitmarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var group = hitRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            MakeBar(hitRoot, "A", new Vector2(6f, 6f), new Vector2(2f, 12f), Color.red).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            MakeBar(hitRoot, "B", new Vector2(-6f, 6f), new Vector2(2f, 12f), Color.red).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            MakeBar(hitRoot, "C", new Vector2(6f, -6f), new Vector2(2f, 12f), Color.red).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            MakeBar(hitRoot, "D", new Vector2(-6f, -6f), new Vector2(2f, 12f), Color.red).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            hud.hitmarker = group;

            // ===== Перезарядка (под прицелом) =====
            var reloadRoot = CreateAnchor(canvasGO.transform, "Reload", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f));
            reloadRoot.sizeDelta = new Vector2(260f, 14f);
            reloadRoot.gameObject.SetActive(false);
            var reloadBg = reloadRoot.gameObject.AddComponent<Image>();
            reloadBg.color = new Color(0f, 0f, 0f, 0.5f);
            var reloadFill = CreateAnchor(reloadRoot, "Fill", Vector2.zero, Vector2.zero, Vector2.zero);
            reloadFill.anchorMax = Vector2.one;
            reloadFill.offsetMin = new Vector2(2f, 2f);
            reloadFill.offsetMax = new Vector2(-2f, -2f);
            var reloadFillImg = reloadFill.gameObject.AddComponent<Image>();
            reloadFillImg.color = new Color(1f, 0.85f, 0.4f);
            hud.reloadRoot = reloadRoot.gameObject;
            hud.reloadFill = reloadFill;

            // ===== Здоровье (слева внизу) =====
            var hpPanel = CreateAnchor(canvasGO.transform, "HealthPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f));
            hpPanel.sizeDelta = new Vector2(340f, 92f);
            var hpBg = hpPanel.gameObject.AddComponent<Image>();
            hpBg.color = new Color(0f, 0f, 0f, 0.4f);

            var hpLabel = MakeText(hpPanel, "Label", font, "ЗДОРОВЬЕ", 16, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f));
            SetRect(hpLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-14f, -30f));

            var hpBar = CreateAnchor(hpPanel, "Bar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -40f));
            hpBar.sizeDelta = new Vector2(312f, 22f);
            var hpBarBg = hpBar.gameObject.AddComponent<Image>();
            hpBarBg.color = new Color(1f, 1f, 1f, 0.12f);

            var hpFill = CreateAnchor(hpBar, "Fill", Vector2.zero, Vector2.zero, Vector2.zero);
            hpFill.anchorMax = new Vector2(1f, 1f);
            hpFill.offsetMin = new Vector2(2f, 2f);
            hpFill.offsetMax = new Vector2(-2f, -2f);
            var hpFillImg = hpFill.gameObject.AddComponent<Image>();
            hpFillImg.color = new Color(0.4f, 0.85f, 0.4f);
            hud.hpFill = hpFill;

            var hpText = MakeText(hpBar, "Value", font, "100", 20, TextAnchor.MiddleRight, Color.white);
            SetRect(hpText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, 0f));
            hud.hpText = hpText;

            // ===== Слоты оружия (справа над патронами) =====
            var slotsPanel = CreateAnchor(canvasGO.transform, "SlotsPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 190f));
            slotsPanel.sizeDelta = new Vector2(340f, 110f);
            var slotsBg = slotsPanel.gameObject.AddComponent<Image>();
            slotsBg.color = new Color(0f, 0f, 0f, 0.4f);

            hud.slotTexts = new Text[2];
            for (int i = 0; i < 2; i++)
            {
                var slot = MakeText(slotsPanel, "Slot" + (i + 1), font, (i + 1) + "   Оружие", 22, TextAnchor.MiddleLeft, Color.white);
                SetRect(slot.rectTransform, new Vector2(0f, 1f - (i + 1) * 0.5f), new Vector2(1f, 1f - i * 0.5f), new Vector2(16f, 0f), new Vector2(-16f, 0f));
                hud.slotTexts[i] = slot;
            }

            // ===== Оружие и патроны (справа внизу) =====
            var weaponPanel = CreateAnchor(canvasGO.transform, "WeaponPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f));
            weaponPanel.sizeDelta = new Vector2(340f, 150f);
            var wpBg = weaponPanel.gameObject.AddComponent<Image>();
            wpBg.color = new Color(0f, 0f, 0f, 0.4f);

            var weaponName = MakeText(weaponPanel, "Name", font, "Оружие", 24, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
            SetRect(weaponName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -46f), new Vector2(-16f, -12f));
            hud.weaponNameText = weaponName;

            var ammo = MakeText(weaponPanel, "Ammo", font, "0 / 0", 42, TextAnchor.LowerRight, Color.white);
            SetRect(ammo.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 10f), new Vector2(-16f, -50f));
            hud.ammoText = ammo;

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            Debug.Log("AimEgo Фаза 1.5: HUD собран на префабе игрока.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ===== Утилиты uGUI =====
    static RectTransform CreateAnchor(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        // Панели у краёв отсчитываются от своего угла, центральные — от центра
        rt.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rt.anchoredPosition = anchoredPosition;
        return rt;
    }

    static Image MakeBar(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text MakeText(Transform parent, string name, Font font, string text, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    // Растянуть прямоугольник внутри родителя с отступами
    static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
