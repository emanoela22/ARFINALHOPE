using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIBinder : MonoBehaviour
{
    [SerializeField] private ScanningTherapyManager manager;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timerText;

    private void Start()
    {
        startButton.onClick.AddListener(manager.StartSession);

        manager.OnScoreChanged += (s) => scoreText.text = $"Score: {s}";
        manager.OnTimeLeftChanged += (t) => timerText.text = $"Time: {t:0.0}s";
        manager.OnSessionEnded += () => startButton.interactable = true;
    }
}
