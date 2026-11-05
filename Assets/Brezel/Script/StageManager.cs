using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StageManager : MonoBehaviour
{
    [System.Serializable]
    public class Stage
    {
        [Tooltip("화면에 표시될 이름 (기본 폰트는 영어만 표시됨)")]
        public string title = "Asteroid Belt";
        public Sprite background;
        [Tooltip("이 스테이지에 나오는 적 프리팹들")]
        public Enemy[] enemies;
        public Boss boss;
        [Tooltip("이만큼 처치하면 보스 등장")]
        public int killsForBoss = 8;
        [Tooltip("적 생성 간격(초)")]
        public float spawnInterval = 2.5f;
        [Tooltip("화면에 동시에 있을 수 있는 최대 적 수")]
        public int maxEnemies = 3;
    }

    [Header("스테이지 (위에서부터 1, 2, 3)")]
    [SerializeField] private Stage[] stages;

    [Header("연결")]
    [SerializeField] private SpriteRenderer background;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Image fadeImage;
    [Tooltip("스테이지 클리어 후 열리는 상점 (비우면 상점 없이 바로 다음 스테이지)")]
    [SerializeField] private Shop shop;

    [Header("연출")]
    [SerializeField] private float messageTime = 2f;
    [SerializeField] private float fadeTime = 1f;
    [Tooltip("화면 밖 얼마나 떨어진 곳에서 적을 만들지")]
    [SerializeField] private float spawnMargin = 1.5f;

    [Header("모든 스테이지 클리어 후")]
    [SerializeField] private string endSceneName = "MainMenu";
    [SerializeField] private float endDelay = 3f;

    [Header("엔딩 (직접 만든 UI 연결)")]
    [Tooltip("엔딩 전체 화면 (평소엔 꺼 둠)")]
    [SerializeField] private GameObject endingPanel;
    [Tooltip("스토리가 한 글자씩 나오는 글자")]
    [SerializeField] private TMP_Text endingStoryText;
    [TextArea(3, 10)]
    [SerializeField] private string endingStory = "외계 비행체들의 거센 유도 미사일 공격과 성운의 어둠을 뚫고\n마침내 정상 항로를 되찾는 데 성공했습니다.\n\n무사히 3024년의 지구로 귀환한 주인공의 활약으로\n지구는 다시 새로운 희망을 맞이하게 됩니다.";
    [Tooltip("한 글자가 나오는 간격(초)")]
    [SerializeField] private float storyCharInterval = 0.05f;
    [Tooltip("스토리가 다 나온 뒤 기다리는 시간(초)")]
    [SerializeField] private float storyHoldTime = 2.5f;
    [Tooltip("아래에서 위로 올라가는 크레딧 글자 (Pivot Y = 1, 앵커 아래 가운데)")]
    [SerializeField] private RectTransform creditsText;
    [Tooltip("크레딧이 올라가는 속도 (초당 픽셀)")]
    [SerializeField] private float creditsSpeed = 80f;
    [Tooltip("누르면 스토리를 바로 다 보여 주고, 누르고 있으면 크레딧이 4배 빨라짐")]
    [SerializeField] private KeyCode endingSkipKey = KeyCode.Space;

    public static int CurrentStage { get; private set; } = 1;
    public static float StageTime { get; private set; }
    public static int KillsForBoss { get; private set; }
    public static bool BossPhase { get; private set; }

    private static int savedStage;
    private static bool timing;
    private static bool skipRequested;
    private static bool bossRequested;

    public static void SkipStage()
    {
        skipRequested = true;
        SoundManager.Play(SoundManager.Sfx.CheatSkipStage); // [사운드 연동] 스테이지 스킵 치트 사운드
    }

    public static void SkipToBoss()
    {
        bossRequested = true;
        SoundManager.Play(SoundManager.Sfx.CheatSkipStage);
    }

    private Camera cam;

    private int StageIndex
    {
        get => DataManager.Instance != null ? DataManager.Instance.currentStage - 1 : savedStage;
        set
        {
            savedStage = value;
            if (DataManager.Instance != null) DataManager.Instance.currentStage = value + 1;
        }
    }

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        cam = Camera.main;
        if (messageText != null) messageText.text = "";

        int index = Mathf.Clamp(StageIndex, 0, stages.Length - 1);
        SetBackground(stages[index].background);

        yield return Fade(1f, 0f);

        while (index < stages.Length)
        {
            StageIndex = index;

            yield return PlayStage(stages[index], index + 1);
            index++;

            if (index < stages.Length)
            {
                StageIndex = index;
                if (shop != null) yield return shop.OpenRoutine();
            }
        }

        StageIndex = 0;
        SoundManager.Play(SoundManager.Sfx.GameClear);
        yield return ShowMessage("VICTORY", endDelay);

        if (GameSystemExtension.Instance != null)
        {
            GameSystemExtension.Instance.StartVictorySequence();
            while (!GameSystemExtension.Instance.IsEndingProcessDone)
            {
                yield return null;
            }
        }

        yield return PlayEnding();

        if (!string.IsNullOrEmpty(endSceneName))
        {
            yield return Fade(0f, 1f);
            SceneManager.LoadScene(endSceneName);
        }
    }

    private void Update()
    {
        if (timing) StageTime += Time.deltaTime;
    }

    private IEnumerator PlayEnding()
    {
        if (endingPanel == null) yield break;

        Time.timeScale = 1f;
        Player player = FindAnyObjectByType<Player>();
        if (player != null) player.enabled = false;

        if (creditsText != null) creditsText.gameObject.SetActive(false);
        endingPanel.SetActive(true);

        if (endingStoryText != null)
        {
            endingStoryText.gameObject.SetActive(true);
            endingStoryText.text = endingStory;
            endingStoryText.ForceMeshUpdate();
            int total = endingStoryText.textInfo.characterCount;

            for (int i = 0; i <= total; i++)
            {
                endingStoryText.maxVisibleCharacters = i;
                if (Input.GetKeyDown(endingSkipKey)) break;
                yield return new WaitForSecondsRealtime(storyCharInterval);
            }

            endingStoryText.maxVisibleCharacters = total;
            yield return null;

            for (float t = 0f; t < storyHoldTime; t += Time.unscaledDeltaTime)
            {
                if (Input.GetKeyDown(endingSkipKey)) break;
                yield return null;
            }

            endingStoryText.gameObject.SetActive(false);
        }

        if (creditsText != null)
        {
            creditsText.gameObject.SetActive(true);
            RectTransform area = creditsText.parent as RectTransform;
            float areaHeight = area != null ? area.rect.height : 1080f;
            Vector2 start = creditsText.anchoredPosition;
            float endY = start.y + areaHeight + creditsText.rect.height;

            while (creditsText.anchoredPosition.y < endY)
            {
                float speed = Input.GetKey(endingSkipKey) ? creditsSpeed * 4f : creditsSpeed;
                creditsText.anchoredPosition += Vector2.up * speed * Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator PlayStage(Stage stage, int number)
    {
        Enemy.KillCount = 0;
        Enemy.StageMoney = 0;
        CurrentStage = number;
        KillsForBoss = stage.killsForBoss;
        BossPhase = false;
        StageTime = 0f;
        timing = false;
        bossRequested = false;
        SetBackground(stage.background);

        SoundManager.PlayStageBGM(); // [사운드 연동] 보스전 후 다음 스테이지 시작 시 스테이지 BGM 복구

        SoundManager.Play(SoundManager.Sfx.StageStart);
        yield return ShowMessage($"STAGE {number}\n{stage.title}", messageTime);

        timing = true;

        while (Enemy.KillCount < stage.killsForBoss && !skipRequested && !bossRequested)
        {
            if (stage.enemies.Length > 0 && CountEnemies() < stage.maxEnemies)
            {
                Enemy prefab = stage.enemies[Random.Range(0, stage.enemies.Length)];

                if (prefab != null)
                {
                    Instantiate(prefab, SpawnPoint(), Quaternion.identity);
                }
            }

            for (float t = 0f; t < stage.spawnInterval && !skipRequested && !bossRequested; t += Time.deltaTime)
            {
                if (Enemy.KillCount >= stage.killsForBoss) break;
                yield return null;
            }
        }

        bossRequested = false;

        if (stage.boss != null && !skipRequested)
        {
            BossPhase = true;
            SoundManager.Play(SoundManager.Sfx.Warning);
            yield return ShowMessage("WARNING", messageTime);

            Boss boss = Instantiate(stage.boss, SpawnPoint(), Quaternion.identity);

            while (boss != null && !skipRequested)
            {
                yield return null;
            }
        }

        skipRequested = false;
        BossPhase = false;
        timing = false;
        ClearField();

        int reward = Enemy.StageMoney;
        Enemy.StageMoney = 0;
        if (DataManager.Instance != null) DataManager.Instance.money += reward;

        yield return ShowMessage($"STAGE CLEAR\n파편 +{reward:N0}", messageTime);
    }

    private int CountEnemies()
    {
        return FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length;
    }

    private Vector2 SpawnPoint()
    {
        float height = cam.orthographicSize;
        float width = height * cam.aspect;
        float radius = Mathf.Sqrt(width * width + height * height) + spawnMargin;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        return (Vector2)cam.transform.position + dir * radius;
    }

    private void ClearField()
    {
        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            Destroy(enemy.gameObject);
        }

        foreach (Boss boss in FindObjectsByType<Boss>(FindObjectsSortMode.None))
        {
            Destroy(boss.gameObject);
        }

        foreach (EnemyBullet bullet in FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None))
        {
            Destroy(bullet.gameObject);
        }
    }

    private void SetBackground(Sprite sprite)
    {
        if (background == null || sprite == null) return;

        background.sprite = sprite;

        Vector2 size = sprite.bounds.size;
        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        float scale = Mathf.Max(width / size.x, height / size.y);

        background.transform.localScale = new Vector3(scale, scale, 1f);
        background.transform.position = new Vector3(
            cam.transform.position.x,
            cam.transform.position.y,
            background.transform.position.z
        );
    }

    private IEnumerator ShowMessage(string text, float time)
    {
        if (messageText != null) messageText.text = text;
        yield return new WaitForSeconds(time);
        if (messageText != null) messageText.text = "";
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null) yield break;

        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / fadeTime));
            yield return null;
        }

        fadeImage.color = new Color(0f, 0f, 0f, to);
    }
}