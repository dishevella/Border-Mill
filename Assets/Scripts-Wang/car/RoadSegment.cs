using System.Collections.Generic;
using UnityEngine;

public class RoadSegment : MonoBehaviour
{
    [Header("Road Nodes")]
    public RoadNode nodeA;
    public RoadNode nodeB;

    [Header("这条路需要检测哪些区域")]
    public List<RegionID> regionsToCheck = new List<RegionID>();

    public bool IsPassable(out string reason)
    {
        reason = "";

        if (nodeA == null || nodeB == null)
        {
            reason = name + " 没有设置 nodeA 或 nodeB";
            return false;
        }

        List<RegionID> checkList = GetRegionsToCheck();

        foreach (RegionID region in checkList)
        {
            TimeState time = TimeManager.Instance.GetRegionTime(region);

            if (time != TimeState.Spring)
            {
                reason = "道路不通：" + region + " 当前不是 Spring，而是 " + time;
                return false;
            }
        }

        reason = "道路可通行";
        return true;
    }

    private List<RegionID> GetRegionsToCheck()
    {
        if (regionsToCheck != null && regionsToCheck.Count > 0)
        {
            return regionsToCheck;
        }

        List<RegionID> result = new List<RegionID>();

        if (nodeA != null && !result.Contains(nodeA.regionID))
        {
            result.Add(nodeA.regionID);
        }

        if (nodeB != null && !result.Contains(nodeB.regionID))
        {
            result.Add(nodeB.regionID);
        }

        return result;
    }

    public RoadNode GetOtherNode(RoadNode currentNode)
    {
        if (currentNode == nodeA) return nodeB;
        if (currentNode == nodeB) return nodeA;

        return null;
    }
}