using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private SoundDetector _soundDetector;
    [SerializeField] private float _moveSpeed = 3f;

    private void Update()
    {
        if (!_soundDetector.IsActive) return;

        transform.position += Vector3.right * (_moveSpeed * Time.deltaTime);
    }
}
