using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    private Dictionary<RegionID, TimeState> regionTimes = new Dictionary<RegionID, TimeState>();

    private void Awake()
    {
        Instance = this;

        // 游戏开始时，四个区域默认都是战时
        regionTimes[RegionID.Mill] = TimeState.War;
        regionTimes[RegionID.Field] = TimeState.War;
        regionTimes[RegionID.Kitchen] = TimeState.War;
        regionTimes[RegionID.Cellar] = TimeState.War;
    }

    public TimeState GetRegionTime(RegionID regionID)
    {
        return regionTimes[regionID];
    }

    public void SetRegionTime(RegionID regionID, TimeState timeState)
    {
        regionTimes[regionID] = timeState;
    }

    public TimeState GetNextTimeState(TimeState currentState)
    {
        switch (currentState)
        {
            case TimeState.Spring:
                return TimeState.War;

            case TimeState.War:
                return TimeState.Autumn;

            case TimeState.Autumn:
                return TimeState.Spring;

            default:
                return TimeState.War;
        }
    }
}