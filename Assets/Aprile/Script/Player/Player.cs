using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public enum ControlMode
    {
        [InspectorName("탱크식 - ←→ 회전, ↑ 전진 (기획서)")] Tank,
        [InspectorName("방향식 - 누른 방향으로 머리를 돌리며 전진")] Direction
    }

    [Header("조작 방식")]
    [SerializeField] private ControlMode controlMode = ControlMode.Tank;
    [Tooltip("켜면 키를 누르지 않아도 머리 방향으로 계속 전진")]
    [SerializeField] private bool autoForward = true;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float slowSpeed = 2.5f;

    [Header("회전")]
    [Tooltip("초당 회전 각도")]
    [SerializeField] private float turnSpeed = 300f;
    [Tooltip("감속(Shift) 중 회전 각도. 느리게 움직이는 대신 더 빨리 돎")]
    [SerializeField] private float slowTurnSpeed = 450f;
    [Tooltip("탱크식에서 멈춰 있을 때 회전 배율 (제자리 회전을 더 빠르게)")]
    [SerializeField] private float idleTurnMultiplier = 1.3f;
    [Tooltip("회전 키를 누른 뒤 최고 회전 속도까지 걸리는 시간(초). 짧게 톡 누르면 조금만 돎 (0 = 즉시)")]
    [SerializeField] private float turnResponse = 0.12f;

    [Header("화면")]
    [Tooltip("켜면 화면 끝으로 나가면 반대쪽에서 나옴. 끄면 화면 끝에서 멈춤")]
    [SerializeField] private bool wrapScreen = true;
    [Tooltip("켜짐: 화면 밖으로 이만큼 나가야 반대쪽으로 넘어감 / 꺼짐: 화면 끝에서 안쪽으로 남길 여백")]
    [SerializeField] private float screenMargin = 0.5f;

    [Header("조작 키")]
    [SerializeField] private KeyCode slowKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode bombKey = KeyCode.Q;

    [Header("태그")]
    [Tooltip("폭탄이 지우는 탄, 운석 같은 방해물에 붙이는 태그")]
    [SerializeField] private string enemyBulletTag = "EnemyBullet";

    [Header("HP")]
    [SerializeField] private int maxHp = 5;
    [Tooltip("미사일이 아닌 EnemyBullet 태그 물체(운석 등)에 맞았을 때 깎이는 양")]
    [SerializeField] private int damagePerHit = 1;

    [Header("폭탄")]
    [SerializeField] private int bombCount = 3;
    [Tooltip("폭탄을 쓸 때 번쩍이는 화면 전체 흰색 이미지 (Raycast Target 끄기)")]
    [SerializeField] private Image bombFlash;
    [Tooltip("번쩍일 때 최대 밝기 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.8f;
    [Tooltip("밝아졌다가 사라지는 시간(초)")]
    [SerializeField] private float flashTime = 0.5f;

    [Header("적 밀어내기")]
    [SerializeField] private float enemyKnockbackDistance = 2f;
    [SerializeField] private float enemyKnockbackDuration = 0.2f;

    [Header("무적 (피격 후)")]
    [SerializeField] private float invincibleTime = 1.5f;
    [SerializeField] private float blinkInterval = 0.1f;

    [Header("게임오버")]
    [Tooltip("켜면 죽은 뒤 같은 스테이지를 다시 시작 (돈·파츠는 유지)")]
    [SerializeField] private bool restartOnDeath = true;
    [SerializeField] private float restartDelay = 2f;
    [SerializeField] private UnityEvent onDeath;

    private Camera mainCam;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;

    private int currentHp;
    private int stageStartScore;
    private float invincibleUntil;
    private Coroutine blinkRoutine;
    private Coroutine flashRoutine;
    private bool isMoving;
    private bool isDead;
    private bool isDefenseUp; // 방어력 강화 아이템 상태 스위치
    private float turnVelocity;
    private Vector2 bounceVelocity;
    private float bounceTimer;

    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public float HpRatio => (float)currentHp / maxHp;
    public int BombCount => bombCount;
    public bool IsDead => isDead;
    public bool IsInvincible => CheatInvincible || Time.time < invincibleUntil;
    public bool CheatInvincible { get; set; }

    private void Awake()
    {
        mainCam = Camera.main;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        col = GetComponentInChildren<Collider2D>();
        currentHp = maxHp;
        Enemy.KillCount = 0;
    }

    private void Start()
    {
        SetFlashAlpha(0f);

        if (DataManager.Instance != null)
        {
            stageStartScore = DataManager.Instance.currentScore;
        }

        if (col == null)
        {
            Debug.LogWarning("[Player] Collider 2D가 없어요. Add Component → Circle Collider 2D를 붙여 주세요.");
        }

        if (GetComponentInChildren<Collider>() != null)
        {
            Debug.LogWarning("[Player] 3D Collider가 붙어 있어요. 이름에 2D가 들어간 Collider로 바꿔 주세요.");
        }
    }

    private void Update()
    {
        if (isDead) return;

        HandleMovement();

        if (Input.GetKeyDown(bombKey) && bombCount > 0)
        {
            UseBomb();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            UseSkill(0);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            UseSkill(1);
        }
    }

    private void HandleMovement()
    {
        bool isSlow = Input.GetKey(slowKey);
        float speed = isSlow ? slowSpeed : moveSpeed;
        float turnRate = isSlow ? slowTurnSpeed : turnSpeed;
        float forward;

        if (controlMode == ControlMode.Tank)
        {
            float turn = Input.GetAxisRaw("Horizontal");
            forward = autoForward ? 1f : Mathf.Max(0f, Input.GetAxisRaw("Vertical"));

            if (forward == 0f) turnRate *= idleTurnMultiplier;

            float targetTurn = -turn * turnRate;
            if (turn == 0f || targetTurn * turnVelocity < 0f) turnVelocity = 0f;

            turnVelocity = turnResponse > 0f
                ? Mathf.MoveTowards(turnVelocity, targetTurn, turnRate / turnResponse * Time.deltaTime)
                : targetTurn;

            transform.Rotate(0f, 0f, turnVelocity * Time.deltaTime);
        }
        else
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool hasInput = input.sqrMagnitude > 0.01f;
            forward = hasInput || autoForward ? 1f : 0f;

            if (hasInput)
            {
                float targetAngle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg - 90f;
                float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, turnRate * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        isMoving = forward > 0f;

        Vector3 newPos = transform.position + transform.up * (forward * speed * Time.deltaTime);

        if (bounceTimer > 0f)
        {
            bounceTimer -= Time.deltaTime;
            newPos += (Vector3)(bounceVelocity * Time.deltaTime);
        }

        Vector2 edgeMin = mainCam.ViewportToWorldPoint(Vector3.zero);
        Vector2 edgeMax = mainCam.ViewportToWorldPoint(Vector3.one);

        if (wrapScreen)
        {
            Vector2 min = edgeMin - Vector2.one * screenMargin;
            Vector2 max = edgeMax + Vector2.one * screenMargin;

            if (newPos.x > max.x) newPos.x = min.x;
            else if (newPos.x < min.x) newPos.x = max.x;

            if (newPos.y > max.y) newPos.y = min.y;
            else if (newPos.y < min.y) newPos.y = max.y;
        }
        else
        {
            Vector2 min = edgeMin + Vector2.one * screenMargin;
            Vector2 max = edgeMax - Vector2.one * screenMargin;

            newPos.x = Mathf.Clamp(newPos.x, min.x, max.x);
            newPos.y = Mathf.Clamp(newPos.y, min.y, max.y);
        }

        transform.position = newPos;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || !other.CompareTag(enemyBulletTag)) return;
        if (other.GetComponentInParent<EnemyBullet>() != null) return;

        Destroy(other.gameObject);
        TakeDamage(damagePerHit);
    }

    // ---------------- [아이템 효과 함수 3종 (중복 제거 완료)] ----------------
    // 1. 생명력 회복 아이템 효과 (기획서 11-③: 습득과 동시에 생명치 증가)
    public void Heal(int amount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        Debug.Log($"[아이템] 생명력 +{amount} 회복! 현재 HP: {currentHp}/{maxHp}");
    }

    // 2. 일정 시간 무적 아이템 효과 (기획서 9-②)
    public void ActivateInvincibleItem(float duration)
    {
        MakeInvincible(duration, true);
        Debug.Log($"[아이템] {duration}초 동안 무적 효과 발동!");
    }

    // 3. 일정 시간 방어력 강화 아이템 효과 (기획서 9-②)
    public void ActivateDefenseUpItem(float duration)
    {
        StartCoroutine(DefenseUpRoutine(duration));
    }

    private IEnumerator DefenseUpRoutine(float duration)
    {
        isDefenseUp = true;
        Debug.Log($"[아이템] {duration}초 동안 방어력 강화 시작!");
        yield return new WaitForSeconds(duration);
        isDefenseUp = false;
        Debug.Log("[아이템] 방어력 강화 종료!");
    }

    public void Bounce(Vector2 dir, float distance, float duration)
    {
        if (isDead) return;

        bounceVelocity = dir.normalized * (distance / Mathf.Max(duration, 0.01f));
        bounceTimer = duration;

        MakeInvincible(invincibleTime, false);
    }

    public void PushEnemy(Enemy enemy)
    {
        if (isDead) return;

        Vector2 toEnemy = enemy.transform.position - transform.position;
        bool inFront = Vector2.Dot(transform.up, toEnemy) > 0f;
        Vector2 dir = isMoving && inFront ? (Vector2)transform.up : toEnemy;

        enemy.ApplyKnockback(dir, enemyKnockbackDistance, enemyKnockbackDuration);
    }

    public void TakeDamage(int damage)
    {
        if (isDead || IsInvincible) return;

        // 방어력 강화 중이면 데미지를 절반(최소 1)으로 감소
        int finalDamage = isDefenseUp ? Mathf.Max(1, damage / 2) : damage;
        currentHp = Mathf.Max(0, currentHp - finalDamage);

        SoundManager.Play(SoundManager.Sfx.Hit);
        Debug.Log($"피격! HP: {currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            Die();
            return;
        }

        MakeInvincible(invincibleTime, true);
    }

    private void Die()
    {
        isDead = true;
        StopAllCoroutines();
        SetFlashAlpha(0f);

        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (col != null) col.enabled = false;

        if (DataManager.Instance != null)
        {
            DataManager.Instance.currentScore = stageStartScore;
        }

        Debug.Log("플레이어 사망");
        onDeath.Invoke();

        if (restartOnDeath)
        {
            Invoke(nameof(Restart), restartDelay);
        }
    }

    private void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void MakeInvincible(float duration, bool blink)
    {
        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);

        if (!blink) return;

        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(BlinkRoutine(duration));
    }

    private IEnumerator BlinkRoutine(float duration)
    {
        float end = Time.time + duration;

        while (Time.time < end)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;

            yield return new WaitForSeconds(blinkInterval);
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        blinkRoutine = null;
    }

    private void UseBomb()
    {
        bombCount--;
        SoundManager.Play(SoundManager.Sfx.Bomb);

        foreach (GameObject bullet in GameObject.FindGameObjectsWithTag(enemyBulletTag))
        {
            if (IsVisibleOnCamera(bullet.transform.position))
            {
                Destroy(bullet);
            }
        }

        if (bombFlash != null)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }
    }

    private void UseSkill(int slotIndex)
    {
        if (DataManager.Instance == null) return;

        PartType skill = DataManager.Instance.quickSlots[slotIndex];
        if (skill == PartType.None) return;

        Item item = GetComponent<Item>();
        if (item != null)
        {
            item.ExecuteSkill(skill);
        }
    }

    private IEnumerator FlashRoutine()
    {
        for (float t = 0f; t < flashTime; t += Time.unscaledDeltaTime)
        {
            float remain = 1f - t / flashTime;
            SetFlashAlpha(flashAlpha * remain * remain);
            yield return null;
        }

        SetFlashAlpha(0f);
        flashRoutine = null;
    }

    private void SetFlashAlpha(float alpha)
    {
        if (bombFlash == null) return;

        Color c = bombFlash.color;
        c.a = alpha;
        bombFlash.color = c;
    }

    private bool IsVisibleOnCamera(Vector3 worldPos)
    {
        Vector3 viewPos = mainCam.WorldToViewportPoint(worldPos);

        return viewPos.z > 0f &&
               viewPos.x >= 0f && viewPos.x <= 1f &&
               viewPos.y >= 0f && viewPos.y <= 1f;
    }
}