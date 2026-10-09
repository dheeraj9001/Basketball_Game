using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PinePie.SimpleJoystick;

[InitializeOnLoad]
public static class BasketballSceneSetup
{
    const string ScenePath = "Assets/BasketBall_Project/Scenes/Main.unity";
    const string GroundPath = "Assets/BasketBall_Project/Fbx/Basekball Ground.fbx";
    const string PlayerPath = "Assets/BasketBall_Project/Fbx/Player01.fbx";
    const string BallPath = "Assets/BasketBall_Project/Fbx/Basekball.fbx";
    const string JoystickPath = "Assets/PinePie/Simple Joystick/Examples/Prefabs/Static Free moving.prefab";
    const string IdlePath = "Assets/BasketBall_Project/Animations/Idle.fbx";
    const string DribblePath = "Assets/BasketBall_Project/Animations/Dribble.fbx";
    const string AnimatorPath = "Assets/BasketBall_Project/Animations/PlayerGameplay.controller";
    const float BallDiameter = .24f;
    const float RimHeight = 3.05f;
    const float FreeThrowDistance = 4.2f;
    static bool setupDone;

    static BasketballSceneSetup()
    {
        EditorApplication.update += EnsureSceneSetup;
    }

    [MenuItem("Basketball Game/Build Visible Gameplay Hierarchy")]
    public static void BuildVisibleGameplayHierarchy()
    {
        SetupScene();
    }

    [MenuItem("Basketball Game/Inspect Imported FBX Layout")]
    public static void InspectImportedModels()
    {
        foreach (string path in new[] { GroundPath, BallPath, PlayerPath })
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) { Debug.LogError("Could not load model: " + path); continue; }
            Debug.Log("MODEL " + path + " root=" + model.name);
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                Renderer renderer = child.GetComponent<Renderer>();
                if (child == model.transform || renderer != null)
                    Debug.Log("FBX NODE " + child.name + " local=" + child.localPosition + " bounds=" + (renderer != null ? renderer.localBoundsSafe() : "-"));
            }
        }
    }

    static void EnsureSceneSetup()
    {
        if (setupDone || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath) return;
        GameObject savedRoot = GameObject.Find("Basketball Game");
        if (savedRoot != null)
        {
            EnsurePlayerController(savedRoot);
            setupDone = true;
            return;
        }
        setupDone = true;
        SetupScene();
    }

    static void SetupScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("Open " + ScenePath + " before building the basketball hierarchy.");
            return;
        }
        GameObject existingRoot = GameObject.Find("Basketball Game");
        if (existingRoot != null)
        {
            EnsurePlayerController(existingRoot);
            Debug.Log("Basketball gameplay hierarchy already exists in Main.unity.");
            return;
        }

        GameObject groundAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GroundPath);
        GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        GameObject ballAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BallPath);
        if (groundAsset == null || playerAsset == null || ballAsset == null)
        {
            Debug.LogError("Basketball FBX setup failed; one or more supplied FBX files could not be loaded.");
            return;
        }

        foreach (BasketballGameController oldController in Object.FindObjectsOfType<BasketballGameController>())
            Undo.DestroyObjectImmediate(oldController);

        var gameRoot = NewGroup("Basketball Game", null, scene);
        var environment = NewGroup("Environment", gameRoot.transform, scene);
        var court = InstantiateAsset(groundAsset, environment.transform, "Court (Ground FBX)");
        court.transform.localPosition = Vector3.zero;
        court.transform.localRotation = Quaternion.identity;
        court.transform.localScale = Vector3.one;
        AddStaticMeshColliders(court);

        Bounds courtBounds = GetRenderBounds(court);
        Vector3 hoopPosition = FindHoopCenter(court, courtBounds);
        hoopPosition.y = RimHeight;
        float heading = Mathf.Abs(hoopPosition.z - courtBounds.min.z) < Mathf.Abs(courtBounds.max.z - hoopPosition.z) ? 180f : 0f;
        Quaternion playerRotation = Quaternion.Euler(0f, heading, 0f);

        var gameplay = NewGroup("Gameplay", gameRoot.transform, scene);
        var playerRoot = NewGroup("Player", gameplay.transform, scene);
        Vector3 handOffset = playerRotation * new Vector3(.28f, 0f, 0f);
        Vector3 shotOffset = playerRotation * Vector3.forward * FreeThrowDistance;
        playerRoot.transform.position = new Vector3(hoopPosition.x - handOffset.x, 0f, hoopPosition.z - shotOffset.z);
        playerRoot.transform.rotation = playerRotation;
        var playerModel = InstantiateAsset(playerAsset, playerRoot.transform, "Player01 (Humanoid FBX)");
        FitModelHeight(playerModel, 1.75f);
        var playerCollider = playerRoot.AddComponent<CapsuleCollider>();
        playerCollider.height = 1.75f;
        playerCollider.radius = .34f;
        playerCollider.center = new Vector3(0f, .875f, 0f);
        var playerBody = playerRoot.AddComponent<Rigidbody>();
        playerBody.isKinematic = false;
        playerBody.useGravity = true;
        playerBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        BasketballPlayerController movementController = playerRoot.AddComponent<BasketballPlayerController>();
        Animator animator = playerModel.GetComponentInChildren<Animator>();
        if (animator == null) animator = playerModel.AddComponent<Animator>();
        Avatar avatar = FindAvatar(PlayerPath);
        if (avatar != null) animator.avatar = avatar;
        var hand = NewGroup("Right Hand Ball Anchor", playerRoot.transform, scene);
        hand.transform.localPosition = new Vector3(.28f, 1.35f, .25f);

        var ballRoot = NewGroup("Basketball", gameplay.transform, scene);
        Vector3 ballOffset = playerRotation * new Vector3(-.27f, 0f, -.2f);
        ballRoot.transform.position = playerRoot.transform.position + new Vector3(ballOffset.x, BallDiameter * .5f, ballOffset.z);
        var ballModel = InstantiateAsset(ballAsset, ballRoot.transform, "Basketball (Ball FBX)");
        FitModelDiameter(ballModel, BallDiameter);
        var ballCollider = ballRoot.AddComponent<SphereCollider>();
        ballCollider.radius = BallDiameter * .5f;
        var ballBody = ballRoot.AddComponent<Rigidbody>();
        ballBody.mass = .624f;
        ballBody.linearDamping = 0f;
        ballBody.angularDamping = .05f;
        ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ballBody.interpolation = RigidbodyInterpolation.Interpolate;
        ballBody.maxAngularVelocity = 30f;
        ballBody.isKinematic = true;

        var hoopGroup = NewGroup("Hoop Gameplay", environment.transform, scene);
        var hoopTarget = NewGroup("Hoop Target", hoopGroup.transform, scene);
        hoopTarget.transform.position = hoopPosition;
        var triggerObject = NewGroup("Scoring Trigger", hoopGroup.transform, scene);
        triggerObject.transform.position = hoopPosition + Vector3.down * .18f;
        var scoreCollider = triggerObject.AddComponent<BoxCollider>();
        scoreCollider.isTrigger = true;
        scoreCollider.size = new Vector3(.35f, .34f, .35f);
        triggerObject.AddComponent<HoopScoreTrigger>();

        var arcObject = NewGroup("Shot Arc Preview", gameplay.transform, scene);
        var arc = arcObject.AddComponent<LineRenderer>();
        arc.useWorldSpace = true;
        arc.positionCount = 0;
        arc.startWidth = .035f;
        arc.endWidth = .012f;
        arc.numCapVertices = 3;
        arc.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        arc.startColor = Color.white;
        arc.endColor = Color.white;

        var cameras = NewGroup("Camera Rig", gameRoot.transform, scene);
        Camera camera = Camera.main;
        if (camera == null) camera = Object.FindObjectOfType<Camera>();
        if (camera != null)
        {
            camera.transform.SetParent(cameras.transform, true);
            camera.tag = "MainCamera";
            camera.fieldOfView = 58f;
            camera.transform.position = playerRoot.transform.position + new Vector3(0f, 2.7f, -6.8f);
            camera.transform.rotation = Quaternion.LookRotation(hoopPosition + Vector3.up * .15f - camera.transform.position, Vector3.up);
        }
        var lighting = NewGroup("Lighting", environment.transform, scene);
        foreach (Light light in Object.FindObjectsOfType<Light>())
            if (light.transform.root != gameRoot.transform) light.transform.SetParent(lighting.transform, true);
        foreach (Volume volume in Object.FindObjectsOfType<Volume>())
            if (volume.transform.root != gameRoot.transform) volume.transform.SetParent(lighting.transform, true);

        var uiAndSystems = NewGroup("UI & Systems", gameRoot.transform, scene);
        var managerObject = NewGroup("Game Manager (HUD + Input)", uiAndSystems.transform, scene);
        var controller = managerObject.AddComponent<BasketballGameController>();
        controller.playerRoot = playerRoot.transform;
        controller.playerController = movementController;
        controller.handAnchor = hand.transform;
        controller.basketball = ballRoot.transform;
        controller.hoopTarget = hoopTarget.transform;
        controller.arcPreview = arc;
        controller.gameCamera = camera;
        controller.rimHeight = RimHeight;
        controller.ballDiameter = BallDiameter;
        controller.launchAngle = 52f;
        controller.chargeSeconds = 1.4f;

        // The original scene's imported ground is replaced by the FBX instance in the organized hierarchy above.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == gameRoot) continue;
            if (root.name.StartsWith("Basekball Ground")) Undo.DestroyObjectImmediate(root);
        }
        EnsurePlayerController(gameRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = gameRoot;
        Debug.Log("Basketball gameplay hierarchy created and saved. Court: " + courtBounds + ", rim target: " + hoopPosition);
    }

    static void EnsurePlayerController(GameObject gameRoot)
    {
        Transform player = FindChild(gameRoot.transform, "Player");
        bool changed = false;
        if (player != null && player.GetComponent<BasketballPlayerController>() == null)
        {
            Undo.AddComponent<BasketballPlayerController>(player.gameObject);
            changed = true;
        }
        var body = player != null ? player.GetComponent<Rigidbody>() : null;
        if (body != null)
        {
            bool bodyChanged = body.isKinematic || !body.useGravity || body.constraints != (RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            if (bodyChanged)
            {
                EditorUtility.SetDirty(body);
                changed = true;
            }
        }
        BasketballGameController manager = gameRoot.GetComponentInChildren<BasketballGameController>(true);
        Transform systems = FindChild(gameRoot.transform, "UI & Systems");
        if (systems == null) systems = NewGroup("UI & Systems", gameRoot.transform, gameRoot.scene).transform;
        Canvas canvas = systems.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasObject = NewGroup("Gameplay Canvas", systems, gameRoot.scene);
            canvasObject.layer = 5;
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            BasketballCanvasUI ui = canvasObject.AddComponent<BasketballCanvasUI>();
            GameObject joystickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(JoystickPath);
            if (joystickPrefab != null)
            {
                GameObject joystickObject = PrefabUtility.InstantiatePrefab(joystickPrefab, canvas.transform) as GameObject;
                if (joystickObject != null)
                {
                    joystickObject.name = "PinePie Movement Joystick";
                    RectTransform rect = joystickObject.GetComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(38, 35); rect.sizeDelta = new Vector2(250, 230);
                    JoystickController stick = joystickObject.GetComponent<JoystickController>();
                    if (manager != null) manager.movementJoystick = stick;
                }
            }
            if (manager != null) manager.canvasUI = ui;
            changed = true;
        }
        else if (manager != null)
        {
            BasketballCanvasUI ui = canvas.GetComponent<BasketballCanvasUI>();
            if (ui == null) { ui = canvas.gameObject.AddComponent<BasketballCanvasUI>(); changed = true; }
            if (manager.canvasUI != ui) { manager.canvasUI = ui; changed = true; }
            JoystickController stick = canvas.GetComponentInChildren<JoystickController>(true);
            if (manager.movementJoystick != stick) { manager.movementJoystick = stick; changed = true; }
            if (ui != null)
            {
                Transform grabTransform = FindChild(canvas.transform, "GrabButton");
                Transform shootTransform = FindChild(canvas.transform, "ShootButton");
                Button grab = grabTransform != null ? grabTransform.GetComponent<Button>() : null;
                Button shoot = shootTransform != null ? shootTransform.GetComponent<Button>() : null;
                if (ui.grabButton != grab) { ui.grabButton = grab; changed = true; }
                if (ui.shootButton != shoot) { ui.shootButton = shoot; changed = true; }
                EditorUtility.SetDirty(ui);
            }
        }
        if (canvas != null && canvas.transform.localScale != Vector3.one)
        {
            canvas.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(canvas.transform);
            changed = true;
        }
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject events = NewGroup("EventSystem", null, gameRoot.scene);
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
            changed = true;
        }
        if (player != null) changed |= EnsurePlayerAnimation(player.gameObject);
        Transform handAnchor = FindChild(gameRoot.transform, "Right Hand Ball Anchor");
        if (handAnchor != null && handAnchor.localPosition != new Vector3(.28f, 1.35f, .25f))
        {
            handAnchor.localPosition = new Vector3(.28f, 1.35f, .25f);
            EditorUtility.SetDirty(handAnchor);
            changed = true;
        }
        Transform environment = FindChild(gameRoot.transform, "Environment");
        if (environment != null && FindChild(environment, "Court Floor Collision") == null)
        {
            GameObject floor = NewGroup("Court Floor Collision", environment, gameRoot.scene);
            floor.transform.position = new Vector3(0f, -.1f, 6f);
            BoxCollider collider = floor.AddComponent<BoxCollider>();
            collider.size = new Vector3(20f, .2f, 24f);
            floor.isStatic = true;
            changed = true;
        }
        if (!changed || player == null) return;
        EditorUtility.SetDirty(player.gameObject);
        EditorSceneManager.MarkSceneDirty(gameRoot.scene);
        EditorSceneManager.SaveScene(gameRoot.scene);
    }

    static bool EnsurePlayerAnimation(GameObject player)
    {
        Animator animator = player.GetComponentInChildren<Animator>(true);
        if (animator == null) return false;
        bool changed = false;
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
        if (controller == null)
        {
            AnimationClip idle = LoadClip(IdlePath);
            AnimationClip dribble = LoadClip(DribblePath);
            if (idle == null || dribble == null) return false;
            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            AnimatorState idleState = controller.layers[0].stateMachine.AddState("Idle");
            AnimatorState dribbleState = controller.layers[0].stateMachine.AddState("Dribble");
            idleState.motion = idle; dribbleState.motion = dribble;
            controller.AddParameter("HasBall", AnimatorControllerParameterType.Bool);
            AnimatorStateTransition toDribble = idleState.AddTransition(dribbleState);
            toDribble.AddCondition(AnimatorConditionMode.If, 0, "HasBall"); toDribble.hasExitTime = false;
            AnimatorStateTransition toIdle = dribbleState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "HasBall"); toIdle.hasExitTime = false;
            EditorUtility.SetDirty(controller);
            changed = true;
        }
        if (animator.runtimeAnimatorController != controller)
        {
            Avatar avatar = FindAvatar(PlayerPath);
            if (avatar != null) animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            changed = true;
        }
        return changed;
    }

    static AnimationClip LoadClip(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null && importer.clipAnimations.Length == 0)
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++) clips[i].loopTime = true;
            if (clips.Length > 0)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        return null;
    }

    static Transform FindChild(Transform root, string childName)
    {
        if (root.name == childName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChild(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject NewGroup(string name, Transform parent, Scene scene)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        if (parent != null) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Build basketball gameplay hierarchy");
        return go;
    }

    static GameObject InstantiateAsset(GameObject asset, Transform parent, string name)
    {
        var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (instance == null) instance = Object.Instantiate(asset);
        Undo.RegisterCreatedObjectUndo(instance, "Add supplied basketball FBX");
        instance.name = name;
        instance.transform.SetParent(parent, false);
        return instance;
    }

    static void AddStaticMeshColliders(GameObject root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }
    }

    static Bounds GetRenderBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, new Vector3(10f, 6f, 30f));
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static Vector3 FindHoopCenter(GameObject root, Bounds bounds)
    {
        Renderer best = null;
        int bestScore = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string node = renderer.name.ToLowerInvariant();
            int score = node.Contains("rim") ? 4 : node.Contains("hoop") ? 3 : node.Contains("basket") ? 2 : node.Contains("ring") ? 1 : 0;
            if (score > bestScore) { best = renderer; bestScore = score; }
        }
        if (best != null) return best.bounds.center;
        // The supplied FBX keeps the hoop near one end of the court; use that end when names are generic.
        return new Vector3(bounds.center.x, RimHeight, bounds.max.z - Mathf.Min(1f, bounds.size.z * .04f));
    }

    static void FitModelHeight(GameObject model, float height)
    {
        Bounds bounds = GetRenderBounds(model);
        if (bounds.size.y <= .0001f) return;
        float scale = height / bounds.size.y;
        model.transform.localScale = Vector3.one * scale;
        bounds = GetRenderBounds(model);
        Vector3 bottomInParent = model.transform.parent.InverseTransformPoint(bounds.min);
        model.transform.localPosition = new Vector3(0f, -bottomInParent.y, 0f);
    }

    static void FitModelDiameter(GameObject model, float diameter)
    {
        Bounds bounds = GetRenderBounds(model);
        float dimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (dimension <= .0001f) return;
        float scale = diameter / dimension;
        model.transform.localScale = Vector3.one * scale;
        bounds = GetRenderBounds(model);
        model.transform.localPosition = -model.transform.parent.InverseTransformPoint(bounds.center);
    }

    static Avatar FindAvatar(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Avatar avatar && avatar.isHuman) return avatar;
        return null;
    }
}

static class BasketballRendererExtensions
{
    public static string localBoundsSafe(this Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned) return skinned.localBounds.ToString();
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.ToString() : "-";
    }
}
