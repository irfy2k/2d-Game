using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Centralized camera / screen FX + audio manager.
/// Place exactly one instance in your startup scene (e.g. MainMenu) and mark it as persistent.
/// - Camera shake & screen flash on hits / parries.
/// - Simple SFX routing for attacks / hits / parries / deaths.
/// - Optional looping background music.
///
/// Hook-up steps in Unity:
/// 1. Create an empty GameObject (e.g. "HitEffectsManager") and add this component.
/// 2. Assign Camera Transform (usually the Main Camera).
/// 3. (Optional) Create a full-screen UI Image on a Screen Space - Overlay Canvas and assign it to hitFlashImage.
/// 4. Add two AudioSources as children or on the same GameObject: one for SFX, one for music.
/// 5. Assign audio clips for the public fields below.
/// 6. Add Animation Events on attack / parry / death animations that call the provided public methods.
/// </summary>
public class HitEffectsManager : MonoBehaviour
{
    public static HitEffectsManager Instance { get; private set; }

    [Header("Camera")]
    [Tooltip("Camera transform to shake. Defaults to Camera.main.")]
    public Transform cameraTransform;

    [Header("Shake Settings")] 
    [Tooltip("Duration of a normal hit shake.")]
    public float hitShakeDuration = 0.08f;
    [Tooltip("Intensity of a normal hit shake.")]
    public float hitShakeIntensity = 0.15f;

    [Tooltip("Duration of a parry shake.")]
    public float parryShakeDuration = 0.1f;
    [Tooltip("Intensity of a parry shake.")]
    public float parryShakeIntensity = 0.22f;

    [Tooltip("Duration of a player-hit shake.")]
    public float playerHitShakeDuration = 0.1f;
    [Tooltip("Intensity of a player-hit shake.")]
    public float playerHitShakeIntensity = 0.25f;

    [Header("Screen Flash")] 
    [Tooltip("Full-screen Image used for a quick hit / parry flash. Optional.")]
    public Image hitFlashImage;
    public float flashDuration = 0.08f;
    public Color enemyHitFlashColor = new Color(1f, 1f, 1f, 0.25f);
    public Color playerHitFlashColor = new Color(1f, 0.2f, 0.2f, 0.35f);
    public Color parryFlashColor = new Color(0.4f, 0.8f, 1f, 0.4f);

    [Header("Audio Sources")] 
    [Tooltip("AudioSource used for one-shot SFX.")]
    public AudioSource sfxSource;
    [Tooltip("AudioSource used for looping background music.")]
    public AudioSource musicSource;

    [Header("SFX Clips")] 
    public AudioClip playerHitSfx;
    public AudioClip enemyHitSfx;
    public AudioClip parrySfx;
    public AudioClip playerAttackSwingSfx;
    public AudioClip enemyAttackSwingSfx;
    public AudioClip enemyDeathSfx;

    [Header("Music")] 
    [Tooltip("Looped background track. Optional.")]
    public AudioClip backgroundMusic;

    [Header("Player Death FX")]
    [Tooltip("Duration of the camera shake when the player dies.")]
    public float playerDeathShakeDuration = 0.2f;
    [Tooltip("Intensity of the camera shake when the player dies.")]
    public float playerDeathShakeIntensity = 0.4f;
    [Tooltip("Duration of the screen flash when the player dies.")]
    public float playerDeathFlashDuration = 0.2f;

    Coroutine _shakeRoutine;
    Coroutine _flashRoutine;

    // Base (unshaken) local position of the camera while a shake is active.
    Vector3 _baseLocalPosition;
    bool _isShaking;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null)
        {
            _baseLocalPosition = cameraTransform.localPosition;
        }

        if (hitFlashImage != null)
        {
            Color c = hitFlashImage.color;
            c.a = 0f;
            hitFlashImage.color = c;
            hitFlashImage.enabled = false;
        }

        // Auto-start background music if configured
        if (musicSource != null && backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    void Update()
    {
        // After a scene reload, the old camera may be destroyed. Reacquire Camera.main.
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            _baseLocalPosition = cameraTransform.localPosition;
        }
    }

    #region Public API - Hit / Parry FX

    /// <summary>
    /// Generic hit effect entry point.
    /// </summary>
    public void PlayHitEffect(Vector3 worldPosition, bool hitPlayer, bool isParry)
    {
        if (cameraTransform != null)
        {
            float duration;
            float intensity;

            if (isParry)
            {
                duration = parryShakeDuration;
                intensity = parryShakeIntensity;
            }
            else if (hitPlayer)
            {
                duration = playerHitShakeDuration;
                intensity = playerHitShakeIntensity;
            }
            else
            {
                duration = hitShakeDuration;
                intensity = hitShakeIntensity;
            }

            StartShake(duration, intensity);
        }

        // Screen flash
        if (hitFlashImage != null)
        {
            Color color = isParry
                ? parryFlashColor
                : (hitPlayer ? playerHitFlashColor : enemyHitFlashColor);

            StartFlash(color, flashDuration);
        }

        // SFX
        PlayHitSfx(hitPlayer, isParry);
    }

    /// <summary>
    /// Convenience wrapper for parry FX.
    /// </summary>
    public void PlayParryEffect(Vector3 worldPosition)
    {
        PlayHitEffect(worldPosition, hitPlayer: false, isParry: true);
    }

    /// <summary>
    /// Stronger, more dramatic effect used specifically when the player dies.
    /// </summary>
    public void PlayPlayerDeathEffect(Vector3 worldPosition)
    {
        if (cameraTransform != null)
        {
            StartShake(playerDeathShakeDuration, playerDeathShakeIntensity);
        }

        if (hitFlashImage != null)
        {
            // Reuse the player hit flash color but extend the duration.
            StartFlash(playerHitFlashColor, playerDeathFlashDuration);
        }
    }

    #endregion

    #region Public API - Animation Event Hooks

    // These are designed to be called from animation events.

    public void PlayPlayerAttackSwing()
    {
        if (sfxSource != null && playerAttackSwingSfx != null)
        {
            sfxSource.PlayOneShot(playerAttackSwingSfx);
        }
    }

    public void PlayEnemyAttackSwing()
    {
        if (sfxSource != null && enemyAttackSwingSfx != null)
        {
            sfxSource.PlayOneShot(enemyAttackSwingSfx);
        }
    }

    public void PlayEnemyDeath()
    {
        if (sfxSource != null && enemyDeathSfx != null)
        {
            sfxSource.PlayOneShot(enemyDeathSfx);
        }
    }

    public void PlayParrySfx()
    {
        if (sfxSource != null && parrySfx != null)
        {
            sfxSource.PlayOneShot(parrySfx);
        }
    }

    #endregion

    #region Internal helpers

    void PlayHitSfx(bool hitPlayer, bool isParry)
    {
        if (sfxSource == null) return;

        AudioClip clip = null;

        if (isParry && parrySfx != null)
        {
            clip = parrySfx;
        }
        else if (hitPlayer && playerHitSfx != null)
        {
            clip = playerHitSfx;
        }
        else if (!hitPlayer && enemyHitSfx != null)
        {
            clip = enemyHitSfx;
        }

        if (clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    void StartShake(float duration, float intensity)
    {
        if (cameraTransform == null)
            return;

        // When starting a new shake while not already shaking, record the current base position.
        if (!_isShaking)
        {
            _baseLocalPosition = cameraTransform.localPosition;
        }

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
        }

        _isShaking = true;
        _shakeRoutine = StartCoroutine(ShakeCoroutine(duration, intensity));
    }

    System.Collections.IEnumerator ShakeCoroutine(float duration, float intensity)
    {
        if (cameraTransform == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Small random offset around the original base position.
            Vector2 offset = Random.insideUnitCircle * intensity;
            cameraTransform.localPosition = _baseLocalPosition + new Vector3(offset.x, offset.y, 0f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        cameraTransform.localPosition = _baseLocalPosition;
        _shakeRoutine = null;
        _isShaking = false;
    }

    void StartFlash(Color color, float duration)
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
        }

        _flashRoutine = StartCoroutine(FlashCoroutine(color, duration));
    }

    System.Collections.IEnumerator FlashCoroutine(Color color, float duration)
    {
        if (hitFlashImage == null)
            yield break;

        hitFlashImage.enabled = true;

        float elapsed = 0f;
        Color c = color;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            c.a = Mathf.Lerp(color.a, 0f, t);
            hitFlashImage.color = c;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        c.a = 0f;
        hitFlashImage.color = c;
        hitFlashImage.enabled = false;
        _flashRoutine = null;
    }

    #endregion
}
