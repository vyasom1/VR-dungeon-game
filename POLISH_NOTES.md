# Boss fight polish

The presentation layer starts automatically in Main Menu, Level 2, Level 4 and End Screen. No new editor menu commands or boss setup steps are required.

## Changed runtime files

- Assets/Scripts/Managers/BossFightPolish.cs: effects, monitor/wrist HUD, generated placeholder audio, menus/settings, intro, statistics and capture helper.
- Assets/Scripts/Player/HandCombatPolish.cs: observes existing fist contacts for presentation. It never applies damage or changes collider settings.
- Assets/Scripts/Boss/BossHealthBarUI.cs: smooth health drain and health-dependent colour.
- Assets/Resources/BossPolishEffects.shader: lightweight URP transparent effect shader, included through Resources for builds.

BossController, BossHealth, BossEnemy, PlayerHealth, VRHandCombat, scene assets, scene-loading components, XR rig, camera and BossSetupMenu were not intentionally edited.

## Controls and behaviour

- F3 toggles the monitor performance panel in either boss arena. FPS uses unscaled frame time; rendering statistics use profiler counters. Unavailable rendering counters display “sampling”.
- In the Editor, the visible panel also records samples in Screenshots/boss-arena-performance.csv.
- Opening supported scenes in Play mode requests an automatic screenshot after four seconds. Normal Game View rendering is needed for ScreenCapture.
- Settings stores haptic intensity and hand preference. Hand preference selects the wrist HUD side; it does not remap locomotion or change the XR rig.
- Audio is procedurally generated placeholder audio, suitable for replacing with recorded assets later.
- “Critical” is a feedback label for a punch during an attack, without changing damage values.
- Combo counts successful damage events; an observed missed hand swing resets it.
- The intro scales and fades the title, then enables the existing boss controller. Camera zoom and physical camera shake were intentionally replaced by UI animation/shake to respect the protected VR camera.
- Existing automatic scene transitions are retained. The End Screen adds victory statistics and navigation controls; it does not interrupt Level 2's existing transition to Level 3.

## Verification limits

Full Unity compilation and automated Editor scene smoke checks were performed. Damage smoke checks call the existing BossHealth.TakeDamage(5); they do not prove physical controller collision/haptic behaviour.

Smoke-*.png files are diagnostic RenderTexture captures from a batch Editor without live XR tracking. Fallback hand poses may obscure the scene. They are not final headset/project-report screenshots.

The batch FPS samples are uncapped Editor timing, not a Quest or normal XR Device Simulator benchmark. Zero rendering counters mean unavailable/no continuously rendered game frames in that run; they are not a zero-cost arena measurement.

Interactive XR Device Simulator play, headset haptics, final report captures and a Meta Quest build/device run still require verification. No claim of a successful Quest build is made.
