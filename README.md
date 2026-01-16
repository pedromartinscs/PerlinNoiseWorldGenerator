# Unity Procedural World Generator (Perlin Noise + Biomes + Chunk Rendering)

A Unity (C#) sample project that generates a deterministic, seed-based world using noise maps and renders it using a simple chunked approach.

The focus here is on the systems side of game dev (common in factory / simulation / strategy games): reproducible generation, separation between generation and rendering, and practical performance considerations.

## Features

- Seed-based deterministic generation.
- Noise-driven land/water classification.
- Biome selection (Forest / Desert) with a simple "hot & dry" biome rule and a configurable shoreline buffer.
- Precomputed shoreline pieces for water tiles (so water tiles can render 0..N shoreline meshes).
- Deterministic decoration placement (coordinate-hashed RNG) so the same seed produces the same layout.
- Chunk-based rendering around the camera target (configurable chunk size + render radius).
- Simple debug grid overlay toggle.
- URP RenderGraph outline feature included (optional; see below).

## Requirements

- **Unity Editor:** `6000.1.5f1` (Unity 6)
- Render pipeline: URP (project includes `com.unity.render-pipelines.universal`)

## Quick start

1. Open the project in Unity.
2. Open the demo scene:
   - `Assets/Scenes/SampleScene.unity`
3. Press **Play**.
4. Use the UI buttons:
   - **Generate Noise** (preview)
   - **Generate Noise + Map** (generates the map and spawns chunks)

### Camera controls

- **Left mouse drag:** pan
- **Right mouse drag:** orbit
- **Mouse wheel:** zoom

(Camera input is ignored while the pointer is over UI elements.)

## How it works

### 1) Generation (data only)

- `PerlinNoiseGenerator.BuildMap(...)` is the main entry point.
- It creates a `MapData` grid containing:
  - `Cell` (Land/Water + Biome)
  - a per-tile `featureValue` used for deterministic decoration rules
  - precomputed shoreline pieces for water tiles

The goal is to keep generation deterministic and independent from rendering.

### 2) Rendering (chunked)

- `ChunkRenderer` is responsible for visualizing the generated `MapData`.
- It builds/destroys chunk roots around the camera target based on a **render radius**, so large maps remain responsive.
- Water tiles can render shoreline prefabs based on the precomputed shore list.

### Optional: URP RenderGraph outline feature

The project also includes an outline render feature implemented using URP + RenderGraph:

- `Assets/Scripts/Rendering/ObjectIdOutlineRenderGraphFeature.cs`
- `Assets/Scripts/Rendering/OutlineObjectId.cs`

If you want to use it, set up the materials referenced by the renderer feature in your URP renderer and assign the outline layer.

## Project structure

- `Assets/Scripts/`
  - `PerlinNoiseGenerator.cs` – noise generation + logical map + biome map + shoreline precompute
  - `Generation/MapData.cs` – generation output container (cells, shores, feature values)
  - `Rendering/ChunkRenderer.cs` – chunk streaming + spawning ground/water/shore + deterministic decorations
  - `UIManager.cs` – UI wiring (seed/scale/octaves/persistence + map size + biome toggles)
  - `CameraController.cs` – pan/orbit/zoom

## Notes on assets & licensing

This repository contains some example prefabs/models/textures to make the demo scene readable.

If you plan to make the repo public, make sure any third‑party art assets are allowed to be redistributed publicly (many Asset Store licenses are *not*). A safe option is to keep the repo **code-only** (Scripts + minimal scene) and use placeholder primitives, or keep the repo private and share access with reviewers.

## What I would improve next

- Split `PerlinNoiseGenerator` into smaller components (noise sampling, biome rules, shoreline evaluation).
- Add automated tests for deterministic generation (same seed -> same map hash).
- Add a small screenshot/GIF in this README.

---

If you have questions or want a cleaner “code-only” branch for public sharing, I can prepare that quickly.
