using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSystemExtension : MonoBehaviour
{
    public static GameSystemExtension Instance { get; private set; }

    private const string ValidSaveKey = "SpaceWar_ValidSave";
    private const string SavedStageKey = "SpaceWar_SavedStage";
    private const string ShipColorIndexKey = "SpaceWar_ShipColor";

    [Header("★ UI 에셋 스프라이트 & 폰트 (비워두면 씬의 버튼/상점에서 자동 추출!) ★")]
    public Sprite panelSprite;
    public Sprite buttonSprite;
    public TMP_FontAsset koreanFont;

    [Header("엔딩")]
    [Tooltip("켜면 랭킹 입력 뒤 이 스크립트의 스토리 창 대신 StageManager의 엔딩 화면(직접 만든 UI)을 사용")]
    public bool useManualEnding = true;

    [Header("2~3스테이지 장애물(운석) 스폰")]
    public GameObject obstaclePrefab;
    public int maxObstacles = 3;
    public float obstacleSpawnInterval = 4f;
    private float obstacleTimer = 0f;

    [Header("3스테이지 시야 제한 설정")]
    public bool enableDarkVision = true;
    public float visionSize = 7f;

    // 기존 씬 직렬화 호환용 변수 (건드리지 않으셔도 됩니다)
    [HideInInspector] public GUISkin customGuiSkin;
    [HideInInspector] public Font customKoreanFont;

    public bool IsEndingProcessDone { get; private set; } = false;

    // 3스테이지 암흑 시야 제한용 캔버스
    private Canvas darkCanvas;
    private RawImage darkImage;
    private Texture2D darkTexture;
    private Transform playerTransform;

    // 정식 uGUI 시스템 캔버스 및 패널들 (OnGUI 완전 대체!)
    private Canvas uiCanvas;
    private GameObject topMenuBar;
    private Button continueBtn;
    private TextMeshProUGUI shipColorBtnText;

    private GameObject modalBlocker; // 뒤쪽 버튼 이중 클릭을 100% 막아주는 레이캐스트 차단막
    private GameObject rankPanel;
    private TextMeshProUGUI rankListText;
    private GameObject helpPanel;
    private GameObject settingsPanel;
    private TextMeshProUGUI bgmLabel;
    private TextMeshProUGUI sfxLabel;
    private TextMeshProUGUI screenModeBtnText;

    private GameObject pausePanel;
    private GameObject deathPanel;
    private TextMeshProUGUI deathStageText;

    private GameObject endingInputPanel;
    private TextMeshProUGUI endingScoreText;
    private TMP_InputField playerIdInput;
    private TextMeshProUGUI rankWarningText;
    private GameObject endingStoryPanel;

    private bool isPaused = false;
    private bool isDeathOpen = false;

    // 기체 도색 컬러 프리셋 5종
    private readonly Color[] shipColors = new Color[]
    {
        new Color(1f, 1f, 1f, 1f),       // 0: 오리지널
        new Color(0f, 1f, 0.87f, 1f),    // 1: 네온 민트
        new Color(1f, 0.35f, 0.35f, 1f), // 2: 크림슨 레드
        new Color(1f, 0.85f, 0.2f, 1f),  // 3: 골드 옐로우
        new Color(0.7f, 0.4f, 1f, 1f)    // 4: 네뷸라 퍼플
    };
    private readonly string[] shipColorNames = { "오리지널", "네온 민트", "크림슨 레드", "골드 옐로우", "네뷸라 퍼플" };
    private int currentColorIndex = 0;

    // =========================================================================
    // [승후 님 구현 유지] 랭킹 아이디 욕설 검열 시스템
    // =========================================================================
    private static readonly string[] BadWords =
    {
        "씨발", "시발", "씨바", "씨빨", "시빨", "씨팔", "시팔", "십팔", "ㅅㅂ", "ㅆㅂ", "ㅅㅃ",
        "병신", "븅신", "빙신", "ㅂㅅ", "ㅄ",
        "좆", "존나", "졸라", "ㅈㄴ",
        "개새", "새끼", "섀끼", "ㅅㄲ",
        "미친놈", "미친년", "미친새", "ㅁㅊ",
        "지랄", "ㅈㄹ", "닥쳐", "꺼져",
        "엠창", "느금", "니애미", "니미", "애미", "애비", "창녀",
        "tlqkf", "qudtls", "whssk",
        "fuck", "fck", "fuk", "shit", "bitch", "dick", "pussy", "asshole", "bastard"
    };

    private static string NormalizeForFilter(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder sb = new StringBuilder(s.Length);
        foreach (char c in s.ToLowerInvariant())
        {
            if (char.IsLetter(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    public static bool ContainsBadWord(string name)
    {
        string normalized = NormalizeForFilter(name);
        if (normalized.Length == 0) return false;
        foreach (string word in BadWords)
        {
            if (normalized.Contains(word)) return true;
        }
        return false;
    }

    private static string MaskBadName(string name)
    {
        return ContainsBadWord(name) ? "***" : name;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentColorIndex = PlayerPrefs.GetInt(ShipColorIndexKey, 0);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        CheckSceneSetup(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckSceneSetup(scene.name);
    }

    private void CheckSceneSetup(string sceneName)
    {
        isPaused = false;
        isDeathOpen = false;
        Time.timeScale = 1f;

        // 1. 현재 씬의 기존 UI(StartButton, ShopPanel 등)에서 에셋 스토어 이미지와 한글 TMP 폰트 자동 추출!
        ExtractAssetsFromCurrentScene();

        // 2. 정식 Canvas UI 생성 또는 갱신
        BuildOrRefreshSystemCanvas(sceneName);

        // 3. 씬 내 모든 버튼에 클릭 효과음 연결
        AttachClickSoundToAllButtons();

        if (sceneName == "MainMenu")
        {
            if (darkCanvas != null) darkCanvas.gameObject.SetActive(false);

            GameObject startBtnObj = GameObject.Find("StartButton");
            if (startBtnObj != null && startBtnObj.TryGetComponent(out Button startBtn))
            {
                startBtn.onClick.RemoveListener(StartNewGameFromMenu);
                startBtn.onClick.AddListener(StartNewGameFromMenu);
            }

            ApplyShipColorInMenu();
        }
        else if (sceneName == "TeamProject")
        {
            FixMessageTextKoreanFont();
            CreateDarknessUI();
            ApplyShipColorInGame();
        }
    }

    // 씬에 이미 적용해 두신 에셋 스토어 스프라이트와 한글 폰트를 자동으로 가져옵니다.
    private void ExtractAssetsFromCurrentScene()
    {
        if (koreanFont == null)
        {
            foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.font != null && !t.font.name.Contains("Liberation"))
                {
                    koreanFont = t.font;
                    break;
                }
            }
        }

        if (buttonSprite == null)
        {
            GameObject startBtn = GameObject.Find("StartButton");
            if (startBtn != null && startBtn.TryGetComponent(out Image img) && img.sprite != null)
                buttonSprite = img.sprite;

            if (buttonSprite == null)
            {
                GameObject closeBtn = GameObject.Find("CloseButton");
                if (closeBtn != null && closeBtn.TryGetComponent(out Image cImg) && cImg.sprite != null)
                    buttonSprite = cImg.sprite;
            }
        }

        if (panelSprite == null)
        {
            GameObject shopPanel = GameObject.Find("ShopPanel");
            if (shopPanel != null && shopPanel.TryGetComponent(out Image pImg) && pImg.sprite != null)
                panelSprite = pImg.sprite;
        }
    }

    // =========================================================================
    // ★ 구식 OnGUI를 대체하는 정식 Canvas(uGUI) 시스템 자동 생성기 ★
    // =========================================================================
    private void BuildOrRefreshSystemCanvas(string sceneName)
    {
        if (uiCanvas == null)
        {
            GameObject canvasObj = new GameObject("SystemUICanvas");
            DontDestroyOnLoad(canvasObj);

            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 100; // 최상단에 표시되어 입력을 정확히 처리

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>(); // 마우스 클릭을 정식으로 처리 (이중 클릭 방지!)

            // 1. 메인 메뉴 상단 바 생성
            BuildTopMenuBar(canvasObj.transform);

            // 2. 팝업창 뒤쪽 클릭을 완벽히 차단하는 암전 배경(Blocker) 생성
            modalBlocker = CreateUIImage("ModalBlocker", canvasObj.transform, null, new Color(0f, 0f, 0f, 0.78f));
            StretchFull(modalBlocker.GetComponent<RectTransform>());
            modalBlocker.GetComponent<Image>().raycastTarget = true; // 뒤쪽 START 버튼 등이 절대 안 눌리게 방어!

            // 3. 각 팝업 패널 생성 (랭킹, 도움말, 설정, 일시정지, 사망, 엔딩)
            BuildRankPanel(modalBlocker.transform);
            BuildHelpPanel(modalBlocker.transform);
            BuildSettingsPanel(modalBlocker.transform);
            BuildPausePanel(modalBlocker.transform);
            BuildDeathPanel(modalBlocker.transform);
            BuildEndingPanels(modalBlocker.transform);
        }
        else
        {
            // 새로 추출된 스프라이트나 폰트가 있다면 업데이트
            RefreshVisualAssets();
        }

        CloseAllModals();

        // 메인 메뉴일 때만 상단 메뉴 바 표시
        if (topMenuBar != null)
        {
            topMenuBar.SetActive(sceneName == "MainMenu");
            if (continueBtn != null) continueBtn.gameObject.SetActive(HasValidSave());
            if (shipColorBtnText != null) shipColorBtnText.text = $"기체 도색: {shipColorNames[currentColorIndex]}";
        }
    }

    private void BuildTopMenuBar(Transform parent)
    {
        topMenuBar = new GameObject("TopMenuBar", typeof(RectTransform));
        topMenuBar.transform.SetParent(parent, false);
        RectTransform rt = topMenuBar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(0f, 70f);

        HorizontalLayoutGroup layout = topMenuBar.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 5, 5);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        // 1) 이어하기 버튼 (세이브가 있을 때만 자동 활성화)
        continueBtn = CreateAssetButton("ContinueBtn", topMenuBar.transform, "이어하기", new Vector2(190, 58), () =>
        {
            if (DataManager.Instance != null)
            {
                DataManager.Instance.LoadGame();
                DataManager.Instance.currentStage = PlayerPrefs.GetInt(SavedStageKey, 1);
            }
            SceneManager.LoadScene("TeamProject");
        });

        // 2) 랭킹 버튼
        CreateAssetButton("RankBtn", topMenuBar.transform, "랭킹 (Top 5)", new Vector2(210, 58), OpenRankModal);

        // 3) 환경 설정 버튼
        CreateAssetButton("SettingsBtn", topMenuBar.transform, "환경 설정", new Vector2(190, 58), OpenSettingsModal);

        // 4) 조작 도움말 버튼
        CreateAssetButton("HelpBtn", topMenuBar.transform, "조작 도움말", new Vector2(210, 58), OpenHelpModal);

        // 5) 기체 도색 변경 버튼
        Button colorBtn = CreateAssetButton("ColorBtn", topMenuBar.transform, $"기체 도색: {shipColorNames[currentColorIndex]}", new Vector2(290, 58), () =>
        {
            SoundManager.Play(SoundManager.Sfx.Equip);
            currentColorIndex = (currentColorIndex + 1) % shipColors.Length;
            PlayerPrefs.SetInt(ShipColorIndexKey, currentColorIndex);
            PlayerPrefs.Save();
            if (shipColorBtnText != null) shipColorBtnText.text = $"기체 도색: {shipColorNames[currentColorIndex]}";
            ApplyShipColorInMenu();
        });
        shipColorBtnText = colorBtn.GetComponentInChildren<TextMeshProUGUI>();

        // 6) 게임 종료 버튼
        CreateAssetButton("QuitBtn", topMenuBar.transform, "게임 종료", new Vector2(180, 58), () => Application.Quit());
    }

    private void BuildRankPanel(Transform parent)
    {
        rankPanel = CreateAssetWindow("RankPanel", parent, "[ 명예의 전당 (TOP 5) ]", new Vector2(720, 540));

        rankListText = CreateTMPText("RankListText", rankPanel.transform, "", 28, TextAlignmentOptions.Center);
        rankListText.rectTransform.anchoredPosition = new Vector2(0, 15);
        rankListText.rectTransform.sizeDelta = new Vector2(640, 320);

        Button closeBtn = CreateAssetButton("CloseBtn", rankPanel.transform, "닫기", new Vector2(220, 65), CloseAllModals);
        closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -200);
    }

    private void BuildHelpPanel(Transform parent)
    {
        helpPanel = CreateAssetWindow("HelpPanel", parent, "[ 게임 스토리 및 조작 도움말 ]", new Vector2(860, 580));

        string helpMsg = "<color=#00FFFF>[스토리 : 미래 3024년]</color>\n" +
                         "항로를 이탈한 주인공 우주선이 외계 비행체의 유도 미사일을 피해\n" +
                         "적끼리 맞추며 3개의 스테이지를 탈출하는 게임입니다!\n\n" +
                         "<color=#FFD700>[조작 방법]</color>\n" +
                         "• 방향키 (좌, 우) : 우주선 회전   /   Left Shift : 감속 회전\n" +
                         "• Q 키 : 비상 폭탄 (화면 내 모든 탄막 제거)\n" +
                         "• 숫자 1, 2 키 : 상점에서 장착한 파츠 스킬 발동\n" +
                         "• ESC 키 : 일시정지 및 환경 설정 / 저장\n" +
                         "• F6 키 : 세이브 및 랭킹 데이터 초기화 치트";

        TextMeshProUGUI body = CreateTMPText("HelpBody", helpPanel.transform, helpMsg, 25, TextAlignmentOptions.Center);
        body.rectTransform.anchoredPosition = new Vector2(0, 15);
        body.rectTransform.sizeDelta = new Vector2(780, 360);

        Button closeBtn = CreateAssetButton("CloseBtn", helpPanel.transform, "닫기", new Vector2(220, 65), CloseAllModals);
        closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -220);
    }

    private void BuildSettingsPanel(Transform parent)
    {
        settingsPanel = CreateAssetWindow("SettingsPanel", parent, "[ 환경 설정 (SETTINGS) ]", new Vector2(700, 540));

        // 1) BGM 볼륨 조절 행
        bgmLabel = CreateTMPText("BgmLabel", settingsPanel.transform, "배경음악(BGM) 음량 : 50%", 28, TextAlignmentOptions.Center);
        bgmLabel.rectTransform.anchoredPosition = new Vector2(0, 120);
        bgmLabel.rectTransform.sizeDelta = new Vector2(400, 50);

        Button bgmDown = CreateAssetButton("BgmDown", settingsPanel.transform, "- 10%", new Vector2(140, 55), () => ChangeVolume(true, -0.1f));
        bgmDown.GetComponent<RectTransform>().anchoredPosition = new Vector2(-110, 60);

        Button bgmUp = CreateAssetButton("BgmUp", settingsPanel.transform, "+ 10%", new Vector2(140, 55), () => ChangeVolume(true, 0.1f));
        bgmUp.GetComponent<RectTransform>().anchoredPosition = new Vector2(110, 60);

        // 2) SFX 볼륨 조절 행
        sfxLabel = CreateTMPText("SfxLabel", settingsPanel.transform, "효과음(SFX) 음량 : 100%", 28, TextAlignmentOptions.Center);
        sfxLabel.rectTransform.anchoredPosition = new Vector2(0, -15);
        sfxLabel.rectTransform.sizeDelta = new Vector2(400, 50);

        Button sfxDown = CreateAssetButton("SfxDown", settingsPanel.transform, "- 10%", new Vector2(140, 55), () => ChangeVolume(false, -0.1f));
        sfxDown.GetComponent<RectTransform>().anchoredPosition = new Vector2(-110, -75);

        Button sfxUp = CreateAssetButton("SfxUp", settingsPanel.transform, "+ 10%", new Vector2(140, 55), () => ChangeVolume(false, 0.1f));
        sfxUp.GetComponent<RectTransform>().anchoredPosition = new Vector2(110, -75);

        // 3) 화면 모드 전환 버튼
        Button screenBtn = CreateAssetButton("ScreenModeBtn", settingsPanel.transform, "화면 모드 전환", new Vector2(440, 60), () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
            RefreshSettingsTexts();
        });
        screenBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -150);
        screenModeBtnText = screenBtn.GetComponentInChildren<TextMeshProUGUI>();

        // 4) 닫기 버튼
        Button closeBtn = CreateAssetButton("CloseBtn", settingsPanel.transform, "닫기", new Vector2(220, 65), () =>
        {
            if (isPaused) OpenPauseModal();
            else CloseAllModals();
        });
        closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -225);
    }

    private void BuildPausePanel(Transform parent)
    {
        pausePanel = CreateAssetWindow("PausePanel", parent, "[ 일시정지 (PAUSED) ]", new Vector2(600, 480));

        Button resumeBtn = CreateAssetButton("ResumeBtn", pausePanel.transform, "게임 이어하기", new Vector2(420, 70), () =>
        {
            isPaused = false;
            Time.timeScale = 1f;
            CloseAllModals();
        });
        resumeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 75);

        Button settingBtn = CreateAssetButton("SettingBtn", pausePanel.transform, "환경 설정", new Vector2(420, 70), OpenSettingsModal);
        settingBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -20);

        Button saveExitBtn = CreateAssetButton("SaveExitBtn", pausePanel.transform, "저장 후 메인메뉴로", new Vector2(420, 70), () =>
        {
            SaveCurrentProgress();
            isPaused = false;
            Time.timeScale = 1f;
            CloseAllModals();
            SceneManager.LoadScene("MainMenu");
        });
        saveExitBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -115);
    }

    private void BuildDeathPanel(Transform parent)
    {
        deathPanel = CreateAssetWindow("DeathPanel", parent, "[ 우주선 파괴 (GAME OVER) ]", new Vector2(640, 520));

        deathStageText = CreateTMPText("DeathStageText", deathPanel.transform, "", 26, TextAlignmentOptions.Center);
        deathStageText.rectTransform.anchoredPosition = new Vector2(0, 125);
        deathStageText.rectTransform.sizeDelta = new Vector2(560, 70);

        Button retryBtn = CreateAssetButton("RetryBtn", deathPanel.transform, "다시 시작하기 (현재 스테이지)", new Vector2(460, 68), () =>
        {
            if (DataManager.Instance != null) DataManager.Instance.currentStage = StageManager.CurrentStage;
            isDeathOpen = false;
            Time.timeScale = 1f;
            CloseAllModals();
            SceneManager.LoadScene("TeamProject");
        });
        retryBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 35);

        Button saveMenuBtn = CreateAssetButton("SaveMenuBtn", deathPanel.transform, "저장 후 돌아가기", new Vector2(460, 68), () =>
        {
            SaveCurrentProgress();
            isDeathOpen = false;
            Time.timeScale = 1f;
            CloseAllModals();
            SceneManager.LoadScene("MainMenu");
        });
        saveMenuBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -55);

        Button noSaveMenuBtn = CreateAssetButton("NoSaveMenuBtn", deathPanel.transform, "저장하지 않고 돌아가기", new Vector2(460, 68), () =>
        {
            ClearSaveData();
            ResetDataForNewGame();
            isDeathOpen = false;
            Time.timeScale = 1f;
            CloseAllModals();
            SceneManager.LoadScene("MainMenu");
        });
        noSaveMenuBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -145);
    }

    private void BuildEndingPanels(Transform parent)
    {
        // 1) Top 5 랭킹 아이디 입력창 (정식 TMP_InputField)
        endingInputPanel = CreateAssetWindow("EndingInputPanel", parent, "[ 축하합니다! TOP 5 기록 달성! ]", new Vector2(700, 480));

        endingScoreText = CreateTMPText("EndingScoreText", endingInputPanel.transform, "", 26, TextAlignmentOptions.Center);
        endingScoreText.rectTransform.anchoredPosition = new Vector2(0, 100);
        endingScoreText.rectTransform.sizeDelta = new Vector2(600, 80);

        // 입력 필드 배경
        GameObject inputBg = CreateUIImage("InputFieldBg", endingInputPanel.transform, null, new Color(0.1f, 0.15f, 0.25f, 0.95f));
        RectTransform inputRt = inputBg.GetComponent<RectTransform>();
        inputRt.anchoredPosition = new Vector2(0, 5);
        inputRt.sizeDelta = new Vector2(420, 65);

        playerIdInput = inputBg.AddComponent<TMP_InputField>();
        playerIdInput.characterLimit = 12;

        TextMeshProUGUI inputText = CreateTMPText("Text", inputBg.transform, "", 30, TextAlignmentOptions.Center);
        StretchFull(inputText.rectTransform);
        playerIdInput.textViewport = inputRt;
        playerIdInput.textComponent = inputText;

        TextMeshProUGUI placeholder = CreateTMPText("Placeholder", inputBg.transform, "아이디 입력...", 26, TextAlignmentOptions.Center);
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);
        StretchFull(placeholder.rectTransform);
        playerIdInput.placeholder = placeholder;

        rankWarningText = CreateTMPText("WarningText", endingInputPanel.transform, "", 22, TextAlignmentOptions.Center);
        rankWarningText.color = new Color(1f, 0.35f, 0.35f, 1f);
        rankWarningText.rectTransform.anchoredPosition = new Vector2(0, -55);
        rankWarningText.rectTransform.sizeDelta = new Vector2(640, 40);

        playerIdInput.onValueChanged.AddListener(_ => { if (rankWarningText != null) rankWarningText.text = ""; });

        Button submitBtn = CreateAssetButton("SubmitRankBtn", endingInputPanel.transform, "랭킹 등록 완료", new Vector2(280, 68), OnSubmitRankingId);
        submitBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -140);

        // 2) 지구 귀환 엔딩 스토리 창
        endingStoryPanel = CreateAssetWindow("EndingStoryPanel", parent, "[ 지구 귀환 성공 (ENDING) ]", new Vector2(820, 520));

        string endingStory = "외계 비행체들의 거센 유도 미사일 공격과 성운의 어둠을 뚫고\n" +
                             "마침내 정상 항로를 되찾는 데 성공했습니다!\n\n" +
                             "무사히 미래 3024년의 지구로 귀환한 주인공의 활약으로\n" +
                             "황폐해진 지구는 다시금 새로운 희망을 맞이하게 됩니다.\n\n" +
                             "<color=#00FFFF>플레이해 주셔서 감사합니다!</color>";

        TextMeshProUGUI storyBody = CreateTMPText("StoryBody", endingStoryPanel.transform, endingStory, 26, TextAlignmentOptions.Center);
        storyBody.rectTransform.anchoredPosition = new Vector2(0, 20);
        storyBody.rectTransform.sizeDelta = new Vector2(740, 320);

        Button endHomeBtn = CreateAssetButton("EndHomeBtn", endingStoryPanel.transform, "메인 메뉴로 이동", new Vector2(280, 68), () =>
        {
            ResetDataForNewGame();
            CloseAllModals();
            IsEndingProcessDone = true;
        });
        endHomeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -185);
    }

    // ---------------- UI 생성 도우미 함수들 (에셋 스토어 스킨 & 폰트 자동 적용) ----------------
    private GameObject CreateAssetWindow(string name, Transform parent, string title, Vector2 size)
    {
        Color bgCol = panelSprite != null ? new Color(0.08f, 0.12f, 0.2f, 0.96f) : new Color(0.06f, 0.09f, 0.16f, 0.96f);
        GameObject win = CreateUIImage(name, parent, panelSprite, bgCol);
        RectTransform rt = win.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI titleTmp = CreateTMPText("Title", win.transform, title, 34, TextAlignmentOptions.Center);
        titleTmp.color = new Color(0f, 1f, 1f, 1f);
        titleTmp.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        titleTmp.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        titleTmp.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleTmp.rectTransform.anchoredPosition = new Vector2(0f, -25f);
        titleTmp.rectTransform.sizeDelta = new Vector2(size.x - 40f, 60f);

        return win;
    }

    private Button CreateAssetButton(string name, Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = CreateUIImage(name, parent, buttonSprite, Color.white);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        if (buttonSprite != null) img.type = Image.Type.Sliced;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.76f, 0.85f, 0.88f, 1f); // StartButton과 동일한 네온 톤
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.32f, 0.53f, 0.61f, 1f);
        btn.colors = cb;

        TextMeshProUGUI txt = CreateTMPText("Label", btnObj.transform, label, 23, TextAlignmentOptions.Center);
        StretchFull(txt.rectTransform);
        txt.raycastTarget = false;

        btn.onClick.AddListener(() =>
        {
            SoundManager.Play(SoundManager.Sfx.Click);
            onClick?.Invoke();
        });

        return btn;
    }

    private GameObject CreateUIImage(string name, Transform parent, Sprite sp, Color col)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sp;
        img.color = col;
        return go;
    }

    private TextMeshProUGUI CreateTMPText(string name, Transform parent, string content, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (koreanFont != null) tmp.font = koreanFont;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.color = Color.white;
        return tmp;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void RefreshVisualAssets()
    {
        if (uiCanvas == null) return;
        foreach (TextMeshProUGUI tmp in uiCanvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (koreanFont != null) tmp.font = koreanFont;
        }
        foreach (Button btn in uiCanvas.GetComponentsInChildren<Button>(true))
        {
            if (buttonSprite != null && btn.TryGetComponent(out Image img))
            {
                img.sprite = buttonSprite;
                img.type = Image.Type.Sliced;
            }
        }
    }

    // ---------------- 팝업창 열기 / 닫기 제어 함수 ----------------
    private void CloseAllModals()
    {
        if (modalBlocker == null) return;
        rankPanel.SetActive(false);
        helpPanel.SetActive(false);
        settingsPanel.SetActive(false);
        pausePanel.SetActive(false);
        deathPanel.SetActive(false);
        endingInputPanel.SetActive(false);
        endingStoryPanel.SetActive(false);
        modalBlocker.SetActive(false);
    }

    private void OpenRankModal()
    {
        CloseAllModals();
        modalBlocker.SetActive(true);
        rankPanel.SetActive(true);

        if (DataManager.Instance == null || DataManager.Instance.rankList.Count == 0)
        {
            rankListText.text = "아직 등록된 랭킹 기록이 없습니다.";
        }
        else
        {
            StringBuilder sb = new StringBuilder();
            var list = DataManager.Instance.rankList;
            for (int i = 0; i < list.Count; i++)
            {
                sb.AppendLine($"{i + 1}위   |   ID: {MaskBadName(list[i].playerName)}   |   {list[i].score:N0} 점\n");
            }
            rankListText.text = sb.ToString();
        }
    }

    private void OpenHelpModal()
    {
        CloseAllModals();
        modalBlocker.SetActive(true);
        helpPanel.SetActive(true);
    }

    private void OpenSettingsModal()
    {
        CloseAllModals();
        modalBlocker.SetActive(true);
        settingsPanel.SetActive(true);
        RefreshSettingsTexts();
    }

    private void OpenPauseModal()
    {
        CloseAllModals();
        modalBlocker.SetActive(true);
        pausePanel.SetActive(true);
    }

    public void OpenDeathMenu()
    {
        SoundManager.Play(SoundManager.Sfx.GameOver);
        isDeathOpen = true;
        isPaused = false;
        CloseAllModals();
        modalBlocker.SetActive(true);
        deathPanel.SetActive(true);
        if (deathStageText != null)
            deathStageText.text = $"현재 스테이지 : Stage {StageManager.CurrentStage}\n원하시는 진행 방식을 선택해 주세요.";
    }

    private void ChangeVolume(bool isBgm, float delta)
    {
        if (SoundManager.Instance == null) return;
        if (isBgm) SoundManager.Instance.BgmVolume = Mathf.Clamp01(SoundManager.Instance.BgmVolume + delta);
        else SoundManager.Instance.SfxVolume = Mathf.Clamp01(SoundManager.Instance.SfxVolume + delta);
        RefreshSettingsTexts();
    }

    private void RefreshSettingsTexts()
    {
        if (SoundManager.Instance != null)
        {
            if (bgmLabel != null) bgmLabel.text = $"배경음악(BGM) 음량 : {Mathf.RoundToInt(SoundManager.Instance.BgmVolume * 100)}%";
            if (sfxLabel != null) sfxLabel.text = $"효과음(SFX) 음량 : {Mathf.RoundToInt(SoundManager.Instance.SfxVolume * 100)}%";
        }
        if (screenModeBtnText != null)
        {
            screenModeBtnText.text = Screen.fullScreen ? "현재: 전체화면 (클릭 시 창모드)" : "현재: 창모드 (클릭 시 전체화면)";
        }
    }

    private void OnSubmitRankingId()
    {
        if (DataManager.Instance == null) return;
        string raw = playerIdInput != null ? playerIdInput.text : "";
        string finalName = string.IsNullOrWhiteSpace(raw) ? "PLAYER" : raw.Trim();

        if (ContainsBadWord(finalName))
        {
            SoundManager.Play(SoundManager.Sfx.Deny);
            if (rankWarningText != null) rankWarningText.text = "부적절한 단어가 포함된 아이디는 등록할 수 없습니다.";
            if (playerIdInput != null) playerIdInput.text = "";
        }
        else
        {
            DataManager.Instance.AddRanking(finalName, DataManager.Instance.currentScore);
            ResetDataForNewGame();
            CloseAllModals();

            if (useManualEnding)
            {
                IsEndingProcessDone = true;
                return;
            }

            modalBlocker.SetActive(true);
            endingStoryPanel.SetActive(true);
        }
    }

    // ---------------- 인게임 로직 (운석, 3스테이지 시야 제한, ESC 일시정지) ----------------
    private void AttachClickSoundToAllButtons()
    {
        foreach (Button btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (uiCanvas != null && btn.transform.IsChildOf(uiCanvas.transform)) continue;
            btn.onClick.RemoveListener(PlayButtonClickSfx);
            btn.onClick.AddListener(PlayButtonClickSfx);
        }
    }

    private void PlayButtonClickSfx() => SoundManager.Play(SoundManager.Sfx.Click);

    private void ApplyShipColorInMenu()
    {
        GameObject shipImg = GameObject.Find("PlayerShip");
        if (shipImg != null && shipImg.TryGetComponent(out Image img))
            img.color = shipColors[currentColorIndex];
    }

    private void ApplyShipColorInGame()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            SpriteRenderer sr = playerObj.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = shipColors[currentColorIndex];
        }
    }

    private void FixMessageTextKoreanFont()
    {
        GameObject msgObj = GameObject.Find("MessageText");
        if (msgObj != null && koreanFont != null && msgObj.TryGetComponent(out TMP_Text msgText))
        {
            msgText.font = koreanFont;
        }
    }

    private void StartNewGameFromMenu() => ResetDataForNewGame();

    private void ResetDataForNewGame()
    {
        if (DataManager.Instance == null) return;
        DataManager.Instance.currentStage = 1;
        DataManager.Instance.currentScore = 0;
        DataManager.Instance.money = 0;
        DataManager.Instance.ownedParts.Clear();
        DataManager.Instance.quickSlots[0] = PartType.None;
        DataManager.Instance.quickSlots[1] = PartType.None;
    }

    public void SaveCurrentProgress()
    {
        int currentStage = StageManager.CurrentStage;
        if (currentStage < 1) currentStage = 1;

        if (DataManager.Instance != null)
        {
            DataManager.Instance.currentStage = currentStage;
            DataManager.Instance.SaveGame();
        }

        PlayerPrefs.SetInt(ValidSaveKey, 1);
        PlayerPrefs.SetInt(SavedStageKey, currentStage);
        PlayerPrefs.Save();
    }

    public void ClearSaveData()
    {
        PlayerPrefs.DeleteKey(ValidSaveKey);
        PlayerPrefs.DeleteKey(SavedStageKey);
        PlayerPrefs.DeleteKey("SpaceShooter_SaveData");
        PlayerPrefs.Save();

        if (continueBtn != null) continueBtn.gameObject.SetActive(false);
    }

    public void ClearAllData()
    {
        ClearSaveData();

        if (DataManager.Instance != null) DataManager.Instance.rankList.Clear();
        PlayerPrefs.DeleteKey("SpaceShooter_RankData");
        PlayerPrefs.Save();

        Debug.Log("[치트 F6] 세이브 데이터 및 랭킹이 완전히 초기화되었습니다!");
    }

    private bool HasValidSave() => PlayerPrefs.GetInt(ValidSaveKey, 0) == 1;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6))
        {
            ClearAllData();
            ResetDataForNewGame();
            SoundManager.Play(SoundManager.Sfx.CheatDebug); // [수정] 데이터 초기화 치트 사운드로 정밀화
        }

        if (SceneManager.GetActiveScene().name != "TeamProject") return;

        GameObject shopPanel = GameObject.Find("ShopPanel");
        bool isShopOpen = shopPanel != null && shopPanel.activeInHierarchy;
        bool isEndingOpen = (endingInputPanel != null && endingInputPanel.activeSelf) || (endingStoryPanel != null && endingStoryPanel.activeSelf);

        if (!isShopOpen && !isDeathOpen && !isEndingOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                isPaused = !isPaused;
                Time.timeScale = isPaused ? 0f : 1f;
                if (isPaused) OpenPauseModal();
                else CloseAllModals();
            }
        }

        int stage = StageManager.CurrentStage;
        if (DataManager.Instance != null && DataManager.Instance.currentStage > stage)
            stage = DataManager.Instance.currentStage;

        if (stage >= 2 && obstaclePrefab != null && !isPaused && !isDeathOpen)
        {
            obstacleTimer += Time.deltaTime;
            if (obstacleTimer >= obstacleSpawnInterval)
            {
                obstacleTimer = 0f;
                int count = FindObjectsByType<Obstacle>(FindObjectsSortMode.None).Length;
                if (count < maxObstacles)
                {
                    Vector2 pos = new Vector2(Random.Range(-7.5f, 7.5f), Random.Range(-4f, 4f));
                    Instantiate(obstaclePrefab, pos, Quaternion.identity);
                }
            }
        }

        if (darkCanvas == null) CreateDarknessUI();

        if (darkCanvas != null)
        {
            bool activeDark = (stage >= 3) && enableDarkVision && !isShopOpen && !isPaused && !isDeathOpen && !isEndingOpen;
            if (darkCanvas.gameObject.activeSelf != activeDark)
                darkCanvas.gameObject.SetActive(activeDark);

            if (activeDark && Camera.main != null)
            {
                if (playerTransform == null)
                {
                    GameObject p = GameObject.FindWithTag("Player");
                    if (p != null) playerTransform = p.transform;
                }
                if (playerTransform != null)
                {
                    Vector3 viewPos = Camera.main.WorldToViewportPoint(playerTransform.position);
                    Vector2 offset = new Vector2(viewPos.x - 0.5f, viewPos.y - 0.5f);
                    darkImage.rectTransform.anchoredPosition = new Vector2(offset.x * 1920f, offset.y * 1080f);
                }
            }
        }
    }

    private void CreateDarknessUI()
    {
        if (darkCanvas != null) return;

        GameObject canvasObj = new GameObject("Stage3_DarkCanvas");
        DontDestroyOnLoad(canvasObj);
        darkCanvas = canvasObj.AddComponent<Canvas>();
        darkCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        darkCanvas.sortingOrder = -1;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imgObj = new GameObject("DarkVignetteImage");
        imgObj.transform.SetParent(canvasObj.transform, false);
        darkImage = imgObj.AddComponent<RawImage>();
        darkImage.raycastTarget = false;
        darkImage.rectTransform.sizeDelta = new Vector2(4000, 4000);

        int size = 256;
        darkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        darkTexture.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radiusRatio = 0.22f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float alpha = Mathf.SmoothStep(0f, 0.96f, (dist - radiusRatio * 0.5f) / radiusRatio);
                darkTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }
        darkTexture.Apply();
        darkImage.texture = darkTexture;
        canvasObj.SetActive(false);
    }

    public void StartVictorySequence()
    {
        IsEndingProcessDone = false;
        int finalScore = DataManager.Instance != null ? DataManager.Instance.currentScore : 0;
        ClearSaveData();
        CloseAllModals();
        modalBlocker.SetActive(true);

        if (DataManager.Instance != null && DataManager.Instance.IsTop5Score(finalScore))
        {
            endingInputPanel.SetActive(true);
            if (endingScoreText != null)
                endingScoreText.text = $"최종 점수 : {finalScore:N0} 점\n명예의 전당에 등록할 아이디(ID)를 입력해 주세요.";
            if (playerIdInput != null) playerIdInput.text = "";
            if (rankWarningText != null) rankWarningText.text = "";
        }
        else if (useManualEnding)
        {
            ResetDataForNewGame();
            CloseAllModals();
            IsEndingProcessDone = true;
        }
        else
        {
            endingStoryPanel.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}