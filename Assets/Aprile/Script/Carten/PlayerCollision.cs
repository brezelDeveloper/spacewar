using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    [Header("충돌 이펙트 리소스")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private Material whiteFlashMaterial;

    [Header("연출 간격")]
    [Tooltip("연출이 너무 자주 터지지 않도록 다음 연출까지 기다리는 시간(초)")]
    [SerializeField] private float feedbackCooldown = 0.3f;

    private SpriteRenderer spriteRenderer;
    private float nextFeedbackTime;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time < nextFeedbackTime) return;

        bool isBoss = other.GetComponentInParent<Boss>() != null;
        bool isEnemy = other.GetComponentInParent<Enemy>() != null;
        if (!isBoss && !isEnemy) return;

        HitFeedbackManager feedback = HitFeedbackManager.Instance;
        if (feedback == null) return;

        nextFeedbackTime = Time.time + feedbackCooldown;

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        Vector2 normal = (Vector2)transform.position - hitPoint;
        Quaternion hitRotation = normal.sqrMagnitude > 0.0001f
            ? Quaternion.FromToRotation(Vector3.up, normal)
            : Quaternion.identity;

        feedback.SpawnHitVFX(hitVfxPrefab, hitPoint, hitRotation);
        feedback.ShakeCamera(0.25f, 0.4f);
        feedback.TriggerHitstop(0.06f);
        feedback.TriggerHitFlash(spriteRenderer, whiteFlashMaterial, 0.05f);
    }
}