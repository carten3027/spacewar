using UnityEngine;

public class Boss_Missile : MonoBehaviour
{
    [UnityEngine.SerializeField]
    private Vector3 destination;
    [UnityEngine.SerializeField]
    private bool isThrow;

    public float Missile_Spd = 10f;

    public Vector2 dir;

    void Update()
    {
        GameObject player = GameObject.FindWithTag("Player");        // 실시간으로 플레이어를 찾아서 방향을 계속 업데이트
        if (player != null)
        {
            dir = player.transform.position - transform.position;


            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;                    // 날아가는 각도도 실시간으로 변경
            transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);    // y축을 정면으로 보기
        }

        // 이동
        transform.position += (Vector3)dir.normalized * Time.deltaTime * Missile_Spd;
    }
}