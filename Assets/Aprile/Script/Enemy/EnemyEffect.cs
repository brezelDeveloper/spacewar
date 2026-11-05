using UnityEngine;

public class EnemyEffect : MonoBehaviour
{
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject deathEffectPrefab;

    // 피격 시 호출
    public void PlayHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null)
        {
            Debug.LogWarning("Hit Effect Prefab이 비어 있습니다.");
            return;
        }

        Debug.Log("피격 이펙트 호출됨");
        GameObject fx = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        Destroy(fx, 2.5f); // 파티클이 끝나는 시간(Duration 1.5 + Lifetime 1)에 맞춤
    }

    // 사망 시 호출
    public void PlayDeathEffect()
    {
        if (deathEffectPrefab == null)
        {
            Debug.LogWarning("Death Effect Prefab이 비어 있습니다.");
            return;
        }

        GameObject fx = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        Destroy(fx, 3f);
    }
}