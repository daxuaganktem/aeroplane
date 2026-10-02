# MotoBrawler: milestone 1 (bike riding and controls)

An arcade motorcycle controller for Unity 6 (URP, Input System, Cinemachine 3). The bike runs on one Rigidbody kept upright by code. Each wheel finds the ground with a raycast, lean is visual only, and steering gets gentler as speed rises. Riding assists come in Arcade and Realistic presets. You can steer with touch buttons or by tilting the phone, or use the keyboard in the Editor. The camera is a Cinemachine follow camera, and an editor menu builds the whole test scene from code.

**Install:** copy `Assets/MotoBrawler` into your Unity project's `Assets/` folder.

---

## 1. Scripts

| Script | Purpose |
|---|---|
| `Scripts/Bike/BikeStats.cs` | ScriptableObject with every per-bike tuning value: engine, brakes, steering, grip, suspension, lean, mass and geometry. |
| `Scripts/Bike/RidingAssists.cs` | ScriptableObject with the assist sliders (steering, stability, brake/ABS, crash speed) and Arcade/Realistic preset loaders. |
| `Scripts/Bike/IBikeInputProvider.cs` | `BikeInputState` struct plus the interface any rider implements (player now, AI later). |
| `Scripts/Bike/BikeController.cs` | Core arcade physics: suspension raycasts, throttle/brake/drag, friction-circle grip, speed-sensitive yaw, self-levelling, airtime. |
| `Scripts/Bike/BikeVisuals.cs` | Visual-only lean, fork steering, wheel spin, suspension travel, accel/brake pitch, and the fallen-over crash pose. |
| `Scripts/Bike/BikeCrashHandler.cs` | Head-on wall crash detection, freeze, and respawn on the road after 2 s using recent safe positions. |
| `Scripts/Controls/ControlSettings.cs` | Saved control preferences: steering mode (buttons/tilt), auto-throttle, tilt sensitivity and calibration. |
| `Scripts/Controls/PlayerBikeInput.cs` | Human rider. Merges keyboard/gamepad (Input System actions built in code) with the mobile controls. |
| `Scripts/Controls/MobileControls.cs` | On-screen controls: multi-touch hold zones, tilt steering, mode/auto-gas/calibrate buttons. |
| `Scripts/Controls/TouchZone.cs` | A rectangular hold zone with a pressed highlight, polled by MobileControls. |
| `Scripts/Camera/BikeCameraFX.cs` | Widens the Cinemachine FOV with speed and rolls the camera (Dutch) with the bike's lean. |
| `Scripts/Debug/BikeDebugHUD.cs` | Debug readout: speed (km/h), steering raw → assisted, lean, preset, input mode, state, FPS. `R` respawns. |
| `Scripts/Debug/AssistPresetSwitcher.cs` | Cycles assist presets with the `P` key or the on-screen button, and remembers the choice. |
| `Scripts/Core/FrameRateSettings.cs` | Mobile runtime settings: 60 fps target, landscape only, screen never sleeps. |
| `Editor/MotoSetupMenu.cs` | **Tools > MotoBrawler** menu: adds layers, creates assets, builds the placeholder bike, camera rig, UI and HUD. |
| `Editor/TestTrackBuilder.cs` | Builds the test track from code: ground, looping road with hill/hairpin/S-bend, two ramps, crash walls, spawn. |

The code uses one namespace per area: `MotoBrawler.Bike`, `.Controls`, `.CameraRig`, `.DebugTools`, `.Core` and `.EditorTools`.

### How the pieces talk

```
PlayerBikeInput ─┐ (IBikeInputProvider)          AIRiderInput (later) ─┐
                 └──────────────► BikeController ◄──────────────────────┘
                                   │  ▲ BikeStats / RidingAssists (ScriptableObjects)
         reads state (speed, lean, │  │
         fork angle, contacts...)  ▼  │ SetCrashed / Teleport / SetAssists / SetStats
   BikeVisuals  BikeCameraFX  BikeDebugHUD  BikeCrashHandler  AssistPresetSwitcher
```

`BikeController` doesn't know about keyboards, touch, cameras or UI. To add AI riders, write another `IBikeInputProvider`. To add another bike, make another `BikeStats` asset. Combat can call `BikeCrashHandler.Crash()` and push the Rigidbody, and it can listen to the `Landed`, `CrashStateChanged`, `Crashed` and `Respawned` events.

---

## 2. Project setup (once)

1. Create the project from the **Universal 3D** template (Unity 6000.x LTS).
2. **Window > Package Manager > Unity Registry**: install **Input System**, **Cinemachine** (3.x) and, optionally, **ProBuilder**. TextMeshPro is built into Unity 6 (uGUI).
3. When the Input System asks to enable the new backend, click **Yes**. If it doesn't ask, open **Edit > Project Settings > Player > Other Settings > Active Input Handling** and set it to **Input System Package (New)**, or **Both**.
4. **Window > TextMeshPro > Import TMP Essential Resources** (needed for the UI text).
5. Copy `Assets/MotoBrawler` into the project and wait for it to compile.
6. **Edit > Project Settings > Time > Fixed Timestep**: `0.0166667` (60 Hz physics, which matches 60 fps). The default 0.02 also works.
7. **Player settings (Android/iOS)**:
   - Default Orientation: **Auto Rotation**, with only Landscape Left and Landscape Right ticked.
   - Android: Scripting Backend **IL2CPP**, ARM64.
   - iOS: add a "Motion Usage Description" if your iOS / Input System version asks for one.
8. **Quality** (for mid-range phones): use the URP *Mobile* / *Performant* render pipeline asset. Set shadow distance to about 60 m, turn MSAA off or to 2x, and turn HDR off.

## 3. Build the test scene (one click)

1. **File > New Scene > Basic (URP)**, then save it as `Assets/Scenes/BikeTest.unity`.
2. **Tools > MotoBrawler > Build Complete Test Scene**.
3. Press **Play**.

This creates:

- **Layers** `Bike`, `Ground`, `Road` and `Wall`.
- **Assets** in `Assets/MotoBrawler/Settings`: `BikeStats_Default`, `Assists_Arcade` and `Assists_Realistic`.
- **Track:**
  - 600 m grid ground.
  - About 780 m road loop: a long start straight, a gentle sweeper, a 9 m hill, a tight hairpin (about 9 m radius), an S-bend and a long sweeper.
  - Two ramps (10° and 16°) in the infield, pointed at a red crash wall 80 m away.
  - A 45° wall for glancing hits.
  - A `SpawnPoint`.
- **Bike_Placeholder:** a primitive bike with exactly the hierarchy from section 4 and everything wired up.
- **BikeCamera:** a Cinemachine camera, with a `CinemachineBrain` added to the Main Camera.
- **BikeUI:** a canvas with the touch zones, setting buttons, assist button and debug HUD, plus an `EventSystem` using `InputSystemUIInputModule`.
- **GameSettings:** the 60 fps / landscape settings object.

Each part can also be run on its own from **Tools > MotoBrawler > Steps**.

**Controls in the Editor**

| Action | Keys |
|---|---|
| Throttle | `W` / `↑` |
| Steer | `A` `D` / `←` `→` |
| Brake | `Space` / `S` / `↓` (hold while stopped to reverse) |
| Cycle assist preset | `P` |
| Respawn | `R` |

You can also click the on-screen zones with the mouse. A gamepad works too.

**Things to test:**

- Ride the start straight at full speed.
- Take the hairpin. You have to brake hard for it.
- Climb the hill and come back down.
- Turn left into the infield and launch off a ramp.
- Hit the red wall head-on above the crash speed: 75 km/h on Arcade, 30 km/h on Realistic. The bike falls over and respawns on the road after 2 s.
- Scrape along the 45° wall. Glancing hits don't crash.

---

## 4. Prefab hierarchy for YOUR model

The rule: **never animate the imported meshes directly.** Wrap each moving part in an empty "pivot" GameObject that sits exactly on the rotation axis and has clean axes. The meshes keep whatever rotation or scale the FBX/glTF gave them (Blender exports often have −90° on X), and the scripts only rotate the pivots.

```
Bike_YourModel                 (layer Bike) Rigidbody, BikeController, PlayerBikeInput, BikeCrashHandler
├── Colliders
│   ├── BodyCollider           CapsuleCollider: Direction Z, Radius 0.35, Height 2.0, Center (0, 0.75, 0)
│   └── RiderCollider          BoxCollider: Size (0.5, 0.7, 0.6), Center (0, 1.3, -0.2)
├── FrontWheelProbe            empty at the FRONT axle centre: (0, 0.32, 0.70)
├── RearWheelProbe             empty at the REAR axle centre:  (0, 0.32, -0.70)
├── CameraTarget               empty at (0, 1.1, 0)
└── Visual                     BikeVisuals
    └── LeanPivot              empty at (0, 0, 0), i.e. on the ground between the tyres
        ├── Body               your [Body] mesh (unchanged transform)
        ├── Rider              empty at the seat/hips (0, 0.9, -0.3)
        │   └── Capsule        placeholder rider (delete its collider)
        ├── SteerPivot         empty at the steering head, rotation (-rake, 0, 0)
        │   ├── FrontFork      your [FrontFork] mesh (handlebars + fork), unchanged
        │   └── FrontWheelSpin empty at the FRONT axle centre, world rotation = bike root rotation
        │       └── FrontWheel your [FrontWheel] mesh, unchanged
        └── RearWheelSpin      empty at the REAR axle centre, rotation (0, 0, 0)
            └── RearWheel      your [RearWheel] mesh, unchanged
```

Why each pivot exists:

| Pivot | Rotates around | Why it's needed |
|---|---|---|
| **Bike root** | (physics) | Origin on the ground, midway between the tyre contact points, facing +Z. Everything physical (Rigidbody, colliders, probes) sits here and never leans. |
| **Visual / LeanPivot** | local Z (lean), local X (small pitch) | It sits at ground level, so the bike leans around the tyre contact line, the way a real bike does. Colliders aren't under it, so physics never leans. |
| **SteerPivot** | local Y | Its local Y must be the steering axis. Real forks are raked: the top tilts back about 24 to 27°, so rotate the pivot (-25, 0, 0). If you rotate around the world vertical instead, the front wheel slides sideways. |
| **FrontWheelSpin / RearWheelSpin** | local X | The pivot must be exactly at the axle centre, or the wheel wobbles. Its X axis must point sideways along the axle. |
| **Rider** | small extra Z | Lets the rider hang off a bit more than the bike. |

### Step by step

1. Drag your model into an empty scene. Right-click it and choose **Prefab > Unpack Completely**, so you can re-parent the children.
2. Create an empty `Bike_YourModel` at (0, 0, 0) with rotation (0, 0, 0). The model must face **+Z**, with the tyres touching y = 0 and the midpoint between the axles at z = 0. Move the model's children so this is true. With a 1.4 m wheelbase, the axles are at z = ±0.70, y = 0.32.
3. **Find the axle centres.** Select `FrontWheel`, set the toolbar handle mode to **Center** (not Pivot) and read the gizmo position. That's the axle centre. Do the same for `RearWheel`.
4. Create the empties from the tree above. Create each one as a child of its parent, then set its position:
   - `FrontWheelProbe` and `FrontWheelSpin` both go at the front axle centre (the spin pivot under SteerPivot, the probe under the root).
   - `RearWheelProbe` and `RearWheelSpin` both go at the rear axle centre.
   - `SteerPivot` goes where the fork's steering axis passes through the top triple clamp / headstock. Then set its rotation to (-rake, 0, 0), for example (-25, 0, 0).

   **Check:** the front axle should lie on the pivot's local −Y line. Select SteerPivot with Local handle mode. The green axis should run along the fork tubes.
   - `FrontWheelSpin`: after parenting it under SteerPivot, set its **world** rotation back to the bike's rotation (0, 0, 0). Its red X axis must point along the axle.
5. Re-parent the meshes **with world position kept** (the default drag in the Hierarchy):
   - `FrontFork` under `SteerPivot`
   - `FrontWheel` under `FrontWheelSpin`
   - `RearWheel` under `RearWheelSpin`
   - `Body` under `LeanPivot`
   - the rider capsule under `Rider`

   > If your **[FrontFork] mesh also contains the front wheel geometry**, split the wheel into its own object in Blender (Separate > Selection). A wheel baked into the fork can't spin.
6. Add the colliders under `Colliders` (sizes in the tree above). Their bottom must stay **above** the tyres' contact patch: the capsule bottom at y ≈ 0.4. The wheels never use colliders, because the raycasts are the wheels. Assign a PhysicsMaterial with zero friction and Minimum combine (the menu creates `Generated/BikeNoFriction`), so the bike slides along walls instead of sticking.
7. Delete the colliders the importer put on the meshes, if any. Visual meshes must not collide.
8. Put the whole bike on the **Bike** layer (on the root, choose "Yes, change children").
9. Add the components and wire them up as in section 5. Then save it as a prefab (drag it into `Assets/MotoBrawler/Prefabs`).

Fastest route: run **Tools > MotoBrawler > Steps > 4. Create Placeholder Bike**. Then drop your meshes into its pivots, replacing the primitive children, and line the pivots up with your model's axles.

---

## 5. Components and values (manual setup reference)

### Layers (Project Settings > Tags and Layers)

| Layer | Used by |
|---|---|
| `Bike` | The bike and all its children. Must **not** be in the ground mask, or the wheel rays would hit the bike itself. |
| `Ground` | Flat ground and ramps. Ridable. |
| `Road` | The road. Ridable, and the only valid respawn surface. |
| `Wall` | Walls and barriers. Can crash the bike. |

### Bike root

**Rigidbody.** BikeController sets these on Awake: Mass from BikeStats, Interpolate, Continuous, no drag. Leave *Use Gravity* on.

**BikeController**

| Field | Value |
|---|---|
| Stats | `BikeStats_Default` |
| Assists | `Assists_Arcade` |
| Front / Rear Wheel Probe | the two probe empties |
| Ground Mask | Default, Ground, Road |
| Grounded Tolerance | 0.08 |
| Landing Probe Distance | 20 |

**PlayerBikeInput.** Mobile Controls = the BikeUI canvas. If left empty, it's found automatically.

**BikeCrashHandler**

| Field | Value |
|---|---|
| Crash Mask | Default, Road, Wall. Floors are ignored by the normal test. |
| Head On Threshold | 0.6 |
| Respawn Delay | 2 |
| Invulnerable Time | 1.5 |
| Spawn Point | SpawnPoint |
| Respawn Surface Mask | Road |
| Respawn Look Back | 2.5 s |

### Visual object

**BikeVisuals:** assign Bike, Lean Pivot, Steer Pivot, Front Wheel Spin, Rear Wheel Spin and Rider.

### Input

The actions are built in code inside `PlayerBikeInput`, so no `.inputactions` asset is needed:

| Action | Keyboard | Gamepad |
|---|---|---|
| Steer | A/D, ←/→ (1D axis composites) | left stick X |
| Throttle | W, ↑ | right trigger |
| Brake | Space, S, ↓ | left trigger |

If you want rebinding later, make an `.inputactions` asset with the same three actions and swap the constructors for `InputActionReference` fields.

### Camera

| Object | Component | Settings |
|---|---|---|
| Main Camera | CinemachineBrain | Defaults (Smart Update handles the interpolated Rigidbody). Near clip 0.1, far 800. |
| BikeCamera | CinemachineCamera | Tracking Target and Look At Target = `CameraTarget`. Lens FOV 60. |
| BikeCamera | Position Control: **Follow** | Binding Mode **Lock To Target With World Up**, so the camera follows yaw only and doesn't rock with slopes. Follow Offset (0, 2, -5.5). Position Damping (0.15, 0.35, 0.45). Rotation Damping (0.5, 0.6, 0.5). |
| BikeCamera | Rotation Control: **Rotation Composer** | Target Offset (0, 0.2, 2) to look a little ahead. Damping (0.4, 0.4). |
| BikeCamera | BikeCameraFX | Base FOV 60, Max FOV Boost 10, FOV Start Speed 0.3, Roll Follow 0.2, Max Roll 10. |

### Mobile UI

**BikeUI canvas.** Screen Space Overlay. CanvasScaler set to *Scale With Screen Size* 1920 × 1080, Match 0.5. The canvas holds the MobileControls and AssistPresetSwitcher components.

**Controls** (a full-screen RectTransform, used as MobileControls *Controls Root*):

| Element | Anchor | Position | Size | Notes |
|---|---|---|---|---|
| `SteerButtons` | full screen | | | Group, hidden in tilt mode |
| └ `SteerLeft` | bottom-left | (40, 40) | 260 × 260 | Image + TouchZone |
| └ `SteerRight` | bottom-left | (320, 40) | 260 × 260 | Image + TouchZone |
| `Brake` | bottom-left (left thumb) | (40, 320) | 540 × 150 | Image + TouchZone |
| `Throttle` | bottom-right (right thumb) | (-40, 40) | 340 × 430 | Image + TouchZone |

Set Image **Raycast Target off** on the zones. MobileControls polls every touch against the zones itself, which gives real multi-touch and lets a thumb slide between left and right without lifting.

**Buttons** (top-right, normal UI Buttons):

| Button | Assigned to | What it does |
|---|---|---|
| `SteeringModeButton` | MobileControls | Toggles BUTTONS ↔ TILT |
| `AutoThrottleButton` | MobileControls | Auto-gas on/off |
| `CalibrateTiltButton` | MobileControls | Sets "straight" to how you hold the phone now. Only shown in tilt mode. |
| `AssistPresetButton` | AssistPresetSwitcher | Cycles Arcade ↔ Realistic |

**DebugHUD:** a TextMeshProUGUI (top-left, 26 pt) with BikeDebugHUD.

The touch controls also show in the Editor so you can test with the mouse. Untick **Show On Desktop** on MobileControls to hide them on PC builds.

**Tilt steering.** Hold the phone in landscape and turn it like a steering wheel. About 25° of tilt is full lock (`ControlSettings.TiltFullLockAngle`), with a 2° dead zone. Tap **Calibrate tilt** while holding the phone in your neutral position. The accelerometer only runs while tilt mode is on, which saves battery.

### Making the two presets by hand

To make them yourself: **Create > MotoBrawler > Riding Assists**, then right-click the asset header and choose **Load Arcade Preset** or **Load Realistic Preset**.

| Preset | Steering | Stability | Brake (ABS) | Crash speed |
|---|---|---|---|---|
| Arcade | 0.8 | 0.9 | 1.0 | 75 km/h |
| Realistic | 0.15 | 0.2 | 0.0 | 30 km/h |

What each assist does in code:

- **Steering assist:**
  - Smooths the input. It takes 0.07 s to reach full lock at 0 and 0.3 s at 1.
  - Cuts up to 40% of the steering at top speed.
  - Caps the turn rate at what the tyres can hold, so you never slide from steering too much.
- **Stability assist:**
  - Adds up to +50% grip, so the bike holds its line.
  - Levels the bike faster on bumps and in the air.
  - Soaks up 30 to 85% of the landing impact.
- **Brake assist (ABS):** braking and cornering share one grip budget (a friction circle). With ABS, the brake force backs off to what's left. Without it, the wheels lock: braking drops 20%, cornering grip drops to `skidGripFactor` (35%), and the rear wheel stops spinning.
- **Crash speed:** the head-on impact speed needed to crash.

---

## 6. Tuning guide

Change one value at a time. Watch the debug HUD while you do: steering raw → assisted, lean, and SKID. You can edit BikeStats during Play mode. ScriptableObject changes made in Play mode are kept after you stop, so note your good values.

### Too slippery (slides wide, drifts, skids too easily)

| Change | Where |
|---|---|
| ↑ `grip` (1.15 → 1.4) | BikeStats |
| ↑ `lateralStiffness` (12 → 18) | BikeStats. Slip dies faster. |
| ↓ `maxCorneringG` below `grip` | BikeStats. Steering then never asks for more than the tyres give. |
| ↑ `stabilityAssist` and/or ↑ `steeringAssist` | RidingAssists |
| ↑ `brakeAssist` if it slides when you brake in corners | RidingAssists |
| ↑ `skidGripFactor` (0.35 → 0.5) so skids are recoverable | BikeStats |

### Too stiff (on rails, no weight, doesn't feel like a bike)

| Change | Where |
|---|---|
| ↓ `lateralStiffness` (12 → 7). A little slip shows. | BikeStats |
| ↓ `steerResponse` (9 → 5). Yaw builds up more gradually. | BikeStats |
| ↓ `springFrequency` (2.2 → 1.6) and ↓ `dampingRatio` (0.55 → 0.4). Softer, more bouncy. | BikeStats |
| ↑ `leanSpeed` slightly and ↑ `maxLeanAngle`. More visible body motion. | BikeStats |
| ↓ `stabilityAssist`. Less auto-levelling. | RidingAssists |
| Camera: ↑ Position/Rotation Damping, ↑ `rollFollow` (0.2 → 0.35) | Follow component / BikeCameraFX |

### Too slow

| Change | Where |
|---|---|
| ↑ `topSpeedKmh` | BikeStats |
| ↑ `acceleration` (8.5 → 11) | BikeStats |
| Raise the middle of `accelerationCurve` (e.g. key 0.85 → 0.55). Keep the last key at (1, 0). | BikeStats |
| ↓ `rollingResistance` and `airDrag`. Coasting loses less speed. | BikeStats |
| Feels slow but isn't: ↑ `maxFovBoost` (10 → 15), ↓ `fovStartSpeed` | BikeCameraFX |
| Feels slow but isn't: bring the Follow Offset closer, e.g. (0, 1.7, -4.5) | Follow component |

### Too twitchy (over-reacts, wobbles at speed, hard on keyboard)

| Change | Where |
|---|---|
| ↑ `steeringAssist` (more smoothing and a high-speed cut) | RidingAssists |
| ↓ `steerResponse` (9 → 6) | BikeStats |
| ↓ `maxCorneringG` (1.35 → 1.0). Gentler at high speed. | BikeStats |
| ↑ `minTurnRadius` (3.5 → 5). Less snappy at low speed. | BikeStats |
| Lean flickers: ↓ `leanSpeed` (7 → 5), ↓ `leanFromInput` | BikeStats |
| Tilt: ↑ full-lock angle (`ControlSettings.TiltFullLockAngle`, 25 → 35), ↑ the dead zone or ↓ `tiltSmoothing` on MobileControls | ControlSettings / MobileControls |
| Bouncy over bumps: ↑ `dampingRatio` (0.55 → 0.8) | BikeStats |

### Other common fixes

| Symptom | Fix |
|---|---|
| Bike sinks into or floats above the road | The wheel probes must be at the axle centres, and `wheelRadius` must match the model. |
| Lands nose-first | ↑ `airAlignSpeed`, or ↑ `stabilityAssist`. |
| Jumps feel floaty | ↑ `airGravityMultiplier` (1.6 → 2.2). |
| Crashes too often | ↑ `crashSpeedKmh`, or ↑ BikeCrashHandler `headOnThreshold` (0.6 → 0.8). |
| Crashes too rarely | Do the opposite. |
| Respawns in a bad place | Make sure only road meshes are on the `Road` layer. Tune `respawnLookBack`. |
| Wheels spin backwards or wobble | The spin pivot isn't centred on the axle, or its X axis isn't along the axle (step 4 in section 4). |
| Fork turns sideways instead of around the head | SteerPivot rotation isn't (-rake, 0, 0), or a mesh, not the pivot, is being rotated. |
| Jitter | Rigidbody Interpolate must be on (it's set in code). Follow `CameraTarget`, not a visual child. |

**60 fps on mid-range phones.** All the physics is two raycasts plus one more in the air, on a single Rigidbody per bike. The cost is in rendering: keep URP Mobile settings, static batching (the track builder marks the track static), and few real-time shadows.
