# IdleTime Prototype Setup

This folder contains the first 2D prototype scaffolding for the IdleOn-style game.

## Generate the greybox scene

1. Open the project in Unity.
2. Wait for scripts to compile.
3. Run `IdleTime > Build Greybox Prototype Scene`.
4. Open `Assets/Scenes/GreyboxPrototype.unity` if Unity does not switch to it automatically.

## Current controls

- `A` / `D` or arrow keys: move
- `Space`, `W`, or up arrow: jump
- `E`: interact with nearby resource nodes or the workbench

## Current prototype loop

- Gather `Wood` from the forest node.
- Gather `Ore` from the mine node.
- Use the town workbench to convert `Wood` into `Plank`.

The scene is intentionally greybox-only so the layout, camera, controls, and idle loop can change quickly.
