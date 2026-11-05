using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum DropItemType
{
    Heal,       // 0번: 생명력 회복
    Invincible, // 1번: 일정 시간 무적
    DefenseUp,  // 2번: 일정 시간 방어력 강화
    Money       // 3번: 파편(돈)
}

[System.Serializable]
public class InventoryItem
{
    public DropItemType itemType;
    public float weight;
    public float value;
    public bool isPermanent;
}

public class InventoryAndItem : MonoBehaviour
{
    public static InventoryAndItem Instance { get; private set; }

    [Header("가방 제한 설정")]
    public int maxItemCount = 5;
    public float maxWeightLimit = 10f;

    [Header("현재 가방 목록")]
    public List<InventoryItem> bag = new List<InventoryItem>();
    public float CurrentWeight { get; private set; } = 0f;

    [Header("화면 UI 표시 설정")]
    public bool showBagUI = true;

    private TextMeshProUGUI bagHudText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public bool TryAddItemToBag(DropItemType type, float weight, float value, float duration = 5f, bool isPermanent = false)
    {
        if (type == DropItemType.Money)
        {
            if (DataManager.Instance != null)
                DataManager.Instance.money += Mathf.RoundToInt(value);
            return true;
        }

        if (bag.Count >= maxItemCount) return false;
        if (CurrentWeight + weight > maxWeightLimit) return false;

        InventoryItem newItem = new InventoryItem
        {
            itemType = type,
            weight = weight,
            value = value,
            isPermanent = isPermanent
        };

        bag.Add(newItem);
        CurrentWeight += weight;

        if (!isPermanent && duration > 0f)
        {
            StartCoroutine(RemoveItemAfterTime(newItem, duration));
        }

        return true;
    }

    private IEnumerator RemoveItemAfterTime(InventoryItem item, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (bag.Contains(item))
        {
            bag.Remove(item);
            CurrentWeight = Mathf.Max(0f, CurrentWeight - item.weight);
        }
    }

    private void LateUpdate()
    {
        if (!showBagUI || SceneManager.GetActiveScene().name != "TeamProject")
        {
            if (bagHudText != null) bagHudText.gameObject.SetActive(false);
            return;
        }

        GameObject shopPanel = GameObject.Find("ShopPanel");
        bool isShopOpen = shopPanel != null && shopPanel.activeInHierarchy;

        if (bagHudText == null) CreateCanvasBagHUD();

        if (bagHudText != null)
        {
            bagHudText.gameObject.SetActive(!isShopOpen);
            if (!isShopOpen)
            {
                string itemNames = "";
                foreach (var item in bag)
                {
                    string korName = item.itemType == DropItemType.Heal ? "회복" :
                                     item.itemType == DropItemType.Invincible ? "무적" : "방어강화";
                    itemNames += $"[{korName}({item.weight}kg)] ";
                }
                if (string.IsNullOrEmpty(itemNames)) itemNames = "비어 있음";

                bagHudText.text = $"배낭 : {bag.Count}/{maxItemCount}개 ({CurrentWeight:F1}/{maxWeightLimit:F1}kg)\n보유 : {itemNames}";
            }
        }
    }

    private void CreateCanvasBagHUD()
    {
        GameObject hudObj = GameObject.Find("HUD");
        if (hudObj == null) return;

        GameObject go = new GameObject("BagStatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(hudObj.transform, false);

        bagHudText = go.GetComponent<TextMeshProUGUI>();
        GameObject sampleObj = GameObject.Find("ScoreText");
        if (sampleObj != null && sampleObj.TryGetComponent(out TMP_Text sampleTmp))
        {
            bagHudText.font = sampleTmp.font;
        }

        bagHudText.fontSize = 26;
        bagHudText.alignment = TextAlignmentOptions.BottomLeft;
        bagHudText.color = Color.white;
        bagHudText.raycastTarget = false;

        RectTransform rt = bagHudText.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(25f, 20f);
        rt.sizeDelta = new Vector2(700f, 80f);
    }
}