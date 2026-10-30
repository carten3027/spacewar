using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BulletSettings
{
    [Tooltip("탄 속도")]
    public float speed = 2f;
    [Tooltip("유도 탄이 초당 꺾는 각도 (작을수록 피하기 쉬움)")]
    public float turnSpeed = 120f;
    [Tooltip("유도 탄이 따라오는 시간(초). 이후엔 그 방향으로 직진")]
    public float homingTime = 4f;
    [Tooltip("탄이 사라지기까지 시간(초)")]
    public float lifeTime = 8f;
}

[System.Serializable]
public class ExplosionSettings
{
    [Tooltip("폭발 반경 (칸)")]
    public float radius = 1.2f;
    [Tooltip("폭발 원이 보였다가 사라지는 시간(초)")]
    public float duration = 0.35f;
    [Tooltip("폭발 범위를 보여 줄 노란 원 프리팹")]
    public SpriteRenderer prefab;
}

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBullet : MonoBehaviour
{
    // === 추가된 부분: 스킬 제어 상태 플래그 ===
    public bool isOverriddenBySkill = false;
    // =====================================

    private const float ArmTime = 0.3f;

    [Header("이 프리팹은 탄의 그림·충돌만 담당. 속도 등은 쏘는 적(Enemy·Boss)에서 설정")]
    [Tooltip("켜면 탄이 닿은 물체 이름을 Console에 표시")]
    [SerializeField] private bool logHits = false;

    private Rigidbody2D rb;
    private Vector3 baseScale;
    private Transform target;
    private BulletSettings settings = new BulletSettings();
    private ExplosionSettings explosion;
    private int damage = 1;
    private bool homing;
    private float lifeTime;
    private float age;
    private bool exploding;
    private float explodeTimer;
    private SpriteRenderer blast;
    private Color blastColor;

    public int Damage => damage;
    private bool IsBomb => explosion != null && explosion.radius > 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        baseScale = transform.localScale;
    }

    public void Launch(Transform target, int damage, bool homing, BulletSettings settings, float life = 0f, ExplosionSettings explosion = null)
    {
        this.target = target;
        this.damage = damage;
        this.homing = homing;
        this.settings = settings ?? new BulletSettings();
        this.explosion = explosion;
        lifeTime = life > 0f ? life : this.settings.lifeTime;
    }

    void FixedUpdate()
    {
        // === 추가된 부분: 스킬에 의해 제어 중이거나 폭발 중이면 기본 이동 중지 ===
        if (exploding || isOverriddenBySkill) return;
        // ==========================================================

        age += Time.fixedDeltaTime;

        if (age >= lifeTime)
        {
            if (IsBomb) StartExplosion();
            else Destroy(gameObject);
            return;
        }

        if (homing && age < settings.homingTime && target != null && target.gameObject.activeInHierarchy)
        {
            Vector2 d = (Vector2)target.position - rb.position;
            float targetAngle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
            rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, targetAngle, settings.turnSpeed * Time.fixedDeltaTime));
        }

        if (IsBomb)
        {
            transform.localScale = baseScale * (1f + Mathf.Sin(age / lifeTime * Mathf.PI) * 0.5f);
        }

        rb.linearVelocity = transform.up * settings.speed;
    }

    void Update()
    {
        if (!exploding) return;

        explodeTimer += Time.deltaTime;
        float t = Mathf.Clamp01(explodeTimer / Mathf.Max(explosion.duration, 0.01f));

        if (blast != null)
        {
            SetBlastSize(Mathf.Lerp(0.7f, 1f, t));
            Color c = blastColor;
            c.a *= 1f - t;
            blast.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }

    void StartExplosion()
    {
        if (exploding) return;
        exploding = true;

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            sr.enabled = false;
        }

        if (explosion.prefab != null)
        {
            blast = Instantiate(explosion.prefab, transform.position, Quaternion.identity);
            blastColor = blast.color;
            SetBlastSize(0.7f);
        }

        DealExplosionDamage();
    }

    void SetBlastSize(float ratio)
    {
        float spriteSize = blast.sprite != null ? Mathf.Max(blast.sprite.bounds.size.x, 0.01f) : 1f;
        float scale = explosion.radius * 2f / spriteSize * ratio;
        blast.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void DealExplosionDamage()
    {
        HashSet<Component> hit = new HashSet<Component>();

        foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, explosion.radius))
        {
            Player player = col.GetComponentInParent<Player>();
            if (player != null)
            {
                if (hit.Add(player)) player.TakeDamage(damage);
                continue;
            }

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null && hit.Add(enemy))
            {
                enemy.TakeDamage(damage);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (exploding) return;

        if (logHits)
        {
            Debug.Log($"[탄] {other.name}에 닿음 (레이어: {LayerMask.LayerToName(other.gameObject.layer)})");
        }

        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            if (player.IsDead) return;

            if (IsBomb)
            {
                StartExplosion();
                return;
            }

            player.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (age <= ArmTime) return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            if (IsBomb)
            {
                StartExplosion();
                return;
            }

            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (blast != null) Destroy(blast.gameObject);
    }
}