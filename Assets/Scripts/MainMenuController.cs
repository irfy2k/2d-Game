using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // Set this to the exact name of your combat scene (e.g., "GameScene" or "SampleScene")
    public string gameSceneName = "YOUR_GAME_SCENE_NAME";

    // Called when the script starts (e.g. when Menu scene loads)
    private void Start()
    {
        // FIX: Ensure time is running when we are in the menu!
        // If we came here from a paused/game-over state, time might be 0.
        Time.timeScale = 1f;
    }

    // Called when the START button is clicked
    public void StartGame()
    {
        // Resets time just in case (redundant safety check)
        Time.timeScale = 1f;

        // Loads the main combat level scene
        SceneManager.LoadScene(gameSceneName);
    }

    // Called when the QUIT button is clicked
    public void QuitGame()
    {
        // Works in standalone builds
        Application.Quit();

        // Used for testing in the Unity Editor
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}