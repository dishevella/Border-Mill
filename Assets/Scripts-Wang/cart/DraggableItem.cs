using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Item Setting")]
    public CartCargo cargoType;
    public RegionID itemRegion;

    [Header("Target Cart")]
    public CartController targetCart;

    private Vector3 startWorldPosition;

    public void OnBeginDrag(PointerEventData eventData)
    {
        startWorldPosition = transform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (targetCart == null)
        {
            ReturnToStart();
            return;
        }

        if (!IsPointerOverCart(eventData))
        {
            ReturnToStart();
            return;
        }

        if (targetCart.GetCurrentRegion() != itemRegion)
        {
            Debug.Log("物品和小推车不在同一区域，不能装入");
            ReturnToStart();
            return;
        }

        bool loaded = targetCart.TryLoadCargo(cargoType);

        if (loaded)
        {
            gameObject.SetActive(false);
        }
        else
        {
            ReturnToStart();
        }
    }

    private bool IsPointerOverCart(PointerEventData eventData)
    {
        RectTransform cartRect = targetCart.GetComponent<RectTransform>();

        if (cartRect == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            cartRect,
            eventData.position,
            eventData.pressEventCamera
        );
    }

    private void ReturnToStart()
    {
        transform.position = startWorldPosition;
    }
}