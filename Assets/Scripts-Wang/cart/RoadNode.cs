using System.Collections.Generic;
using UnityEngine;

public class RoadNode : MonoBehaviour
{
    [Header("Node Setting")]
    public string nodeName;
    public RegionID regionID;

    [HideInInspector]
    public List<RoadSegment> connectedSegments = new List<RoadSegment>();
}