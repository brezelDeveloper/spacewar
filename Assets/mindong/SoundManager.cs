using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public enum Sfx
    {
        [InspectorName("버튼 클릭")] Click,
        [InspectorName("상점 - 구매 성공")] Buy,
        [InspectorName("상점 - 구매 실패")] Deny,
        [InspectorName("상점 - 장착·해제")] Equip,
        [InspectorName("아이템 획득")] ItemPickup,
        [InspectorName("아이템 - 가방이 가득 참")] BagFull
    }

    [System.Serializable]
    public class SfxEntry
    {
        public Sfx type;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public static SoundManager Instance { get; private set; }

    [Header("효과음 (종류별로 소리 파일 연결)")]
    [SerializeField]
    private SfxEntry[] effects =
    {
        new SfxEntry { type = Sfx.Click },
        new SfxEntry { type = Sfx.Buy },
        new SfxEntry { type = Sfx.Deny },
        new SfxEntry { type = Sfx.Equip },
        new SfxEntry { type = Sfx.ItemPickup },
        new SfxEntry { type = Sfx.BagFull }
    };

    [Header("전체 음량")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    private AudioSource source;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
    }

    public static void Play(Sfx type)
    {
        if (Instance != null) Instance.PlayEffect(type);
    }

    void PlayEffect(Sfx type)
    {
        foreach (SfxEntry entry in effects)
        {
            if (entry.type != type || entry.clip == null) continue;

            source.PlayOneShot(entry.clip, entry.volume * masterVolume);
            return;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}