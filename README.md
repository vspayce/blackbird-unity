# The Black Bird — Unity

A test of moving [The Black Bird](https://github.com/vspayce/blackbird) from the browser prototype
(three.js) to Unity, toward a polished native iOS release. This repo holds **Chapter I, Burritt
Alley only**: walking the alley with Watson, the fog and gaslight, Focus, and examining the scene
and the body. Notebook, interrogation and the Mind Palace stay in the web prototype for now.

It also builds to **WebGL**, published to GitHub Pages, so it can be tried on a phone.

## Opening it

1. `git clone https://github.com/vspayce/blackbird-unity ~/blackbird-unity`
2. Unity Hub ▸ **Add ▸ Add project from disk** ▸ `~/blackbird-unity`, with a **Unity 6** editor that has
   **WebGL Build Support** (and iOS Build Support for later).
3. On first open, `ProjectSetup.cs` installs URP, the Input System and MCP for Unity. Accept the
   Input System's prompt to enable the new input backend (the editor restarts).

## Getting the art in

The set and cast are exported from the web repo's `.blend` files (not its GLBs, which use WebP and
Draco that Unity can't import):

```bash
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
  --python Tools/blender/export_unity.py -- ~/BlackBird ~/blackbird-unity
```

That writes `Assets/BlackBird/Art/Set/burritt.fbx` and `Assets/BlackBird/Art/Characters/*.fbx`.
Characters import as **Humanoid**, so any humanoid animation retargets onto them.

**Better walks:** add Unity's free *Starter Assets – Third Person* from the Asset Store (or drop any
humanoid mocap FBX into `Assets/BlackBird/Art/Animations/`). The builder prefers those clips over the
hand-made ones exported with the cast. Clips named `Idle`, `Walk`/`Walk_N`, `Run`/`Run_N` are picked up.

## Menu: Black Bird

| Item | What it does |
|---|---|
| **Build Burritt Alley** | Makes `Scenes/BurrittAlley.unity` from scratch: URP asset, grade, fog, lights, cast, controllers, HUD |
| Configure Player Settings | Landscape-only, linear colour, WebGL gzip with fallback (for GitHub Pages) |
| Build WebGL | Builds to `WebGL/` (ignored by git) |
| Publish WebGL to GitHub Pages | Pushes the build to the `gh-pages` branch. First time: repo Settings ▸ Pages ▸ Deploy from branch ▸ `gh-pages` |
| Setup ▸ Enforce T-Pose on Cast | Straightens the cast's reference pose so mocap retargets cleanly (run by the builder) |

## How it's put together

- `Assets/BlackBird/Runtime/Chapter1.cs` — the chapter's data, ported from `src/cases/archer.js` and
  `src/world/alley.js`, still in the web game's coordinates (mapped by `Web.W`).
- `Walker.cs` — drives each character's Animator from real ground speed (stride matched to speed, no
  skating), smooth turning, foot IK and head look-at.
- `Atmosphere.cs` — drifting lit fog banks, light cones under the lamps, gaslight flicker. Distance fog
  and the night grade are set by the builder.
- `Focus.cs` — slow time, the Focus grade, and details only Focus reveals.
- `GameInput.cs` — keyboard/mouse, gamepads, and touch (stick bottom-left, drag to look).
- `Hud.cs` — the interface, built in code.
- `Editor/Build/SceneBuilder.cs` — assembles everything; rerun it after changing the art.

Rights notes from the web repo's `DESIGN.md` apply here too (the novel only, not the 1941 film; check
non-US storefronts before release). Credits: see the web repo's `CREDITS.md`; the font is IM FELL
English SC (OFL, `Assets/BlackBird/Fonts/OFL.txt`).
