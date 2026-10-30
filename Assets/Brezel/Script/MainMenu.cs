using System.Collections;
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

        SoundManager.Play(SoundManager.Sfx.Click); // [추가] 시작 버튼 클릭 효과음!

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
        SceneManager.LoadScene(gameSceneName);
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