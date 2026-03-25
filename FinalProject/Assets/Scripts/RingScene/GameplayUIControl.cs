using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayUIController : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MenuScene";

    public void OnBackToMenuPressed()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}