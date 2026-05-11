using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public class CartVisualData
{
    public CartCargo cargo;

    [Header("Direction Sprites")]
    public Sprite leftSprite;
    public Sprite rightSprite;
    public Sprite upSprite;
    public Sprite downSprite;

    public Sprite GetSprite(CartDirection direction)
    {
        Sprite result = null;

        switch (direction)
        {
            case CartDirection.Left:
                result = leftSprite;
                break;

            case CartDirection.Right:
                result = rightSprite;
                break;

            case CartDirection.Up:
                result = upSprite;
                break;

            case CartDirection.Down:
                result = downSprite;
                break;
        }

        if (result != null) return result;

        if (rightSprite != null) return rightSprite;
        if (leftSprite != null) return leftSprite;
        if (upSprite != null) return upSprite;
        if (downSprite != null) return downSprite;

        return null;
    }
}

public class CartController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Cart State")]
    public RoadNode currentNode;
    public CartCargo currentCargo = CartCargo.Empty;
    public CartDirection currentDirection = CartDirection.Right;

    [Header("Drag Setting")]
    public float snapDistance = 120f;

    [Header("Visual")]
    public Image cartImage;
    public List<CartVisualData> cartVisualDatas = new List<CartVisualData>();

    private Vector3 startWorldPosition;
    private bool canDrag;

    private void Awake()
    {
        if (cartImage == null)
        {
            cartImage = GetComponent<Image>();
        }
    }

    private void Start()
    {
        if (currentNode != null)
        {
            transform.position = currentNode.transform.position;
        }

        RefreshCartVisual();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canDrag = true;

        if (currentNode != null)
        {
            startWorldPosition = currentNode.transform.position;
        }
        else
        {
            startWorldPosition = transform.position;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!canDrag) return;

        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!canDrag) return;

        canDrag = false;

        // 注意：现在是在松手时才判断当前区域能不能移动
        if (!CanStartMove())
        {
            ReturnToStart();
            return;
        }

        RoadNode targetNode = FindNearestNode();

        if (targetNode == null)
        {
            ReturnToStart();
            return;
        }

        if (targetNode == currentNode)
        {
            transform.position = currentNode.transform.position;
            return;
        }

        TryMoveToNode(targetNode);
    }

    private bool CanStartMove()
    {
        if (currentNode == null)
        {
            Debug.LogWarning("小推车没有 currentNode");
            return false;
        }

        TimeState currentRegionTime = TimeManager.Instance.GetRegionTime(currentNode.regionID);

        if (currentRegionTime != TimeState.Spring)
        {
            Debug.Log("小推车不能移动：当前所在区域 " + currentNode.regionID + " 不是 Spring，而是 " + currentRegionTime);
            return false;
        }

        return true;
    }

    private void TryMoveToNode(RoadNode targetNode)
    {
        foreach (RoadSegment segment in currentNode.connectedSegments)
        {
            RoadNode otherNode = segment.GetOtherNode(currentNode);

            if (otherNode == targetNode)
            {
                if (segment.IsPassable(out string reason))
                {
                    MoveToNode(targetNode);
                    Debug.Log("小推车移动成功：" + currentNode.nodeName);
                    return;
                }
                else
                {
                    Debug.Log(reason);
                    ReturnToStart();
                    return;
                }
            }
        }

        Debug.Log("目标节点不是相邻节点，不能直接移动");
        ReturnToStart();
    }

    private void MoveToNode(RoadNode targetNode)
    {
        Vector3 delta = targetNode.transform.position - currentNode.transform.position;

        currentDirection = GetDirectionFromDelta(delta);
        currentNode = targetNode;

        transform.position = currentNode.transform.position;

        RefreshCartVisual();
    }

    private void ReturnToStart()
    {
        transform.position = startWorldPosition;
    }

    private RoadNode FindNearestNode()
    {
        RoadNode[] nodes = FindObjectsByType<RoadNode>(FindObjectsSortMode.None);

        RoadNode nearestNode = null;
        float nearestDistance = snapDistance;

        foreach (RoadNode node in nodes)
        {
            float distance = Vector2.Distance(transform.position, node.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestNode = node;
            }
        }

        return nearestNode;
    }

    private CartDirection GetDirectionFromDelta(Vector3 delta)
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            if (delta.x >= 0)
            {
                return CartDirection.Right;
            }
            else
            {
                return CartDirection.Left;
            }
        }
        else
        {
            if (delta.y >= 0)
            {
                return CartDirection.Up;
            }
            else
            {
                return CartDirection.Down;
            }
        }
    }

    public bool TryLoadCargo(CartCargo cargo)
    {
        if (currentCargo != CartCargo.Empty)
        {
            Debug.Log("小推车已经有东西了，不能再装");
            return false;
        }

        currentCargo = cargo;
        RefreshCartVisual();

        Debug.Log("小推车装入：" + cargo);
        return true;
    }

    public bool IsEmpty()
    {
        return currentCargo == CartCargo.Empty;
    }

    public RegionID GetCurrentRegion()
    {
        if (currentNode == null)
        {
            return RegionID.Mill;
        }

        return currentNode.regionID;
    }

    private void RefreshCartVisual()
    {
        if (cartImage == null) return;

        CartVisualData data = GetVisualData(currentCargo);

        if (data == null) return;

        Sprite sprite = data.GetSprite(currentDirection);

        if (sprite != null)
        {
            cartImage.sprite = sprite;
        }
    }

    private CartVisualData GetVisualData(CartCargo cargo)
    {
        foreach (CartVisualData data in cartVisualDatas)
        {
            if (data.cargo == cargo)
            {
                return data;
            }
        }

        return null;
    }
}