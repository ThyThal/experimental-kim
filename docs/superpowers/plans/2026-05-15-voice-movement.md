# Voice Movement System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three Unity scripts that capture microphone input, detect sustained sound, and smoothly move the player right while sound is active.

**Architecture:** `MicrophoneManager` captures raw RMS amplitude from the mic each frame. `SoundDetector` watches that amplitude and fires `OnSoundStart` / `OnSoundStop` events when threshold is crossed (with a silence grace period to prevent flicker). `PlayerController` reads `SoundDetector.IsActive` each frame and translates the player right at `moveSpeed`.

**Tech Stack:** Unity 2D URP, C#, Unity Microphone API (`UnityEngine.Microphone`)

---

## File Map

| File | Status | Responsibility |
|------|--------|----------------|
| `Assets/Scripts/MicrophoneManager.cs` | CREATE | Wraps Unity Microphone API, exposes `Amplitude` float and `IsReady` bool |
| `Assets/Scripts/SoundDetector.cs` | CREATE | Threshold detection, fires `OnSoundStart` / `OnSoundStop`, exposes `IsActive` |
| `Assets/Scripts/PlayerController.cs` | CREATE | Moves player right while `IsActive`, idles when silent |

---

## Task 1: MicrophoneManager

**Files:**
- Create: `Assets/Scripts/MicrophoneManager.cs`

- [ ] **Step 1: Create the script**

Create `Assets/Scripts/MicrophoneManager.cs` with this exact content:

```csharp
using UnityEngine;

public class MicrophoneManager : MonoBehaviour
{
    [SerializeField] private int sampleWindow = 128;

    private AudioClip _micClip;

    public float Amplitude { get; private set; }
    public bool IsReady { get; private set; }

    private void Awake()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[MicrophoneManager] No microphone detected.");
            return;
        }

        _micClip = Microphone.Start(null, true, 1, AudioSettings.outputSampleRate);
        IsReady = true;
    }

    private void OnDestroy()
    {
        if (Microphone.IsRecording(null))
            Microphone.End(null);
    }

    private void Update()
    {
        if (!IsReady) return;

        int micPosition = Microphone.GetPosition(null);
        if (micPosition < sampleWindow) return;

        float[] samples = new float[sampleWindow];
        _micClip.GetData(samples, micPosition - sampleWindow);

        float sum = 0f;
        for (int i = 0; i < sampleWindow; i++)
            sum += samples[i] * samples[i];

        Amplitude = Mathf.Sqrt(sum / sampleWindow);
    }
}
```

- [ ] **Step 2: Verify in Play Mode**

  1. Create an empty GameObject in the SampleScene, name it `AudioSystem`.
  2. Add the `MicrophoneManager` component to it.
  3. Enter Play Mode.
  4. Select the `AudioSystem` GameObject and watch the Inspector.
  5. Make noise (clap, talk) — `Amplitude` field should rise above `0`. Silence — it should drop toward `0`.
  6. Check the Console: no warnings about "No microphone detected." (if you see that warning, check OS microphone permissions).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/MicrophoneManager.cs
git commit -m "feat: add MicrophoneManager with RMS amplitude sampling"
```

---

## Task 2: SoundDetector

**Files:**
- Create: `Assets/Scripts/SoundDetector.cs`
- Depends on: `MicrophoneManager` (Task 1)

- [ ] **Step 1: Create the script**

Create `Assets/Scripts/SoundDetector.cs` with this exact content:

```csharp
using System;
using UnityEngine;

public class SoundDetector : MonoBehaviour
{
    [SerializeField] private MicrophoneManager microphoneManager;
    [SerializeField] private float threshold = 0.02f;
    [SerializeField] private float silenceDelay = 0.1f;

    public event Action OnSoundStart;
    public event Action OnSoundStop;
    public bool IsActive { get; private set; }

    private float _silenceTimer;

    private void Update()
    {
        if (!microphoneManager.IsReady) return;

        if (microphoneManager.Amplitude > threshold)
        {
            _silenceTimer = 0f;

            if (!IsActive)
            {
                IsActive = true;
                OnSoundStart?.Invoke();
            }
        }
        else if (IsActive)
        {
            _silenceTimer += Time.deltaTime;

            if (_silenceTimer >= silenceDelay)
            {
                IsActive = false;
                OnSoundStop?.Invoke();
            }
        }
    }
}
```

- [ ] **Step 2: Wire up in the Inspector**

  1. Add `SoundDetector` component to the `AudioSystem` GameObject (same one as `MicrophoneManager`).
  2. Drag the `AudioSystem` GameObject into the `Microphone Manager` field on `SoundDetector`.

- [ ] **Step 3: Verify in Play Mode**

  1. Enter Play Mode.
  2. Select `AudioSystem` in the Hierarchy.
  3. Watch the `Is Active` field in the Inspector (it shows as a checkbox on the component).
  4. Make noise — `IsActive` should flip to `true` within ~0.1s.
  5. Go silent — `IsActive` should flip to `false` after ~0.1s (the `silenceDelay`).
  6. Clap several times rapidly — `IsActive` should stay `true` the whole time (no flicker between claps).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/SoundDetector.cs
git commit -m "feat: add SoundDetector with threshold and silence grace period"
```

---

## Task 3: PlayerController

**Files:**
- Create: `Assets/Scripts/PlayerController.cs`
- Depends on: `SoundDetector` (Task 2)

- [ ] **Step 1: Create the script**

Create `Assets/Scripts/PlayerController.cs` with this exact content:

```csharp
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private SoundDetector soundDetector;
    [SerializeField] private float moveSpeed = 3f;

    private void Update()
    {
        if (!soundDetector.IsActive) return;

        transform.position += Vector3.right * moveSpeed * Time.deltaTime;
    }
}
```

- [ ] **Step 2: Create the Player GameObject**

  1. In the SampleScene Hierarchy, create a new 2D Object → Sprite → Square. Name it `Player`.
  2. In the Inspector, set its `Transform Position` to `(0, 0, 0)`.
  3. Set the `Sprite Renderer` color to something visible (e.g. bright blue).
  4. Add the `PlayerController` component to `Player`.
  5. Drag the `AudioSystem` GameObject into the `Sound Detector` field on `PlayerController`.

- [ ] **Step 3: Verify in Play Mode — golden path**

  1. Enter Play Mode.
  2. Make sustained noise (talk, clap repeatedly, hum) — the `Player` sprite should move smoothly to the right.
  3. Go silent — the `Player` should stop immediately (within `silenceDelay` seconds).
  4. Make noise again — movement resumes from where it stopped.

- [ ] **Step 4: Verify in Play Mode — edge cases**

  - **Brief silence between claps:** Clap at a comfortable pace. Player should move continuously without stuttering or stopping between claps (silenceDelay handles this).
  - **Very quiet environment:** If the player moves without you making noise, increase the `Threshold` value on `SoundDetector` in the Inspector (try `0.05`).
  - **No microphone:** If you see `[MicrophoneManager] No microphone detected.` in Console, the OS has blocked mic access — check Windows Privacy Settings → Microphone.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/PlayerController.cs
git commit -m "feat: add PlayerController, player moves right while sound is active"
```

---

## Task 4: Scene Save and Final Verification

- [ ] **Step 1: Save the scene**

  In Unity: File → Save (Ctrl+S). This saves the GameObject wiring to `Assets/Scenes/SampleScene.unity`.

- [ ] **Step 2: Full end-to-end test**

  1. Exit Play Mode, then re-enter it fresh.
  2. Make noise → player moves right.
  3. Stop → player idles.
  4. Confirm no errors in the Console.

- [ ] **Step 3: Commit scene**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "chore: save scene with MicrophoneManager, SoundDetector, PlayerController wired up"
```

---

## Tuning Reference

| Inspector Field | Component | Default | What to change |
|---|---|---|---|
| `sampleWindow` | MicrophoneManager | 128 | Lower = more reactive, higher = smoother |
| `threshold` | SoundDetector | 0.02 | Raise if player moves from ambient noise |
| `silenceDelay` | SoundDetector | 0.1s | Raise if player stops between claps |
| `moveSpeed` | PlayerController | 3 | Units per second |

---

## Future Extension Point

To add pattern detection later, create a new `PatternDetector` MonoBehaviour that subscribes to `SoundDetector.OnSoundStart` and `OnSoundStop`. Record timestamps of each event and compare intervals against known patterns. No existing scripts need to change.
