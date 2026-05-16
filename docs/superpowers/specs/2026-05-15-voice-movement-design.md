# Voice Movement System — Design Spec
**Date:** 2026-05-15  
**Project:** MR KIM (Unity 2D URP, side-scroller adventure)

---

## Overview

A sound-driven movement system where the player moves right while making noise (clapping, talking, or any sustained sound) and idles when silent. Built with a three-script pipeline that is intentionally extensible for future pattern detection mechanics.

---

## Architecture

```
MicrophoneManager     — raw amplitude float, updated every frame
       ↓
SoundDetector         — OnSoundStart / OnSoundStop events (threshold crossing)
       ↓
PlayerController      — moves right while active, idles when stopped

(future)
SoundDetector → PatternDetector → specific game actions
```

Each script has one responsibility. The `OnSoundStart` / `OnSoundStop` events on `SoundDetector` are the extension point for future pattern detection — a `PatternDetector` subscribes to those events and measures timing between bursts to recognize rhythms or voice patterns without touching any existing script.

---

## Components

### MicrophoneManager
- **Purpose:** Wraps Unity's `Microphone` API and provides a clean `Amplitude` float.
- **Lifecycle:** `Microphone.Start()` on `Awake`, loops a short `AudioClip` buffer.
- **Per-frame:** Reads the last `sampleWindow` samples from the clip and computes RMS (Root Mean Square) — standard measure of perceived loudness.
- **Public API:**
  - `float Amplitude` — current RMS amplitude (0.0–1.0)
  - `bool IsReady` — true once the microphone has initialized
- **Inspector fields:**
  - `sampleWindow` (int, default 128) — number of samples to average per frame

### SoundDetector
- **Purpose:** Converts raw amplitude into semantic sound events.
- **Depends on:** `MicrophoneManager` (inspector reference)
- **Logic:** Watches `MicrophoneManager.Amplitude` each frame. Fires `OnSoundStart` when amplitude rises above `threshold`. Fires `OnSoundStop` only after amplitude stays below `threshold` for `silenceDelay` seconds — prevents flicker from brief gaps between claps.
- **Public API:**
  - `event Action OnSoundStart`
  - `event Action OnSoundStop`
  - `bool IsActive` — current sound state
- **Inspector fields:**
  - `threshold` (float, default 0.02) — minimum amplitude to count as sound
  - `silenceDelay` (float, default 0.1s) — grace period before declaring silence

### PlayerController
- **Purpose:** Moves the player right while sound is active; idles when silent.
- **Depends on:** `SoundDetector` (inspector reference)
- **Lifecycle:** Subscribes to `OnSoundStart` / `OnSoundStop` in `OnEnable`, unsubscribes in `OnDisable`.
- **Movement:** In `Update`, if `SoundDetector.IsActive`, translates `transform.position` right by `moveSpeed * Time.deltaTime`. No physics/Rigidbody needed for this phase.
- **Inspector fields:**
  - `moveSpeed` (float, units/sec) — how fast the player moves while sound is detected

---

## Data Flow

1. `MicrophoneManager` samples mic → writes `Amplitude` float each frame
2. `SoundDetector` reads `Amplitude` → fires `OnSoundStart` / `OnSoundStop` events + maintains `IsActive`
3. `PlayerController` listens to events to toggle active state, uses `IsActive` in `Update` to drive movement

---

## Future: Pattern Detection

A `PatternDetector` script subscribes to `SoundDetector.OnSoundStart` and `OnSoundStop`. It records the timestamps of each event and compares intervals against known patterns (e.g., two short claps = jump, long sound = special action). When a pattern matches, it fires its own game-specific events. No existing scripts change.

---

## Scene Setup

- Three GameObjects (or one, inspector-wired): `MicrophoneManager`, `SoundDetector`, `PlayerController`
- Player GameObject needs a `SpriteRenderer` (and optionally an `Animator`)
- No Rigidbody2D required for this phase (transform-based movement)
- Microphone permission must be declared in Player Settings (Unity handles the OS prompt at runtime)

---

## Out of Scope (this phase)

- Pattern detection implementation
- Jump, fall, or vertical movement
- Animation controller wiring (placeholder only)
- Event zones / trigger areas
- Multiple microphone device selection
