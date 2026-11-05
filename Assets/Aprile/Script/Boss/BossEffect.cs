using UnityEngine;

public class BossEffect : MonoBehaviour
{
    [Header("피격 이펙트")]
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("이펙트 크기 배율 (보스가 크면 키워 주세요)")]
    [SerializeField] private float hitEffectScale = 1.5f;
    [SerializeField] private float hitEffectLifetime = 2.5f;

    [Header("사망 이펙트")]
    [SerializeField] private GameObject deathEffectPrefab;
    [Tooltip("사망 이펙트 크기 배율")]
    [SerializeField] private float deathEffectScale = 2.5f;
    [Tooltip("보스 몸 위에 터뜨릴 폭발 개수")]
    [SerializeField] private int deathBurstCount = 6;
    [Tooltip("폭발이 퍼지는 범위 (보스 그림 크기에 대한 비율)")]
    [SerializeField, Range(0.1f, 1f)] private float deathSpreadRatio = 0.6f;
    [SerializeField] private float deathEffectLifetime = 3f;

    // 피격 시 호출 (맞은 위치에서 재생)
    public void PlayHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null)
        {
            Debug.LogWarning("BossEffect: Hit Effect Prefab이 비어 있습니다.");
            return;
        }

        Spawn(hitEffectPrefab, position, hitEffectScale, hitEffectLifetime);
    }

    // 사망 시 호출 (보스 몸 위 여러 곳에서 동시에 폭발)
    public void PlayDeathEffect()
    {
        if (deathEffectPrefab == null)
        {
            Debug.LogWarning("BossEffect: Death Effect Prefab이 비어 있습니다.");
            return;
        }

        // 중앙에 큰 폭발 1개
        Spawn(deathEffectPrefab, transform.position, deathEffectScale, deathEffectLifetime);

        // 몸 주변에 작은 폭발 여러 개
        float radius = GetBodyRadius() * deathSpreadRatio;
        for (int i = 0; i < deathBurstCount; i++)
        {
            Vector3 offset = Random.insideUnitCircle * radius;
            Spawn(deathEffectPrefab, transform.position + offset, deathEffectScale * 0.6f, deathEffectLifetime);
        }
    }

    void Spawn(GameObject prefab, Vector3 position, float scale, float lifetime)
    {
        position.z = 0f; // 2D 게임이라 Z를 0으로 맞춤
        GameObject fx = Instantiate(prefab, position, Quaternion.identity);
        fx.transform.localScale *= scale;
        Destroy(fx, lifetime);
    }

    // 보스 그림의 대략적인 반지름 (스프라이트 크기 기준)
    float GetBodyRadius()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return 1f;

        Vector3 extents = sr.bounds.extents;
        return Mathf.Max(Mathf.Min(extents.x, extents.y), 0.1f);
    }
}