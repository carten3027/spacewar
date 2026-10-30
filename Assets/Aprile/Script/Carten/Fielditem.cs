// =========================================================================================
// [1. 네임스페이스(Namespace) 불러오기]
// =========================================================================================
using UnityEngine; // 유니티 엔진의 기본 클래스(MonoBehaviour, Destroy, OnTriggerEnter2D 등) 사용

// =========================================================================================
// [2. FieldItem 메인 클래스]
// - 필드(맵) 상에 드롭된 아이템 오브젝트에 부착되는 스크립트입니다.
// - MonoBehaviour 상속: 게임 오브젝트에 부착되어 충돌 감지 및 타이머 기능을 수행할 수 있게 합니다.
// =========================================================================================
public class FieldItem : MonoBehaviour
{
    // [Header("...")] : 유니티 인스펙터(Inspector) 창에서 변수들을 그룹화해 보여주는 시각적 속성
    [Header("아이템 속성 설정")]
    // 이전 스크립트에서 선언한 DropItemType 열거형(Enum) 사용 (기본값: Heal)
    public DropItemType itemType = DropItemType.Heal;

    // float: 소수점을 포함하는 실수 데이터 타입 (숫자 끝에 'f'를 붙여 명시)
    public float itemWeight = 2f;    // 아이템 무게 (기본값: 2)
    public float effectValue = 20f;  // 효과 치수 (회복량, 버프 지속시간, 돈 액수 등)

    [Header("소멸 설정 (기획서 11-⑦, 13-④)")]
    // bool: 참(true) 또는 거짓(false)만 저장하는 논리형 데이터 타입
    public bool destroyAfterTime = true; // 일정 시간이 지나면 자동 소멸할지 여부
    public float lifeTime = 8f;          // 생성 후 소멸되기까지 걸리는 시간 (초 단위, 기본값: 8초)

    // -------------------------------------------------------------------------------------
    // [Start() : 유니티 초기화 생명주기(Life Cycle)]
    // - 이 게임 오브젝트가 씬에 생성될 때 딱 1번 실행되는 함수입니다.
    // -------------------------------------------------------------------------------------
    private void Start()
    {
        // 자동 소멸 옵션(destroyAfterTime)이 true라면
        if (destroyAfterTime)
        {
            // Destroy(오브젝트, 지연시간): 지정한 시간(초)이 지난 후 대상을 파괴하는 유니티 내장 함수
            // gameObject: 이 스크립트가 부착된 '자기 자신 게임 오브젝트'를 가리킵니다.
            Destroy(gameObject, lifeTime);
        }
    }

    // -------------------------------------------------------------------------------------
    // [OnTriggerEnter2D : 유니티 2D 트리거 충돌 이벤트]
    // - 작동 조건: 
    //   1. 두 오브젝트 중 최소 하나에 Collider2D의 'Is Trigger'가 체크되어 있어야 함
    //   2. 최소 하나에 Rigidbody2D 컴포넌트가 부착되어 있어야 함
    // - other: 이 아이템의 영역 안으로 들어온 '상대방 오브젝트의 Collider2D' 정보
    // -------------------------------------------------------------------------------------
    private void OnTriggerEnter2D(Collider2D other)
    {
        // CompareTag("태그명"): 상대방 오브젝트의 태그(Tag)가 "Player"인지 검사
        // ※ other.tag == "Player" 보다 메모리 할당을 하지 않아 실행 속도가 빠르고 효율적입니다.
        if (other.CompareTag("Player"))
        {
            // Null 체크 (Safety Check): 인벤토리 싱글톤 인스턴스가 존재하는지 확인하여 에러 방지
            if (InventoryAndItem.Instance != null)
            {
                // TryAddItemToBag()을 호출하여 가방에 아이템 추가를 시도함
                // 성공 시 true, 실패(가방 가득 참 / 무게 초과) 시 false가 pickedUp 변수에 저장됨
                bool pickedUp = InventoryAndItem.Instance.TryAddItemToBag(itemType, itemWeight, effectValue);

                // 아이템 습득 효과음 (성공: 획득 / 실패: 가방 가득 참)
                SoundManager.Play(pickedUp ? SoundManager.Sfx.ItemPickup : SoundManager.Sfx.BagFull);

                // 아이템 습득에 성공한 경우 (가방 개수/무게 제한 통과)
                if (pickedUp)
                {
                    // 부딪힌 플레이어에게서 Player 스크립트 컴포넌트를 찾아옴
                    Player player = other.GetComponentInParent<Player>();
                    if (player != null)
                    {
                        // 아이템 종류가 Heal(회복)이면 플레이어 체력 회복!
                        if (itemType == DropItemType.Heal)
                        {
                            SoundManager.Play(SoundManager.Sfx.ItemPickup); // [추가] 아이템 획득 소리!
                            player.Heal(Mathf.RoundToInt(effectValue));
                        }
                        // 아이템 종류가 Invincible(무적)이면 플레이어 무적 발동!
                        else if (itemType == DropItemType.Invincible)
                        {
                            SoundManager.Play(SoundManager.Sfx.BagFull);
                            player.ActivateInvincibleItem(effectValue);
                        }
                    }

                    // 맵 필드에 남아있는 아이템 오브젝트를 즉시 삭제
                    Destroy(gameObject);
                }
            }
        }
    }
}