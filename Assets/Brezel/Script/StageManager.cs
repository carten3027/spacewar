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

    public static int CurrentStage { get; private set; } = 1;
    public static float StageTime { get; private set; }
    public static int KillsForBoss { get; private set; }
    public static bool BossPhase { get; private set; }

    private static int savedStage;
    private static bool timing;
    private static bool skipRequested;

    public static void SkipStage()
    {
        skipRequested = true;
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
        cam = Camera.main;
        if (messageText != null) messageText.text = "";

        int index = Mathf.Clamp(StageIndex, 0, stages.Length - 1);
        SetBackground(stages[index].background);

        yield return Fade(1f, 0f);

        while (index < stages.Length)
        {
            StageIndex = index;
            if (DataManager.Instance != null) DataManager.Instance.SaveGame();

            yield return PlayStage(stages[index], index + 1);
            index++;
        }

        StageIndex = 0;
        yield return ShowMessage("VICTORY", endDelay);

        if (GameSystemExtension.Instance != null)
        {
            GameSystemExtension.Instance.StartVictorySequence();
            while (!GameSystemExtension.Instance.IsEndingProcessDone)
            {
                yield return null;
            }
        }

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

    private IEnumerator PlayStage(Stage stage, int number)
    {
        Enemy.KillCount = 0;
        Enemy.StageMoney = 0;
        CurrentStage = number;
        KillsForBoss = stage.killsForBoss;
        BossPhase = false;
        StageTime = 0f;
        timing = false;
        SetBackground(stage.background);

        yield return ShowMessage($"STAGE {number}\n{stage.title}", messageTime);

        timing = true;

        while (Enemy.KillCount < stage.killsForBoss && !skipRequested)
        {
            if (stage.enemies.Length > 0 && CountEnemies() < stage.maxEnemies)
            {
                Enemy prefab = stage.enemies[Random.Range(0, stage.enemies.Length)];

                if (prefab != null)
                {
                    Instantiate(prefab, SpawnPoint(), Quaternion.identity);
                }
            }

            for (float t = 0f; t < stage.spawnInterval && !skipRequested; t += Time.deltaTime)
            {
                if (Enemy.KillCount >= stage.killsForBoss) break;
                yield return null;
            }
        }

        if (stage.boss != null && !skipRequested)
        {
            BossPhase = true;
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