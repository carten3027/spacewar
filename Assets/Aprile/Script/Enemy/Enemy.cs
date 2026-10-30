using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    public enum EnemyType
    {
        [InspectorName("정찰기 - 단발 유도")] Scout,
        [InspectorName("전투기 - 3연발 유도")] Fighter,
        [InspectorName("중장갑함 - 산탄 + 유도")] Heavy,
        [InspectorName("요격기 - 직선 고속탄")] Interceptor,
        [InspectorName("폭격기 - 곡사 폭탄")] Bomber
    }

    public static int KillCount;
    public static int StageMoney;

    [Header("종류")]
    [SerializeField] private EnemyType type;

    [Header("능력치")]
    [SerializeField] private int maxHp = 2;
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("공격 간격(초)")]
    [SerializeField] private float fireInterval = 2f;
    [Tooltip("미사일 공격력. 플레이어·적 누구에게 맞아도 이만큼 깎임")]
    [SerializeField] private int damage = 1;

    [Header("위치 잡기")]
    [Tooltip("플레이어와 이 거리 안으로 들어오면 멈춰서 몸만 돌리며 사격")]
    [SerializeField] private float keepDistance = 5f;
    [Tooltip("플레이어가 이 거리보다 멀어지면 다시 따라감")]
    [SerializeField] private float chaseDistance = 9f;
    [Tooltip("멈춰 있을 때 옆으로 맴도는 속도 비율 (0 = 완전 정지)")]
    [SerializeField, Range(0f, 1f)] private float orbitRatio = 0f;
    [Tooltip("멈출 때 화면 끝에서 최소 이만큼 안쪽이어야 함")]
    [SerializeField] private float holdMargin = 1f;

    [Header("탄 모양 (프리팹은 그림·충돌만 담당)")]
    [SerializeField] private EnemyBullet missilePrefab;
    [Tooltip("폭격기만 사용 (비우면 미사일 그림 사용)")]
    [SerializeField] private EnemyBullet bombPrefab;

    [Header("탄 성질 (이 적이 쏘는 모든 탄)")]
    [SerializeField] private BulletSettings bullet = new BulletSettings();

    [Header("── 전투기만 사용 ──")]
    [Tooltip("연발 수")]
    [SerializeField] private int burstCount = 3;
    [Tooltip("연발 간격(초)")]
    [SerializeField] private float burstInterval = 0.15f;

    [Header("── 중장갑함만 사용 ──")]
    [Tooltip("부채꼴 발 수 (가운데 1발만 유도)")]
    [SerializeField] private int spreadCount = 5;
    [Tooltip("탄 사이 각도")]
    [SerializeField] private float spreadAngle = 20f;

    [Header("── 요격기만 사용 ──")]
    [Tooltip("멈춰 있을 때 좌우로 흔들리는 크기")]
    [SerializeField] private float weaveAmount = 0.7f;

    [Header("── 폭격기만 사용 ──")]
    [Tooltip("몇 초 뒤 플레이어 위치를 예측해서 던질지")]
    [SerializeField] private float leadTime = 1f;
    [SerializeField] private ExplosionSettings explosion = new ExplosionSettings();

    [Header("처치 보상 (점수는 즉시, 파편은 스테이지 클리어 때 지급)")]
    [SerializeField] private int score = 100;
    [SerializeField] private int money = 10;

    [Header("화면 밖으로 밀려났을 때")]
    [SerializeField] private float returnSpeed = 6f;

    [Header("연결")]
    [SerializeField] private string playerTag = "Player";

    private const float ReturnTime = 0.5f;

    private Rigidbody2D rb;
    private Camera cam;
    private Player target;
    private Vector2 lastPlayerPos;
    private Vector2 playerVelocity;
    private int hp;
    private float fireTimer;
    private float pushTimer;
    private float orbitDir;
    private bool chasing = true;
    private bool entered;
    private bool dead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;
        hp = maxHp;
        fireTimer = Random.Range(0.5f, fireInterval);
        orbitDir = Random.value < 0.5f ? 1f : -1f;
    }

    void FixedUpdate()
    {
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
        rb.MoveRotation(AngleTo(playerPos));

        CheckScreen();

        if (pushTimer > 0f)
        {
            pushTimer -= Time.fixedDeltaTime;
            return;
        }

        Move(playerPos);
        UpdateFire(playerPos);
    }

    void FindPlayer()
    {
        GameObject found = GameObject.FindWithTag(playerTag);
        if (found == null || !found.TryGetComponent(out target)) return;

        lastPlayerPos = target.transform.position;
    }

    void Move(Vector2 playerPos)
    {
        Vector2 toPlayer = playerPos - rb.position;
        float dist = toPlayer.magnitude;
        Vector2 dir = toPlayer.normalized;
        Vector2 side = new Vector2(-dir.y, dir.x) * orbitDir;

        if (dist > chaseDistance) chasing = true;
        else if (dist <= keepDistance && InsideMargin(rb.position)) chasing = false;

        Vector2 move = chasing ? dir : side * orbitRatio;

        if (type == EnemyType.Interceptor)
        {
            move += side * Mathf.Sin(Time.time * 5f) * weaveAmount;
        }

        rb.linearVelocity = Vector2.ClampMagnitude(move, 1f) * moveSpeed;
    }

    bool InsideMargin(Vector2 p)
    {
        Vector2 min = (Vector2)cam.ViewportToWorldPoint(Vector3.zero) + Vector2.one * holdMargin;
        Vector2 max = (Vector2)cam.ViewportToWorldPoint(Vector3.one) - Vector2.one * holdMargin;
        return p.x > min.x && p.x < max.x && p.y > min.y && p.y < max.y;
    }

    void CheckScreen()
    {
        Vector2 min = cam.ViewportToWorldPoint(Vector3.zero);
        Vector2 max = cam.ViewportToWorldPoint(Vector3.one);
        Vector2 p = rb.position;
        bool inside = p.x > min.x && p.x < max.x && p.y > min.y && p.y < max.y;

        if (inside)
        {
            entered = true;
        }
        else if (entered)
        {
            Vector2 center = cam.transform.position;
            rb.linearVelocity = (center - p).normalized * returnSpeed;
            pushTimer = ReturnTime;
        }
    }

    void UpdateFire(Vector2 playerPos)
    {
        if (!entered) return;

        fireTimer -= Time.fixedDeltaTime;
        if (fireTimer > 0f) return;

        Fire(playerPos);
        fireTimer = fireInterval;
    }

    void Fire(Vector2 playerPos)
    {
        float aim = AngleTo(playerPos);

        switch (type)
        {
            case EnemyType.Scout:
                Shoot(missilePrefab, aim, true);
                break;

            case EnemyType.Fighter:
                StartCoroutine(Burst());
                break;

            case EnemyType.Heavy:
                for (int i = 0; i < spreadCount; i++)
                {
                    float offset = (i - (spreadCount - 1) * 0.5f) * spreadAngle;
                    Shoot(missilePrefab, aim + offset, i == spreadCount / 2);
                }
                break;

            case EnemyType.Interceptor:
                Shoot(missilePrefab, aim, false);
                break;

            case EnemyType.Bomber:
                EnemyBullet bomb = bombPrefab != null ? bombPrefab : missilePrefab;
                Vector2 landing = playerPos + playerVelocity * leadTime;
                float fuse = Mathf.Max(Vector2.Distance(rb.position, landing) / Mathf.Max(bullet.speed, 0.01f), 0.5f);
                Shoot(bomb, AngleTo(landing), false, fuse, explosion);
                break;
        }
    }

    IEnumerator Burst()
    {
        for (int i = 0; i < burstCount; i++)
        {
            if (target == null || target.IsDead) yield break;

            Shoot(missilePrefab, AngleTo(target.transform.position), true);
            yield return new WaitForSeconds(burstInterval);
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

    void OnTriggerStay2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            player.PushEnemy(this);
        }
    }

    public void ApplyKnockback(Vector2 dir, float distance, float duration)
    {
        rb.linearVelocity = dir.normalized * (distance / duration);
        pushTimer = duration;
    }

    public void TakeDamage(int amount)
    {
        if (dead) return;

        hp -= amount;
        SoundManager.Play(SoundManager.Sfx.Hit);
        Debug.Log($"{name} HP {Mathf.Max(hp, 0)}/{maxHp}");

        if (hp <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (dead) return;
        dead = true;

        KillCount++;
        StageMoney += money;

        if (DataManager.Instance != null)
        {
            DataManager.Instance.currentScore += score;
        }

        Destroy(gameObject);
    }
}