using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RegionPanel : MonoBehaviour
{
    [Header("Region Setting")]
    public RegionID regionID;
    public string regionName;

    [Header("Current State")]
    public TimeState currentTime = TimeState.War;
    public ViewMode currentView = ViewMode.Outdoor;

    [Header("UI")]
    public Image sceneImage;
    public TextMeshProUGUI timeLabel;
    public Slider timeSlider;
    public Button enterIndoorButton;

    [Header("Visual Data")]
    public RegionVisualData[] visualDatas;

    [Header("Can Enter Indoor?")]
    public bool hasIndoor = true;

    private void Start()
    {
        currentTime = TimeManager.Instance.GetRegionTime(regionID);
        currentView = ViewMode.Outdoor;

        if (timeSlider != null)
        {
            timeSlider.minValue = 0;
            timeSlider.maxValue = 2;
            timeSlider.wholeNumbers = true;
            timeSlider.SetValueWithoutNotify(TimeToSliderValue(currentTime));
            timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        }

        if (enterIndoorButton != null)
        {
            enterIndoorButton.onClick.AddListener(EnterIndoor);
        }

        RefreshOutdoorVisual();
        RefreshButtons();
    }

    private void OnTimeSliderChanged(float value)
    {
        TimeState newTime = SliderValueToTime(value);
        SetTime(newTime);
    }

    public void SetTime(TimeState newTime)
    {
        currentTime = newTime;
        TimeManager.Instance.SetRegionTime(regionID, currentTime);

        if (timeSlider != null)
        {
            timeSlider.SetValueWithoutNotify(TimeToSliderValue(currentTime));
        }

        RefreshOutdoorVisual();

        if (currentView == ViewMode.Indoor &&
            IndoorViewManager.Instance != null &&
            IndoorViewManager.Instance.IsOpenFor(this))
        {
            IndoorViewManager.Instance.RefreshIndoorView();
        }

        Debug.Log(regionName + " 时间切换为：" + currentTime);
    }

    public void EnterIndoor()
    {
        if (!hasIndoor)
        {
            Debug.Log(regionName + " 没有室内场景");
            return;
        }

        currentView = ViewMode.Indoor;
        RefreshButtons();

        if (IndoorViewManager.Instance != null)
        {
            IndoorViewManager.Instance.OpenIndoorView(this);
        }
        else
        {
            Debug.LogWarning("场景里没有 IndoorViewManager");
        }
    }

    public void SetOutdoorViewFromIndoorManager()
    {
        currentView = ViewMode.Outdoor;

        RefreshOutdoorVisual();
        RefreshButtons();
    }

    private void RefreshOutdoorVisual()
    {
        RegionVisualData data = GetVisualData(currentTime);

        if (data == null)
        {
            Debug.LogWarning(regionName + " 没有找到对应时间的 Visual Data: " + currentTime);
            return;
        }

        if (sceneImage != null)
        {
            if (data.outdoorSprite != null)
            {
                sceneImage.sprite = data.outdoorSprite;
                sceneImage.color = Color.white;
            }
            else
            {
                Color color = data.outdoorFallbackColor;
                color.a = 1f;

                sceneImage.sprite = null;
                sceneImage.color = color;
            }
        }

        if (timeLabel != null)
        {
            timeLabel.text =
                regionName +
                "\nOutdoor / 室外" +
                "\nTime: " + GetTimeName(currentTime);
        }
    }

    private void RefreshButtons()
    {
        if (enterIndoorButton != null)
        {
            enterIndoorButton.gameObject.SetActive(hasIndoor && currentView == ViewMode.Outdoor);
        }
    }

    public RegionVisualData GetVisualData(TimeState timeState)
    {
        foreach (RegionVisualData data in visualDatas)
        {
            if (data.timeState == timeState)
            {
                return data;
            }
        }

        return null;
    }

    private float TimeToSliderValue(TimeState timeState)
    {
        switch (timeState)
        {
            case TimeState.Spring:
                return 0;

            case TimeState.War:
                return 1;

            case TimeState.Autumn:
                return 2;

            default:
                return 1;
        }
    }

    private TimeState SliderValueToTime(float value)
    {
        int index = Mathf.RoundToInt(value);

        switch (index)
        {
            case 0:
                return TimeState.Spring;

            case 1:
                return TimeState.War;

            case 2:
                return TimeState.Autumn;

            default:
                return TimeState.War;
        }
    }

    private string GetTimeName(TimeState timeState)
    {
        switch (timeState)
        {
            case TimeState.Spring:
                return "Spring / 春天";

            case TimeState.War:
                return "War / 战时";

            case TimeState.Autumn:
                return "Autumn / 秋天";

            default:
                return "Unknown";
        }
    }
}