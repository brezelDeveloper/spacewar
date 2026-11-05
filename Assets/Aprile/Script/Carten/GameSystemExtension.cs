using System.Collections;
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

    [Header("★ UI 에셋 스킨 연결 (Demo 씬의 GUISkin 연결!) ★")]
    [Tooltip("에셋 폴더에서 .guiskin 파일을 찾아 여기에 넣으면 모든 코드 UI가 에셋 디자인으로 바뀝니다!")]
    public GUISkin customGuiSkin;
    [Tooltip("글자가 안 보일 때를 대비한 한글 폰트 (.ttf / .otf)")]
    public Font customKoreanFont;

    [Header("2~3스테이지 장애물(운석) 스폰")]
    public GameObject obstaclePrefab;
    public int maxObstacles = 3;
    public float obstacleSpawnInterval = 4f;
    private float obstacleTimer = 0f;

    [Header("3스테이지 시야 제한 설정")]
    public bool enableDarkVision = true;
    public float visionSize = 7f;

    public bool IsEndingProcessDone { get; private set; } = false;

    private Canvas darkCanvas;
    private RawImage darkImage;
    private Texture2D darkTexture;
    private Transform playerTransform;

    private bool showRankWindow = false;
    private bool showHelpWindow = false;
    private bool showMenuSettings = false;
    private bool showEndingRankInput = false;
    private bool showEndingMessage = false;
    private bool showDeathMenu = false;
    private bool isPaused = false;
    private bool showSettingsInPause = false;

    private string inputPlayerId = "";

    // [추가 기능 1] 기체(우주선) 도색 컬러 프리셋 5종
    private readonly Color[] shipColors = new Color[]
    {
        new Color(0f, 1f, 0.87f, 1f),    // 0: 기본 네온 민트
        new Color(1f, 1f, 1f, 1f),       // 1: 오리지널 화이트
        new Color(1f, 0.35f, 0.35f, 1f), // 2: 크림슨 레드
        new Color(1f, 0.85f, 0.2f, 1f),  // 3: 골드 옐로우
        new Color(0.7f, 0.4f, 1f, 1f)    // 4: 네뷸라 퍼플
    };
    private readonly string[] shipColorNames = { "네온 민트", "오리지널", "크림슨 레드", "골드 옐로우", "네뷸라 퍼플" };
    private int currentColorIndex = 0;

    // =========================================================================
    // [승후 님 구현 유지] 랭킹 아이디 욕설 검열 시스템
    // - 비교 전에 소문자화 + 공백/숫자/특수문자 제거 → 우회 입력도 완벽 차단
    // =========================================================================
    private string rankInputWarning = "";

    private static readonly string[] BadWords =
    {
        // 한글
        "씨발", "시발", "씨바", "씨빨", "시빨", "씨팔", "시팔", "십팔", "ㅅㅂ", "ㅆㅂ", "ㅅㅃ",
        "병신", "븅신", "빙신", "ㅂㅅ", "ㅄ",
        "좆", "존나", "졸라", "ㅈㄴ",
        "개새", "새끼", "섀끼", "ㅅㄲ",
        "미친놈", "미친년", "미친새", "ㅁㅊ",
        "지랄", "ㅈㄹ", "닥쳐", "꺼져",
        "엠창", "느금", "니애미", "니미", "애미", "애비", "창녀",
        // 영문 자판으로 친 한글 욕 (예: tlqkf = 시발)
        "tlqkf", "qudtls", "whssk",
        // 영어
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
        showRankWindow = false;
        showHelpWindow = false;
        showMenuSettings = false;
        showEndingRankInput = false;
        showEndingMessage = false;
        showDeathMenu = false;
        isPaused = false;
        showSettingsInPause = false;
        rankInputWarning = "";
        Time.timeScale = 1f;

        // 씬 내의 모든 UI Button에 클릭 효과음 자동 연결
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

    private void AttachClickSoundToAllButtons()
    {
        foreach (Button btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            btn.onClick.RemoveListener(PlayButtonClickSfx);
            btn.onClick.AddListener(PlayButtonClickSfx);
        }
    }

    private void PlayButtonClickSfx()
    {
        SoundManager.Play(SoundManager.Sfx.Click);
    }

    private void ApplyShipColorInMenu()
    {
        // 새로 만든 PlayerShip 또는 기존 MenuBackground(미사일 이미지)에 도색 적용
        GameObject shipImg = GameObject.Find("PlayerShip");
        if (shipImg != null && shipImg.TryGetComponent(out Image img))
        {
            img.color = shipColors[currentColorIndex];
        }
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
        GameObject scoreObj = GameObject.Find("ScoreText");
        if (msgObj != null && scoreObj != null)
        {
            TMP_Text msgText = msgObj.GetComponent<TMP_Text>();
            TMP_Text scoreText = scoreObj.GetComponent<TMP_Text>();
            if (msgText != null && scoreText != null && scoreText.font != null)
            {
                msgText.font = scoreText.font;
            }
        }
    }

    private void StartNewGameFromMenu()
    {
        ResetDataForNewGame();
    }

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
        PlayerPrefs.DeleteKey("SaveStage");
        PlayerPrefs.DeleteKey("SaveScore");
        PlayerPrefs.DeleteKey("SaveMoney");
        PlayerPrefs.DeleteKey("SaveOwnedParts");
        PlayerPrefs.DeleteKey("SaveSlot0");
        PlayerPrefs.DeleteKey("SaveSlot1");
        PlayerPrefs.Save();

        if (DataManager.Instance != null && DataManager.Instance.rankList != null)
        {
            DataManager.Instance.rankList.Clear();
            PlayerPrefs.DeleteKey("RankList");
            PlayerPrefs.Save();
        }
        Debug.Log("[치트 F6] 세이브 데이터 및 랭킹이 완전히 초기화되었습니다!");
    }

    private bool HasValidSave()
    {
        return PlayerPrefs.GetInt(ValidSaveKey, 0) == 1;
    }

    public void OpenDeathMenu()
    {
        showDeathMenu = true;
        isPaused = false;
    }

    private void Update()
    {
        // [기획서 14] F6 치트키: 세이브 및 랭킹 데이터 즉시 초기화
        if (Input.GetKeyDown(KeyCode.F6))
        {
            ClearSaveData();
            ResetDataForNewGame();
            SoundManager.Play(SoundManager.Sfx.Buy);
        }

        if (SceneManager.GetActiveScene().name != "TeamProject") return;

        GameObject shopPanel = GameObject.Find("ShopPanel");
        bool isShopOpen = shopPanel != null && shopPanel.activeInHierarchy;

        if (!isShopOpen && !showDeathMenu && !showEndingRankInput && !showEndingMessage)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                isPaused = !isPaused;
                showSettingsInPause = false;
                Time.timeScale = isPaused ? 0f : 1f;
            }
        }

        int stage = StageManager.CurrentStage;
        if (DataManager.Instance != null && DataManager.Instance.currentStage > stage)
        {
            stage = DataManager.Instance.currentStage;
        }

        if (stage >= 2 && obstaclePrefab != null && !isPaused && !showDeathMenu)
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
            bool activeDark = (stage >= 3) && enableDarkVision && !isShopOpen && !isPaused && !showDeathMenu && !showEndingRankInput && !showEndingMessage;
            if (darkCanvas.gameObject.activeSelf != activeDark)
            {
                darkCanvas.gameObject.SetActive(activeDark);
            }

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
        inputPlayerId = "";
        rankInputWarning = "";

        int finalScore = DataManager.Instance != null ? DataManager.Instance.currentScore : 0;
        ClearSaveData();

        if (DataManager.Instance != null && DataManager.Instance.IsTop5Score(finalScore))
        {
            showEndingRankInput = true;
        }
        else
        {
            showEndingMessage = true;
        }
    }

    private void OnGUI()
    {
        // 에셋 스토어 데모 씬의 GUISkin 적용
        if (customGuiSkin != null)
        {
            GUI.skin = customGuiSkin;
        }

        string currentScene = SceneManager.GetActiveScene().name;

        GUIStyle solidBoxStyle = new GUIStyle(GUI.skin.box) { fontSize = 24, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
        solidBoxStyle.normal.textColor = Color.white;
        if (customKoreanFont != null) solidBoxStyle.font = customKoreanFont;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        btnStyle.normal.textColor = Color.white;
        if (customKoreanFont != null) btnStyle.font = customKoreanFont;

        GUIStyle textStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        textStyle.normal.textColor = Color.white;
        if (customKoreanFont != null) textStyle.font = customKoreanFont;

        // =========================================================================
        // 1. 메인 메뉴 화면 UI
        // =========================================================================
        if (currentScene == "MainMenu")
        {
            float startX = 20f;

            if (HasValidSave())
            {
                if (GUI.Button(new Rect(startX, 20, 140, 48), "이어하기", btnStyle))
                {
                    SoundManager.Play(SoundManager.Sfx.Click);
                    if (DataManager.Instance != null)
                    {
                        DataManager.Instance.LoadGame();
                        DataManager.Instance.currentStage = PlayerPrefs.GetInt(SavedStageKey, 1);
                    }
                    SceneManager.LoadScene("TeamProject");
                }
                startX += 150f;
            }

            if (GUI.Button(new Rect(startX, 20, 150, 48), "랭킹 (Top 5)", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                showRankWindow = !showRankWindow;
                showHelpWindow = false;
                showMenuSettings = false;
            }
            startX += 160f;

            if (GUI.Button(new Rect(startX, 20, 150, 48), "환경 설정", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                showMenuSettings = !showMenuSettings;
                showRankWindow = false;
                showHelpWindow = false;
            }
            startX += 160f;

            if (GUI.Button(new Rect(startX, 20, 150, 48), "조작 도움말", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                showHelpWindow = !showHelpWindow;
                showRankWindow = false;
                showMenuSettings = false;
            }
            startX += 160f;

            if (GUI.Button(new Rect(startX, 20, 220, 48), $"기체 도색: {shipColorNames[currentColorIndex]}", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Equip);
                currentColorIndex = (currentColorIndex + 1) % shipColors.Length;
                PlayerPrefs.SetInt(ShipColorIndexKey, currentColorIndex);
                PlayerPrefs.Save();
                ApplyShipColorInMenu();
            }
            startX += 230f;

            if (GUI.Button(new Rect(startX, 20, 130, 48), "게임 종료", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                Application.Quit();
            }

            GUI.Label(new Rect(20, Screen.height - 40, 320, 30), "치트 안내: [F6] 세이브/랭킹 초기화",
                new GUIStyle(textStyle) { fontSize = 14, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.yellow } });

            if (showRankWindow && DataManager.Instance != null)
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
                Rect rankRect = new Rect(Screen.width * 0.5f - 250, Screen.height * 0.5f - 200, 500, 400);
                GUI.Box(rankRect, "[ 명예의 전당 (TOP 5) ]", solidBoxStyle);

                var list = DataManager.Instance.rankList;
                if (list.Count == 0)
                {
                    GUI.Label(new Rect(rankRect.x, rankRect.y + 150, 500, 50), "아직 등록된 랭킹 기록이 없습니다.", textStyle);
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        // [승후 님 구현 유지] 이미 저장돼 있던 욕설 아이디도 *** 로 가려서 표시
                        string rankLine = $"{i + 1}위   |   ID: {MaskBadName(list[i].playerName)}   |   {list[i].score} 점";
                        GUI.Label(new Rect(rankRect.x + 30, rankRect.y + 70 + i * 50, 440, 40), rankLine, textStyle);
                    }
                }

                if (GUI.Button(new Rect(rankRect.x + 175, rankRect.y + 330, 150, 50), "닫기", btnStyle))
                {
                    SoundManager.Play(SoundManager.Sfx.Click);
                    showRankWindow = false;
                }
            }

            if (showMenuSettings)
            {
                DrawSettingsWindow(solidBoxStyle, textStyle, btnStyle, () => showMenuSettings = false);
            }

            if (showHelpWindow)
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
                Rect helpRect = new Rect(Screen.width * 0.5f - 300, Screen.height * 0.5f - 200, 600, 400);
                GUI.Box(helpRect, "[ 게임 스토리 및 조작 도움말 ]", solidBoxStyle);
                string helpMsg = "[스토리 (미래 3024년)]\n항로를 이탈한 주인공 우주선이 적의 유도 미사일을 피해\n적끼리 맞추며 3개의 스테이지를 탈출하는 게임입니다!\n\n" +
                                 "[조작 방법]\n- 방향키 (좌, 우) : 우주선 회전  /  Left Shift : 감속\n" +
                                 "- Q 키 : 비상 폭탄 (화면 내 모든 탄막 제거)\n" +
                                 "- 숫자 1, 2 키 : 상점에서 장착한 파츠 스킬 발동\n" +
                                 "- ESC 키 : 일시정지 및 환경 설정 / 저장\n" +
                                 "- F6 키 : 세이브 및 랭킹 데이터 초기화 치트";
                GUI.Label(new Rect(helpRect.x + 20, helpRect.y + 50, 560, 260), helpMsg, textStyle);

                if (GUI.Button(new Rect(helpRect.x + 225, helpRect.y + 330, 150, 50), "닫기", btnStyle))
                {
                    SoundManager.Play(SoundManager.Sfx.Click);
                    showHelpWindow = false;
                }
            }
        }

        // =========================================================================
        // 2. 인게임 사망(Game Over) 시 3가지 선택창
        // =========================================================================
        if (currentScene == "TeamProject" && showDeathMenu)
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            Rect dRect = new Rect(Screen.width * 0.5f - 240, Screen.height * 0.5f - 180, 480, 360);
            GUI.Box(dRect, "[ 우주선 파괴 (GAME OVER) ]", solidBoxStyle);

            GUI.Label(new Rect(dRect.x + 20, dRect.y + 55, 440, 50),
                $"현재 스테이지: Stage {StageManager.CurrentStage}\n원하시는 진행 방식을 선택해 주세요.", textStyle);

            if (GUI.Button(new Rect(dRect.x + 60, dRect.y + 125, 360, 55), "다시 시작하기", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                if (DataManager.Instance != null) DataManager.Instance.currentStage = StageManager.CurrentStage;
                showDeathMenu = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene("TeamProject");
            }

            if (GUI.Button(new Rect(dRect.x + 60, dRect.y + 195, 360, 55), "저장 후 돌아가기", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                SaveCurrentProgress();
                showDeathMenu = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene("MainMenu");
            }

            if (GUI.Button(new Rect(dRect.x + 60, dRect.y + 265, 360, 55), "저장하지 않고 돌아가기", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                ClearSaveData();
                ResetDataForNewGame();
                showDeathMenu = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene("MainMenu");
            }
        }

        // =========================================================================
        // 3. 인게임 ESC 일시정지 메뉴 & 환경 설정창
        // =========================================================================
        if (currentScene == "TeamProject" && isPaused && !showDeathMenu)
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            if (!showSettingsInPause)
            {
                Rect pRect = new Rect(Screen.width * 0.5f - 220, Screen.height * 0.5f - 170, 440, 340);
                GUI.Box(pRect, "[ 일시정지 (PAUSED) ]", solidBoxStyle);

                if (GUI.Button(new Rect(pRect.x + 50, pRect.y + 80, 340, 55), "이어하기", btnStyle))
                {
                    SoundManager.Play(SoundManager.Sfx.Click);
                    isPaused = false;
                    Time.timeScale = 1f;
                }

                if (GUI.Button(new Rect(pRect.x + 50, pRect.y + 155, 340, 55), "설정", btnStyle))
                {
                    SoundManager.Play(SoundManager.Sfx.Click);
                    showSettingsInPause = true;
                }

                if (GUI.Button(new Rect(pRect.x + 50, pRect.y + 230, 340, 55), "저장 후 돌아가기", btnStyle))
                {
                    SoundManager.Play(SoundManager.Sfx.Click);
                    SaveCurrentProgress();
                    isPaused = false;
                    Time.timeScale = 1f;
                    SceneManager.LoadScene("MainMenu");
                }
            }
            else
            {
                DrawSettingsWindow(solidBoxStyle, textStyle, btnStyle, () => showSettingsInPause = false);
            }
        }

        // =========================================================================
        // 4. 3스테이지 클리어 시 Top 5 아이디 입력창 (승후 님 욕설 검열 유지) & 엔딩 메시지
        // =========================================================================
        if (showEndingRankInput && DataManager.Instance != null)
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            Rect inRect = new Rect(Screen.width * 0.5f - 260, Screen.height * 0.5f - 150, 520, 300);
            GUI.Box(inRect, "[ 축하합니다! TOP 5 기록 달성! ]", solidBoxStyle);

            GUI.Label(new Rect(inRect.x + 20, inRect.y + 60, 480, 60),
                $"최종 점수: {DataManager.Instance.currentScore} 점\n랭킹에 등록할 아이디(ID)를 입력해 주세요:", textStyle);

            GUIStyle fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            if (customKoreanFont != null) fieldStyle.font = customKoreanFont;

            string newInput = GUI.TextField(new Rect(inRect.x + 110, inRect.y + 135, 300, 45), inputPlayerId, 12, fieldStyle);
            if (newInput != inputPlayerId) rankInputWarning = "";
            inputPlayerId = newInput;

            // [승후 님 구현 유지] 욕설 입력 시 경고 문구 표시
            if (!string.IsNullOrEmpty(rankInputWarning))
            {
                GUIStyle warnStyle = new GUIStyle(textStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
                warnStyle.normal.textColor = new Color(1f, 0.35f, 0.35f);
                GUI.Label(new Rect(inRect.x + 20, inRect.y + 182, 480, 26), rankInputWarning, warnStyle);
            }

            if (GUI.Button(new Rect(inRect.x + 160, inRect.y + 210, 200, 55), "랭킹 등록 완료", btnStyle))
            {
                string finalName = string.IsNullOrWhiteSpace(inputPlayerId) ? "PLAYER" : inputPlayerId.Trim();

                if (ContainsBadWord(finalName))
                {
                    SoundManager.Play(SoundManager.Sfx.Deny);
                    rankInputWarning = "부적절한 단어가 포함된 아이디는 등록할 수 없습니다.";
                    inputPlayerId = "";
                }
                else
                {
                    SoundManager.Play(SoundManager.Sfx.Click);
                    DataManager.Instance.AddRanking(finalName, DataManager.Instance.currentScore);
                    ResetDataForNewGame();
                    rankInputWarning = "";
                    showEndingRankInput = false;
                    showEndingMessage = true;
                }
            }
        }

        if (showEndingMessage)
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            Rect endRect = new Rect(Screen.width * 0.5f - 320, Screen.height * 0.5f - 180, 640, 360);
            GUI.Box(endRect, "[ 지구 귀환 성공 (ENDING) ]", solidBoxStyle);

            string endingStory = "외계 비행체들의 거센 유도 미사일 공격과 성운의 어둠을 뚫고\n" +
                                 "마침내 정상 항로를 되찾는 데 성공했습니다!\n\n" +
                                 "무사히 미래 3024년의 지구로 귀환한 주인공의 활약으로\n" +
                                 "황폐해진 지구는 다시금 새로운 희망을 맞이하게 됩니다.\n\n" +
                                 "플레이해 주셔서 감사합니다!";
            GUI.Label(new Rect(endRect.x + 30, endRect.y + 60, 580, 200), endingStory, textStyle);

            if (GUI.Button(new Rect(endRect.x + 220, endRect.y + 280, 200, 55), "메인 메뉴로 이동", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                ResetDataForNewGame();
                showEndingMessage = false;
                IsEndingProcessDone = true;
            }
        }
    }

    private void DrawSettingsWindow(GUIStyle boxStyle, GUIStyle textStyle, GUIStyle btnStyle, System.Action onClose)
    {
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
        Rect sRect = new Rect(Screen.width * 0.5f - 250, Screen.height * 0.5f - 180, 500, 360);
        GUI.Box(sRect, "[ 환경 설정 (SETTINGS) ]", boxStyle);

        if (SoundManager.Instance != null)
        {
            GUI.Label(new Rect(sRect.x + 40, sRect.y + 70, 420, 30), $"배경음악(BGM) 음량 : {Mathf.RoundToInt(SoundManager.Instance.BgmVolume * 100)}%", textStyle);
            SoundManager.Instance.BgmVolume = GUI.HorizontalSlider(new Rect(sRect.x + 80, sRect.y + 110, 340, 30), SoundManager.Instance.BgmVolume, 0f, 1f);

            GUI.Label(new Rect(sRect.x + 40, sRect.y + 150, 420, 30), $"효과음(SFX) 음량 : {Mathf.RoundToInt(SoundManager.Instance.SfxVolume * 100)}%", textStyle);
            SoundManager.Instance.SfxVolume = GUI.HorizontalSlider(new Rect(sRect.x + 80, sRect.y + 190, 340, 30), SoundManager.Instance.SfxVolume, 0f, 1f);
        }

        string screenModeText = Screen.fullScreen ? "현재: 전체화면 모드 (클릭 시 창모드)" : "현재: 창모드 (클릭 시 전체화면)";
        if (GUI.Button(new Rect(sRect.x + 60, sRect.y + 230, 380, 45), screenModeText, btnStyle))
        {
            SoundManager.Play(SoundManager.Sfx.Click);
            Screen.fullScreen = !Screen.fullScreen;
        }

        if (GUI.Button(new Rect(sRect.x + 150, sRect.y + 290, 200, 50), "닫기", btnStyle))
        {
            SoundManager.Play(SoundManager.Sfx.Click);
            onClose?.Invoke();
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