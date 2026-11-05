using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    [Header("충돌 이펙트 리소스")]
    [SerializeField] private GameObject hitVfxPrefab;   // 피격 파티클 프리팹
    [SerializeField] private Material whiteFlashMaterial; // 흰색 피격 하이라이트 셰이더 머티리얼

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Boss") || collision.gameObject.CompareTag("Enemy"))
        {
            ContactPoint2D contact = collision.GetContact(0);
            Vector2 hitPoint = contact.point;
            Quaternion hitRotation = Quaternion.FromToRotation(Vector3.up, contact.normal);

            if (HitFeedbackManager.Instance != null)
            {
                // 1. 타격 파티클 이펙트 생성 (접촉 지점에 생성)
                HitFeedbackManager.Instance.SpawnHitVFX(hitVfxPrefab, hitPoint, hitRotation);

                // 2. 화면 흔들림 및 순간 멈춤 적용
                HitFeedbackManager.Instance.ShakeCamera(0.25f, 0.4f);
                HitFeedbackManager.Instance.TriggerHitstop(0.06f);

                // 3. 스프라이트 흰색 섬광 연출 (0.05초간 하얗게 빛남)
                HitFeedbackManager.Instance.TriggerHitFlash(spriteRenderer, whiteFlashMaterial, 0.05f);
            }
        }
    }
}