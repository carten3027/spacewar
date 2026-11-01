using UnityEngine;

public class StageEnvironment : MonoBehaviour
{
    [Header("2스테이지 이상: 장애물(운석) 자동 생성 (기획서 1-⑥)")]
    [Tooltip("이전에 만든 Obstacle(운석) 프리팹을 연결하세요")]
    public GameObject obstaclePrefab;
    public int maxObstaclesOnScreen = 3; // 화면에 유지할 최대 운석 개수
    public float spawnInterval = 4f;     // 운석 생성 간격(초)
    private float spawnTimer = 0f;

    [Header("3스테이지 전용: 시야 제한 효과 (기획서 1-⑥)")]
    public bool enableDarkVisionInStage3 = true;
    [Tooltip("시야 구멍 크기 (작을수록 시야가 좁아짐)")]
    public float visionScale = 6f;

    private GameObject darknessOverlay; // 코드로 자동 생성할 어두운 시야 가림막
    private Transform playerTransform;

    private void Start()
    {
        // 플레이어 찾기
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        // 3스테이지 시야 제한용 검은색 구멍 뚫린 가림막 자동 생성
        CreateDarknessOverlay();
    }

    private void Update()
    {
        if (DataManager.Instance == null) return;
        int stage = DataManager.Instance.currentStage;

        // ---------------- 1. 2스테이지 이상일 때 운석 스폰 ----------------
        if (stage >= 2 && obstaclePrefab != null)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                // 현재 화면의 운석 개수가 최대치보다 적을 때만 새로 생성
                int currentCount = FindObjectsByType<Obstacle>(FindObjectsSortMode.None).Length;
                if (currentCount < maxObstaclesOnScreen)
                {
                    Vector2 randomPos = new Vector2(Random.Range(-7.5f, 7.5f), Random.Range(-4f, 4f));
                    Instantiate(obstaclePrefab, randomPos, Quaternion.identity);
                }
            }
        }

        // ---------------- 2. 3스테이지 시야 제한 효과 제어 ----------------
        if (darknessOverlay != null)
        {
            bool shouldShowDarkness = (stage == 3) && enableDarkVisionInStage3;
            if (darknessOverlay.activeSelf != shouldShowDarkness)
            {
                darknessOverlay.SetActive(shouldShowDarkness);
            }

            // 시야 가림막의 중심(밝은 구멍)이 항상 플레이어를 따라다니게 위치 고정
            if (shouldShowDarkness)
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

    // 별도의 포토샵 이미지 없이도 코드로 '가운데만 투명하게 뚫린 검은색 안개 이미지'를 즉석에서 만드는 함수
    private void CreateDarknessOverlay()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 중심(플레이어 위치)에서의 거리 비율 계산
                float dist = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                // 중심(0.15 이하)은 투명(0)하고, 외곽(0.45 이상)으로 갈수록 진한 검은색(0.96)이 되도록 설정
                float alpha = Mathf.SmoothStep(0f, 0.96f, (dist - 0.12f) / 0.3f);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }
        tex.Apply();

        Sprite vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 10f);

        darknessOverlay = new GameObject("Stage3_DarkVision");
        SpriteRenderer sr = darknessOverlay.AddComponent<SpriteRenderer>();
        sr.sprite = vignetteSprite;
        sr.sortingOrder = 50; // 배경과 적보다 위에 그려져 시야를 가리도록 설정
        darknessOverlay.transform.localScale = new Vector3(visionScale, visionScale, 1f);
        darknessOverlay.SetActive(false);
    }
}

// =========================================================================================
// [StageEnvironment.cs 핵심 문법 & 용어 완벽 정리 사전]
// =========================================================================================
// 1. Texture2D & SetPixel(x, y, Color) :
//    외부에서 그림 파일을 구해오지 않아도, 코드로 직접 가로 256 x 세로 256개의 점(픽셀)을 찍어
//    가운데는 투명하고 바깥쪽은 시커먼 '손전등 시야 제한 그림'을 자동으로 만들어내는 기술입니다.
// 2. Mathf.SmoothStep(최소, 최대, 비율) :
//    경계선이 칼로 자른 듯 딱딱하지 않고, 안개처럼 부드럽게 투명 -> 반투명 -> 검은색으로 변하게 해줍니다.
// 3. sortingOrder = 50 :
//    2D 스프라이트의 그리기 순서입니다. 숫자가 클수록 화면 맨 앞쪽에 그려져 적과 미사일을 어둠 속에 숨깁니다.
// =========================================================================================