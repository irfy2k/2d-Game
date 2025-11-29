using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Handles pausing/resuming the game and basic Game Over / Restart logic.
/// Attach this to a GameObject in your scene (e.g. "GameManager").
/// Wire up pauseMenuUI and gameOverUI in the Inspector.
/// </summary>
public class GamePauseManager : MonoBehaviour
{
    // CRITICAL: Singleton access point
    public static GamePauseManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Root GameObject of the pause menu UI.")]
    public GameObject pauseUI;

    [Tooltip("Root GameObject of the game over UI (optional, for pause blocking).")]
    public GameObject gameOverUI;

    [Header("Scenes")]
    [Tooltip("Name of the main menu scene as added in Build Settings.")]
    public string menuSceneName = "MainMenu";

    private bool _isPaused; // Private variable to store the state

    // NEW: Public getter property for other scripts (like PlayerController) to check pause state
    public bool IsPaused => _isPaused;

    // --- SETUP ---

    private void Awake()
    {
        // Must be a Singleton for the PlayerController to find it!
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            if (pauseUI != null) pauseUI.SetActive(false);
            if (gameOverUI != null) gameOverUI.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    // --- INPUT & GAME FLOW METHODS ---

    // This is called by Player Input (Escape Key)
    public void OnPause(InputAction.CallbackContext ctx)
    {
        // Only toggle pause if the action was performed (not started or cancelled) 
        // AND if the Game Over screen is NOT currently active.
        if (ctx.performed && gameOverUI != null && !gameOverUI.activeSelf)
        {
            // The method toggles between the two states
            TogglePause(!_isPaused);
        }
    }

    public void TogglePause(bool shouldPause)
    {
        // Don't pause if the game is already paused, or if the game is over
        if (gameOverUI != null && gameOverUI.activeSelf) return;

        _isPaused = shouldPause;

        // Show/Hide UI
        if (pauseUI != null) pauseUI.SetActive(shouldPause);

        // Freeze/Resume Time
        Time.timeScale = shouldPause ? 0f : 1f;
    }


    // Called by PlayerController when HP reaches 0
    public void HandleGameOver()
    {
        _isPaused = true;

        // Let the endless-mode manager update and store scores before freezing time.
        if (EndlessGameManager.Instance != null)
        {
            EndlessGameManager.Instance.OnGameOver();
        }

        if (gameOverUI != null) gameOverUI.SetActive(true);
        Time.timeScale = 0f; // Freeze time
    }

    // UI button hook: Resume game (called by the 'Resume' button)
    public void OnResumeButton()
    {
        TogglePause(false); // Resume
    }

    // UI button hook: Restart current level from pause or game over.
    public void RestartLevel()
    {
        Time.timeScale = 1f; // Ensure time is running in the new scene.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // UI button hook: Return to main menu from pause or game over.
    public void ReturnToMenu()
    {
        if (string.IsNullOrEmpty(menuSceneName))
        {
            Debug.LogError("GamePauseManager: mainMenuSceneName is not set.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    // NEW: UI button hook to exit the application entirely.
    public void ExitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

        // Used for testing in the Unity Editor
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}