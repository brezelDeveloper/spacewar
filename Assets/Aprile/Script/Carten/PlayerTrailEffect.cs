using System.Collections;
using UnityEngine;

public class PlayerTrailEffect : MonoBehaviour
{
    [Header("잔상 설정")]
    [SerializeField] private float spawnInterval = 0.05f;
    [SerializeField] private float ghostLifetime = 0.3f;
    [SerializeField] private Color ghostColor = new Color(0f, 1f, 0.9f, 0.6f);

    private SpriteRenderer playerSprite;
    private Rigidbody2D rb;
    private Coroutine trailCoroutine;

    private void Awake()
    {
        playerSprite = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
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
            if (rb != null && rb.linearVelocity.magnitude > 0.1f)
            {
                // 매니저를 통해 잔상 생성 요청
                if (HitFeedbackManager.Instance != null)
                {
                    HitFeedbackManager.Instance.GenerateGhost(playerSprite, ghostColor, ghostLifetime);
                }
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}