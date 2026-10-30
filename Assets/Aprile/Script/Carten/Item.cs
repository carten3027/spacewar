using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("Skill Settings")]
    public float skillCooldown = 5f;
    private Dictionary<PartType, float> cooldownEnd = new Dictionary<PartType, float>();

    // ����ü ���� ĳ���� ���� ��ųʸ�
    private Dictionary<GameObject, ThrowState> states = new Dictionary<GameObject, ThrowState>();

    // 3�ܰ�: ����ü(EnemyBullet) ���� ������ ���� Ŭ����
    private class ThrowState
    {
        public Transform tf;
        public Boss_Missile missile;
        public EnemyBullet enemyBullet;
        public Rigidbody2D rb;
        // ������ ����Ͻô� �߰� �������� �ִٸ� �̰��� �����ϼ���.
    }

    // 2�ܰ�: Player_2.cs�� UseSkill()���� ������ �����͸� ������� ȣ��Ǵ� ���� ���� �޼���
    // (���� Update() �ȿ� �ִ� Input.GetKeyDown ������ ������ �����մϴ�.)
    public bool ExecuteSkill(PartType skill)
    {
        // ��Ÿ�� üũ
        if (cooldownEnd.TryGetValue(skill, out float end) && Time.time < end)
        {
            Debug.Log($"{skill} ���� ��� ��");
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

        // ��ų ������ �������� ��� ��Ÿ�� ����
        if (success)
        {
            cooldownEnd[skill] = Time.time + skillCooldown;
        }
        return success;
    }

    #region ���� ��ų ������ (���� ���� ����)
    private bool ExecuteForcedGuidance()
    {
        // TODO: ���� ���� ��ų ������ ���⿡ �ۼ��ϼ���.
        return true;
    }

    private bool ExecuteTimeStop()
    {
        // TODO: �ð� ���� ��ų ������ ���⿡ �ۼ��ϼ���.
        return true;
    }

    private bool ExecuteReflection()
    {
        // TODO: �ݻ� ��ų ������ ���⿡ �ۼ��ϼ���.
        return true;
    }

    private bool ExecuteBlackHole()
    {
        // TODO: ����Ȧ ��ų ������ ���⿡ �ۼ��ϼ���.
        return true;
    }

    private bool ExecuteSlowField()
    {
        // TODO: ���ο� �ʵ� ��ų ������ ���⿡ �ۼ��ϼ���.
        return true;
    }
    #endregion

    // 1�ܰ�: �ݻ� ������ ó�� �� ����(int) ��ȯ ����
    public void HitEnemy(GameObject enemy, float reverseHitDamage)
    {
        Boss boss = enemy.GetComponent<Boss>();
        if (boss != null)
        {
            boss.TakeDamage(Mathf.RoundToInt(reverseHitDamage));
        }

        // �Ϲ� ���� �ǰ� ���� �� ���� �ڵ带 �Ʒ��� �����ϼ���.
    }

    // 3�ܰ�: EnemyBullet���� �����Ͽ� ����ü�� �����ϵ��� ������ GetState
    private ThrowState GetState(GameObject obj)
    {
        if (states.TryGetValue(obj, out ThrowState st)) return st;

        Boss_Missile missile = obj.GetComponent<Boss_Missile>();
        EnemyBullet enemyBullet = obj.GetComponent<EnemyBullet>();
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();

        // EnemyBullet�� �νĵǵ��� ���� ������
        if (missile == null && enemyBullet == null && rb == null) return null;

        st = new ThrowState();
        st.tf = obj.transform;
        st.missile = missile;
        st.enemyBullet = enemyBullet; // EnemyBullet ĳ��
        st.rb = rb;
        states.Add(obj, st);

        return st;
    }

    // ��ų ȿ�� ���� �� ����ü ����� Ż�� ���� �޼���
    public void ApplyVelocity(GameObject obj, Vector2 velocity)
    {
        ThrowState state = GetState(obj);

        // ����ü�� ��ü Update/FixedUpdate �̵��� ������Ŵ
        if (state != null && state.enemyBullet != null)
        {
            state.enemyBullet.isOverriddenBySkill = true;
        }

        if (state != null && state.rb != null)
        {
            state.rb.linearVelocity = velocity;
        }
    }
}