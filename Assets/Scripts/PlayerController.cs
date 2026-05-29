using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private SoundClassifier _soundClassifier;
    [SerializeField] private float _moveSpeed = 3f;
    [Tooltip("Voice type required to move through the gray travel areas.")]
    [SerializeField] private ZoneInputType _travelInputType = ZoneInputType.Talk;

    // Gated by ChallengeController: false while a specific-zone challenge runs.
    public bool CanMove { get; set; } = true;

    private void Update()
    {
        if (!CanMove) return;
        if (!_soundClassifier.IsActive) return;
        if (_soundClassifier.CurrentType != _travelInputType) return;

        transform.position += Vector3.right * (_moveSpeed * Time.deltaTime);
    }
}
