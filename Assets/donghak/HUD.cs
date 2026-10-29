using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("플레이어")]
    [Tooltip("HP 바 (채워지는 쪽 이미지). Pivot X를 0으로 두면 왼쪽부터 줄어듦")]
    [SerializeField] private Image hpBar;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text bombText;

    [Header("기록")]
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text timeText;
    [Tooltip("보스 등장까지 남은 처치 수 (처치 3/8)")]
    [SerializeField] private TMP_Text killText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text moneyText;

    [Header("보스")]
    [Tooltip("보스가 있을 때만 켜지는 묶음 (보스 HP 바의 부모)")]
    [SerializeField] private GameObject bossGroup;
    [SerializeField] private Image bossBar;

    [Header("색")]
    [SerializeField] private Color hpColor = new Color(0.3f, 0.9f, 0.4f);
    [SerializeField] private Color hpLowColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("HP가 이 비율 이하면 경고 색")]
    [SerializeField, Range(0f, 1f)] private float lowHpRatio = 0.4f;
    [SerializeField] private Color bombReadyColor = Color.white;
    [SerializeField] private Color bombEmptyColor = new Color(1f, 1f, 1f, 0.3f);

    private Player player;
    private Boss boss;
    private float bossSearchTimer;

    void Start()
    {
        if (bossGroup != null) bossGroup.SetActive(false);
    }

    void Update()
    {
        UpdatePlayer();
        UpdateRecord();
        UpdateBoss();
    }

    void UpdatePlayer()
    {
        if (player == null) player = FindAnyObjectByType<Player>();
        if (player == null) return;

        SetBar(hpBar, player.HpRatio);
        if (hpBar != null) hpBar.color = player.HpRatio <= lowHpRatio ? hpLowColor : hpColor;
        SetText(hpText, $"HP {player.CurrentHp}/{player.MaxHp}");

        if (bombText != null)
        {
            bool ready = player.BombCount > 0;
            SetText(bombText, ready ? $"폭탄 [Q] x{player.BombCount}" : "폭탄 없음");
            bombText.color = ready ? bombReadyColor : bombEmptyColor;
        }
    }

    void UpdateRecord()
    {
        DataManager data = DataManager.Instance;
        int score = data != null ? data.currentScore : 0;
        int money = data != null ? data.money : 0;
        int seconds = Mathf.FloorToInt(StageManager.StageTime);

        SetText(stageText, $"STAGE {StageManager.CurrentStage}");
        SetText(timeText, $"{seconds / 60:00}:{seconds % 60:00}");

        int kills = Mathf.Min(Enemy.KillCount, StageManager.KillsForBoss);
        SetText(killText, StageManager.BossPhase ? "보스전" : $"처치 {kills}/{StageManager.KillsForBoss}");
        SetText(scoreText, $"점수 {score:N0}");
        SetText(moneyText, Enemy.StageMoney > 0 ? $"파편 {money:N0} (+{Enemy.StageMoney:N0})" : $"파편 {money:N0}");
    }

    void UpdateBoss()
    {
        if (boss == null)
        {
            bossSearchTimer -= Time.deltaTime;
            if (bossSearchTimer <= 0f)
            {
                bossSearchTimer = 0.5f;
                boss = FindAnyObjectByType<Boss>();
            }
        }

        bool show = boss != null;
        if (bossGroup != null && bossGroup.activeSelf != show) bossGroup.SetActive(show);
        if (show) SetBar(bossBar, boss.HpRatio);
    }

    void SetBar(Image bar, float ratio)
    {
        if (bar == null) return;

        ratio = Mathf.Clamp01(ratio);

        if (bar.type == Image.Type.Filled) bar.fillAmount = ratio;
        else bar.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
    }

    void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }
}