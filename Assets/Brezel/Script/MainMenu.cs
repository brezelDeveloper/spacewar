using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class MainMenu : MonoBehaviour
{
    [Header("영상 (있으면 영상 사용)")]
    [Tooltip("메인화면에서 반복 재생되는 영상")]
    [SerializeField] private VideoPlayer idlePlayer;
    [Tooltip("시작 버튼을 누르면 재생되는 발사 영상")]
    [SerializeField] private VideoPlayer launchPlayer;

    [Header("이미지 (발사 영상이 없을 때 사용)")]
    [Tooltip("시작하면 머리 방향으로 날아갈 미사일 UI 이미지")]
    [SerializeField] private RectTransform missileImage;
    [Tooltip("대기 중 위아래로 흔들리는 정도(픽셀)")]
    [SerializeField] private float bobAmount = 12f;
    [Tooltip("날아가는 거리(픽셀)")]
    [SerializeField] private float launchDistance = 2500f;
    [Tooltip("날아가는 시간(초)")]
    [SerializeField] private float launchTime = 1.5f;

    [Header("화면")]
    [SerializeField] private Button startButton;
    [Tooltip("시작을 누르면 숨길 제목·버튼 묶음")]
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeTime = 1f;

    [Header("오프닝 화면 (직접 만든 UI를 연결하면 그걸 사용 — 비우면 임시로 코드가 만듦)")]
    [Tooltip("오프닝 전체 화면 (평소엔 꺼 둠)")]
    [SerializeField] private GameObject openingPanel;
    [Tooltip("문장이 한 글자씩 나오는 글자")]
    [SerializeField] private TMP_Text openingText;
    [Tooltip("(선택) 오른쪽 아래 조작 안내 글자")]
    [SerializeField] private TMP_Text openingHintText;

    [Header("오프닝 내용")]
    [Tooltip("끄면 오프닝 없이 바로 게임 시작")]
    [SerializeField] private bool showOpening = true;
    [Tooltip("한 칸이 한 화면. 칸마다 한 글자씩 나옴")]
    [SerializeField, TextArea(2, 5)]
    private string[] openingPages =
    {
        "3024년.\n지구를 떠난 탐사선 한 대가 항로를 잃고\n미지의 우주로 떨어졌다.",
        "무기도 없는 작은 우주선.\n남은 것은 비상 폭탄과 재빠른 몸놀림뿐.",
        "외계 비행체의 유도 미사일을 피해\n적끼리 맞히며 지구로 돌아가야 한다."
    };
    [SerializeField] private float openingCharInterval = 0.05f;
    [Tooltip("한 화면의 글자가 다 나온 뒤 다음 화면까지 기다리는 시간(초)")]
    [SerializeField] private float openingPageHold = 2f;
    [SerializeField] private float openingFontSize = 44f;
    [Tooltip("비우면 씬에 있는 글자의 폰트를 사용")]
    [SerializeField] private TMP_FontAsset openingFont;
    [Tooltip("누르면 지금 화면 글자를 바로 다 보여 주고, 다 나온 상태면 다음 화면")]
    [SerializeField] private KeyCode openingNextKey = KeyCode.Space;
    [Tooltip("누르면 오프닝 전체 건너뛰기")]
    [SerializeField] private KeyCode openingSkipKey = KeyCode.Escape;

    [Header("이동")]
    [SerializeField] private string gameSceneName = "TeamProject";

    private bool started;
    private Vector2 missileBasePos;

    void Start()
    {
        SetFade(0f);

        if (missileImage != null)
        {
            missileBasePos = missileImage.anchoredPosition;
        }

        if (launchPlayer != null)
        {
            launchPlayer.targetCameraAlpha = 0f;
            launchPlayer.Prepare();
        }

        startButton.onClick.AddListener(OnStartClicked);
    }

    void Update()
    {
        if (started || missileImage == null) return;

        float bob = Mathf.Sin(Time.time * 2f) * bobAmount;
        missileImage.anchoredPosition = missileBasePos + Vector2.up * bob;
    }

    void OnStartClicked()
    {
        if (started) return;
        started = true;

        SoundManager.Play(SoundManager.Sfx.Click);

        startButton.interactable = false;
        if (menuGroup != null) menuGroup.alpha = 0f;

        StartCoroutine(StartRoutine());
    }

    IEnumerator StartRoutine()
    {
        float wait = 0.5f;

        if (launchPlayer != null && launchPlayer.clip != null)
        {
            launchPlayer.Play();

            float timeout = 2f;
            while (launchPlayer.frame < 1 && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            launchPlayer.targetCameraAlpha = 1f;
            if (idlePlayer != null) idlePlayer.Stop();

            wait = (float)launchPlayer.length;
        }
        else if (missileImage != null)
        {
            StartCoroutine(LaunchImage());
            wait = launchTime;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, wait - fadeTime));

        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            SetFade(t / fadeTime);
            yield return null;
        }

        SetFade(1f);

        if (showOpening && openingPages != null && openingPages.Length > 0)
        {
            yield return PlayOpening();
        }

        SceneManager.LoadScene(gameSceneName);
    }

    IEnumerator PlayOpening()
    {
        TMP_Text body;
        TMP_Text hint;

        if (openingPanel != null && openingText != null)
        {
            openingPanel.SetActive(true);
            body = openingText;
            hint = openingHintText;
        }
        else
        {
            BuildTemporaryOpening(out body, out hint);
        }

        if (hint != null) hint.text = $"[{openingNextKey}] 넘기기   [{openingSkipKey}] 건너뛰기";

        bool skipAll = false;

        foreach (string page in openingPages)
        {
            body.text = page;
            body.maxVisibleCharacters = 0;
            body.ForceMeshUpdate();
            int total = body.textInfo.characterCount;

            for (int i = 0; i <= total; i++)
            {
                if (Input.GetKeyDown(openingSkipKey)) { skipAll = true; break; }
                if (Input.GetKeyDown(openingNextKey)) break;

                body.maxVisibleCharacters = i;
                yield return new WaitForSecondsRealtime(openingCharInterval);
            }

            if (skipAll) break;

            body.maxVisibleCharacters = total;
            yield return null;

            for (float t = 0f; t < openingPageHold; t += Time.unscaledDeltaTime)
            {
                if (Input.GetKeyDown(openingSkipKey)) { skipAll = true; break; }
                if (Input.GetKeyDown(openingNextKey)) break;
                yield return null;
            }

            if (skipAll) break;
            yield return null;
        }
    }

    void BuildTemporaryOpening(out TMP_Text body, out TMP_Text hint)
    {
        GameObject canvasObj = new GameObject("OpeningCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject back = new GameObject("Black", typeof(RectTransform), typeof(Image));
        RectTransform backRt = back.GetComponent<RectTransform>();
        backRt.SetParent(canvasObj.transform, false);
        backRt.anchorMin = Vector2.zero;
        backRt.anchorMax = Vector2.one;
        backRt.offsetMin = Vector2.zero;
        backRt.offsetMax = Vector2.zero;
        back.GetComponent<Image>().color = Color.black;

        TMP_FontAsset font = openingFont;
        if (font == null)
        {
            TMP_Text sample = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
            if (sample != null) font = sample.font;
        }

        TextMeshProUGUI bodyText = CreateText(canvasObj.transform, "Body", font, openingFontSize, TextAlignmentOptions.Center);
        bodyText.rectTransform.sizeDelta = new Vector2(1500f, 600f);

        TextMeshProUGUI hintText = CreateText(canvasObj.transform, "Hint", font, 22f, TextAlignmentOptions.BottomRight);
        RectTransform hintRt = hintText.rectTransform;
        hintRt.anchorMin = new Vector2(1f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(1f, 0f);
        hintRt.anchoredPosition = new Vector2(-40f, 30f);
        hintRt.sizeDelta = new Vector2(600f, 40f);
        hintText.color = new Color(1f, 1f, 1f, 0.5f);

        body = bodyText;
        hint = hintText;
    }

    TextMeshProUGUI CreateText(Transform parent, string objName, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    IEnumerator LaunchImage()
    {
        Vector2 start = missileImage.anchoredPosition;
        Vector2 dir = ((Vector2)missileImage.up).normalized;

        for (float t = 0f; t < launchTime; t += Time.deltaTime)
        {
            float p = t / launchTime;
            missileImage.anchoredPosition = start + dir * launchDistance * p * p;
            yield return null;
        }
    }

    void SetFade(float alpha)
    {
        if (fadeImage != null)
        {
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
        }
    }
}