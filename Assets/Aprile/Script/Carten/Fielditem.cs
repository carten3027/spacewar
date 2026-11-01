using UnityEngine;

public class FieldItem : MonoBehaviour
{
    [Header("아이템 종류 및 무게 설정 (기획서 1-5-2, 9-②)")]
    public DropItemType itemType = DropItemType.Heal;
    public float itemWeight = 2f;      // 가방에서 차지할 무게 (kg)
    public float effectValue = 1f;     // 회복량(1~2) 또는 버프 지속시간(초), 돈 액수
    public float bagDuration = 5f;     // 가방에 머무르며 효과가 유지되는 시간(초)
    public bool isPermanent = false;   // 체크하면 가방에서 소멸하지 않음 (기획서 11-⑦)

    [Header("필드 자동 소멸 설정 (기획서 13-④, ⑤)")]
    public bool destroyAfterTime = true;
    public float lifeTime = 8f;        // 먹지 않으면 8초 뒤 맵에서 사라짐

    private void Start()
    {
        if (destroyAfterTime)
        {
            Destroy(gameObject, lifeTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        if (InventoryAndItem.Instance != null)
        {
            // 가방 개수 및 무게 제한 검사 시도
            bool pickedUp = InventoryAndItem.Instance.TryAddItemToBag(itemType, itemWeight, effectValue, bagDuration, isPermanent);

            if (pickedUp)
            {
                // 1. 아이템 획득 효과음 재생
                SoundManager.Play(SoundManager.Sfx.ItemPickup);

                // 2. 종류별 실제 효과 즉시 발동
                if (itemType == DropItemType.Heal)
                {
                    player.Heal(Mathf.RoundToInt(effectValue));
                }
                else if (itemType == DropItemType.Invincible)
                {
                    player.ActivateInvincibleItem(effectValue);
                }
                else if (itemType == DropItemType.DefenseUp)
                {
                    player.ActivateDefenseUpItem(effectValue);
                }

                // 3. 필드에서 아이템 오브젝트 제거
                Destroy(gameObject);
            }
            else
            {
                // 가방이 가득 차거나 무게 초과일 때 경고 효과음 재생!
                SoundManager.Play(SoundManager.Sfx.BagFull);
            }
        }
    }
}