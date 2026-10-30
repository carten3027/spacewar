using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("스킬 기본 설정")]
    public float skillCooldown = 5f;
    private Dictionary<PartType, float> cooldownEnd = new Dictionary<PartType, float>();

    [Header("스킬 세부 파라미터")]
    [Tooltip("시간 정지 지속 시간")]
    public float timeStopDuration = 3f;
    [Tooltip("감속장 범위 및 비율")]
    public float slowRadius = 5f;
    public float slowFactor = 0.2f;
    public float slowDuration = 4f;
    [Tooltip("반사/블랙홀 반응 범위")]
    public float reflectRadius = 4f;
    public float blackHoleRadius = 6f;
    public float blackHoleDuration = 3f;

    // 퀵슬롯 스킬 실행 (Player.cs에서 호출)
    public bool ExecuteSkill(PartType skill)
    {
        if (cooldownEnd.TryGetValue(skill, out float end) && Time.time < end)
        {
            Debug.Log($"{skill} 재사용 대기 중 (남은 시간: {end - Time.time:F1}초)");
            return false;
        }

        bool success = false;
        switch (skill)
        {
            case PartType.ForceHoming: success = ExecuteForcedGuidance(); break;
            case PartType.TimeStop: success = ExecuteTimeStop(); break;
            case PartType.Reflect: success = ExecuteReflection(); break;
            case PartType.BlackHole: success = ExecuteBlackHole(); break;
            case PartType.SlowField: success = ExecuteSlowField(); break;
        }

        if (success)
        {
            cooldownEnd[skill] = Time.time + skillCooldown;
            Debug.Log($"[스킬 발동 성공] {skill}");
        }
        return success;
    }

    #region 5대 스킬 실질 기능 구현

    // 1. 강제 유도: 화면 내 적 투사체들을 가장 가까운 적에게 방향 전환
    private bool ExecuteForcedGuidance()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Boss boss = FindFirstObjectByType<Boss>();

        Transform targetTransform = null;
        if (boss != null) targetTransform = boss.transform;
        else if (enemies.Length > 0) targetTransform = enemies[0].transform;

        if (targetTransform == null) return false;

        EnemyBullet[] bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var bullet in bullets)
        {
            Vector2 dir = (targetTransform.position - bullet.transform.position).normalized;
            bullet.isOverriddenBySkill = true;

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = dir * 12f;
                bullet.transform.up = dir;
            }
        }
        return true;
    }

    // 2. 시간 정지: 일정 시간 동안 적의 투사체 속도를 0으로 고정
    private bool ExecuteTimeStop()
    {
        StartCoroutine(TimeStopRoutine(timeStopDuration));
        return true;
    }

    private IEnumerator TimeStopRoutine(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            EnemyBullet[] bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
            foreach (var bullet in bullets)
            {
                bullet.isOverriddenBySkill = true;
                Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
            yield return null;
        }

        // 시간 정지 해제
        EnemyBullet[] remainBullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var bullet in remainBullets)
        {
            bullet.isOverriddenBySkill = false;
        }
    }

    // 3. 공격 반사: 주변 투사체의 진행 방향을 반대로 역전
    private bool ExecuteReflection()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, reflectRadius);
        int reflectedCount = 0;

        foreach (var col in cols)
        {
            EnemyBullet bullet = col.GetComponentInParent<EnemyBullet>();
            if (bullet != null)
            {
                bullet.isOverriddenBySkill = true;
                Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = -rb.linearVelocity * 1.5f;
                    bullet.transform.up = -bullet.transform.up;
                    reflectedCount++;
                }
            }
        }
        return reflectedCount > 0;
    }

    // 4. 블랙홀: 주변 투사체들을 플레이어 위치(또는 지정 위치)로 끌어당겨 소멸
    private bool ExecuteBlackHole()
    {
        StartCoroutine(BlackHoleRoutine(transform.position, blackHoleDuration));
        return true;
    }

    private IEnumerator BlackHoleRoutine(Vector3 center, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            Collider2D[] cols = Physics2D.OverlapCircleAll(center, blackHoleRadius);

            foreach (var col in cols)
            {
                EnemyBullet bullet = col.GetComponentInParent<EnemyBullet>();
                if (bullet != null)
                {
                    bullet.isOverriddenBySkill = true;
                    Vector2 pullDir = (center - bullet.transform.position);

                    if (pullDir.magnitude < 0.8f)
                    {
                        Destroy(bullet.gameObject); // 블랙홀 중심에 도달하면 소멸
                    }
                    else
                    {
                        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                        if (rb != null) rb.linearVelocity = pullDir.normalized * 8f;
                    }
                }
            }
            yield return null;
        }
    }

    // 5. 감속장: 플레이어 주변 투사체의 속도를 대폭 감소
    private bool ExecuteSlowField()
    {
        StartCoroutine(SlowFieldRoutine(slowDuration));
        return true;
    }

    private IEnumerator SlowFieldRoutine(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, slowRadius);

            foreach (var col in cols)
            {
                EnemyBullet bullet = col.GetComponentInParent<EnemyBullet>();
                if (bullet != null)
                {
                    bullet.isOverriddenBySkill = true;
                    Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = rb.linearVelocity.normalized * (2f * slowFactor);
                }
            }
            yield return null;
        }
    }

    #endregion

    public void HitEnemy(GameObject enemy, float reverseHitDamage)
    {
        Boss boss = enemy.GetComponent<Boss>();
        if (boss != null)
        {
            boss.TakeDamage(Mathf.RoundToInt(reverseHitDamage));
        }
    }
}