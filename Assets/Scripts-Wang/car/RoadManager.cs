using UnityEngine;

public class RoadManager : MonoBehaviour
{
    private void Awake()
    {
        RoadNode[] nodes = FindObjectsByType<RoadNode>(FindObjectsSortMode.None);

        foreach (RoadNode node in nodes)
        {
            node.connectedSegments.Clear();
        }

        RoadSegment[] segments = FindObjectsByType<RoadSegment>(FindObjectsSortMode.None);

        foreach (RoadSegment segment in segments)
        {
            if (segment.nodeA != null && !segment.nodeA.connectedSegments.Contains(segment))
            {
                segment.nodeA.connectedSegments.Add(segment);
            }

            if (segment.nodeB != null && !segment.nodeB.connectedSegments.Contains(segment))
            {
                segment.nodeB.connectedSegments.Add(segment);
            }
        }
    }
}