using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    // 기존 동료가 만든 0~5번 순서를 그대로 유지하고, 아래에 전투 효과음(6~9번)을 추가했습니다.
    public enum Sfx
    {
        [InspectorName("버튼 클릭")] Click = 0,
        [InspectorName("상점 - 구매 성공")] Buy = 1,
        [InspectorName("상점 - 구매 실패")] Deny = 2,
        [InspectorName("상점 - 장착·해제")] Equip = 3,
        [InspectorName("아이템 획득")] ItemPickup = 4,
        [InspectorName("아이템 - 가방이 가득 참")] BagFull = 5,
        [InspectorName("전투 - 미사일 발사")] Shoot = 6,
        [InspectorName("전투 - 피격 및 폭발")] Hit = 7,
        [InspectorName("전투 - 폭탄(Q) 사용")] Bomb = 8,
        [InspectorName("전투 - 파츠 스킬 발동")] Skill = 9
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
    [Tooltip("MainMenu 씬에서 재생될 배경음악")]
    [SerializeField] private AudioClip menuBgm;
    [Tooltip("TeamProject(게임 시작) 씬에서 재생될 기본 배경음악")]
    [SerializeField] private AudioClip stageBgm;
    [Tooltip("보스 등장 시 재생될 배경음악 (비워두면 기본 배경음악 유지)")]
    [SerializeField] private AudioClip bossBgm;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;

    [Header("효과음 (종류별로 소리 파일 연결)")]
    [SerializeField]
    private SfxEntry[] effects =
    {
        new SfxEntry { type = Sfx.Click },
        new SfxEntry { type = Sfx.Buy },
        new SfxEntry { type = Sfx.Deny },
        new SfxEntry { type = Sfx.Equip },
        new SfxEntry { type = Sfx.ItemPickup },
        new SfxEntry { type = Sfx.BagFull },
        new SfxEntry { type = Sfx.Shoot },
        new SfxEntry { type = Sfx.Hit },
        new SfxEntry { type = Sfx.Bomb },
        new SfxEntry { type = Sfx.Skill }
    };

    [Header("전체 효과음 음량")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    private AudioSource sfxSource; // 효과음 전용 스피커
    private AudioSource bgmSource; // 배경음악 전용 스피커

    void Awake()
    {
        // 이미 SoundManager가 존재한다면 중복 실행 방지
        // (Managers 오브젝트의 다른 스크립트가 지워지지 않도록 자기 컴포넌트만 제거)
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 1. 효과음(SFX) 스피커 생성
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        // 2. 배경음악(BGM) 스피커 생성
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true; // 배경음악은 무한 반복
        bgmSource.volume = bgmVolume;

        // 씬이 전환될 때마다 자동으로 BGM을 바꿔주는 이벤트 등록
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // 처음 시작할 때 현재 씬 이름에 맞는 BGM 자동 재생
        PlayBgmForScene(SceneManager.GetActiveScene().name);
    }

    // 씬이 바뀔 때(MainMenu -> TeamProject 등) 자동으로 호출되는 함수
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

    // ---------------- 외부에서 부를 수 있는 BGM 재생 함수 ----------------
    public static void PlayBGM(AudioClip clip)
    {
        if (Instance == null || clip == null) return;

        Instance.bgmSource.volume = Instance.bgmVolume;

        // 이미 똑같은 배경음악이 나오고 있다면 끊지 않고 계속 재생
        if (Instance.bgmSource.clip == clip && Instance.bgmSource.isPlaying) return;

        Instance.bgmSource.clip = clip;
        Instance.bgmSource.Play();
    }

    // 보스 등장 시 호출하면 보스 BGM으로 변경
    public static void PlayBossBGM()
    {
        if (Instance != null && Instance.bossBgm != null)
        {
            PlayBGM(Instance.bossBgm);
        }
    }

    // 스테이지 전환 시 다시 일반 스테이지 BGM으로 변경
    public static void PlayStageBGM()
    {
        if (Instance != null && Instance.stageBgm != null)
        {
            PlayBGM(Instance.stageBgm);
        }
    }

    // ---------------- 외부에서 부를 수 있는 효과음(SFX) 재생 함수 ----------------
    public static void Play(Sfx type)
    {
        if (Instance != null) Instance.PlayEffect(type);
    }

    void PlayEffect(Sfx type)
    {
        if (sfxSource == null) return;

        foreach (SfxEntry entry in effects)
        {
            if (entry.type != type || entry.clip == null) continue;

            sfxSource.PlayOneShot(entry.clip, entry.volume * masterVolume);
            return;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}

// =========================================================================================
// [업그레이드된 SoundManager.cs 핵심 문법 & 용어 완벽 정리 사전]
// =========================================================================================
//
// 1. 코드로 직접 컴포넌트 추가하기: gameObject.AddComponent<AudioSource>()
// - 유니티 에디터 인스펙터 창에서 [Add Component] 버튼을 누르지 않아도,
//   게임이 켜질 때(Awake) 코드가 알아서 효과음 스피커(sfxSource)와 배경음 스피커(bgmSource) 2개를 생성해 붙입니다.
//
// -----------------------------------------------------------------------------------------
// 2. 씬 전환 자동 감지 이벤트: SceneManager.sceneLoaded += OnSceneLoaded;
// - 유니티에서 새로운 씬(MainMenu 또는 TeamProject)이 열릴 때마다 자동으로 OnSceneLoaded 함수를 실행해 달라고 예약(구독)하는 문법입니다.
// - 이 기능 덕분에 MainMenu 스크립트나 StageManager 스크립트를 복잡하게 수정하지 않아도,
//   메인 메뉴에 있을 땐 menuBgm이 나오고 게임을 시작해 TeamProject로 넘어가면 즉시 stageBgm으로 음악이 바뀝니다!
//
// -----------------------------------------------------------------------------------------
// 3. 정적(static) 호출 함수: SoundManager.Play(SoundManager.Sfx.Shoot);
// - Play() 함수 앞에 'static'이 붙어 있어서, 다른 스크립트에서 SoundManager.Instance.Play(...) 처럼 길게 쓰지 않고
//   곧바로 'SoundManager.Play(SoundManager.Sfx.종류);' 한 줄만 적으면 어디서든 소리가 납니다.
//
// -----------------------------------------------------------------------------------------
// 4. 왜 중복 제거 시 Destroy(gameObject) 대신 Destroy(this)를 썼나요?
// - 현재 TeamProject 씬의 'Managers' 오브젝트에는 DataManager, InventoryAndItem, SoundManager가 한몸에 붙어 있습니다.
// - 만약 Destroy(gameObject)를 쓰면 Managers 오브젝트 통째로 날아가서 인벤토리까지 사라질 위험이 있으므로,
//   오직 중복된 SoundManager 스크립트 부품(this)만 쏙 빼서 지우도록 안전하게 변경했습니다.
// =========================================================================================