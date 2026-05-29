using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private SoundClassifier _soundClassifier;
    [SerializeField] private BackgroundSpawner _backgroundSpawner;
    [SerializeField] private float _moveSpeed = 3f;

    private void Update()
    {
        if (!_soundClassifier.IsActive) return;

        var required = _backgroundSpawner != null
            ? _backgroundSpawner.GetInputTypeAt(transform.position.x)
            : ZoneInputType.Talk;

        if (_soundClassifier.CurrentType != required) return;

        transform.position += Vector3.right * (_moveSpeed * Time.deltaTime);
    }
}
