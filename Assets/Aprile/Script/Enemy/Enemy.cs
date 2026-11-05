using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    public enum EnemyType
    {
        [InspectorName("정찰기 - 단발 유도")] Scout,
        [InspectorName("전투기 - 3연발 유도")] Fighter,
        [InspectorName("중장갑함 - 산탄 + 유도")] Heavy,
        [InspectorName("요격기 - 직선 고속탄")] Interceptor,
        [InspectorName("폭격기 - 곡사 폭탄")] Bomber
    }

    public static int KillCount;
    public static int StageMoney;

    [Header("종류")]
    [SerializeField] private EnemyType type;

    [Header("능력치")]
    [SerializeField] private int maxHp = 2;
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("공격 간격(초)")]
    [SerializeField] private float fireInterval = 2f;
    [Tooltip("미사일 공격력. 플레이어·적 누구에게 맞아도 이만큼 깎임")]
    [SerializeField] private int damage = 1;

    [Header("위치 잡기")]
    [Tooltip("플레이어와 이 거리 안으로 들어오면 멈춰서 몸만 돌리며 사격")]
    [SerializeField] private float keepDistance = 5f;
    [Tooltip("플레이어가 이 거리보다 멀어지면 다시 따라감")]
    [SerializeField] private float chaseDistance = 9f;
    [Tooltip("멈춰 있을 때 옆으로 맴도는 속도 비율 (0 = 완전 정지)")]
    [SerializeField, Range(0f, 1f)] private float orbitRatio = 0f;
    [Tooltip("멈출 때 화면 끝에서 최소 이만큼 안쪽이어야 함")]
    [SerializeField] private float holdMargin = 1f;

    [Header("탄 모양 (프리팹은 그림·충돌만 담당)")]
    [SerializeField] private EnemyBullet missilePrefab;
    [Tooltip("폭격기만 사용 (비우면 미사일 그림 사용)")]
    [SerializeField] private EnemyBullet bombPrefab;

    [Header("탄 성질 (이 적이 쏘는 모든 탄)")]
    [SerializeField] private BulletSettings bullet = new BulletSettings();

    [Header("── 전투기만 사용 ──")]
    [Tooltip("연발 수")]
    [SerializeField] private int burstCount = 3;
    [Tooltip("연발 간격(초)")]
    [SerializeField] private float burstInterval = 0.15f;

    [Header("── 중장갑함만 사용 ──")]
    [Tooltip("부채꼴 발 수 (가운데 1발만 유도)")]
    [SerializeField] private int spreadCount = 5;
    [Tooltip("탄 사이 각도")]
    [SerializeField] private float spreadAngle = 20f;

    [Header("── 요격기만 사용 ──")]
    [Tooltip("멈춰 있을 때 좌우로 흔들리는 크기")]
    [SerializeField] private float weaveAmount = 0.7f;

    [Header("── 폭격기만 사용 ──")]
    [Tooltip("몇 초 뒤 플레이어 위치를 예측해서 던질지")]
    [SerializeField] private float leadTime = 1f;
    [SerializeField] private ExplosionSettings explosion = new ExplosionSettings();

    [Header("처치 보상 (점수는 즉시, 파편은 스테이지 클리어 때 지급)")]
    [SerializeField] private int score = 100;
    [SerializeField] private int money = 10;

    [Header("아이템 드랍 설정")]
    [SerializeField] private GameObject[] normalItemPrefabs; // 일반 아이템 3종 (회복, 무적, 방어력)
    [SerializeField] private GameObject rareItemPrefab;      // 고급 아이템 1종
    [SerializeField, Range(0f, 1f)] private float normalDropChance = 0.35f; // 일반 아이템 드랍 확률 (35%)
    [SerializeField, Range(0f, 1f)] private float rareDropChance = 0.10f;   // 고급 아이템 드랍 확률 (10%)

    [Header("HP 바 (적 프리팹 안에 직접 만든 그림 연결)")]
    [Tooltip("HPBack·HPFill을 묶은 부모. 적이 돌아도 바는 돌지 않고 늘 아래에 있음")]
    [SerializeField] private Transform hpBarRoot;
    [Tooltip("HP만큼 줄어드는 쪽 그림 (가운데 기준 Square 그림)")]
    [SerializeField] private SpriteRenderer hpBarFill;
    [Tooltip("적 중심에서 바까지 거리. Y가 음수면 아래쪽")]
    [SerializeField] private Vector2 hpBarOffset = new Vector2(0f, -0.7f);
    [Tooltip("켜면 HP가 가득일 때는 바를 숨김")]
    [SerializeField] private bool hideHpBarWhenFull = false;
    [SerializeField] private Color hpBarColor = new Color(0.3f, 0.9f, 0.4f);
    [SerializeField] private Color hpBarLowColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("HP가 이 비율 이하면 경고 색")]
    [SerializeField, Range(0f, 1f)] private float hpBarLowRatio = 0.4f;

    [Header("화면 밖으로 밀려났을 때")]
    [SerializeField] private float returnSpeed = 6f;

    [Header("연결")]
    [SerializeField] private string playerTag = "Player";

    private const float ReturnTime = 0.5f;

    private Rigidbody2D rb;
    private EnemyEffect effect; // [동료분 추가] 피격/사망 이펙트 담당
    private Camera cam;
    private Player target;
    private Vector2 lastPlayerPos;
    private Vector2 playerVelocity;
    private int hp;
    private float fireTimer;
    private float pushTimer;
    private float orbitDir;
    private bool chasing = true;
    private bool entered;
    private bool dead;
    private Vector3 fillFullScale;
    private Vector3 fillFullPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        effect = GetComponent<EnemyEffect>(); // [동료분 추가] 같은 오브젝트의 EnemyEffect 가져오기
        cam = Camera.main;
        hp = maxHp;
        fireTimer = Random.Range(0.5f, fireInterval);
        orbitDir = Random.value < 0.5f ? 1f : -1f;

        if (hpBarFill != null)
        {
            fillFullScale = hpBarFill.transform.localScale;
            fillFullPos = hpBarFill.transform.localPosition;
        }

        UpdateHpBar();
    }

    void FixedUpdate()
    {
        if (target == null) FindPlayer();

        if (target == null || target.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 playerPos = target.transform.position;
        playerVelocity = (playerPos - lastPlayerPos) / Time.fixedDeltaTime;
        lastPlayerPos = playerPos;

        rb.angularVelocity = 0f;
        rb.MoveRotation(AngleTo(playerPos));

        CheckScreen();

        if (pushTimer > 0f)
        {
            pushTimer -= Time.fixedDeltaTime;
            return;
        }

        Move(playerPos);
        UpdateFire(playerPos);
    }

    void FindPlayer()
    {
        GameObject found = GameObject.FindWithTag(playerTag);
        if (found == null || !found.TryGetComponent(out target)) return;

        lastPlayerPos = target.transform.position;
    }

    void Move(Vector2 playerPos)
    {
        Vector2 toPlayer = playerPos - rb.position;
        float dist = toPlayer.magnitude;
        Vector2 dir = toPlayer.normalized;
        Vector2 side = new Vector2(-dir.y, dir.x) * orbitDir;

        if (dist > chaseDistance) chasing = true;
        else if (dist <= keepDistance && InsideMargin(rb.position)) chasing = false;

        Vector2 move = chasing ? dir : side * orbitRatio;

        if (type == EnemyType.Interceptor)
        {
            move += side * Mathf.Sin(Time.time * 5f) * weaveAmount;
        }

        rb.linearVelocity = Vector2.ClampMagnitude(move, 1f) * moveSpeed;
    }

    bool InsideMargin(Vector2 p)
    {
        Vector2 min = (Vector2)cam.ViewportToWorldPoint(Vector3.zero) + Vector2.one * holdMargin;
        Vector2 max = (Vector2)cam.ViewportToWorldPoint(Vector3.one) - Vector2.one * holdMargin;
        return p.x > min.x && p.x < max.x && p.y > min.y && p.y < max.y;
    }

    void CheckScreen()
    {
        Vector2 min = cam.ViewportToWorldPoint(Vector3.zero);
        Vector2 max = cam.ViewportToWorldPoint(Vector3.one);
        Vector2 p = rb.position;
        bool inside = p.x > min.x && p.x < max.x && p.y > min.y && p.y < max.y;

        if (inside)
        {
            entered = true;
        }
        else if (entered)
        {
            Vector2 center = cam.transform.position;
            rb.linearVelocity = (center - p).normalized * returnSpeed;
            pushTimer = ReturnTime;
        }
    }

    void UpdateFire(Vector2 playerPos)
    {
        if (!entered) return;

        fireTimer -= Time.fixedDeltaTime;
        if (fireTimer > 0f) return;

        Fire(playerPos);
        fireTimer = fireInterval;
    }

    void Fire(Vector2 playerPos)
    {
        float aim = AngleTo(playerPos);

        switch (type)
        {
            case EnemyType.Scout:
                Shoot(missilePrefab, aim, true);
                break;

            case EnemyType.Fighter:
                StartCoroutine(Burst());
                break;

            case EnemyType.Heavy:
                for (int i = 0; i < spreadCount; i++)
                {
                    float offset = (i - (spreadCount - 1) * 0.5f) * spreadAngle;
                    Shoot(missilePrefab, aim + offset, i == spreadCount / 2);
                }
                break;

            case EnemyType.Interceptor:
                Shoot(missilePrefab, aim, false);
                break;

            case EnemyType.Bomber:
                EnemyBullet bomb = bombPrefab != null ? bombPrefab : missilePrefab;
                Vector2 landing = playerPos + playerVelocity * leadTime;
                float fuse = Mathf.Max(Vector2.Distance(rb.position, landing) / Mathf.Max(bullet.speed, 0.01f), 0.5f);
                Shoot(bomb, AngleTo(landing), false, fuse, explosion);
                break;
        }
    }

    IEnumerator Burst()
    {
        for (int i = 0; i < burstCount; i++)
        {
            if (target == null || target.IsDead) yield break;

            Shoot(missilePrefab, AngleTo(target.transform.position), true);
            yield return new WaitForSeconds(burstInterval);
        }
    }

    void Shoot(EnemyBullet prefab, float angle, bool homing, float life = 0f, ExplosionSettings blast = null)
    {
        if (prefab == null || target == null) return;

        SoundManager.Play(SoundManager.Sfx.PlayerShoot); // Shoot -> PlayerShoot 으로 변경

        EnemyBullet shot = Instantiate(prefab, rb.position, Quaternion.Euler(0f, 0f, angle));
        shot.Launch(target.transform, damage, homing, bullet, life, blast);
    }

    float AngleTo(Vector2 point)
    {
        Vector2 d = point - rb.position;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            player.PushEnemy(this);
        }
    }

    void LateUpdate()
    {
        if (hpBarRoot == null) return;

        hpBarRoot.rotation = Quaternion.identity;
        hpBarRoot.position = transform.position + (Vector3)hpBarOffset;
    }

    void UpdateHpBar()
    {
        float ratio = Mathf.Clamp01((float)hp / Mathf.Max(maxHp, 1));

        if (hpBarRoot != null)
        {
            bool show = !(hideHpBarWhenFull && ratio >= 1f);
            if (hpBarRoot.gameObject.activeSelf != show) hpBarRoot.gameObject.SetActive(show);
        }

        if (hpBarFill == null) return;

        float spriteWidth = hpBarFill.sprite != null ? hpBarFill.sprite.bounds.size.x : 1f;
        float fullWidth = spriteWidth * fillFullScale.x;

        Transform fill = hpBarFill.transform;
        fill.localScale = new Vector3(fillFullScale.x * ratio, fillFullScale.y, fillFullScale.z);
        fill.localPosition = fillFullPos - new Vector3(fullWidth * (1f - ratio) * 0.5f, 0f, 0f);
        hpBarFill.color = ratio <= hpBarLowRatio ? hpBarLowColor : hpBarColor;
    }

    public void ApplyKnockback(Vector2 dir, float distance, float duration)
    {
        rb.linearVelocity = dir.normalized * (distance / duration);
        pushTimer = duration;
    }

    public void TakeDamage(int amount)
    {
        if (dead) return;

        hp -= amount;
        UpdateHpBar();
        SoundManager.Play(SoundManager.Sfx.EnemyHit); // Hit -> EnemyHit 으로 변경
        Debug.Log($"{name} HP {Mathf.Max(hp, 0)}/{maxHp}");

        if (hp <= 0)
        {
            Die();
        }
        else if (effect != null)
        {
            effect.PlayHitEffect(transform.position); // [동료분 추가] 살아남았을 때만 피격 이펙트
        }
    }

    public void Die()
    {
        if (dead) return;
        dead = true;

        SoundManager.Play(SoundManager.Sfx.EnemyDeath);

        if (effect != null) effect.PlayDeathEffect(); // [동료분 추가] 사망 이펙트

        KillCount++;
        StageMoney += money; // 스테이지 클리어 보상 표시용 누적

        if (DataManager.Instance != null)
        {
            DataManager.Instance.currentScore += score;
        }

        // [아이템 드랍 로직] 1순위: 고급 아이템 확률 검사 -> 2순위: 일반 아이템 확률 검사
        float roll = Random.value;
        if (rareItemPrefab != null && roll <= rareDropChance)
        {
            Instantiate(rareItemPrefab, transform.position, Quaternion.identity);
        }
        else if (normalItemPrefabs != null && normalItemPrefabs.Length > 0 && roll <= rareDropChance + normalDropChance)
        {
            int idx = Random.Range(0, normalItemPrefabs.Length);
            Instantiate(normalItemPrefabs[idx], transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}