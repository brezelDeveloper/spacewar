using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [System.Serializable]
    public class CooldownSlot
    {
        [Tooltip("이 칸 전체 묶음. 아이템 칸은 퀵슬롯이 비면 자동으로 꺼짐")]
        public GameObject root;
        [Tooltip("폭탄·아이템 그림")]
        public Image icon;
        [Tooltip("그림 위를 덮는 어두운 이미지. 쿨타임이 줄어드는 만큼 시계처럼 사라짐")]
        public Image cover;
        [Tooltip("남은 초 숫자")]
        public TMP_Text timeText;
    }

    [System.Serializable]
    public class PartIcon
    {
        public PartType type;
        public Sprite icon;
    }

    [Header("플레이어")]
    [Tooltip("HP 바 (채워지는 쪽 이미지). Pivot X를 0으로 두면 왼쪽부터 줄어듦")]
    [SerializeField] private Image hpBar;
    [SerializeField] private TMP_Text hpText;

    [Header("폭탄 UI (쿨타임 게이지)")]
    [Tooltip("(선택) 쿨타임이 찰수록 늘어나는 게이지")]
    [SerializeField] private Image bombGaugeFill;

    [Header("쿨타임 칸 (직접 만든 UI 연결)")]
    [SerializeField] private CooldownSlot bombSlot = new CooldownSlot();
    [Tooltip("위에서부터 퀵슬롯 1번, 2번")]
    [SerializeField] private CooldownSlot[] skillSlots = { new CooldownSlot(), new CooldownSlot() };
    [Tooltip("파츠 종류별 아이콘 그림")]
    [SerializeField]
    private PartIcon[] partIcons =
    {
        new PartIcon { type = PartType.ForceHoming },
        new PartIcon { type = PartType.TimeStop },
        new PartIcon { type = PartType.Reflect },
        new PartIcon { type = PartType.BlackHole },
        new PartIcon { type = PartType.SlowField }
    };
    [Header("쿨타임 칸 자동 생성 (임시 — 나중에 직접 만들면 끄고 위 칸에 연결)")]
    [Tooltip("켜면 게임 시작 때 HP바 밑에 칸 3개를 자동으로 만듦")]
    [SerializeField] private bool autoBuildSlots = true;
    [SerializeField] private Sprite bombIcon;
    [Tooltip("화면 왼쪽 위 모서리에서 칸 줄까지 거리 (1920x1080 기준 픽셀)")]
    [SerializeField] private Vector2 autoBarPosition = new Vector2(15f, -55f);
    [SerializeField] private float autoSlotSize = 64f;
    [SerializeField] private float autoSlotSpacing = 10f;
    [SerializeField] private float autoIconPadding = 6f;
    [SerializeField] private float autoKeyFontSize = 16f;
    [SerializeField] private float autoTimeFontSize = 30f;
    [SerializeField] private Color autoSlotBackColor = new Color(0.1f, 0.12f, 0.2f, 0.85f);
    [SerializeField] private Color autoCoverColor = new Color(0f, 0f, 0f, 0.7f);

    [SerializeField] private Color iconReadyColor = Color.white;
    [SerializeField] private Color iconCoolingColor = new Color(0.55f, 0.55f, 0.55f);

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

    private Player player;
    private Item item;
    private Boss boss;
    private float bossSearchTimer;

    void Start()
    {
        if (bossGroup != null) bossGroup.SetActive(false);

        if (autoBuildSlots) BuildSlots();

        CheckSlot(bombSlot, "Bomb Slot");
        PrepareCover(bombSlot);

        for (int i = 0; i < skillSlots.Length; i++)
        {
            CheckSlot(skillSlots[i], $"Skill Slots {i}");
            PrepareCover(skillSlots[i]);
        }
    }

    void Update()
    {
        UpdatePlayer();
        UpdateCooldowns();
        UpdateRecord();
        UpdateBoss();
    }

    void BuildSlots()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject bar = new GameObject("AutoSkillBar", typeof(RectTransform));
        RectTransform barRt = bar.GetComponent<RectTransform>();
        barRt.SetParent(canvas.rootCanvas.transform, false);
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(0f, 1f);
        barRt.pivot = new Vector2(0f, 1f);
        barRt.anchoredPosition = autoBarPosition;
        barRt.sizeDelta = new Vector2(autoSlotSize * 3f + autoSlotSpacing * 2f, autoSlotSize);

        HorizontalLayoutGroup layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = autoSlotSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TMP_FontAsset font = hpText != null ? hpText.font : null;

        bombSlot = CreateSlot(barRt, "BombSlot", "Q", bombIcon, font);
        skillSlots = new[]
        {
            CreateSlot(barRt, "Slot1", "1", null, font),
            CreateSlot(barRt, "Slot2", "2", null, font)
        };
    }

    CooldownSlot CreateSlot(RectTransform parent, string slotName, string key, Sprite sprite, TMP_FontAsset font)
    {
        GameObject go = new GameObject(slotName, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(autoSlotSize, autoSlotSize);
        Image back = go.GetComponent<Image>();
        back.color = autoSlotBackColor;
        back.raycastTarget = false;

        Image icon = CreateImage("Icon", rt);
        icon.sprite = sprite;
        icon.preserveAspect = true;

        Image cover = CreateImage("Cover", rt);
        cover.color = autoCoverColor;

        TMP_Text keyText = CreateText("KeyText", rt, font, autoKeyFontSize, TextAlignmentOptions.TopLeft, 3f);
        keyText.text = key;

        TMP_Text time = CreateText("TimeText", rt, font, autoTimeFontSize, TextAlignmentOptions.Center, 0f);
        time.fontStyle = FontStyles.Bold;
        time.text = "";

        return new CooldownSlot { root = go, icon = icon, cover = cover, timeText = time };
    }

    Image CreateImage(string objName, RectTransform parent)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt, autoIconPadding);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    TMP_Text CreateText(string objName, RectTransform parent, TMP_FontAsset font, float size, TextAlignmentOptions align, float padding)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt, padding);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    void Stretch(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    void CheckSlot(CooldownSlot slot, string label)
    {
        if (slot.icon == null) Debug.LogWarning($"[HUD] {label} → Icon 칸이 비어 있어요.");
        if (slot.cover == null) Debug.LogWarning($"[HUD] {label} → Cover 칸이 비어 있어요.");
        if (slot.timeText == null) Debug.LogWarning($"[HUD] {label} → Time Text 칸이 비어 있어요.");
        if (label == "Bomb Slot" && slot.icon != null && slot.icon.sprite == null)
            Debug.LogWarning("[HUD] 폭탄 Icon에 그림(Source Image)이 없어요.");
    }

    void UpdatePlayer()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
            if (player != null) item = player.GetComponent<Item>();
        }
        if (player == null) return;

        SetBar(hpBar, player.HpRatio);
        if (hpBar != null) hpBar.color = player.HpRatio <= lowHpRatio ? hpLowColor : hpColor;
        SetText(hpText, $"HP {player.CurrentHp}/{player.MaxHp}");

        if (bombGaugeFill != null)
        {
            bombGaugeFill.fillAmount = 1f - player.BombCooldownRatio;
        }
    }

    void UpdateCooldowns()
    {
        if (player == null) return;

        SetCooldown(bombSlot, player.BombRemaining, player.BombCooldownRatio);

        DataManager data = DataManager.Instance;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            CooldownSlot slot = skillSlots[i];
            PartType type = data != null && i < data.quickSlots.Length ? data.quickSlots[i] : PartType.None;
            bool show = type != PartType.None;

            if (slot.root != null && slot.root.activeSelf != show) slot.root.SetActive(show);
            if (!show) continue;

            SetIcon(slot, IconOf(type));

            float remain = item != null ? item.Remaining(type) : 0f;
            float ratio = item != null ? item.CooldownRatio(type) : 0f;
            SetCooldown(slot, remain, ratio);
        }
    }

    Sprite IconOf(PartType type)
    {
        foreach (PartIcon p in partIcons)
        {
            if (p.type == type) return p.icon;
        }

        return null;
    }

    void SetIcon(CooldownSlot slot, Sprite sprite)
    {
        if (slot.icon == null || sprite == null || slot.icon.sprite == sprite) return;

        slot.icon.sprite = sprite;
        PrepareCover(slot);
    }

    void PrepareCover(CooldownSlot slot)
    {
        if (slot.cover == null) return;

        if (slot.icon != null)
        {
            slot.cover.sprite = slot.icon.sprite;
            slot.cover.preserveAspect = slot.icon.preserveAspect;
        }

        slot.cover.type = Image.Type.Filled;
        slot.cover.fillMethod = Image.FillMethod.Radial360;
        slot.cover.fillOrigin = (int)Image.Origin360.Top;
        slot.cover.fillClockwise = false;
        slot.cover.raycastTarget = false;
        slot.cover.fillAmount = 0f;
    }

    void SetCooldown(CooldownSlot slot, float remain, float ratio)
    {
        bool cooling = remain > 0f;

        if (slot.cover != null) slot.cover.fillAmount = Mathf.Clamp01(ratio);
        if (slot.icon != null) slot.icon.color = cooling ? iconCoolingColor : iconReadyColor;
        SetText(slot.timeText, cooling ? Mathf.CeilToInt(remain).ToString() : "");
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