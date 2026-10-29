using System.Collections.Generic;
using UnityEngine;

public class CheatManager : MonoBehaviour
{
    public static CheatManager Instance { get; private set; }

    [Header("치트키 (F1 ~ F12 중 자유롭게 설정)")]
    [SerializeField] private KeyCode debugModeKey = KeyCode.F1;
    [SerializeField] private KeyCode invincibleKey = KeyCode.F2;
    [SerializeField] private KeyCode killEnemiesKey = KeyCode.F3;
    [SerializeField] private KeyCode addMoneyKey = KeyCode.F4;
    [SerializeField] private KeyCode skipStageKey = KeyCode.F5;

    [Header("강제 적 사망")]
    [Tooltip("화면 안의 적·보스에게 주는 데미지")]
    [SerializeField] private int killDamage = 9999;

    [Header("돈 추가")]
    [SerializeField] private int moneyAmount = 1000;

    [Header("태그")]
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string enemyBulletTag = "EnemyBullet";

    [Header("디버그 모드 표시")]
    [SerializeField] private float lineWidth = 0.04f;
    [SerializeField] private float refreshInterval = 0.1f;
    [SerializeField] private int circleSegments = 24;
    [SerializeField] private bool showStatusGUI = true;

    private Player player;
    private Camera cam;
    private bool debugMode;
    private bool invincible;
    private Material lineMaterial;
    private float refreshTimer;
    private readonly List<Collider2D> colliders = new List<Collider2D>();
    private readonly Dictionary<Collider2D, LineRenderer> lines = new Dictionary<Collider2D, LineRenderer>();

    public bool IsDebugMode => debugMode;
    public bool IsInvincible => invincible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader != null) lineMaterial = new Material(shader);
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugModeKey)) ToggleDebugMode();
        if (Input.GetKeyDown(invincibleKey)) ToggleInvincible();
        if (Input.GetKeyDown(killEnemiesKey)) KillEnemiesOnScreen();
        if (Input.GetKeyDown(addMoneyKey)) AddMoney();
        if (Input.GetKeyDown(skipStageKey)) SkipStage();
    }

    private void LateUpdate()
    {
        Player p = GetPlayer();
        if (p != null) p.CheatInvincible = invincible;

        if (debugMode) UpdateDebugLines();
    }

    private void SkipStage()
    {
        StageManager.SkipStage();
        Debug.Log("[치트] 스테이지 넘기기");
    }

    private Player GetPlayer()
    {
        if (player == null) player = FindAnyObjectByType<Player>();
        return player;
    }

    private bool IsVisible(Vector3 worldPos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Vector3 v = cam.WorldToViewportPoint(worldPos);
        return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
    }

    private void ToggleDebugMode()
    {
        debugMode = !debugMode;
        Debug.Log($"[치트] 디버그 모드: {(debugMode ? "ON" : "OFF")}");

        if (debugMode) refreshTimer = refreshInterval;
        else ClearDebugLines();
    }

    private void UpdateDebugLines()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            RefreshColliderList();
        }

        foreach (Collider2D col in colliders)
        {
            if (col == null) continue;

            LineRenderer lr = GetOrCreateLine(col);
            bool show = col.enabled && col.gameObject.activeInHierarchy;
            lr.enabled = show;
            if (show) DrawCollider(col, lr);
        }
    }

    private void RefreshColliderList()
    {
        colliders.Clear();
        colliders.AddRange(FindObjectsByType<Collider2D>(FindObjectsSortMode.None));

        HashSet<Collider2D> alive = new HashSet<Collider2D>(colliders);
        List<Collider2D> removeList = null;

        foreach (var pair in lines)
        {
            if (pair.Key == null || !alive.Contains(pair.Key))
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
                if (removeList == null) removeList = new List<Collider2D>();
                removeList.Add(pair.Key);
            }
        }

        if (removeList != null)
        {
            foreach (var key in removeList) lines.Remove(key);
        }
    }

    private LineRenderer GetOrCreateLine(Collider2D col)
    {
        if (lines.TryGetValue(col, out LineRenderer existing) && existing != null)
            return existing;

        GameObject go = new GameObject("DebugCollider");
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        if (lineMaterial != null) lr.material = lineMaterial;
        lr.sortingOrder = 1000;

        Color c = GetColor(col);
        lr.startColor = c;
        lr.endColor = c;

        lines[col] = lr;
        return lr;
    }

    private Color GetColor(Collider2D col)
    {
        if (col.GetComponentInParent<Player>() != null) return Color.green;
        if (col.GetComponentInParent<Boss>() != null) return Color.magenta;
        if (col.CompareTag(enemyTag)) return Color.red;
        if (col.CompareTag(enemyBulletTag)) return Color.yellow;
        return Color.cyan;
    }

    private void DrawCollider(Collider2D col, LineRenderer lr)
    {
        Transform t = col.transform;

        if (col is CircleCollider2D circle)
        {
            float r = circle.radius * Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y));
            Vector3 center = t.TransformPoint(circle.offset);

            lr.positionCount = circleSegments;
            for (int i = 0; i < circleSegments; i++)
            {
                float angle = (i / (float)circleSegments) * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r);
            }
        }
        else if (col is BoxCollider2D box)
        {
            Vector2 h = box.size * 0.5f;
            Vector2 o = box.offset;

            lr.positionCount = 4;
            lr.SetPosition(0, t.TransformPoint(o + new Vector2(-h.x, -h.y)));
            lr.SetPosition(1, t.TransformPoint(o + new Vector2(-h.x, h.y)));
            lr.SetPosition(2, t.TransformPoint(o + new Vector2(h.x, h.y)));
            lr.SetPosition(3, t.TransformPoint(o + new Vector2(h.x, -h.y)));
        }
        else if (col is PolygonCollider2D poly && poly.pathCount > 0)
        {
            Vector2[] path = poly.GetPath(0);

            lr.positionCount = path.Length;
            for (int i = 0; i < path.Length; i++)
                lr.SetPosition(i, t.TransformPoint(path[i] + poly.offset));
        }
        else
        {
            Bounds b = col.bounds;
            lr.positionCount = 4;
            lr.SetPosition(0, new Vector3(b.min.x, b.min.y, 0f));
            lr.SetPosition(1, new Vector3(b.min.x, b.max.y, 0f));
            lr.SetPosition(2, new Vector3(b.max.x, b.max.y, 0f));
            lr.SetPosition(3, new Vector3(b.max.x, b.min.y, 0f));
        }
    }

    private void ClearDebugLines()
    {
        foreach (var pair in lines)
        {
            if (pair.Value != null) Destroy(pair.Value.gameObject);
        }
        lines.Clear();
        colliders.Clear();
    }

    private void ToggleInvincible()
    {
        invincible = !invincible;
        Debug.Log($"[치트] 무적: {(invincible ? "ON" : "OFF")}");
    }

    private void KillEnemiesOnScreen()
    {
        int count = 0;

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (!IsVisible(enemy.transform.position)) continue;

            enemy.TakeDamage(killDamage);
            count++;
        }

        foreach (Boss boss in FindObjectsByType<Boss>(FindObjectsSortMode.None))
        {
            if (!IsVisible(boss.transform.position)) continue;

            boss.TakeDamage(killDamage);
            count++;
        }

        Debug.Log($"[치트] 강제 적 사망: {count}마리");
    }

    private void AddMoney()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[치트] DataManager가 없어서 돈을 추가할 수 없어요.");
            return;
        }

        DataManager.Instance.money += moneyAmount;
        Debug.Log($"[치트] 돈 +{moneyAmount} (현재 {DataManager.Instance.money})");
    }

    private void OnGUI()
    {
        if (!showStatusGUI) return;

        GUI.Label(new Rect(10, Screen.height - 50, 500, 24),
            $"[{debugModeKey}] 디버그: {(debugMode ? "ON" : "OFF")}   [{invincibleKey}] 무적: {(invincible ? "ON" : "OFF")}");
        GUI.Label(new Rect(10, Screen.height - 30, 500, 24),
            $"[{killEnemiesKey}] 화면 적 즉사   [{addMoneyKey}] 돈 +{moneyAmount}   [{skipStageKey}] 스테이지 넘기기");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}