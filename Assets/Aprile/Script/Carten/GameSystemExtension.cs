using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSystemExtension : MonoBehaviour
{
    public static GameSystemExtension Instance { get; private set; }

    [Header("스테이지 장애물 스폰")]
    public GameObject obstaclePrefab;
    public int maxObstacles = 3;
    public float obstacleSpawnInterval = 4f;
    private float obstacleTimer = 0f;

    [Header("3스테이지 시야 제한 설정")]
    public bool enableDarkVision = true;
    public float visionSize = 7f;

    public bool IsEndingProcessDone { get; private set; } = false;

    private GameObject darknessOverlay;
    private Transform playerTransform;

    private bool showRankWindow = false;
    private bool showHelpWindow = false;
    private bool showEndingRankInput = false;
    private bool showEndingMessage = false;
    private string inputPlayerId = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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
        showEndingRankInput = false;
        showEndingMessage = false;

        if (sceneName == "TeamProject")
        {
            CreateDarknessOverlay();
        }
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != "TeamProject") return;
        if (DataManager.Instance == null) return;

        int stage = DataManager.Instance.currentStage;

        // 1. 2스테이지 이상일 때 운석 자동 스폰
        if (stage >= 2 && obstaclePrefab != null)
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

        // 2. 3스테이지 시야 제한 효과 (상점이 열려 있을 때는 시야 제한을 잠시 꺼서 상점이 잘 보이게 함!)
        if (darknessOverlay == null) CreateDarknessOverlay();

        if (darknessOverlay != null)
        {
            GameObject shopPanel = GameObject.Find("ShopPanel");
            bool isShopOpen = shopPanel != null && shopPanel.activeInHierarchy;

            bool activeDark = (stage >= 3) && enableDarkVision && !isShopOpen && !showEndingRankInput && !showEndingMessage;
            if (darknessOverlay.activeSelf != activeDark)
            {
                darknessOverlay.SetActive(activeDark);
            }

            if (activeDark)
            {
                if (playerTransform == null)
                {
                    GameObject p = GameObject.FindWithTag("Player");
                    if (p != null) playerTransform = p.transform;
                }
                if (playerTransform != null)
                {
                    darknessOverlay.transform.position = new Vector3(playerTransform.position.x, playerTransform.position.y, 0f);
                }
            }
        }
    }

    private void CreateDarknessOverlay()
    {
        if (darknessOverlay != null) return;

        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float alpha = Mathf.SmoothStep(0f, 0.97f, (dist - 0.10f) / 0.28f);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }
        tex.Apply();

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 8f);
        darknessOverlay = new GameObject("Stage3_DarkVision");
        SpriteRenderer sr = darknessOverlay.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingOrder = 99;
        darknessOverlay.transform.localScale = new Vector3(visionSize, visionSize, 1f);
        darknessOverlay.SetActive(false);
    }

    public void StartVictorySequence()
    {
        IsEndingProcessDone = false;
        inputPlayerId = "";

        int finalScore = DataManager.Instance != null ? DataManager.Instance.currentScore : 0;

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
        string currentScene = SceneManager.GetActiveScene().name;

        // 팝업창 배경이 투명해서 뒤쪽 글씨와 겹쳐 보이지 않도록 진한 검은색 배경 생성
        GUIStyle solidBoxStyle = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.UpperCenter };
        solidBoxStyle.normal.textColor = Color.white;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        GUIStyle textStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        textStyle.normal.textColor = Color.white;

        // =========================================================================
        // 1. 메인 메뉴 화면 UI (기존 중앙 타이틀·START 버튼과 절대 안 겹치게 상단 바에 배치!)
        // =========================================================================
        if (currentScene == "MainMenu")
        {
            // 상단 가로 메뉴 바 형태로 깔끔하게 배치
            if (DataManager.Instance != null && DataManager.Instance.HasSaveFile())
            {
                if (GUI.Button(new Rect(20, 20, 210, 45), $"이어하기 (Stage {DataManager.Instance.currentStage})", btnStyle))
                {
                    SoundManager.Play(SoundManager.Sfx.Click);
                    DataManager.Instance.LoadGame();
                    SceneManager.LoadScene("TeamProject");
                }
            }

            if (GUI.Button(new Rect(240, 20, 170, 45), "TOP 5", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                showRankWindow = !showRankWindow;
                showHelpWindow = false;
            }
            if (GUI.Button(new Rect(420, 20, 170, 45), "조작 도움말", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                showHelpWindow = !showHelpWindow;
                showRankWindow = false;
            }
            if (GUI.Button(new Rect(600, 20, 140, 45), "게임 종료", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                Application.Quit();
            }

            // 랭킹 창이나 도움말 창을 열었을 때만 어두운 배경을 깔아 뒤쪽 글씨와 안 겹치게 처리
            if (showRankWindow && DataManager.Instance != null)
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), ""); // 화면 전체 암전 가림막
                Rect rankRect = new Rect(Screen.width * 0.5f - 250, Screen.height * 0.5f - 200, 500, 400);
                GUI.Box(rankRect, "TOP 5", solidBoxStyle);

                var list = DataManager.Instance.rankList;
                if (list.Count == 0)
                {
                    GUI.Label(new Rect(rankRect.x, rankRect.y + 150, 500, 50), "아직 등록된 랭킹 기록이 없습니다.", textStyle);
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        string rankLine = $"{i + 1}위   |   ID: {list[i].playerName}   |   {list[i].score} 점";
                        GUI.Label(new Rect(rankRect.x + 30, rankRect.y + 70 + i * 50, 440, 40), rankLine, textStyle);
                    }
                }

                if (GUI.Button(new Rect(rankRect.x + 175, rankRect.y + 330, 150, 50), "닫기", btnStyle))
                {
                    showRankWindow = false;
                }
            }

            if (showHelpWindow)
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), ""); // 화면 전체 암전 가림막
                Rect helpRect = new Rect(Screen.width * 0.5f - 300, Screen.height * 0.5f - 200, 600, 400);
                GUI.Box(helpRect, "게임 스토리 및 조작 도움말", solidBoxStyle);
                string helpMsg = "[스토리 (미래 3024년)]\n항로를 이탈한 주인공 우주선이 적의 유도 미사일을 피해\n적끼리 맞추며 3개의 스테이지를 탈출하는 게임입니다!\n\n" +
                                 "[조작 방법]\nA D) : 우주선 회전  /  Left Shift : 감속\n" +
                                 "Q 키 : 비상 폭탄 (화면 내 모든 탄막 제거)\n" +
                                 "숫자 1, 2 키 : 상점에서 장착한 파츠 스킬 발동\n" +
                                 "보스전 규칙 : 보스는 주인공의 몸통박치기로만 타격 가능!";
                GUI.Label(new Rect(helpRect.x + 20, helpRect.y + 50, 560, 260), helpMsg, textStyle);

                if (GUI.Button(new Rect(helpRect.x + 225, helpRect.y + 330, 150, 50), "닫기", btnStyle))
                {
                    showHelpWindow = false;
                }
            }
        }

        // =========================================================================
        // 2. 3스테이지 클리어 시 Top 5 아이디 입력창 & 엔딩 메시지
        // =========================================================================
        if (showEndingRankInput && DataManager.Instance != null)
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            Rect inRect = new Rect(Screen.width * 0.5f - 260, Screen.height * 0.5f - 150, 520, 300);
            GUI.Box(inRect, "축하합니다! TOP 5 기록 달성!", solidBoxStyle);

            GUI.Label(new Rect(inRect.x + 20, inRect.y + 60, 480, 60),
                $"최종 점수: {DataManager.Instance.currentScore} 점\n랭킹에 등록할 아이디(ID)를 입력해 주세요:", textStyle);

            GUIStyle fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            inputPlayerId = GUI.TextField(new Rect(inRect.x + 110, inRect.y + 135, 300, 45), inputPlayerId, 12, fieldStyle);

            if (GUI.Button(new Rect(inRect.x + 160, inRect.y + 210, 200, 55), "랭킹 등록 완료", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                string finalName = string.IsNullOrWhiteSpace(inputPlayerId) ? "PLAYER" : inputPlayerId.Trim();
                DataManager.Instance.AddRanking(finalName, DataManager.Instance.currentScore);
                showEndingRankInput = false;
                showEndingMessage = true;
            }
        }

        if (showEndingMessage)
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            Rect endRect = new Rect(Screen.width * 0.5f - 320, Screen.height * 0.5f - 180, 640, 360);
            GUI.Box(endRect, "지구 귀환 성공", solidBoxStyle);

            string endingStory = "외계 비행체들의 거센 유도 미사일 공격과 성운의 어둠을 뚫고\n" +
                                 "마침내 정상 항로를 되찾는 데 성공했습니다!\n\n" +
                                 "무사히 미래 3024년의 지구로 귀환한 주인공의 활약으로\n" +
                                 "황폐해진 지구는 다시금 새로운 희망을 맞이하게 됩니다.\n\n" +
                                 "플레이해 주셔서 감사합니다!";
            GUI.Label(new Rect(endRect.x + 30, endRect.y + 60, 580, 200), endingStory, textStyle);

            if (GUI.Button(new Rect(endRect.x + 220, endRect.y + 280, 200, 55), "메인 메뉴로 이동", btnStyle))
            {
                SoundManager.Play(SoundManager.Sfx.Click);
                showEndingMessage = false;
                IsEndingProcessDone = true;
            }
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