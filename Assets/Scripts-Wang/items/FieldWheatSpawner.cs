using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FieldWheatSpawner : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    [Header("Field Setting")]
    public RegionID fieldRegion = RegionID.Field;
    public TimeState requiredTime = TimeState.Autumn;

    [Header("Wheat Setting")]
    public RectTransform wheatPrefab;
    public RectTransform itemLayer;
    public Vector2 wheatSize = new Vector2(80f, 80f);

    [Header("Target Cart")]
    public CartController targetCart;

    [Header("Canvas")]
    public Canvas canvas;

    private RectTransform currentWheat;
    private bool isHoldingWheat;
    private bool hasEndedThisDrag;

    public void OnPointerDown(PointerEventData eventData)
    {
        hasEndedThisDrag = false;

        if (!CanCreateWheat())
        {
            return;
        }

        SpawnWheat(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isHoldingWheat || currentWheat == null) return;

        SetWheatPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        FinishWheatDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        FinishWheatDrag(eventData);
    }

    private bool CanCreateWheat()
    {
        TimeState currentTime = TimeManager.Instance.GetRegionTime(fieldRegion);

        if (currentTime != requiredTime)
        {
            Debug.Log("�����������룺���ﵱǰ���� Autumn������ " + currentTime);
            return false;
        }

        return true;
    }

    private void SpawnWheat(PointerEventData eventData)
    {
        if (wheatPrefab == null || itemLayer == null)
        {
            Debug.LogWarning("FieldWheatSpawner û������ Wheat Prefab �� Item Layer");
            return;
        }

        currentWheat = Instantiate(wheatPrefab, itemLayer);
        currentWheat.sizeDelta = wheatSize;

        Image wheatImage = currentWheat.GetComponent<Image>();
        if (wheatImage != null)
        {
            wheatImage.raycastTarget = false;
        }

        isHoldingWheat = true;

        SetWheatPosition(eventData);
    }

    private void SetWheatPosition(PointerEventData eventData)
    {
        if (currentWheat == null || itemLayer == null) return;

        Camera eventCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            itemLayer,
            eventData.position,
            eventCamera,
            out Vector2 localPoint
        );

        currentWheat.anchoredPosition = localPoint;
    }

    private void FinishWheatDrag(PointerEventData eventData)
    {
        if (hasEndedThisDrag) return;
        hasEndedThisDrag = true;

        if (!isHoldingWheat || currentWheat == null) return;

        bool loadedToCart = false;

        if (CanLoadToCart(eventData))
        {
            loadedToCart = targetCart.TryLoadCargo(CartCargo.Wheat);
        }

        Destroy(currentWheat.gameObject);

        currentWheat = null;
        isHoldingWheat = false;

        if (loadedToCart)
        {
            Debug.Log("����ɹ�װ��С�Ƴ�");
        }
        else
        {
            Debug.Log("����û��װ��С�Ƴ�������ʧ");
        }
    }

    private bool CanLoadToCart(PointerEventData eventData)
    {
        if (targetCart == null)
        {
            Debug.LogWarning("û������ Target Cart�������޷�װ��С�Ƴ�");
            return false;
        }

        if (targetCart.GetCurrentRegion() != fieldRegion)
        {
            Debug.Log("�����޷�װ�룺С�Ƴ�������������");
            return false;
        }

        if (!IsPointerOverCart(eventData))
        {
            Debug.Log("����û�зŵ�С�Ƴ���");
            return false;
        }

        return true;
    }

    private bool IsPointerOverCart(PointerEventData eventData)
    {
        RectTransform cartRect = targetCart.GetComponent<RectTransform>();

        if (cartRect == null)
        {
            return false;
        }

        Camera eventCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            cartRect,
            eventData.position,
            eventCamera
        );
    }
}