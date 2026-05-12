using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PreludeManager : MonoBehaviour
{
    [Header("Quarters Order: 0 LT, 1 RT, 2 LB, 3 RB")]
    public RectTransform[] quarters;
    public Image[] quarterImages;
    public Image[] springImages;
    public Button[] quarterButtons;

    [Header("Spring Images")]
    public Sprite[] springSprites;
    public Color springPlaceholderColor = new Color(0.35f, 0.85f, 0.35f, 1f);

    [Header("War Overlay Images")]
    public Image[] warOverlayImages;
    public Sprite[] warSprites;
    public Color warPlaceholderColor = new Color(0.45f, 0.08f, 0.05f, 1f);

    [Header("Autumn Overlay Images")]
    public Image[] autumnOverlayImages;
    public Sprite[] autumnSprites;
    public Color autumnPlaceholderColor = new Color(0.95f, 0.62f, 0.18f, 1f);

    [Header("Pulse Hint")]
    public RectTransform pulseHint;
    public Image pulseHintImage;

    [Header("Center Click Area")]
    public RectTransform centerClickArea;
    public Button centerClickButton;

    [Header("Rockets")]
    public RectTransform[] rockets;
    public RectTransform[] rocketStartPoints;
    public RectTransform rocketEndPoint;

    [Header("Timing")]
    public float springFadeTime = 0.8f;
    public float afterSpringPauseTime = 1.2f;

    public float rocketFlyTime = 2.3f;
    public float rocketDelay = 0.18f;

    public float warRevealStartRatio = 0.25f;
    public float warEachRevealTime = 1.25f;
    public float warBetweenDelay = 0.45f;
    public float afterWarPauseTime = 1.2f;

    public float autumnFlashFadeTime = 0.18f;
    public float autumnFlashHoldTime = 0.18f;
    public float autumnFinalFadeTime = 0.7f;
    public float autumnBetweenDelay = 0.25f;

    [Header("Timeline")]
    public GameObject timelinePanel;
    public CanvasGroup timelineCanvasGroup;
    public RectTransform timelineKnob;
    public RectTransform timelineSpringPoint;
    public RectTransform timelineWarPoint;
    public RectTransform timelineAutumnPoint;

    public float timelineFadeInTime = 0.8f;
    public float timelineMoveTime = 1.0f;
    public float timelineMiddlePauseTime = 0.5f;
    public float timelineEndPauseTime = 0.4f;

    private int currentIndex = 0;
    private int autumnClickCount = 0;

    private Coroutine pulseCoroutine;
    private bool inputLocked = false;

    private enum PreludePhase
    {
        SpringClick,
        Busy,
        AutumnClick,
        Finished
    }

    private PreludePhase phase;

    void Start()
    {
        InitQuarters();
        InitSpringImages();
        InitWarOverlays();
        InitAutumnOverlays();
        InitRockets();
        InitTimeline();
        InitCenterClickArea();

        phase = PreludePhase.SpringClick;
        ShowPulseAt(quarters[currentIndex]);
    }

    void InitQuarters()
    {
        for (int i = 0; i < quarters.Length; i++)
        {
            int index = i;

            quarterImages[i].sprite = null;
            quarterImages[i].color = Color.white;

            quarterButtons[i].interactable = true;
            quarterButtons[i].onClick.RemoveAllListeners();
            quarterButtons[i].onClick.AddListener(() => OnQuarterClicked(index));
        }
    }

    void InitSpringImages()
    {
        for (int i = 0; i < springImages.Length; i++)
        {
            if (springImages[i] == null) continue;

            springImages[i].raycastTarget = false;

            if (springSprites != null && i < springSprites.Length && springSprites[i] != null)
            {
                springImages[i].sprite = springSprites[i];

                Color c = Color.white;
                c.a = 0f;
                springImages[i].color = c;
            }
            else
            {
                springImages[i].sprite = null;

                Color c = springPlaceholderColor;
                c.a = 0f;
                springImages[i].color = c;
            }
        }
    }

    void InitWarOverlays()
    {
        for (int i = 0; i < warOverlayImages.Length; i++)
        {
            if (warOverlayImages[i] == null) continue;

            warOverlayImages[i].raycastTarget = false;

            if (warSprites != null && i < warSprites.Length && warSprites[i] != null)
            {
                warOverlayImages[i].sprite = warSprites[i];

                Color c = Color.white;
                c.a = 0f;
                warOverlayImages[i].color = c;
            }
            else
            {
                warOverlayImages[i].sprite = null;

                Color c = warPlaceholderColor;
                c.a = 0f;
                warOverlayImages[i].color = c;
            }
        }
    }

    void InitAutumnOverlays()
    {
        for (int i = 0; i < autumnOverlayImages.Length; i++)
        {
            if (autumnOverlayImages[i] == null) continue;

            autumnOverlayImages[i].raycastTarget = false;
            autumnOverlayImages[i].transform.SetAsLastSibling();

            if (autumnSprites != null && i < autumnSprites.Length && autumnSprites[i] != null)
            {
                autumnOverlayImages[i].sprite = autumnSprites[i];

                Color c = Color.white;
                c.a = 0f;
                autumnOverlayImages[i].color = c;
            }
            else
            {
                autumnOverlayImages[i].sprite = null;

                Color c = autumnPlaceholderColor;
                c.a = 0f;
                autumnOverlayImages[i].color = c;
            }
        }
    }

    void InitRockets()
    {
        for (int i = 0; i < rockets.Length; i++)
        {
            if (rockets[i] != null)
            {
                rockets[i].gameObject.SetActive(false);
            }
        }
    }

    void InitTimeline()
    {
        if (timelinePanel != null)
        {
            timelinePanel.SetActive(false);
        }

        if (timelineCanvasGroup != null)
        {
            timelineCanvasGroup.alpha = 0f;
            timelineCanvasGroup.interactable = false;
            timelineCanvasGroup.blocksRaycasts = false;
        }
    }

    void InitCenterClickArea()
    {
        if (centerClickButton != null)
        {
            centerClickButton.onClick.RemoveAllListeners();
            centerClickButton.onClick.AddListener(OnCenterClicked);

            centerClickButton.interactable = false;
            centerClickButton.gameObject.SetActive(false);
        }
    }

    void OnQuarterClicked(int index)
    {
        if (inputLocked) return;

        if (phase == PreludePhase.SpringClick)
        {
            if (index != currentIndex) return;

            StartCoroutine(HandleSpringClick(index));
        }
       
    }

    void OnCenterClicked()
    {
        if (inputLocked) return;

        if (phase != PreludePhase.AutumnClick) return;

        StartCoroutine(HandleAutumnCenterClick());
    }

    IEnumerator HandleSpringClick(int index)
    {
        inputLocked = true;
        HidePulse();

        yield return StartCoroutine(FadeInSpringImage(index));

        currentIndex++;

        if (currentIndex < quarters.Length)
        {
            ShowPulseAt(quarters[currentIndex]);
            inputLocked = false;
        }
        else
        {
            yield return StartCoroutine(SpringFinishedSequence());
        }
    }

    IEnumerator FadeInSpringImage(int index)
    {
        if (index >= springImages.Length || springImages[index] == null)
        {
            yield break;
        }

        Image img = springImages[index];

        if (springSprites != null && index < springSprites.Length && springSprites[index] != null)
        {
            img.sprite = springSprites[index];

            Color c = Color.white;
            c.a = 0f;
            img.color = c;
        }
        else
        {
            img.sprite = null;

            Color c = springPlaceholderColor;
            c.a = 0f;
            img.color = c;
        }

        float timer = 0f;

        while (timer < springFadeTime)
        {
            timer += Time.deltaTime;

            float t = timer / springFadeTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            Color c = img.color;
            c.a = t;
            img.color = c;

            yield return null;
        }

        Color finalColor = img.color;
        finalColor.a = 1f;
        img.color = finalColor;
    }

    IEnumerator SpringFinishedSequence()
    {
        phase = PreludePhase.Busy;
        inputLocked = true;
        HidePulse();

        SetAllButtons(false);

        yield return new WaitForSeconds(afterSpringPauseTime);

        yield return StartCoroutine(PlayRocketWarSequence());

        yield return new WaitForSeconds(afterWarPauseTime);

        StartAutumnClickPhase();
    }

    IEnumerator PlayRocketWarSequence()
    {
        Coroutine rocketRoutine = StartCoroutine(LaunchRocketsSequence());
        Coroutine warRoutine = StartCoroutine(RevealWarByRocketPath());

        yield return rocketRoutine;
        yield return warRoutine;
    }

    IEnumerator LaunchRocketsSequence()
    {
        for (int i = 0; i < rockets.Length; i++)
        {
            if (i < rocketStartPoints.Length)
            {
                StartCoroutine(FlyOneRocket(rockets[i], rocketStartPoints[i], rocketEndPoint));
            }

            if (i < rockets.Length - 1)
            {
                yield return new WaitForSeconds(rocketDelay);
            }
        }

        yield return new WaitForSeconds(rocketFlyTime + 0.25f);
    }

    IEnumerator FlyOneRocket(RectTransform rocket, RectTransform startPoint, RectTransform endPoint)
    {
        if (rocket == null || startPoint == null || endPoint == null)
        {
            yield break;
        }

        rocket.gameObject.SetActive(true);

        Vector2 start = startPoint.anchoredPosition;
        Vector2 end = endPoint.anchoredPosition;

        rocket.anchoredPosition = start;

        Vector2 direction = end - start;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        rocket.localRotation = Quaternion.Euler(0f, 0f, angle);

        float timer = 0f;

        while (timer < rocketFlyTime)
        {
            timer += Time.deltaTime;

            float t = timer / rocketFlyTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            rocket.anchoredPosition = Vector2.Lerp(start, end, t);

            yield return null;
        }

        rocket.anchoredPosition = end;

        yield return new WaitForSeconds(0.15f);

        rocket.gameObject.SetActive(false);
    }

    IEnumerator RevealWarByRocketPath()
    {
        yield return new WaitForSeconds(rocketFlyTime * warRevealStartRatio);

        int[] revealOrder =
        {
            1, // 右上
            3, // 右下
            0, // 左上
            2  // 左下
        };

        for (int i = 0; i < revealOrder.Length; i++)
        {
            int index = revealOrder[i];

            StartCoroutine(FadeInWarOverlay(index));

            yield return new WaitForSeconds(warBetweenDelay);
        }

        yield return new WaitForSeconds(warEachRevealTime + 0.2f);
    }

    IEnumerator FadeInWarOverlay(int index)
    {
        if (index >= warOverlayImages.Length || warOverlayImages[index] == null)
        {
            yield break;
        }

        Image img = warOverlayImages[index];

        if (warSprites != null && index < warSprites.Length && warSprites[index] != null)
        {
            img.sprite = warSprites[index];

            Color c = Color.white;
            c.a = 0f;
            img.color = c;
        }
        else
        {
            img.sprite = null;

            Color c = warPlaceholderColor;
            c.a = 0f;
            img.color = c;
        }

        float timer = 0f;

        while (timer < warEachRevealTime)
        {
            timer += Time.deltaTime;

            float t = timer / warEachRevealTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            Color c = img.color;
            c.a = t;
            img.color = c;

            yield return null;
        }

        Color finalColor = img.color;
        finalColor.a = 1f;
        img.color = finalColor;
    }

    void StartAutumnClickPhase()
    {
        phase = PreludePhase.AutumnClick;
        inputLocked = false;
        autumnClickCount = 0;

        SetAllButtons(false);

        if (centerClickButton != null)
        {
            centerClickButton.gameObject.SetActive(true);
            centerClickButton.interactable = true;
        }

        ShowPulseAt(centerClickArea);
    }

    IEnumerator HandleAutumnCenterClick()
    {
        inputLocked = true;
        HidePulse();

        autumnClickCount++;

        if (autumnClickCount < 3)
        {
            // 第 1 次、第 2 次：四块一起闪成秋天，然后回到战争
            yield return StartCoroutine(FlashAllAutumnOverlays());

            ShowPulseAt(centerClickArea);
            inputLocked = false;
        }
        else
        {
            // 第 3 次：四块一起固定成秋天
            if (centerClickButton != null)
            {
                centerClickButton.interactable = false;
                centerClickButton.gameObject.SetActive(false);
            }

            yield return StartCoroutine(FadeInAllAutumnOverlays(autumnFinalFadeTime));

            yield return new WaitForSeconds(0.45f);

            yield return StartCoroutine(TimeRewindSequence());
        }
    }

    IEnumerator FlashAutumnOverlay(int index)
    {
        if (index >= autumnOverlayImages.Length || autumnOverlayImages[index] == null)
        {
            yield break;
        }

        Image img = autumnOverlayImages[index];
        PrepareAutumnImage(index);

        float timer = 0f;

        // 秋天出现
        while (timer < autumnFlashFadeTime)
        {
            timer += Time.deltaTime;

            float t = timer / autumnFlashFadeTime;

            Color c = img.color;
            c.a = Mathf.SmoothStep(0f, 1f, t);
            img.color = c;

            yield return null;
        }

        yield return new WaitForSeconds(autumnFlashHoldTime);

        timer = 0f;

        // 秋天退回去，露出下面的战争状态
        while (timer < autumnFlashFadeTime)
        {
            timer += Time.deltaTime;

            float t = timer / autumnFlashFadeTime;

            Color c = img.color;
            c.a = Mathf.SmoothStep(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Color finalColor = img.color;
        finalColor.a = 0f;
        img.color = finalColor;
    }

    IEnumerator FadeInAutumnOverlay(int index, float duration)
    {
        if (index >= autumnOverlayImages.Length || autumnOverlayImages[index] == null)
        {
            yield break;
        }

        Image img = autumnOverlayImages[index];
        PrepareAutumnImage(index);

        float startAlpha = img.color.a;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            Color c = img.color;
            c.a = Mathf.Lerp(startAlpha, 1f, t);
            img.color = c;

            yield return null;
        }

        Color finalColor = img.color;
        finalColor.a = 1f;
        img.color = finalColor;
    }

    IEnumerator FlashAllAutumnOverlays()
    {
        PrepareAllAutumnImages();

        yield return StartCoroutine(FadeAllAutumnAlpha(0f, 1f, autumnFlashFadeTime));

        yield return new WaitForSeconds(autumnFlashHoldTime);

        yield return StartCoroutine(FadeAllAutumnAlpha(1f, 0f, autumnFlashFadeTime));
    }

    IEnumerator FadeInAllAutumnOverlays(float duration)
    {
        PrepareAllAutumnImages();

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < autumnOverlayImages.Length; i++)
            {
                if (autumnOverlayImages[i] == null) continue;

                Color c = autumnOverlayImages[i].color;
                c.a = Mathf.Lerp(c.a, 1f, t);
                autumnOverlayImages[i].color = c;
            }

            yield return null;
        }

        SetAllAutumnAlpha(1f);
    }

    IEnumerator FadeAllAutumnAlpha(float from, float to, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            float alpha = Mathf.Lerp(from, to, t);

            SetAllAutumnAlpha(alpha);

            yield return null;
        }

        SetAllAutumnAlpha(to);
    }

    void PrepareAllAutumnImages()
    {
        for (int i = 0; i < autumnOverlayImages.Length; i++)
        {
            if (autumnOverlayImages[i] == null) continue;

            PrepareAutumnImage(i);
        }
    }

    void SetAllAutumnAlpha(float alpha)
    {
        for (int i = 0; i < autumnOverlayImages.Length; i++)
        {
            if (autumnOverlayImages[i] == null) continue;

            Color c = autumnOverlayImages[i].color;
            c.a = alpha;
            autumnOverlayImages[i].color = c;
        }
    }

    IEnumerator TimeRewindSequence()
    {
        phase = PreludePhase.Busy;
        inputLocked = true;
        HidePulse();
        SetAllButtons(false);

        if (timelinePanel != null)
        {
            timelinePanel.SetActive(true);
        }

        if (timelineKnob != null && timelineAutumnPoint != null)
        {
            timelineKnob.anchoredPosition = timelineAutumnPoint.anchoredPosition;
        }

        yield return StartCoroutine(FadeTimeline(0f, 1f, timelineFadeInTime));

        yield return new WaitForSeconds(0.35f);

        // 秋天 -> 战时
        if (timelineKnob != null && timelineWarPoint != null)
        {
            yield return StartCoroutine(MoveTimelineKnob(
                timelineKnob.anchoredPosition,
                timelineWarPoint.anchoredPosition
            ));
        }

        ShowWarWorldImmediate();

        yield return new WaitForSeconds(timelineMiddlePauseTime);

        // 战时 -> 春天
        if (timelineKnob != null && timelineSpringPoint != null)
        {
            yield return StartCoroutine(MoveTimelineKnob(
                timelineKnob.anchoredPosition,
                timelineSpringPoint.anchoredPosition
            ));
        }

        ShowSpringWorldImmediate();

        yield return new WaitForSeconds(timelineEndPauseTime);

        phase = PreludePhase.Finished;
        inputLocked = true;

        Debug.Log("Timeline rewind finished. Next step: load scene.");
    }

    IEnumerator FadeTimeline(float from, float to, float duration)
    {
        if (timelineCanvasGroup == null)
        {
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            timelineCanvasGroup.alpha = Mathf.Lerp(from, to, t);

            yield return null;
        }

        timelineCanvasGroup.alpha = to;
    }

    IEnumerator MoveTimelineKnob(Vector2 start, Vector2 end)
    {
        if (timelineKnob == null)
        {
            yield break;
        }

        float timer = 0f;

        while (timer < timelineMoveTime)
        {
            timer += Time.deltaTime;

            float t = timer / timelineMoveTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            timelineKnob.anchoredPosition = Vector2.Lerp(start, end, t);

            yield return null;
        }

        timelineKnob.anchoredPosition = end;
    }

    void ShowWarWorldImmediate()
    {
        for (int i = 0; i < springImages.Length; i++)
        {
            SetImageAlpha(springImages[i], 1f);
        }

        for (int i = 0; i < warOverlayImages.Length; i++)
        {
            SetImageAlpha(warOverlayImages[i], 1f);
        }

        for (int i = 0; i < autumnOverlayImages.Length; i++)
        {
            SetImageAlpha(autumnOverlayImages[i], 0f);
        }
    }

    void ShowSpringWorldImmediate()
    {
        for (int i = 0; i < springImages.Length; i++)
        {
            SetImageAlpha(springImages[i], 1f);
        }

        for (int i = 0; i < warOverlayImages.Length; i++)
        {
            SetImageAlpha(warOverlayImages[i], 0f);
        }

        for (int i = 0; i < autumnOverlayImages.Length; i++)
        {
            SetImageAlpha(autumnOverlayImages[i], 0f);
        }
    }

    void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;

        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    void PrepareAutumnImage(int index)
    {
        Image img = autumnOverlayImages[index];

        if (autumnSprites != null && index < autumnSprites.Length && autumnSprites[index] != null)
        {
            img.sprite = autumnSprites[index];

            Color c = Color.white;
            c.a = img.color.a;
            img.color = c;
        }
        else
        {
            img.sprite = null;

            Color c = autumnPlaceholderColor;
            c.a = img.color.a;
            img.color = c;
        }
    }

    void SetAllButtons(bool value)
    {
        for (int i = 0; i < quarterButtons.Length; i++)
        {
            if (quarterButtons[i] != null)
            {
                quarterButtons[i].interactable = value;
            }
        }
    }

    void ShowPulseAt(RectTransform target)
    {
        pulseHint.gameObject.SetActive(true);

        pulseHint.SetParent(target, false);
        pulseHint.anchorMin = new Vector2(0.5f, 0.5f);
        pulseHint.anchorMax = new Vector2(0.5f, 0.5f);
        pulseHint.pivot = new Vector2(0.5f, 0.5f);
        pulseHint.anchoredPosition = Vector2.zero;
        pulseHint.SetAsLastSibling();

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }

        pulseCoroutine = StartCoroutine(PulseAnimation());
    }

    void HidePulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }

        pulseHint.gameObject.SetActive(false);
    }

    IEnumerator PulseAnimation()
    {
        float timer = 0f;

        while (true)
        {
            timer += Time.deltaTime * 3f;

            float scale = 1f + Mathf.Sin(timer) * 0.18f;
            pulseHint.localScale = new Vector3(scale, scale, 1f);

            if (pulseHintImage != null)
            {
                Color c = pulseHintImage.color;
                c.a = 0.55f + Mathf.Sin(timer) * 0.25f;
                pulseHintImage.color = c;
            }

            yield return null;
        }
    }
}