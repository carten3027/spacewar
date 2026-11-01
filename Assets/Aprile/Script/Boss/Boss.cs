using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Boss : MonoBehaviour
{
    public enum BossPattern
    {
        [InspectorName("유도 부채꼴 (3발 유도)")] HomingFan,
        [InspectorName("원형 탄막 (12방향)")] Ring,
        [InspectorName("나선 탄막")] Spiral,
        [InspectorName("예측 폭격 (폭탄 3개)")] Bombing,
        [InspectorName("유도 연사 (5발 유도)")] HomingBurst
    }

    public static event System.Action Defeated;

    [Header("능력치")]
    [SerializeField] private int maxHp = 8;
    [SerializeField] private float moveSpeed = 1.5f;
    [Tooltip("공격 간격(초)")]
    [SerializeField] private float fireInterval = 2.5f;
    [Tooltip("HP가 절반 이하일 때 공격 간격 배율 (작을수록 빨라짐)")]
    [SerializeField, Range(0.3f, 1f)] private float rageMultiplier = 0.6f;
    [Tooltip("미사일 공격력. Enemy의 Damage와 같게")]
    [SerializeField] private int damage = 1;

    [Header("몸통박치기 (기획서: 보스는 박치기로만 피해, 밀리지 않음)")]
    [Tooltip("플레이어가 한 번 부딪힐 때 깎이는 보스 HP")]
    [SerializeField] private int ramDamage = 1;
    [Tooltip("박치기 후 다음 박치기까지 대기(초)")]
    [SerializeField] private float ramCooldown = 1f;
    [Tooltip("부딪힌 플레이어가 튕겨 나가는 거리")]
    [SerializeField] private float bounceDistance = 3f;
    [SerializeField] private float bounceDuration = 0.25f;

    [Header("패턴 (위에서부터 순서대로 반복)")]
    [SerializeField]
    private BossPattern[] patterns =
    {
        BossPattern.HomingFan,
        BossPattern.Ring,
        BossPattern.HomingBurst
    };

    [Header("탄 모양 (프리팹은 그림·충돌만 담당)")]
    [SerializeField] private EnemyBullet missilePrefab;
    [Tooltip("예측 폭격 패턴에 쓰는 폭탄 그림 (비우면 미사일 그림 사용)")]
    [SerializeField] private EnemyBullet bombPrefab;

    [Header("탄 성질 (보스가 쏘는 모든 탄)")]
    [SerializeField] private BulletSettings bullet = new BulletSettings();
    [Tooltip("예측 폭격 패턴의 폭발")]
    [SerializeField] private ExplosionSettings explosion = new ExplosionSettings();

    [Header("처치 보상 (점수는 즉시, 파편은 스테이지 클리어 때 지급)")]
    [SerializeField] private int score = 1000;
    [SerializeField] private int money = 100;

    [Header("모습")]
    [Tooltip("보스 그림의 긴 쪽(가로·세로 중 큰 값)을 화면 높이의 이 비율로 자동 맞춤 (0 = 프리팹 크기 그대로)")]
    [SerializeField, Range(0f, 1f)] private float screenSizeRatio = 0.15f;
    [Tooltip("켜면 몸이 플레이어 쪽으로 돎. 끄면 한 방향을 보고 고정")]
    [SerializeField] private bool facePlayer = false;
    [Tooltip("고정 방향 각도 (180 = 아래를 봄). 그림 방향이 안 맞으면 90 단위로 조절")]
    [SerializeField] private float fixedAngle = 180f;

    public enum MoveStyle
    {
        [InspectorName("좌우 왕복")] Sweep,
        [InspectorName("구역 안 무작위")] Wander
    }

    [Header("이동 방식")]
    [SerializeField] private MoveStyle moveStyle = MoveStyle.Sweep;
    [Tooltip("좌우 왕복 중 위아래로 살짝 흔들리는 크기")]
    [SerializeField] private float bobAmount = 0.3f;

    [Header("이동 구역 (화면 비율: 왼쪽 아래 0,0 ~ 오른쪽 위 1,1)")]
    [SerializeField] private Vector2 zoneMin = new Vector2(0.2f, 0.62f);
    [SerializeField] private Vector2 zoneMax = new Vector2(0.8f, 0.85f);
    [Tooltip("등장할 때 화면 위쪽 밖에서 시작하는 거리")]
    [SerializeField] private float entryOffset = 2f;

    [Header("피격 연출")]
    [Tooltip("맞았을 때 깜빡이는 시간(초)")]
    [SerializeField] private float hitBlinkTime = 0.6f;
    [SerializeField] private float hitBlinkInterval = 0.08f;

    [Header("연결")]
    [SerializeField] private string playerTag = "Player";

    private const float LeadTime = 1f;

    private SpriteRenderer[] renderers;
    private Coroutine blinkRoutine;

    private Rigidbody2D rb;
    private Camera cam;
    private Player target;
    private Vector2 lastPlayerPos;
    private Vector2 playerVelocity;
    private Vector2 wanderPoint;
    private int hp;
    private int patternIndex;
    private float fireTimer;
    private float ramTimer;
    private float sweepPhase;
    private bool entered;
    private bool dead;

    public float HpRatio => (float)Mathf.Max(hp, 0) / maxHp;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;
        hp = maxHp;
        fireTimer = fireInterval;
        renderers = GetComponentsInChildren<SpriteRenderer>();
    }

    void Start()
    {
        FitToScreen();

        Vector3 top = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, 0f));
        Vector3 start = new Vector3(top.x, top.y + entryOffset, 0f);
        transform.position = start;
        rb.position = start;

        if (!facePlayer)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, fixedAngle);
            rb.rotation = fixedAngle;
        }
    }

    void FitToScreen()
    {
        if (screenSizeRatio <= 0f) return;

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Vector2 local = sr.sprite.bounds.size;
        Vector3 s = sr.transform.lossyScale;
        float size = Mathf.Max(local.x * Mathf.Abs(s.x), local.y * Mathf.Abs(s.y));
        if (size <= 0f) return;

        float goal = cam.orthographicSize * 2f * screenSizeRatio;
        transform.localScale *= goal / size;
    }

    void FixedUpdate()
    {
        if (ramTimer > 0f) ramTimer -= Time.fixedDeltaTime;

        if (target == null) FindPlayer();

        if (target == null || target.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 playerPos = target.transform.position;
        playerVelocity = (playerPos - lastPlayerPos) / Time.fixedDeltaTime;
        lastPlayerPos = playerPos;

        rb.angularVelocity = 0f;
        rb.MoveRotation(facePlayer ? AngleTo(playerPos) : fixedAngle);

        Move();
        UpdateFire(playerPos);
    }

    void FindPlayer()
    {
        GameObject found = GameObject.FindWithTag(playerTag);
        if (found == null || !found.TryGetComponent(out target)) return;

        lastPlayerPos = target.transform.position;
    }

    void Move()
    {
        if (!entered)
        {
            GetZone(out Vector2 min, out Vector2 max);
            Vector2 center = (min + max) * 0.5f;
            rb.linearVelocity = (center - rb.position).normalized * moveSpeed * 2f;

            if (InZone(rb.position))
            {
                entered = true;
                PickWanderPoint();
            }
            return;
        }

        if (moveStyle == MoveStyle.Sweep)
        {
            Sweep();
            return;
        }

        if (Vector2.Distance(rb.position, wanderPoint) < 0.3f)
        {
            PickWanderPoint();
        }

        rb.linearVelocity = (wanderPoint - rb.position).normalized * moveSpeed;
    }

    void Sweep()
    {
        GetZone(out Vector2 min, out Vector2 max);
        Vector2 center = (min + max) * 0.5f;
        float halfWidth = Mathf.Max((max.x - min.x) * 0.5f, 0.01f);

        sweepPhase += moveSpeed / halfWidth * Time.fixedDeltaTime;

        Vector2 goal = new Vector2(
            center.x + Mathf.Sin(sweepPhase) * halfWidth,
            center.y + Mathf.Sin(sweepPhase * 2f) * bobAmount
        );

        Vector2 velocity = (goal - rb.position) / Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.ClampMagnitude(velocity, moveSpeed * 2f);
    }

    void GetZone(out Vector2 min, out Vector2 max)
    {
        min = cam.ViewportToWorldPoint(zoneMin);
        max = cam.ViewportToWorldPoint(zoneMax);
    }

    bool InZone(Vector2 p)
    {
        GetZone(out Vector2 min, out Vector2 max);
        return p.x > min.x && p.x < max.x && p.y > min.y && p.y < max.y;
    }

    void PickWanderPoint()
    {
        GetZone(out Vector2 min, out Vector2 max);
        wanderPoint = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
    }

    void UpdateFire(Vector2 playerPos)
    {
        if (!entered || patterns.Length == 0) return;

        fireTimer -= Time.fixedDeltaTime;
        if (fireTimer > 0f) return;

        Fire(playerPos, patterns[patternIndex % patterns.Length]);
        patternIndex++;

        bool rage = hp <= maxHp / 2;
        fireTimer = fireInterval * (rage ? rageMultiplier : 1f);
    }

    void Fire(Vector2 playerPos, BossPattern pattern)
    {
        float aim = AngleTo(playerPos);

        switch (pattern)
        {
            case BossPattern.HomingFan:
                for (int i = -1; i <= 1; i++)
                {
                    Shoot(missilePrefab, aim + i * 30f, true);
                }
                break;

            case BossPattern.Ring:
                for (int i = 0; i < 12; i++)
                {
                    Shoot(missilePrefab, aim + i * 30f, false);
                }
                break;

            case BossPattern.Spiral:
                StartCoroutine(SpiralRoutine(aim));
                break;

            case BossPattern.Bombing:
                EnemyBullet bomb = bombPrefab != null ? bombPrefab : missilePrefab;
                Vector2 landing = playerPos + playerVelocity * LeadTime;
                Vector2 toLanding = landing - rb.position;
                Vector2 side = new Vector2(-toLanding.y, toLanding.x).normalized * 2f;

                for (int i = -1; i <= 1; i++)
                {
                    Vector2 point = landing + side * i;
                    float fuse = Mathf.Max(Vector2.Distance(rb.position, point) / Mathf.Max(bullet.speed, 0.01f), 0.5f);
                    Shoot(bomb, AngleTo(point), false, fuse, explosion);
                }
                break;

            case BossPattern.HomingBurst:
                StartCoroutine(HomingBurstRoutine());
                break;
        }
    }

    IEnumerator SpiralRoutine(float startAngle)
    {
        for (int i = 0; i < 24; i++)
        {
            float angle = startAngle + i * 25f;
            Shoot(missilePrefab, angle, false);
            Shoot(missilePrefab, angle + 180f, false);
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator HomingBurstRoutine()
    {
        for (int i = 0; i < 5; i++)
        {
            if (target == null || target.IsDead) yield break;

            Shoot(missilePrefab, AngleTo(target.transform.position), true);
            yield return new WaitForSeconds(0.2f);
        }
    }

    void Shoot(EnemyBullet prefab, float angle, bool homing, float life = 0f, ExplosionSettings blast = null)
    {
        SoundManager.Play(SoundManager.Sfx.Shoot); // [추가] 미사일 발사 효과음!
        if (prefab == null) return;

        EnemyBullet shot = Instantiate(prefab, rb.position, Quaternion.Euler(0f, 0f, angle));
        shot.Launch(target.transform, damage, homing, bullet, life, blast);
    }

    float AngleTo(Vector2 point)
    {
        Vector2 d = point - rb.position;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
    }

    // [Boss.cs 의 OnTriggerStay2D 함수를 아래 코드로 교체]
    void OnTriggerStay2D(Collider2D other)
    {
        if (dead || !entered) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        // 1. 플레이어가 보스 몸을 통과하지 못하도록 쿨타임과 상관없이 항상 바깥으로 튕겨냄!
        Vector2 away = (Vector2)player.transform.position - rb.position;
        if (away.sqrMagnitude < 0.001f) away = Vector2.down; // 완전히 겹쳤을 때 예외 방지
        player.Bounce(away, bounceDistance, bounceDuration);

        // 2. 보스 체력을 깎는 데미지만 쿨타임(ramTimer)마다 1번씩 들어가도록 분리!
        if (ramTimer <= 0f)
        {
            ramTimer = ramCooldown;
            TakeDamage(ramDamage);
        }
    }

    public void TakeDamage(int amount)
    {
        if (dead) return;

        hp -= amount;
        SoundManager.Play(SoundManager.Sfx.Hit);
        Debug.Log($"보스 HP {Mathf.Max(hp, 0)}/{maxHp}");

        if (hp <= 0)
        {
            Die();
            return;
        }

        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(HitBlinkRoutine());
    }

    IEnumerator HitBlinkRoutine()
    {
        float end = Time.time + hitBlinkTime;
        bool visible = true;

        while (Time.time < end)
        {
            visible = !visible;
            SetVisible(visible);
            yield return new WaitForSeconds(hitBlinkInterval);
        }

        SetVisible(true);
        blinkRoutine = null;
    }

    void SetVisible(bool visible)
    {
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != null) sr.enabled = visible;
        }
    }

    void Die()
    {
        dead = true;

        if (DataManager.Instance != null)
        {
            DataManager.Instance.currentScore += score;
        }

        Enemy.StageMoney += money;
        Debug.Log("보스 처치");
        Defeated?.Invoke();
        Destroy(gameObject);
    }
}