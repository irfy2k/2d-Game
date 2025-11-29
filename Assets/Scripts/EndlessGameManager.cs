using UnityEngine;
using TMPro;

/// <summary>
/// Simple endless-mode manager.
/// - Spawns enemies one at a time.
/// - Awards +1 score per kill.
/// - Scales enemy difficulty the longer you survive.
/// - Stores and displays a local high score via PlayerPrefs.
/// </summary>
public class EndlessGameManager : MonoBehaviour
{
    public static EndlessGameManager Instance { get; private set; }

    [Header("Enemy Spawning")]
    [Tooltip("Enemy prefab to spawn. Must be an EnemyController.")]
    public EnemyController enemyPrefab;

    [Tooltip("Possible spawn points for enemies. One will be picked at random for each spawn.")]
    public Transform[] spawnPoints;

    [Tooltip("Delay before the first enemy appears.")]
    public float initialSpawnDelay = 1.0f;

    [Tooltip("Minimum delay between spawns per enemy.")]
    public float minSpawnDelay = 0.8f;

    [Tooltip("How quickly spawn delay shrinks over time (seconds of survival per difficulty step).")]
    public float spawnDifficultyRampSeconds = 30f;

    [Header("Enemy Count Scaling")]
    [Tooltip("How many enemies can exist at once at the start of the run.")]
    public int startingMaxEnemies = 1;

    [Tooltip("Maximum enemies that can exist at once once difficulty is maxed.")]
    public int maxConcurrentEnemies = 5;

    [Tooltip("Seconds of survival to reach maxConcurrentEnemies.")]
    public float enemyCountRampSeconds = 90f;

    [Header("Enemy Difficulty Scaling")]
    [Tooltip("Maximum multiplier applied to enemy movement speed over time.")]
    public float maxSpeedMultiplier = 2.5f;

    [Tooltip("Maximum multiplier applied to detection range over time.")]
    public float maxDetectionMultiplier = 2.0f;

    [Tooltip("Minimum fraction of the original attack cooldown (lower = more frequent attacks).")]
    [Range(0.2f, 1f)] public float minAttackCooldownFraction = 0.4f;

    [Header("Scoring & UI")]
    [Tooltip("Optional UI text for the current score.")]
    public TextMeshProUGUI scoreText;

    [Tooltip("Optional UI text for the best/high score during gameplay.")]
    public TextMeshProUGUI highScoreText;

    [Tooltip("Optional UI text for the score on the Game Over screen.")]
    public TextMeshProUGUI gameOverScoreText;

    [Tooltip("Optional UI text for the high score on the Game Over screen.")]
    public TextMeshProUGUI gameOverHighScoreText;

    [Header("PlayerPrefs")]
    [Tooltip("Key used to store the local high score in PlayerPrefs.")]
    public string highScoreKey = "EndlessHighScore";

    private int _score;
    private int _highScore;
    private float _elapsedTime;
    private bool _gameOver;

    // Number of enemies currently alive in the scene.
    private int _activeEnemies;

    // Next time we are allowed to spawn another enemy.
    private float _nextSpawnTime;

    // Cached enemy base stats (taken from the prefab once).
    private float _baseSpeed;
    private float _baseDetectionRange;
    private float _baseAttackCooldown;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (enemyPrefab != null)
        {
            _baseSpeed = enemyPrefab.speed;
            _baseDetectionRange = enemyPrefab.detectionRange;
            _baseAttackCooldown = enemyPrefab.attackCooldown;
        }

        _highScore = PlayerPrefs.GetInt(highScoreKey, 0);
        UpdateScoreUI();
        UpdateHighScoreUI();
    }

    private void OnEnable()
    {
        EnemyController.OnEnemyKilled += HandleEnemyKilled;
    }

    private void OnDisable()
    {
        EnemyController.OnEnemyKilled -= HandleEnemyKilled;
    }

    private void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("EndlessGameManager: enemyPrefab is not assigned.");
            return;
        }

        // Start with an initial delay before the very first spawn.
        _nextSpawnTime = Time.time + initialSpawnDelay;
    }

    private void Update()
    {
        if (_gameOver) return;

        _elapsedTime += Time.deltaTime;

        int desiredMax = GetDesiredMaxEnemies();

        // If we can spawn another enemy and the cooldown has elapsed, do so.
        if (_activeEnemies < desiredMax && Time.time >= _nextSpawnTime)
        {
            SpawnEnemy();

            // Shorten spawn delay over time, but never below minSpawnDelay.
            float difficultyFactor = Mathf.Clamp01(_elapsedTime / spawnDifficultyRampSeconds);
            float delay = Mathf.Lerp(initialSpawnDelay, minSpawnDelay, difficultyFactor);
            _nextSpawnTime = Time.time + delay;
        }
    }

    // Called once when an enemy finishes its death animation and is destroyed.
    private void HandleEnemyKilled(EnemyController enemy)
    {
        if (_gameOver) return;

        _score++;
        UpdateScoreUI();

        // An active enemy has been removed.
        _activeEnemies = Mathf.Max(0, _activeEnemies - 1);
    }

    private int GetDesiredMaxEnemies()
    {
        int clampedStart = Mathf.Max(1, startingMaxEnemies);
        int clampedMax = Mathf.Max(clampedStart, maxConcurrentEnemies);

        if (enemyCountRampSeconds <= 0f)
            return clampedMax;

        float t = Mathf.Clamp01(_elapsedTime / enemyCountRampSeconds);
        float maxEnemiesF = Mathf.Lerp(clampedStart, clampedMax, t);
        return Mathf.RoundToInt(maxEnemiesF);
    }

    private void SpawnEnemy()
    {
        if (_gameOver || enemyPrefab == null) return;

        // Choose spawn position.
        Vector3 spawnPos = Vector3.zero;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int idx = Random.Range(0, spawnPoints.Length);
            if (spawnPoints[idx] != null)
                spawnPos = spawnPoints[idx].position;
        }
        else
        {
            // Fallback: spawn at the manager's position.
            spawnPos = transform.position;
        }

        EnemyController enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        _activeEnemies++;

        // Scale difficulty based on how long we've survived.
        float t = Mathf.Clamp01(_elapsedTime / spawnDifficultyRampSeconds);

        if (_baseSpeed > 0f)
        {
            float speedMult = Mathf.Lerp(1f, maxSpeedMultiplier, t);
            enemy.speed = _baseSpeed * speedMult;
        }

        if (_baseDetectionRange > 0f)
        {
            float detMult = Mathf.Lerp(1f, maxDetectionMultiplier, t);
            enemy.detectionRange = _baseDetectionRange * detMult;
        }

        // Keep the enemy's attack cadence consistent so animations play correctly.
        if (_baseAttackCooldown > 0f)
        {
            enemy.attackCooldown = _baseAttackCooldown;
        }

        // Keep the 1 HP rule intact.
        enemy.health = 1;
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {_score}";

        if (gameOverScoreText != null)
            gameOverScoreText.text = $"Score: {_score}";
    }

    private void UpdateHighScoreUI()
    {
        if (highScoreText != null)
            highScoreText.text = $"Best: {_highScore}";

        if (gameOverHighScoreText != null)
            gameOverHighScoreText.text = $"Best: {_highScore}";
    }

    /// <summary>
    /// Called from GamePauseManager when the player dies / game over.
    /// Updates and saves high score and locks further spawning/scoring.
    /// </summary>
    public void OnGameOver()
    {
        if (_gameOver) return;
        _gameOver = true;

        if (_score > _highScore)
        {
            _highScore = _score;
            PlayerPrefs.SetInt(highScoreKey, _highScore);
            PlayerPrefs.Save();
        }

        UpdateHighScoreUI();
        UpdateScoreUI();
    }
}
