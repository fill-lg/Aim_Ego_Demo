using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Настройщик фазы 1. Запуск: меню AimEgo > Setup > "Фаза 1: оружие, эффекты и мишени"
/// (при открытой сцене SampleScene; сначала должна быть выполнена Фаза 0).
///
/// Создаёт:
///  1. Слой ViewModel (вьюмодель не попадает под свои́ же рейкасты).
///  2. Текстуры и материалы эффектов: трассер, вспышка, декаль, частицы.
///  3. Префаб ImpactEffect (всплеск частиц при попадании в стену).
///  4. Конфиги оружия WEAP_Deagle и WEAP_AK74 (ScriptableObject).
///  5. Префабы вьюмоделей VM_Deagle / VM_AK74 (Model + Muzzle + вспышка).
///  6. Дописывает в Player.prefab: PlayerShooting, PlayerHealth, хитбоксы, DebugHud.
///  7. Три мишени-болванки на средней линии карты (падают от попаданий, воскресают).
///
/// Повторный запуск безопасен: существующие ассеты не пересоздаются.
/// ВАЖНО: если заново запустить Фазу 0, префаб игрока пересоберётся без оружия —
/// просто запусти Фазу 1 ещё раз.
/// </summary>
public static class AimEgoPhase1Setup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string DummyPrefabPath = "Assets/Prefabs/HumanDummy_M White.prefab";
    const string AkPath = "Assets/Low Poly Weapons VOL.1/Models/AK74.fbx";
    // Дигл — pistol2 из пака Low Poly Guns
    const string DeaglePath = "Assets/Low Poly Guns/Models/Guns/pistol2/pistol2.fbx";

    [MenuItem("AimEgo/Setup/Фаза 1: оружие, эффекты и мишени")]
    public static void Run()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            Debug.LogError("AimEgo Фаза 1: открой сцену Assets/Scenes/SampleScene.unity и запусти ещё раз.");
            return;
        }
        if (!File.Exists(PlayerPrefabPath))
        {
            Debug.LogError("AimEgo Фаза 1: нет " + PlayerPrefabPath + " — сначала запусти Фазу 0.");
            return;
        }

        EnsureViewModelLayer();

        Directory.CreateDirectory("Assets/Art/Generated");
        Directory.CreateDirectory("Assets/Materials");
        Directory.CreateDirectory("Assets/Prefabs/Weapons");
        Directory.CreateDirectory("Assets/Settings/Weapons");
        AssetDatabase.Refresh();

        // 1. Эффекты
        var decalTex = CreateTexture("Assets/Art/Generated/DecalDot.png", MakeDecalPixel);
        var flashTex = CreateTexture("Assets/Art/Generated/FlashStar.png", MakeFlashPixel);
        var tracerMat = CreateUnlitMaterial("Assets/Materials/TracerMat.mat", new Color(1f, 0.85f, 0.55f, 0.9f), transparent: true, additive: true);
        var flashMat = CreateUnlitMaterial("Assets/Materials/MuzzleFlashMat.mat", new Color(1f, 0.9f, 0.6f, 1f), transparent: true, additive: true, texture: flashTex);
        var decalMat = CreateUnlitMaterial("Assets/Materials/DecalMat.mat", new Color(0.05f, 0.05f, 0.05f, 0.85f), transparent: true, additive: false, texture: decalTex);
        var impactMat = CreateUnlitMaterial("Assets/Materials/ImpactParticleMat.mat", new Color(1f, 0.8f, 0.55f), transparent: false, additive: false);
        var impactPrefab = EnsureImpactPrefab(impactMat);

        // 2. Конфиги оружия
        var deagle = CreateConfig("Assets/Settings/Weapons/WEAP_Deagle.asset", cfg =>
        {
            cfg.displayName = "Desert Eagle";
            cfg.automatic = false;
            cfg.damage = 53f;
            cfg.range = 300f;
            cfg.fireInterval = 0.28f;
            cfg.magazineSize = 7;
            cfg.reserveAmmo = 35;
            cfg.reloadTime = 2.2f;
            cfg.baseSpread = 0.15f;
            cfg.spreadPerShot = 0.9f;
            cfg.maxSpread = 4f;
            cfg.spreadRecovery = 5f;
            cfg.recoilPitch = 3.5f;
            cfg.recoilYaw = 0.5f;
            cfg.muzzleLocalPosition = new Vector3(0f, 0.05f, 0.35f);
        });
        var ak = CreateConfig("Assets/Settings/Weapons/WEAP_AK74.asset", cfg =>
        {
            cfg.displayName = "AK-74";
            cfg.automatic = true;
            cfg.damage = 36f;
            cfg.range = 300f;
            cfg.fireInterval = 0.1f;
            cfg.magazineSize = 30;
            cfg.reserveAmmo = 90;
            cfg.reloadTime = 2.4f;
            cfg.baseSpread = 0.25f;
            cfg.spreadPerShot = 0.45f;
            cfg.maxSpread = 5f;
            cfg.spreadRecovery = 6f;
            cfg.recoilPitch = 1.3f;
            cfg.recoilYaw = 0.35f;
            cfg.muzzleLocalPosition = new Vector3(0f, 0.06f, 0.6f);
        });

        // 3. Вьюмодели из моделей проекта. Пересоздаём с нуля:
        // модель дигла теперь pistol2, а нормализация всё равно автоматическая
        AssetDatabase.DeleteAsset("Assets/Prefabs/Weapons/VM_Deagle.prefab");
        AssetDatabase.DeleteAsset("Assets/Prefabs/Weapons/VM_AK74.prefab");

        var deagleModel = AssetDatabase.LoadAssetAtPath<GameObject>(DeaglePath);
        var akModel = AssetDatabase.LoadAssetAtPath<GameObject>(AkPath);
        if (akModel == null) akModel = FindByTypeName("AK74");
        if (deagleModel == null) Debug.LogWarning("AimEgo Фаза 1: не нашёл модель deagle — VM_Deagle создан без модели, пропиши вручную.");
        if (akModel == null) Debug.LogWarning("AimEgo Фаза 1: не нашёл модель AK74 — VM_AK74 создан без модели, пропиши вручную.");

        deagle.viewModelPrefab = BuildViewModelPrefab("Deagle", deagleModel, deagle, flashMat);
        ak.viewModelPrefab = BuildViewModelPrefab("AK74", akModel, ak, flashMat);
        EditorUtility.SetDirty(deagle);
        EditorUtility.SetDirty(ak);

        // Нормализация вьюмоделей: масштаб, ствол вперёд (+Z), дуло на конце ствола
        AimEgoPhase15Setup.NormalizeViewModels(flip180: false);

        // 4. Игрок: стрельба, здоровье, хитбоксы, HUD
        UpgradePlayerPrefab(deagle, ak, tracerMat, decalMat, impactPrefab);
        AimEgoPhase15Setup.BuildHud();

        // 5. Мишени
        PlacePracticeDummies();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("AimEgo Фаза 1: готово. Play: ЛКМ — огонь, R — перезарядка, 1/2 — оружие. Мишени на средней линии карты.");

        if (UnityEngine.Object.FindAnyObjectByType<PlayerController>() == null)
            Debug.LogWarning("AimEgo Фаза 1: в сцене нет игрока — запусти Фазу 0, затем Фазу 1 ещё раз.");
    }

    // ===== СЛОЙ =====
    static void EnsureViewModelLayer()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");
        if (layers == null || !layers.isArray || layers.arraySize != 32) return;

        for (int i = 0; i < 32; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == "ViewModel") return;

        for (int i = 8; i < 32; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = "ViewModel";
                so.ApplyModifiedProperties();
                return;
            }
        }
        Debug.LogWarning("AimEgo: нет свободного слоя для ViewModel.");
    }

    // ===== ТЕКСТУРЫ (генерируются кодом) =====
    static Texture2D CreateTexture(string path, Func<int, int, Color> pixel)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        UnityEngine.Object.DestroyImmediate(tex);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Мягкое тёмное пятно — след от пули
    static Color MakeDecalPixel(int x, int y)
    {
        float dx = (x + 0.5f) / 64f - 0.5f;
        float dy = (y + 0.5f) / 64f - 0.5f;
        float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;
        float a = Mathf.Clamp01((0.85f - d) / 0.15f);
        a *= 0.6f + 0.4f * Mathf.Clamp01(1f - d);
        return new Color(0f, 0f, 0f, a);
    }

    // Ядро + 4 луча — вспышка выстрела
    static Color MakeFlashPixel(int x, int y)
    {
        float dx = (x + 0.5f) / 64f - 0.5f;
        float dy = (y + 0.5f) / 64f - 0.5f;
        float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;
        float ang = Mathf.Atan2(dy, dx);
        float spikes = Mathf.Max(Mathf.Pow(Mathf.Abs(Mathf.Cos(ang)), 6f), Mathf.Pow(Mathf.Abs(Mathf.Sin(ang)), 6f));
        float core = Mathf.Clamp01(1f - d * 2.2f);
        float b = Mathf.Clamp01(core + spikes * Mathf.Clamp01(1f - d) * 0.9f);
        return new Color(1f, 0.92f, 0.65f, b);
    }

    // ===== МАТЕРИАЛЫ =====
    static Material CreateUnlitMaterial(string path, Color baseColor, bool transparent, bool additive, Texture texture = null)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("AimEgo: не найден URP Unlit shader.");
            return null;
        }

        var mat = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        if (texture != null) mat.SetTexture("_BaseMap", texture);

        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_Cull", 0f); // двухсторонний
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ===== ЭФФЕКТ ПОПАДАНИЯ =====
    static GameObject EnsureImpactPrefab(Material mat)
    {
        const string path = "Assets/Prefabs/Weapons/ImpactEffect.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var root = new GameObject("ImpactEffect");
        var ps = root.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.gravityModifier = 0.7f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.55f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)14) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.04f;

        root.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // ===== КОНФИГИ =====
    static WeaponConfig CreateConfig(string path, Action<WeaponConfig> init)
    {
        var existing = AssetDatabase.LoadAssetAtPath<WeaponConfig>(path);
        if (existing != null) return existing;

        var cfg = ScriptableObject.CreateInstance<WeaponConfig>();
        init(cfg);
        AssetDatabase.CreateAsset(cfg, path);
        return cfg;
    }

    // ===== ВЬЮМОДЕЛИ =====
    static GameObject FindByTypeName(string namePart)
    {
        foreach (var guid in AssetDatabase.FindAssets(namePart))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) return go;
            }
        }
        return null;
    }

    static GameObject BuildViewModelPrefab(string name, GameObject modelAsset, WeaponConfig config, Material flashMat)
    {
        var path = "Assets/Prefabs/Weapons/VM_" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var root = new GameObject("VM_" + name);

        // Модель ствола. Если смотрит не туда — открой префаб и поверни Model.
        if (modelAsset != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
        }

        // Дуло: от него стартует трассер, на нём вспышка
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = config.muzzleLocalPosition;

        var flashGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flashGo.name = "Flash";
        UnityEngine.Object.DestroyImmediate(flashGo.GetComponent<Collider>());
        flashGo.transform.SetParent(muzzle.transform, false);
        flashGo.transform.localRotation = Quaternion.identity;
        flashGo.transform.localScale = Vector3.one * 0.15f;

        var flashRenderer = flashGo.GetComponent<Renderer>();
        flashRenderer.sharedMaterial = flashMat;
        flashRenderer.enabled = false;

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(flashGo.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 2.5f;
        light.intensity = 0f;
        light.color = new Color(1f, 0.85f, 0.6f);
        light.shadows = LightShadows.None;
        light.enabled = false;

        var mf = flashGo.AddComponent<MuzzleFlash>();
        mf.quadRenderer = flashRenderer;
        mf.flashLight = light;

        root.AddComponent<WeaponViewModel>();

        int vmLayer = LayerMask.NameToLayer("ViewModel");
        if (vmLayer >= 0) SetLayerRecursive(root, vmLayer);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log("AimEgo Фаза 1: создан " + path);
        return prefab;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    // ===== ИГРОК =====
    static void UpgradePlayerPrefab(WeaponConfig deagle, WeaponConfig ak, Material tracerMat, Material decalMat, GameObject impactPrefab)
    {
        var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var shooting = contents.GetComponent<PlayerShooting>();
            if (shooting == null) shooting = contents.AddComponent<PlayerShooting>();

            shooting.input = contents.GetComponent<PlayerInputHandler>();
            var camGo = contents.transform.Find("CameraRoot/Camera");
            if (camGo != null)
            {
                shooting.eyes = camGo.GetComponent<Camera>();
                shooting.weaponHolder = camGo.Find("WeaponHolder");
            }
            shooting.loadout = new[] { deagle, ak };
            shooting.tracerMaterial = tracerMat;
            shooting.decalMaterial = decalMat;
            shooting.impactPrefab = impactPrefab;

            if (contents.GetComponent<PlayerHealth>() == null)
                contents.AddComponent<PlayerHealth>();

            var model = contents.transform.Find("Model");
            if (model != null) AddHitboxes(model.gameObject);

            if (contents.GetComponent<DebugHud>() == null)
                contents.AddComponent<DebugHud>();

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            Debug.Log("AimEgo Фаза 1: Player.prefab дополнен стрельбой, здоровьем и хитбоксами.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ===== ХИТБОКСЫ =====
    // Тело и голова. Ставятся на мишени и на Model игрока (для сетевой стрельбы в фазе 2).
    static void AddHitboxes(GameObject attachTo)
    {
        if (attachTo.transform.Find("Hitboxes/Body") != null) return;

        var container = new GameObject("Hitboxes");
        container.transform.SetParent(attachTo.transform, false);

        var body = new GameObject("Body");
        body.transform.SetParent(container.transform, false);
        var bodyCollider = body.AddComponent<BoxCollider>();
        bodyCollider.center = new Vector3(0f, 0.75f, 0f);
        bodyCollider.size = new Vector3(0.55f, 1.3f, 0.45f);
        body.AddComponent<Hitbox>().multiplier = 1f;

        var head = new GameObject("Head");
        head.transform.SetParent(container.transform, false);
        var headCollider = head.AddComponent<BoxCollider>();
        headCollider.center = new Vector3(0f, 1.62f, 0f);
        headCollider.size = new Vector3(0.32f, 0.32f, 0.32f);
        head.AddComponent<Hitbox>().multiplier = 4f;
    }

    // ===== МИШЕНИ =====
    static void PlacePracticeDummies()
    {
        var old = GameObject.Find("PracticeDummies");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
        if (dummyPrefab == null)
        {
            Debug.LogWarning("AimEgo Фаза 1: не нашёл " + DummyPrefabPath + " — мишени не расставлены.");
            return;
        }

        var container = new GameObject("PracticeDummies");

        foreach (float x in new[] { -5f, 0f, 5f })
        {
            // Пол ищем рейкастом — как у SpawnPoint
            float y = 2f;
            if (Physics.Raycast(new Vector3(x, 15f, 0f), Vector3.down, out var hit, 60f, LayerMask.GetMask("Ground")))
                y = hit.point.y + 0.05f;

            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, container.transform);
            dummy.transform.position = new Vector3(x, y, 0f);
            dummy.transform.rotation = Quaternion.LookRotation(Vector3.left); // лицом к TSpawn

            var health = dummy.GetComponent<PlayerHealth>();
            if (health == null) health = dummy.AddComponent<PlayerHealth>();
            health.respawnOnDeath = true;
            health.respawnDelay = 3f;

            AddHitboxes(dummy);
        }
        Debug.Log("AimEgo Фаза 1: мишени расставлены (контейнер PracticeDummies).");
    }
}
