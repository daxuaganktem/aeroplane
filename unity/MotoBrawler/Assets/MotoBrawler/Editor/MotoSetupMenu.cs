using MotoBrawler.Bike;
using MotoBrawler.CameraRig;
using MotoBrawler.Controls;
using MotoBrawler.Core;
using MotoBrawler.DebugTools;
using TMPro;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MotoBrawler.EditorTools
{
    /// <summary>
    /// Tools > MotoBrawler menu. Each step can be run on its own, or all at once with
    /// "Build Complete Test Scene" (run it in a new, empty scene).
    ///
    /// The placeholder bike is built with exactly the hierarchy described in the README,
    /// so you can later swap the primitive meshes for your own model's meshes.
    /// </summary>
    public static class MotoSetupMenu
    {
        public const string RootFolder = "Assets/MotoBrawler";
        public const string SettingsFolder = RootFolder + "/Settings";
        public const string GeneratedFolder = RootFolder + "/Generated";

        public const string BikeLayer = "Bike";
        public const string GroundLayer = "Ground";
        public const string RoadLayer = "Road";
        public const string WallLayer = "Wall";

        // =====================================================================
        // Menu items
        // =====================================================================

        [MenuItem("Tools/MotoBrawler/Build Complete Test Scene", priority = 0)]
        public static void BuildEverything()
        {
            EnsureLayers();
            var (stats, arcade, realistic) = CreateSettingsAssets();
            Transform spawn = TestTrackBuilder.Build(GetLayers());
            GameObject bike = CreatePlaceholderBike(stats, arcade, spawn);
            CreateCameraRig(bike);
            CreateUI(bike, arcade, realistic);
            CreateGameSettings();
            Selection.activeGameObject = bike;
            MarkDirty();
            Debug.Log("MotoBrawler: test scene built. Press Play. Keyboard: W/↑ gas, A/D or ←/→ steer, " +
                      "Space brake/reverse, P assist preset, R respawn.");
        }

        [MenuItem("Tools/MotoBrawler/Steps/1. Add Layers (Bike, Ground, Road, Wall)", priority = 20)]
        public static void EnsureLayersMenu() => EnsureLayers();

        [MenuItem("Tools/MotoBrawler/Steps/2. Create Settings Assets (BikeStats + Arcade/Realistic)", priority = 21)]
        public static void CreateSettingsMenu()
        {
            Selection.activeObject = CreateSettingsAssets().stats;
        }

        [MenuItem("Tools/MotoBrawler/Steps/3. Build Test Track", priority = 22)]
        public static void BuildTrackMenu()
        {
            EnsureLayers();
            TestTrackBuilder.Build(GetLayers());
            MarkDirty();
        }

        [MenuItem("Tools/MotoBrawler/Steps/4. Create Placeholder Bike", priority = 23)]
        public static void CreateBikeMenu()
        {
            EnsureLayers();
            var settings = CreateSettingsAssets();
            GameObject spawn = GameObject.Find("SpawnPoint");
            Selection.activeGameObject = CreatePlaceholderBike(settings.stats, settings.arcade, spawn ? spawn.transform : null);
            MarkDirty();
        }

        [MenuItem("Tools/MotoBrawler/Steps/5. Create Camera Rig (for selected bike)", priority = 24)]
        public static void CreateCameraMenu()
        {
            GameObject bike = SelectedBike();
            if (bike) { CreateCameraRig(bike); MarkDirty(); }
        }

        [MenuItem("Tools/MotoBrawler/Steps/6. Create Mobile UI + Debug HUD (for selected bike)", priority = 25)]
        public static void CreateUIMenu()
        {
            GameObject bike = SelectedBike();
            if (!bike) return;
            var settings = CreateSettingsAssets();
            CreateUI(bike, settings.arcade, settings.realistic);
            CreateGameSettings();
            MarkDirty();
        }

        private static GameObject SelectedBike()
        {
            BikeController bike = Selection.activeGameObject
                ? Selection.activeGameObject.GetComponentInParent<BikeController>()
                : Object.FindFirstObjectByType<BikeController>();
            if (!bike) Debug.LogError("MotoBrawler: select a bike (object with BikeController) first.");
            return bike ? bike.gameObject : null;
        }

        // =====================================================================
        // Layers
        // =====================================================================

        public static void EnsureLayers()
        {
            EnsureLayer(BikeLayer);
            EnsureLayer(GroundLayer);
            EnsureLayer(RoadLayer);
            EnsureLayer(WallLayer);
        }

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"MotoBrawler: added layer '{layerName}' at index {i}.");
                return i;
            }
            Debug.LogError($"MotoBrawler: no free layer slot for '{layerName}'.");
            return 0;
        }

        private static TestTrackBuilder.Layers GetLayers() => new TestTrackBuilder.Layers
        {
            ground = LayerMask.NameToLayer(GroundLayer),
            road = LayerMask.NameToLayer(RoadLayer),
            wall = LayerMask.NameToLayer(WallLayer),
        };

        private static int Mask(params string[] layerNames) => LayerMask.GetMask(layerNames);

        // =====================================================================
        // ScriptableObject assets
        // =====================================================================

        public static (BikeStats stats, RidingAssists arcade, RidingAssists realistic) CreateSettingsAssets()
        {
            EnsureFolder(SettingsFolder);
            var stats = LoadOrCreate<BikeStats>($"{SettingsFolder}/BikeStats_Default.asset", null);
            var arcade = LoadOrCreate<RidingAssists>($"{SettingsFolder}/Assists_Arcade.asset", a => a.LoadArcadePreset());
            var realistic = LoadOrCreate<RidingAssists>($"{SettingsFolder}/Assists_Realistic.asset", a => a.LoadRealisticPreset());
            AssetDatabase.SaveAssets();
            return (stats, arcade, realistic);
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // =====================================================================
        // Placeholder bike (same hierarchy you'll use for the real model)
        // =====================================================================

        public static GameObject CreatePlaceholderBike(BikeStats stats, RidingAssists assists, Transform spawn)
        {
            EnsureFolder(GeneratedFolder);
            int bikeLayer = LayerMask.NameToLayer(BikeLayer);
            if (bikeLayer < 0)
            {
                Debug.LogWarning("MotoBrawler: 'Bike' layer missing, using Default. Run Steps > 1 and rebuild the bike.");
                bikeLayer = 0;
            }
            Material bodyMat = GetOrCreateMaterial("BikeBody", new Color(0.1f, 0.35f, 0.85f));
            Material darkMat = GetOrCreateMaterial("BikeDark", new Color(0.08f, 0.08f, 0.09f));
            Material metalMat = GetOrCreateMaterial("BikeMetal", new Color(0.7f, 0.7f, 0.72f));
            Material riderMat = GetOrCreateMaterial("Rider", new Color(0.9f, 0.75f, 0.2f));

            float r = stats.wheelRadius;
            float halfBase = stats.wheelbase * 0.5f;
            const float rake = 25f;              // fork rake (deg)
            const float headHeight = 0.95f;      // steering head height above ground

            // ---- Root: physics + gameplay components ----
            var root = new GameObject("Bike_Placeholder");
            Undo.RegisterCreatedObjectUndo(root, "Create Placeholder Bike");
            if (spawn) root.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            var controller = root.AddComponent<BikeController>();   // adds the Rigidbody
            root.AddComponent<PlayerBikeInput>();
            var crash = root.AddComponent<BikeCrashHandler>();

            // ---- Colliders (never lean): body capsule + rider box, zero friction ----
            PhysicsMaterial slippery = GetOrCreatePhysicsMaterial();
            Transform colliders = Child("Colliders", root.transform, Vector3.zero);
            var capsule = Child("BodyCollider", colliders, Vector3.zero).gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 2;   // along Z
            capsule.radius = 0.35f;
            capsule.height = stats.wheelbase + 2f * r * 0.95f;
            capsule.center = new Vector3(0f, 0.75f, 0f);
            capsule.sharedMaterial = slippery;
            var riderBox = Child("RiderCollider", colliders, Vector3.zero).gameObject.AddComponent<BoxCollider>();
            riderBox.size = new Vector3(0.5f, 0.7f, 0.6f);
            riderBox.center = new Vector3(0f, 1.3f, -0.2f);
            riderBox.sharedMaterial = slippery;

            // ---- Wheel probes at the axles, camera target ----
            Transform frontProbe = Child("FrontWheelProbe", root.transform, new Vector3(0f, r, halfBase));
            Transform rearProbe = Child("RearWheelProbe", root.transform, new Vector3(0f, r, -halfBase));
            Child("CameraTarget", root.transform, new Vector3(0f, 1.1f, 0f));

            // ---- Visual hierarchy ----
            Transform visual = Child("Visual", root.transform, Vector3.zero);
            var visuals = visual.gameObject.AddComponent<BikeVisuals>();
            Transform leanPivot = Child("LeanPivot", visual, Vector3.zero);

            Transform body = Child("Body", leanPivot, Vector3.zero);
            Box("Frame", body, new Vector3(0f, 0.6f, -0.05f), new Vector3(0.28f, 0.36f, 1.0f), bodyMat);
            Box("Tank", body, new Vector3(0f, 0.85f, 0.15f), new Vector3(0.36f, 0.22f, 0.45f), bodyMat);
            Box("Seat", body, new Vector3(0f, 0.86f, -0.35f), new Vector3(0.3f, 0.08f, 0.5f), darkMat);
            Box("Engine", body, new Vector3(0f, 0.42f, 0.05f), new Vector3(0.34f, 0.3f, 0.45f), metalMat);

            // Rider placeholder: pivot at the hips so the extra lean rotates around the seat.
            Transform rider = Child("Rider", leanPivot, new Vector3(0f, 0.9f, -0.3f));
            Transform torso = Primitive(PrimitiveType.Capsule, "Torso", rider, new Vector3(0f, 0.4f, 0.12f), new Vector3(0.42f, 0.42f, 0.42f), riderMat);
            torso.localRotation = Quaternion.Euler(30f, 0f, 0f);
            Primitive(PrimitiveType.Sphere, "Helmet", rider, new Vector3(0f, 0.85f, 0.32f), Vector3.one * 0.28f, darkMat);

            // Steering head: local Y = steering axis, tilted back by the rake angle.
            // The front axle lies on that axis, 'forkLength' below the head.
            float forkLength = (headHeight - r) / Mathf.Cos(rake * Mathf.Deg2Rad);
            float headZ = halfBase - forkLength * Mathf.Sin(rake * Mathf.Deg2Rad);
            Transform steerPivot = Child("SteerPivot", leanPivot, new Vector3(0f, headHeight, headZ));
            steerPivot.localRotation = Quaternion.Euler(-rake, 0f, 0f);

            Transform fork = Child("FrontFork", steerPivot, Vector3.zero);
            Box("ForkLegs", fork, new Vector3(0f, -forkLength * 0.5f, 0f), new Vector3(0.22f, forkLength, 0.06f), metalMat);
            Box("Handlebar", fork, new Vector3(0f, 0.08f, -0.05f), new Vector3(0.7f, 0.035f, 0.035f), darkMat);

            Transform frontSpin = Child("FrontWheelSpin", steerPivot, new Vector3(0f, -forkLength, 0f));
            frontSpin.rotation = root.transform.rotation;   // local X = axle, regardless of the rake
            Wheel("FrontWheel", frontSpin, r, darkMat, metalMat);

            Transform rearSpin = Child("RearWheelSpin", leanPivot, new Vector3(0f, r, -halfBase));
            Wheel("RearWheel", rearSpin, r, darkMat, metalMat);
            Box("Swingarm", body, new Vector3(0f, r + 0.05f, -halfBase * 0.55f), new Vector3(0.2f, 0.06f, halfBase * 0.9f), metalMat);

            SetLayerRecursive(root, bikeLayer);

            // ---- Wire references ----
            SetRef(controller, "stats", stats);
            SetRef(controller, "assists", assists);
            SetRef(controller, "frontWheelProbe", frontProbe);
            SetRef(controller, "rearWheelProbe", rearProbe);
            SetMask(controller, "groundMask", Mask(GroundLayer, RoadLayer, "Default"));

            SetMask(crash, "crashMask", Mask(WallLayer, RoadLayer, "Default"));
            SetMask(crash, "respawnSurfaceMask", Mask(RoadLayer));
            if (spawn) SetRef(crash, "spawnPoint", spawn);

            SetRef(visuals, "bike", controller);
            SetRef(visuals, "leanPivot", leanPivot);
            SetRef(visuals, "steerPivot", steerPivot);
            SetRef(visuals, "frontWheelSpin", frontSpin);
            SetRef(visuals, "rearWheelSpin", rearSpin);
            SetRef(visuals, "rider", rider);

            return root;
        }

        private static void Wheel(string name, Transform spinPivot, float radius, Material tyre, Material marker)
        {
            // Cylinder's axis is Y; rotate it onto X (the axle). Default cylinder: Ø1 m, 2 m tall.
            Transform wheel = Primitive(PrimitiveType.Cylinder, name, spinPivot, Vector3.zero,
                new Vector3(radius * 2f, 0.06f, radius * 2f), tyre);
            wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // A bright block on the rim so you can see the wheel spin.
            Box(name + "_Marker", spinPivot, new Vector3(0f, radius * 0.65f, 0f), new Vector3(0.14f, radius * 0.5f, 0.05f), marker);
        }

        // =====================================================================
        // Camera
        // =====================================================================

        public static void CreateCameraRig(GameObject bike)
        {
            Transform target = bike.transform.Find("CameraTarget") ?? bike.transform;

            Camera main = Camera.main;
            if (!main)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
                main = camGo.GetComponent<Camera>();
            }
            if (!main.GetComponent<CinemachineBrain>()) main.gameObject.AddComponent<CinemachineBrain>();
            main.nearClipPlane = 0.1f;
            main.farClipPlane = 800f;

            var go = new GameObject("BikeCamera");
            Undo.RegisterCreatedObjectUndo(go, "Create Bike Camera");
            go.transform.position = target.position + bike.transform.rotation * new Vector3(0f, 2f, -5.5f);

            var cm = go.AddComponent<CinemachineCamera>();
            cm.Follow = target;
            cm.LookAt = target;
            LensSettings lens = cm.Lens;
            lens.FieldOfView = 60f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 800f;
            cm.Lens = lens;

            // Behind and above, yaw follows the bike but pitch/roll don't (World Up), soft lag.
            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 2f, -5.5f);
            TrackerSettings tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
            tracker.PositionDamping = new Vector3(0.15f, 0.35f, 0.45f);
            tracker.RotationDamping = new Vector3(0.5f, 0.6f, 0.5f);
            follow.TrackerSettings = tracker;

            // Aim slightly ahead of and above the bike, with gentle damping.
            var aim = go.AddComponent<CinemachineRotationComposer>();
            aim.TargetOffset = new Vector3(0f, 0.2f, 2f);
            aim.Damping = new Vector2(0.4f, 0.4f);

            var fx = go.AddComponent<BikeCameraFX>();
            SetRef(fx, "bike", bike.GetComponent<BikeController>());
            SetRef(fx, "visuals", bike.GetComponentInChildren<BikeVisuals>());
        }

        // =====================================================================
        // Mobile UI + debug HUD
        // =====================================================================

        public static void CreateUI(GameObject bike, RidingAssists arcade, RidingAssists realistic)
        {
            if (!Object.FindFirstObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            var canvasGo = new GameObject("BikeUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Bike UI");
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvasT = canvasGo.transform;

            // ---- Touch controls ----
            RectTransform controlsRoot = Stretch("Controls", canvasT);
            RectTransform steerGroup = Stretch("SteerButtons", controlsRoot);
            TouchZone left = Zone("SteerLeft", steerGroup, new Vector2(0, 0), new Vector2(40, 40), new Vector2(260, 260), "<");
            TouchZone right = Zone("SteerRight", steerGroup, new Vector2(0, 0), new Vector2(320, 40), new Vector2(260, 260), ">");
            TouchZone brake = Zone("Brake", controlsRoot, new Vector2(0, 0), new Vector2(40, 320), new Vector2(540, 150), "BRAKE");
            TouchZone gas = Zone("Throttle", controlsRoot, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(340, 430), "GAS");

            // ---- Setting buttons (top right, two rows) ----
            Button modeBtn = UIButton("SteeringModeButton", canvasT, new Vector2(-40, -40), "Steer: BUTTONS", out TMP_Text modeLabel);
            Button autoBtn = UIButton("AutoThrottleButton", canvasT, new Vector2(-380, -40), "Auto-gas: OFF", out TMP_Text autoLabel);
            Button calibrateBtn = UIButton("CalibrateTiltButton", canvasT, new Vector2(-40, -150), "Calibrate tilt", out _);
            Button presetBtn = UIButton("AssistPresetButton", canvasT, new Vector2(-380, -150), "Assists: Arcade", out TMP_Text presetLabel);

            var mobile = canvasGo.AddComponent<MobileControls>();
            SetRef(mobile, "steerLeftZone", left);
            SetRef(mobile, "steerRightZone", right);
            SetRef(mobile, "throttleZone", gas);
            SetRef(mobile, "brakeZone", brake);
            SetRef(mobile, "steerButtonsGroup", steerGroup.gameObject);
            SetRef(mobile, "steeringModeButton", modeBtn);
            SetRef(mobile, "steeringModeLabel", modeLabel);
            SetRef(mobile, "autoThrottleButton", autoBtn);
            SetRef(mobile, "autoThrottleLabel", autoLabel);
            SetRef(mobile, "calibrateTiltButton", calibrateBtn);
            SetRef(mobile, "controlsRoot", controlsRoot.gameObject);

            var bikeController = bike.GetComponent<BikeController>();
            var playerInput = bike.GetComponent<PlayerBikeInput>();
            if (playerInput) SetRef(playerInput, "mobileControls", mobile);

            // ---- Assist preset switcher ----
            var switcher = canvasGo.AddComponent<AssistPresetSwitcher>();
            SetRef(switcher, "bike", bikeController);
            SetRef(switcher, "cycleButton", presetBtn);
            SetRef(switcher, "buttonLabel", presetLabel);
            var so = new SerializedObject(switcher);
            SerializedProperty presets = so.FindProperty("presets");
            presets.arraySize = 2;
            presets.GetArrayElementAtIndex(0).objectReferenceValue = arcade;
            presets.GetArrayElementAtIndex(1).objectReferenceValue = realistic;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- Debug HUD (top left) ----
            RectTransform hudRect = Rect("DebugHUD", canvasT, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(760, 330));
            var hudText = hudRect.gameObject.AddComponent<TextMeshProUGUI>();
            hudText.fontSize = 26;
            hudText.alignment = TextAlignmentOptions.TopLeft;
            hudText.raycastTarget = false;
            hudText.text = "HUD";
            hudText.outlineWidth = 0.2f;
            hudText.outlineColor = Color.black;
            var hud = hudRect.gameObject.AddComponent<BikeDebugHUD>();
            SetRef(hud, "bike", bikeController);
            SetRef(hud, "visuals", bike.GetComponentInChildren<BikeVisuals>());
            SetRef(hud, "crashHandler", bike.GetComponent<BikeCrashHandler>());
            SetRef(hud, "text", hudText);
        }

        public static void CreateGameSettings()
        {
            if (Object.FindFirstObjectByType<FrameRateSettings>()) return;
            var go = new GameObject("GameSettings", typeof(FrameRateSettings));
            Undo.RegisterCreatedObjectUndo(go, "Create Game Settings");
        }

        // ---- UI helpers ----

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform Stretch(string name, Transform parent)
        {
            RectTransform rt = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TMP_Text Label(Transform parent, string text, float size)
        {
            RectTransform rt = Stretch("Label", parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static TouchZone Zone(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text)
        {
            RectTransform rt = Rect(name, parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.18f);
            img.raycastTarget = false;   // zones are polled, not clicked
            Label(rt, text, 56);
            return rt.gameObject.AddComponent<TouchZone>();
        }

        private static Button UIButton(string name, Transform parent, Vector2 pos, string text, out TMP_Text label)
        {
            RectTransform rt = Rect(name, parent, new Vector2(1, 1), new Vector2(1, 1), pos, new Vector2(320, 90));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            label = Label(rt, text, 30);
            return btn;
        }

        // =====================================================================
        // Generic helpers (also used by TestTrackBuilder)
        // =====================================================================

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static Material GetOrCreateMaterial(string matName, Color color, Texture texture = null, float tiling = 1f)
        {
            EnsureFolder(GeneratedFolder);
            string path = $"{GeneratedFolder}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };   // URP Lit maps .color to _BaseColor
            if (texture)
            {
                mat.mainTexture = texture;                    // ... and .mainTexture to _BaseMap
                mat.mainTextureScale = new Vector2(tiling, tiling);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static PhysicsMaterial GetOrCreatePhysicsMaterial()
        {
            string path = $"{GeneratedFolder}/BikeNoFriction.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm) return pm;
            pm = new PhysicsMaterial("BikeNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        private static Transform Child(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        private static Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // visuals never collide
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Transform Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat) =>
            Primitive(PrimitiveType.Cube, name, parent, localPos, scale, mat);

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"MotoBrawler: no field '{field}' on {target.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMask(Object target, string field, int mask)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"MotoBrawler: no field '{field}' on {target.GetType().Name}"); return; }
            prop.intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MarkDirty() =>
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
