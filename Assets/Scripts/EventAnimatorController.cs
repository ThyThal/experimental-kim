using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a group of animated decorations for a single event type. <see cref="Activate"/>
/// enables the assigned animators and UI images, gives each animator a random
/// playback speed (so they look slightly out of sync), and plays the event audio
/// clip; <see cref="Deactivate"/> disables them and stops the audio to save
/// resources. Place one per event type and assign them to the ChallengeController.
/// </summary>
public class EventAnimatorController : MonoBehaviour
{
    [Tooltip("Which event type this controller animates.")]
    [SerializeField] private ZoneInputType _eventType;
    [SerializeField] private Animator[] _animators;
    [SerializeField] private Image[] _images;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [Tooltip("Clip played (looped) while the event runs.")]
    [SerializeField] private AudioClip _clip;

    [Header("Speed")]
    [Tooltip("Random playback speed range applied to each animator on Activate.")]
    [SerializeField] private float _minSpeed = 0.8f;
    [SerializeField] private float _maxSpeed = 1.2f;

    [Header("Random rotation / flip")]
    [Tooltip("Objects given a random Z tilt and optional horizontal flip on Activate.")]
    [SerializeField] private Transform[] _rotationTargets;
    [Tooltip("Random Z tilt range in degrees, e.g. -30 / +30.")]
    [SerializeField] private float _minZRotation = -30f;
    [SerializeField] private float _maxZRotation = 30f;
    [Tooltip("Randomly flip each target horizontally (Y rotation 180).")]
    [SerializeField] private bool _randomFlip = true;

    public ZoneInputType EventType => _eventType;

    // Start disabled so nothing animates/renders/sounds until its event fires.
    private void Awake() => Deactivate();

    /// <summary>Enable the images and animators (random speeds) and start the audio.</summary>
    public void Activate()
    {
        if (_images != null)
            foreach (var img in _images)
                if (img != null) img.enabled = true;

        if (_animators != null)
            foreach (var anim in _animators)
            {
                if (anim == null) continue;
                anim.enabled = true;
                anim.speed = Random.Range(_minSpeed, _maxSpeed);
            }

        if (_rotationTargets != null)
            foreach (var t in _rotationTargets)
            {
                if (t == null) continue;
                float y = (_randomFlip && Random.value < 0.5f) ? 180f : 0f;
                float z = Random.Range(_minZRotation, _maxZRotation);
                t.localRotation = Quaternion.Euler(0f, y, z);
            }

        if (_audioSource != null && _clip != null)
        {
            _audioSource.clip = _clip;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }

    /// <summary>Disable the animators and images and stop the audio.</summary>
    public void Deactivate()
    {
        if (_animators != null)
            foreach (var anim in _animators)
                if (anim != null) anim.enabled = false;

        if (_images != null)
            foreach (var img in _images)
                if (img != null) img.enabled = false;

        if (_audioSource != null) _audioSource.Stop();
    }

    /// <summary>
    /// Editor helper: populate the animator and image lists from this object's
    /// children (including inactive ones), and grab a child AudioSource if none
    /// is assigned. Wired to a button via EventAnimatorControllerEditor.
    /// </summary>
    public void FetchFromChildren()
    {
        _animators = GetComponentsInChildren<Animator>(true);
        _images = GetComponentsInChildren<Image>(true);

        // Default the rotation targets to the image objects (we flip the images).
        _rotationTargets = new Transform[_images.Length];
        for (int i = 0; i < _images.Length; i++)
            _rotationTargets[i] = _images[i].transform;

        if (_audioSource == null)
            _audioSource = GetComponentInChildren<AudioSource>(true);
    }
}
