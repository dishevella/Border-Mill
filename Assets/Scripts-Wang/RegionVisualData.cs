using UnityEngine;

[System.Serializable]
public class RegionVisualData
{
    public TimeState timeState;

    [Header("Outdoor Visual")]
    public Sprite outdoorSprite;
    public Color outdoorFallbackColor = Color.white;

    [Header("Indoor Visual")]
    public Sprite indoorSprite;
    public Color indoorFallbackColor = Color.gray;

    [Header("Indoor Exit Button Setting")]
    public Vector2 indoorExitButtonPosition = new Vector2(0, -400);
    public Vector2 indoorExitButtonSize = new Vector2(180, 100);
}