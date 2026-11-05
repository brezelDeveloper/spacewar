using System.Collections;
using UnityEngine;

public class PlayerTrailEffect : MonoBehaviour
{
    [Header("잔상 설정")]
    [SerializeField] private float spawnInterval = 0.05f;
    [SerializeField] private float ghostLifetime = 0.3f;
    [SerializeField] private Color ghostColor = new Color(0f, 1f, 0.9f, 0.6f);
    [Tooltip("한 간격 동안 이만큼 이상 움직였을 때만 잔상 생성")]
    [SerializeField] private float minMove = 0.01f;

    private SpriteRenderer playerSprite;
    private Coroutine trailCoroutine;
    private Vector3 lastPos;

    private void Awake()
    {
        playerSprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        lastPos = transform.position;
        trailCoroutine = StartCoroutine(GenerateTrail());
    }

    private void OnDisable()
    {
        if (trailCoroutine != null)
            StopCoroutine(trailCoroutine);
    }

    private IEnumerator GenerateTrail()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            float moved = (transform.position - lastPos).magnitude;
            lastPos = transform.position;

            if (moved < minMove || playerSprite == null || !playerSprite.enabled) continue;

            if (HitFeedbackManager.Instance != null)
            {
                HitFeedbackManager.Instance.GenerateGhost(playerSprite, ghostColor, ghostLifetime);
            }
        }
    }
}