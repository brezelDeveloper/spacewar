using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    // ===================================================================
    // 1. 사운드 카테고리 세분화 (UI, 전투, 파츠 스킬, 치트키)[cite: 2, 3]
    // ===================================================================
    public enum Sfx
    {
        // --- [ UI & 상점 ] ---
        [InspectorName("UI - 버튼 클릭")] Click = 0,
        [InspectorName("상점 - 구매 성공")] Buy = 1,
        [InspectorName("상점 - 구매 실패")] Deny = 2,
        [InspectorName("상점 - 장착 및 해제")] Equip = 3,
        [InspectorName("UI - 가방이 가득 참")] BagFull = 4,

        // --- [ 아이템 ] ---
        [InspectorName("아이템 - 습득")] ItemPickup = 10,

        // --- [ 전투 및 피격 / 사망 / 폭발 ] ---
        [InspectorName("전투 - 플레이어 일반 사격")] PlayerShoot = 20,
        [InspectorName("전투 - 미사일 폭발")] MissileExplosion = 21,
        [InspectorName("전투 - 폭탄(Q) 사용")] Bomb = 22,
        [InspectorName("전투 - 플레이어 피격")] PlayerHit = 23,
        [InspectorName("전투 - 플레이어 사망")] PlayerDeath = 24,
        [InspectorName("전투 - 일반 적 피격")] EnemyHit = 25,
        [InspectorName("전투 - 일반 적 사망")] EnemyDeath = 26,
        [InspectorName("전투 - 보스 피격")] BossHit = 27,
        [InspectorName("전투 - 보스 사망")] BossDeath = 28,

        // --- [ 파츠 스킬 사운드 (상점 파츠 1~5 세분화) ] ---
        [InspectorName("스킬 - 기본/공통 스킬")] SkillGeneric = 30,
        [InspectorName("스킬 - 강제 유도 (Part 1)")] SkillHoming = 31,
        [InspectorName("스킬 - 시간 정지 (Part 2)")] SkillTimeStop = 32,
        [InspectorName("스킬 - 공격 반사 (Part 3)")] SkillReflect = 33,
        [InspectorName("스킬 - 블랙홀 (Part 4)")] SkillBlackHole = 34,
        [InspectorName("스킬 - 감속장 (Part 5)")] SkillSlowField = 35,

        // --- [ 치트키 사운드 (종류별 분리) ] ---
        [InspectorName("치트 - 디버그 모드 토글")] CheatDebug = 40,
        [InspectorName("치트 - 무적 모드 토글")] CheatInvincible = 41,
        [InspectorName("치트 - 화면 적/탄환 전멸")] CheatKillAll = 42,
        [InspectorName("치트 - 골드/재화 추가")] CheatAddMoney = 43,
        [InspectorName("치트 - 스테이지 스킵")] CheatSkipStage = 44
    }

    [System.Serializable]
    public class SfxEntry
    {
        public Sfx type;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public static SoundManager Instance { get; private set; }

    [Header("배경음악 (BGM 파일 연결)")]
    [Tooltip("MainMenu 씬 배경음악")]
    [SerializeField] private AudioClip menuBgm;
    [Tooltip("TeamProject 스테이지 배경음악")]
    [SerializeField] private AudioClip stageBgm;
    [Tooltip("보스 전투 배경음악")]
    [SerializeField] private AudioClip bossBgm;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;

    [Header("효과음 (SFX 리스트 설정)")]
    [SerializeField]
    private SfxEntry[] effects = new SfxEntry[]
    {
        // UI
        new SfxEntry { type = Sfx.Click },
        new SfxEntry { type = Sfx.Buy },
        new SfxEntry { type = Sfx.Deny },
        new SfxEntry { type = Sfx.Equip },
        new SfxEntry { type = Sfx.BagFull },

        // Item
        new SfxEntry { type = Sfx.ItemPickup },

        // Combat
        new SfxEntry { type = Sfx.PlayerShoot },
        new SfxEntry { type = Sfx.MissileExplosion },
        new SfxEntry { type = Sfx.Bomb },
        new SfxEntry { type = Sfx.PlayerHit },
        new SfxEntry { type = Sfx.PlayerDeath },
        new SfxEntry { type = Sfx.EnemyHit },
        new SfxEntry { type = Sfx.EnemyDeath },
        new SfxEntry { type = Sfx.BossHit },
        new SfxEntry { type = Sfx.BossDeath },

        // Skills
        new SfxEntry { type = Sfx.SkillGeneric },
        new SfxEntry { type = Sfx.SkillHoming },
        new SfxEntry { type = Sfx.SkillTimeStop },
        new SfxEntry { type = Sfx.SkillReflect },
        new SfxEntry { type = Sfx.SkillBlackHole },
        new SfxEntry { type = Sfx.SkillSlowField },

        // Cheats
        new SfxEntry { type = Sfx.CheatDebug },
        new SfxEntry { type = Sfx.CheatInvincible },
        new SfxEntry { type = Sfx.CheatKillAll },
        new SfxEntry { type = Sfx.CheatAddMoney },
        new SfxEntry { type = Sfx.CheatSkipStage }
    };

    [Header("전체 효과음 음량")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    private AudioSource sfxSource;
    private AudioSource bgmSource;

    public float BgmVolume
    {
        get => bgmVolume;
        set
        {
            bgmVolume = Mathf.Clamp01(value);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
        }
    }

    public float SfxVolume
    {
        get => masterVolume;
        set => masterVolume = Mathf.Clamp01(value);
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

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        PlayBgmForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayBgmForScene(scene.name);
    }

    private void PlayBgmForScene(string sceneName)
    {
        if (sceneName == "MainMenu")
        {
            PlayBGM(menuBgm);
        }
        else if (sceneName == "TeamProject")
        {
            PlayBGM(stageBgm);
        }
    }

    public static void PlayBGM(AudioClip clip)
    {
        if (Instance == null || clip == null) return;

        Instance.bgmSource.volume = Instance.bgmVolume;
        if (Instance.bgmSource.clip == clip && Instance.bgmSource.isPlaying) return;

        Instance.bgmSource.clip = clip;
        Instance.bgmSource.Play();
    }

    public static void PlayBossBGM()
    {
        if (Instance != null && Instance.bossBgm != null)
        {
            PlayBGM(Instance.bossBgm);
        }
    }

    public static void PlayStageBGM()
    {
        if (Instance != null && Instance.stageBgm != null)
        {
            PlayBGM(Instance.stageBgm);
        }
    }

    // ---------------- 외부에서 부르는 기본 효과음 재생 ----------------
    public static void Play(Sfx type)
    {
        if (Instance != null) Instance.PlayEffect(type);
    }

    private void PlayEffect(Sfx type)
    {
        if (sfxSource == null) return;

        foreach (var entry in effects)
        {
            if (entry.type == type && entry.clip != null)
            {
                sfxSource.PlayOneShot(entry.clip, entry.volume * masterVolume);
                return;
            }
        }
    }

    // ---------------- 편의 헬퍼 1: 파츠 종류 번호(1~5)로 사운드 자동 분기 ----------------
    public static void PlaySkill(int partType)
    {
        Sfx targetSfx = partType switch
        {
            1 => Sfx.SkillHoming,    // 강제 유도
            2 => Sfx.SkillTimeStop,  // 시간 정지
            3 => Sfx.SkillReflect,   // 공격 반사
            4 => Sfx.SkillBlackHole, // 블랙홀
            5 => Sfx.SkillSlowField, // 감속장
            _ => Sfx.SkillGeneric
        };
        Play(targetSfx);
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