using System.Collections;
using UnityEngine;

public class HitFeedbackManager : MonoBehaviour
{
    public static HitFeedbackManager Instance { get; private set; }

    [Header("카메라 설정")]
    [SerializeField] private Camera targetCamera;
    private Transform mainCameraTransform;
    private Vector3 originalCamPos;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null)
        {
            mainCameraTransform = targetCamera.transform;
            originalCamPos = mainCameraTransform.localPosition;
        }
    }

    #region 1. 화면 흔들림 (Camera Shake)
    public void ShakeCamera(float duration = 0.2f, float intensity = 0.3f)
    {
        if (mainCameraTransform != null)
            StartCoroutine(ShakeCoroutine(duration, intensity));
    }

    private IEnumerator ShakeCoroutine(float duration, float intensity)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 randomOffset = (Vector3)Random.insideUnitCircle * intensity;
            mainCameraTransform.localPosition = originalCamPos + randomOffset;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        mainCameraTransform.localPosition = originalCamPos;
    }
    #endregion

    #region 2. 히트스탑 (Hitstop)
    public void TriggerHitstop(float duration = 0.08f)
    {
        StartCoroutine(HitstopCoroutine(duration));
    }

    private IEnumerator HitstopCoroutine(float duration)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
    #endregion

    #region 3. 잔상 연출 (Ghost Trail)
    public void GenerateGhost(SpriteRenderer playerSprite, Color ghostColor, float ghostLifetime)
    {
        if (playerSprite == null) return;

        GameObject ghost = new GameObject("GhostTrail");
        ghost.transform.position = playerSprite.transform.position;
        ghost.transform.rotation = playerSprite.transform.rotation;
        ghost.transform.localScale = playerSprite.transform.lossyScale;

        SpriteRenderer ghostSr = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite = playerSprite.sprite;
        ghostSr.color = ghostColor;
        ghostSr.sortingLayerID = playerSprite.sortingLayerID;
        ghostSr.sortingOrder = playerSprite.sortingOrder - 1;

        StartCoroutine(FadeAndDestroyGhost(ghost, ghostSr, ghostLifetime));
    }

    private IEnumerator FadeAndDestroyGhost(GameObject ghost, SpriteRenderer sr, float lifetime)
    {
        float elapsed = 0f;
        Color initialColor = sr.color;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(initialColor.a, 0f, elapsed / lifetime);
            sr.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
            yield return null;
        }

        Destroy(ghost);
    }
    #endregion

    #region 4. 타격 VFX & 하이라이트 플래시 (Hit VFX & Flash)
    public void SpawnHitVFX(GameObject vfxPrefab, Vector3 position, Quaternion rotation)
    {
        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, position, rotation);
        }
    }

    public void TriggerHitFlash(SpriteRenderer targetSprite, Material flashMaterial, float duration = 0.05f)
    {
        if (targetSprite != null && flashMaterial != null)
        {
            StartCoroutine(HitFlashCoroutine(targetSprite, flashMaterial, duration));
        }
    }

    private IEnumerator HitFlashCoroutine(SpriteRenderer targetSprite, Material flashMaterial, float duration)
    {
        Material originalMaterial = targetSprite.material;
        targetSprite.material = flashMaterial;
        yield return new WaitForSeconds(duration);
        targetSprite.material = originalMaterial;
    }
    #endregion
}