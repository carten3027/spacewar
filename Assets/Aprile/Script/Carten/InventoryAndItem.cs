using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DropItemType
{
    Heal,       // 0번: 생명력 회복
    Invincible, // 1번: 일정 시간 무적
    DefenseUp,  // 2번: 일정 시간 방어력 강화
    Money       // 3번: 파편(돈) - 가방 무게 차지 안 함
}

[System.Serializable]
public class InventoryItem
{
    public DropItemType itemType;
    public float weight;
    public float value;
    public bool isPermanent; // true면 소멸하지 않고 가방에 영구 유지 (기획서 11-⑦)
}

public class InventoryAndItem : MonoBehaviour
{
    public static InventoryAndItem Instance { get; private set; }

    [Header("가방 제한 설정 (기획서 1-5-2)")]
    public int maxItemCount = 5;       // 최대 보유 개수 제한
    public float maxWeightLimit = 10f; // 최대 무게 제한

    [Header("현재 가방 목록")]
    public List<InventoryItem> bag = new List<InventoryItem>();
    public float CurrentWeight { get; private set; } = 0f;

    [Header("화면 UI 표시 설정 (기획서 나-7)")]
    public bool showBagUI = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // 아이템 획득 시도 (개수 및 무게 제한 검사)
    public bool TryAddItemToBag(DropItemType type, float weight, float value, float duration = 5f, bool isPermanent = false)
    {
        // 1. 돈(파편)은 가방 칸/무게를 차지하지 않고 즉시 획득
        if (type == DropItemType.Money)
        {
            if (DataManager.Instance != null)
                DataManager.Instance.money += Mathf.RoundToInt(value);
            return true;
        }

        // 2. 가방 개수 제한 검사
        if (bag.Count >= maxItemCount)
        {
            Debug.Log("[배낭] 개수 제한 초과로 아이템을 획득할 수 없습니다!");
            return false;
        }

        // 3. 가방 무게 제한 검사
        if (CurrentWeight + weight > maxWeightLimit)
        {
            Debug.Log("[배낭] 무게 제한 초과로 아이템을 획득할 수 없습니다!");
            return false;
        }

        // 4. 가방에 아이템 추가 및 무게 증가
        InventoryItem newItem = new InventoryItem
        {
            itemType = type,
            weight = weight,
            value = value,
            isPermanent = isPermanent
        };

        bag.Add(newItem);
        CurrentWeight += weight;

        // 5. 기획서 11-⑦: 영구 아이템이 아니라면 일정 시간(duration) 후 기능과 함께 가방에서 자동 소멸
        if (!isPermanent && duration > 0f)
        {
            StartCoroutine(RemoveItemAfterTime(newItem, duration));
        }

        return true;
    }

    // 일정 시간이 흐른 뒤 가방에서 아이템을 소멸시켜 무게와 칸을 돌려주는 코루틴
    private IEnumerator RemoveItemAfterTime(InventoryItem item, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (bag.Contains(item))
        {
            bag.Remove(item);
            CurrentWeight = Mathf.Max(0f, CurrentWeight - item.weight);
            Debug.Log($"[배낭] {item.itemType} 아이템의 지속시간이 끝나 소멸했습니다. (현재 무게: {CurrentWeight}/{maxWeightLimit})");
        }
    }

    // 별도의 Canvas 작업 없이도 화면 좌측 하단에 배낭 상태와 습득 아이템을 깔끔하게 표시
    // [InventoryAndItem.cs 맨 아래 OnGUI() 함수를 이 코드로 교체]
    private void OnGUI()
    {
        if (!showBagUI) return;

        // 1. 메인 메뉴 씬에서는 가방 UI를 띄우지 않음! (UI 겹침 방지)
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "TeamProject") return;

        // 2. 파츠 상점(ShopPanel)이 열려 있을 때도 가방 UI를 숨김! (상점 UI와 겹침 방지)
        GameObject shopPanel = GameObject.Find("ShopPanel");
        if (shopPanel != null && shopPanel.activeInHierarchy) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 16;
        style.alignment = TextAnchor.UpperLeft;
        style.normal.textColor = Color.white;

        string itemNames = "";
        foreach (var item in bag)
        {
            string korName = item.itemType == DropItemType.Heal ? "회복" :
                             item.itemType == DropItemType.Invincible ? "무적" : "방어강화";
            itemNames += $"[{korName}({item.weight}kg)] ";
        }
        if (string.IsNullOrEmpty(itemNames)) itemNames = "비어 있음";

        string bagInfo = $"[배낭 상태] : {bag.Count}/{maxItemCount}개 ({CurrentWeight:F1}/{maxWeightLimit:F1}kg)\n" +
                         $"- 보유: {itemNames}";

        // 좌측 하단 구석에 작고 깔끔하게 표시하여 기존 상단 HUD와 절대 안 겹치게 배치
        GUI.Box(new Rect(15, Screen.height - 75, 360, 60), bagInfo, style);
    }
}

// =========================================================================================
// [InventoryAndItem.cs 업그레이드 핵심 문법 정리 사전]
// =========================================================================================
// 1. bag.Remove(item) : 리스트에서 특정 아이템 객체를 찾아 쏙 빼내는 함수입니다.
// 2. RemoveItemAfterTime 코루틴 : 아이템을 먹었을 때 가방 무게와 개수를 차지하다가,
//    효과 시간(예: 5초)이 끝나면 가방에서 자동 소멸하여 다시 새 아이템을 먹을 수 있게 해줍니다.
// 3. OnGUI() : 유니티가 기본 제공하는 화면 그리기 함수로, Canvas에 복잡한 텍스트를 추가하지 않아도
//    화면 좌측 하단에 실시간으로 [개수 / 무게 / 현재 습득한 아이템 목록] 박스를 그려줍니다.
// =========================================================================================