using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Buttons for the Good/Bad ending scenes. Wire <see cref="Retry"/> and
/// <see cref="Exit"/> to UI Button OnClick events.
/// </summary>
public class EndScreenController : MonoBehaviour
{
    [Tooltip("Gameplay scene to reload on Retry (must be in Build Settings).")]
    [SerializeField] private string _gameplayScene = "SampleScene";

    public void Retry() => SceneManager.LoadScene(_gameplayScene);

    public void Exit()
    {
        Debug.Log("[EndScreenController] Exit requested.");
        Application.Quit();
    }
}
