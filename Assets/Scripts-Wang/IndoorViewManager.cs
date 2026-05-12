using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IndoorViewManager : MonoBehaviour
{
    public static IndoorViewManager Instance;

    [Header("Indoor Overlay")]
    public GameObject indoorRoot;
    public CanvasGroup indoorCanvasGroup;
    public Image indoorSceneImage;
    public TextMeshProUGUI indoorLabel;

    [Header("Buttons")]
    public Button exitButton;
    
    [Header("Fade Setting")]
    public float fadeDuration = 0.35f;

    public Slider indoorTimeSlider;

    private RegionPanel currentRegion;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        Instance = this;

        if (indoorRoot != null)
        {
            indoorRoot.SetActive(false);
        }

        if (indoorCanvasGroup != null)
        {
            indoorCanvasGroup.alpha = 0f;
            indoorCanvasGroup.interactable = false;
            indoorCanvasGroup.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(CloseIndoorView);
        }

        

        if (indoorTimeSlider != null)
        {
            indoorTimeSlider.minValue = 0;
            indoorTimeSlider.maxValue = 2;
            indoorTimeSlider.wholeNumbers = true;
            indoorTimeSlider.onValueChanged.AddListener(OnIndoorTimeSliderChanged);
        }
    }

    public void OpenIndoorView(RegionPanel region)
    {
        if (region == null) return;

        currentRegion = region;

        if (indoorRoot != null)
        {
            indoorRoot.SetActive(true);
        }

        RefreshIndoorView();

        StartFade(0f, 1f, true);
    }

    public void CloseIndoorView()
    {
        StartFade(1f, 0f, false);
    }

    public void RefreshIndoorView()
    {
        if (currentRegion == null) return;

        RegionVisualData data = currentRegion.GetVisualData(currentRegion.currentTime);

        if (data == null)
        {
            Debug.LogWarning("û���ҵ����ڻ������ݣ�" + currentRegion.regionName);
            return;
        }

        if (indoorTimeSlider != null)
        {
            indoorTimeSlider.SetValueWithoutNotify(TimeToSliderValue(currentRegion.currentTime));
        }

        ApplyExitButtonLayout(data);

        if (indoorSceneImage != null)
        {
            if (data.indoorSprite != null)
            {
                indoorSceneImage.sprite = data.indoorSprite;
                indoorSceneImage.color = Color.white;
            }
            else
            {
                Color color = data.indoorFallbackColor;
                color.a = 1f;

                indoorSceneImage.sprite = null;
                indoorSceneImage.color = color;
            }
        }

        if (indoorLabel != null)
        {
            indoorLabel.text =
                currentRegion.regionName +
                "\nIndoor / ����" +
                "\nTime: " + currentRegion.currentTime;
        }
    }

    private void ApplyExitButtonLayout(RegionVisualData data)
    {
        if (exitButton == null || data == null) return;

        RectTransform exitButtonRect = exitButton.GetComponent<RectTransform>();

        if (exitButtonRect == null) return;

        exitButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        exitButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        exitButtonRect.pivot = new Vector2(0.5f, 0.5f);

        exitButtonRect.anchoredPosition = data.indoorExitButtonPosition;
        exitButtonRect.sizeDelta = data.indoorExitButtonSize;

        Debug.Log(
            "Exit Button λ�ø��£�" +
            currentRegion.regionName +
            " / " +
            currentRegion.currentTime +
            " / Pos: " +
            data.indoorExitButtonPosition +
            " / Size: " +
            data.indoorExitButtonSize
        );
    }

    

    private void StartFade(float from, float to, bool opening)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeRoutine(from, to, opening));
    }

    private IEnumerator FadeRoutine(float from, float to, bool opening)
    {
        if (indoorCanvasGroup == null)
        {
            yield break;
        }

        indoorCanvasGroup.alpha = from;
        indoorCanvasGroup.interactable = false;
        indoorCanvasGroup.blocksRaycasts = true;

        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;

            indoorCanvasGroup.alpha = Mathf.Lerp(from, to, t);

            yield return null;
        }

        indoorCanvasGroup.alpha = to;

        if (opening)
        {
            indoorCanvasGroup.interactable = true;
            indoorCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            indoorCanvasGroup.interactable = false;
            indoorCanvasGroup.blocksRaycasts = false;

            if (currentRegion != null)
            {
                currentRegion.SetOutdoorViewFromIndoorManager();
            }

            currentRegion = null;

            if (indoorRoot != null)
            {
                indoorRoot.SetActive(false);
            }
        }

        fadeCoroutine = null;
    }

    public bool IsOpenFor(RegionPanel region)
    {
        return currentRegion == region && indoorRoot != null && indoorRoot.activeSelf;
    }

    private void OnIndoorTimeSliderChanged(float value)
    {
        if (currentRegion == null) return;

        TimeState newTime = SliderValueToTime(value);
        currentRegion.SetTime(newTime);
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
}