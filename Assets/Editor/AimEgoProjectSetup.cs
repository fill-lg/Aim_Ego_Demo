using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Одноразовый (но безопасный к повторному запуску) настройщик проекта.
/// Запуск: меню AimEgo > Setup > "Фаза 0: собрать префаб игрока и сцену".
///
/// Делает:
///  1. Создаёт ассет ввода Assets/Settings/PlayerControls.inputactions (новый Input System).
///  2. Собирает префаб Assets/Prefabs/Player.prefab:
///     Player (CharacterController + PlayerController + PlayerInputHandler)
///       ├─ Model (HumanDummy — тело, можно прятать от своей камеры)
///       ├─ CameraRoot (высота глаз, управляется приседом)
///           └─ Camera (pitch, FOV 100, SMAA)
///               └─ WeaponHolder (точка крепления вьюмодели оружия — фаза 1)
///  3. Убирает старого игрока из сцены, ставит нового на TSpawn,
///     вешает SpawnPoint (CT/T) на точки спавна, добавляет сцену в Build Settings.
/// </summary>
public static class AimEgoProjectSetup
{
    const string InputActionsPath = "Assets/Settings/PlayerControls.inputactions";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string DummyPrefabPath  = "Assets/Prefabs/HumanDummy_M White.prefab";
    const string ScenePath        = "Assets/Scenes/SampleScene.unity";

    [MenuItem("AimEgo/Setup/Фаза 0: собрать префаб игрока и сцену")]
    public static void Run()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            Debug.LogError("AimEgo Setup: открой сцену Assets/Scenes/SampleScene.unity и запусти настройку ещё раз.");
            return;
        }

        InputActionAsset actions = EnsureInputActions();
        GameObject playerPrefab = EnsurePlayerPrefab(actions);
        UpdateScene(playerPrefab);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("AimEgo Setup: готово — префаб игрока собран, спавны размечены, игрок стоит на TSpawn.", playerPrefab);
    }

    // ===== 1. АССЕТ ВВОДА =====
    static InputActionAsset EnsureInputActions()
    {
        var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<InputActionAsset>();

        var player = new InputActionMap("Player");

        var move = player.AddAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("up", "<Keyboard>/w")
            .With("down", "<Keyboard>/s")
            .With("left", "<Keyboard>/a")
            .With("right", "<Keyboard>/d");

        player.AddAction("Look",   InputActionType.Value,  "<Mouse>/delta");
        player.AddAction("Jump",   InputActionType.Button, "<Keyboard>/space");

        var crouch = player.AddAction("Crouch", InputActionType.Button);
        crouch.AddBinding("<Keyboard>/leftCtrl");
        crouch.AddBinding("<Keyboard>/c");

        player.AddAction("Fire",   InputActionType.Button, "<Mouse>/leftButton");
        player.AddAction("Reload", InputActionType.Button, "<Keyboard>/r");
        // Внимание: клавиши цифр в Input System называются "1"/"2", а не "digit1"/"digit2"
        player.AddAction("Slot1",  InputActionType.Button, "<Keyboard>/1");
        player.AddAction("Slot2",  InputActionType.Button, "<Keyboard>/2");
        player.AddAction("QuickSwap", InputActionType.Button, "<Keyboard>/q");

        var ui = new InputActionMap("UI");
        ui.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");

        asset.AddActionMap(player);
        asset.AddActionMap(ui);

        Directory.CreateDirectory("Assets/Settings");
        File.WriteAllText(InputActionsPath, asset.ToJson());
        AssetDatabase.ImportAsset(InputActionsPath);
        Debug.Log("AimEgo Setup: создан " + InputActionsPath);

        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
    }

    // ===== 2. ПРЕФАБ ИГРОКА =====
    static GameObject EnsurePlayerPrefab(InputActionAsset actions)
    {
        // Убираем старого игрока (префаб-инстанс болванки со скриптами поверх)
        foreach (var old in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include))
        {
            Debug.Log("AimEgo Setup: удаляю старого игрока '" + old.name + "'");
            Object.DestroyImmediate(old.gameObject);
        }

        var root = new GameObject("Player");

        var controller = root.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.4f;
        controller.skinWidth = 0.08f;
        controller.center = new Vector3(0f, 1f, 0f); // пивот у ног

        var inputHandler = root.AddComponent<PlayerInputHandler>();
        var playerController = root.AddComponent<PlayerController>();
        playerController.groundMask = LayerMask.GetMask("Ground");

        // Тело — болванка отдельным ребёнком (потом скроем от своей камеры, а чужим игрокам она видна)
        var dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
        if (dummy != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(dummy, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
        }
        else
        {
            Debug.LogWarning("AimEgo Setup: не нашёл " + DummyPrefabPath + " — тело игрока не добавлено.");
        }

        // CameraRoot: меняется ТОЛЬКО по высоте (присед). Наклон — у камеры.
        var cameraRoot = new GameObject("CameraRoot").transform;
        cameraRoot.SetParent(root.transform, false);
        cameraRoot.localPosition = new Vector3(0f, 1.8f, 0f);

        var cameraGO = new GameObject("Camera");
        cameraGO.transform.SetParent(cameraRoot, false);
        cameraGO.tag = "MainCamera";

        var cam = cameraGO.AddComponent<Camera>();
        cam.fieldOfView = 100f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.19f, 0.30f, 0.47f);
        cam.allowHDR = true;
        cameraGO.AddComponent<AudioListener>();

        var camData = cam.GetUniversalAdditionalCameraData();
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;

        var camScript = cameraGO.AddComponent<PlayerCamera>();
        camScript.input = inputHandler;
        camScript.playerBody = root.transform;

        // Точка крепления вьюмодели оружия (фаза 1) — справа снизу от центра экрана
        var weaponHolder = new GameObject("WeaponHolder");
        weaponHolder.transform.SetParent(cameraGO.transform, false);
        weaponHolder.transform.localPosition = new Vector3(0.25f, -0.25f, 0.5f);

        playerController.cameraRoot = cameraRoot;
        playerController.input = inputHandler;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("AimEgo Setup: собран " + PlayerPrefabPath);
        return prefab;
    }

    // ===== 3. СЦЕНА =====
    static void UpdateScene(GameObject playerPrefab)
    {
        SetupSpawn("CTSpawn", SpawnPoint.Team.CT);
        SetupSpawn("TSpawn", SpawnPoint.Team.T);

        // Игрок уже в сцене (повторный запуск) — просто переставляем его на спавн
        GameObject player;
        var existing = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            player = existing.gameObject;
            Debug.Log("AimEgo Setup: переставляю существующего игрока на точку спавна.");
        }
        else
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            Selection.activeGameObject = player;
        }

        // Позицию и поворот берём у SpawnPoint: он ищет поверхность пола рейкастом
        var spawnGO = GameObject.Find("TSpawn");
        var spawn = spawnGO != null ? spawnGO.GetComponent<SpawnPoint>() : null;
        Vector3 pos = spawn != null ? spawn.Position : new Vector3(-10f, 2f, 0f);
        Quaternion rot = spawn != null ? spawn.Rotation() : FaceCenter(pos);
        player.transform.SetPositionAndRotation(pos, rot);

        // Сцена — в Build Settings
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    static void SetupSpawn(string objectName, SpawnPoint.Team team)
    {
        var go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning("AimEgo Setup: не нашёл объект '" + objectName + "' в сцене.");
            return;
        }
        var sp = go.GetComponent<SpawnPoint>();
        if (sp == null) sp = go.AddComponent<SpawnPoint>();
        sp.team = team;
        sp.groundMask = LayerMask.GetMask("Ground");
    }

    // Спавны смотрят лицом к центру карты (на симметричной карте — друг на друга)
    static Quaternion FaceCenter(Vector3 pos)
    {
        Vector3 toCenter = Vector3.ProjectOnPlane(-pos, Vector3.up);
        if (toCenter.sqrMagnitude < 0.001f) toCenter = Vector3.forward;
        return Quaternion.LookRotation(toCenter.normalized, Vector3.up);
    }
}
