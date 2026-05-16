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
    public RectTransform hintLayer;
    public Canvas rootCanvas;

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
    public float afterAutumnRainTime = 2.5f;
    
    [Header("Audio Sources")]
    public AudioSource ambienceSource; // 用来播放春天/战争/秋天环境音
    public AudioSource musicSource;    // 用来播放悲伤/悲壮音乐

    [Header("Special SFX Sources")]
    public AudioSource arrowSource;
    public AudioSource glassBreakSource;

    [Header("Audio Clips")]
    public AudioClip springClip;
    public AudioClip warClip;
    public AudioClip autumnClip;
    public AudioClip sadClip;

    [Header("Special SFX Clips")]
    public AudioClip arrowClip;
    public AudioClip glassBreakClip;


    [Header("Audio Volume")]
    [Range(0f, 1f)] public float springVolume = 0.7f;
    [Range(0f, 1f)] public float warVolume = 0.8f;
    [Range(0f, 1f)] public float autumnVolume = 0.65f;
    [Range(0f, 1f)] public float sadVolume = 0.75f;
    [Range(0f, 1f)] public float sadTimelineVolume = 0.25f;

    [Header("Special SFX Settings")]
    [Range(0f, 1f)] public float arrowVolume = 0.8f;
    [Range(0.1f, 3f)] public float arrowPitch = 1f;

    [Range(0f, 1f)] public float glassBreakVolume = 1f;
    [Range(0.1f, 3f)] public float glassBreakPitch = 1f;

    public float audioFadeTime = 0.8f;

    private Coroutine ambienceAudioCoroutine;
    private Coroutine musicAudioCoroutine;

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
    public float timelineSceneFadeTime = 0.8f;

    [Header("Story Girl Images")]
    public Image warGirlImage;      // 战时中间的小女孩
    public Image autumnGirlImage;   // 秋天状态的小女孩

    public float girlFadeTime = 0.8f;
    [Header("Frame Animation - Windmill")]
    public Image windmillImage;
    public Sprite[] windmillFrames;
    public float windmillFps = 8f;

    [Header("Frame Animation - Rockets")]
    public Image[] rocketImages;
    public Sprite[] rocketFrames;
    public float rocketFps = 12f;

    [Header("Rocket Scale")]
    public Vector3 rocketStartScale = new Vector3(0.45f, 0.45f, 1f);
    public Vector3 rocketEndScale = new Vector3(1.15f, 1.15f, 1f);

    [Header("Rain Animator")]
    public Image rainImage;
    public Animator rainAnimator;

    [Header("Rocket Rotation")]
    public float rocketRotationOffset = 135f;

    private Coroutine windmillAnimCoroutine;
    private Coroutine[] rocketAnimCoroutines;

    private int currentIndex = 0;
    private int autumnClickCount = 0;

    private Coroutine pulseCoroutine;
    private bool inputLocked = false;
    private bool arrowPlayed = false;
    private bool glassBreakPlayed = false;

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
        InitFrameAnimations();
        InitStoryGirlImages();

        phase = PreludePhase.SpringClick;
        ShowPulseAt(quarters[currentIndex]);
    }

    void InitStoryGirlImages()
    {
        if (warGirlImage != null)
        {
            warGirlImage.gameObject.SetActive(true);
            warGirlImage.raycastTarget = false;
            SetImageAlpha(warGirlImage, 0f);
            warGirlImage.transform.SetAsLastSibling();
        }

        if (autumnGirlImage != null)
        {
            autumnGirlImage.gameObject.SetActive(true);
            autumnGirlImage.raycastTarget = false;
            SetImageAlpha(autumnGirlImage, 0f);
            autumnGirlImage.transform.SetAsLastSibling();
        }
    }

    IEnumerator FadeImageAlpha(Image img, float from, float to, float duration)
    {
        if (img == null)
        {
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            Color c = img.color;
            c.a = Mathf.Lerp(from, to, t);
            img.color = c;

            yield return null;
        }

        Color finalColor = img.color;
        finalColor.a = to;
        img.color = finalColor;
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

        // 第一次点击第一张春天图时，开始播放春天环境音
        if (index == 0)
        {
            ChangeAmbience(springClip, springVolume);
            StartWindmillAnimation();
        }


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

        StopWindmillAnimation();

        ChangeAmbience(warClip, warVolume);

        yield return StartCoroutine(PlayRocketWarSequence());

        yield return new WaitForSeconds(afterWarPauseTime);

        StartAutumnClickPhase();
    }

    IEnumerator PlayRocketWarSequence()
    {
        arrowPlayed = false;
        glassBreakPlayed = false;

        Coroutine rocketRoutine = StartCoroutine(LaunchRocketsSequence());
        Coroutine warRoutine = StartCoroutine(RevealWarByRocketPath());

        yield return rocketRoutine;
        yield return warRoutine;
    }

    IEnumerator LaunchRocketsSequence()
    {
        PlayArrowSfxOnce();
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
        rocket.localScale = rocketStartScale;

        Vector2 direction = end - start;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 因为你的火箭图片原本不是朝右，而是朝左下，所以需要加一个角度修正
        rocket.localRotation = Quaternion.Euler(0f, 0f, angle + rocketRotationOffset);

        int rocketIndex = System.Array.IndexOf(rockets, rocket);

        if (rocketIndex >= 0 && rocketIndex < rocketImages.Length && rocketImages[rocketIndex] != null)
        {
            if (rocketAnimCoroutines[rocketIndex] != null)
            {
                StopCoroutine(rocketAnimCoroutines[rocketIndex]);
            }

            rocketAnimCoroutines[rocketIndex] = StartCoroutine(
                PlayFrameAnimation(rocketImages[rocketIndex], rocketFrames, rocketFps, true)
            );
        }

        float timer = 0f;

        while (timer < rocketFlyTime)
        {
            timer += Time.deltaTime;

            float t = timer / rocketFlyTime;
            t = Mathf.SmoothStep(0f, 1f, t);

            rocket.anchoredPosition = Vector2.Lerp(start, end, t);

            // 近大远小：飞行过程中慢慢变大
            rocket.localScale = Vector3.Lerp(rocketStartScale, rocketEndScale, t);

            yield return null;
        }

        rocket.anchoredPosition = end;
        rocket.localScale = rocketEndScale;

        PlayGlassBreakSfxOnce();

        yield return new WaitForSeconds(0.15f);

        if (rocketIndex >= 0 && rocketIndex < rocketAnimCoroutines.Length)
        {
            if (rocketAnimCoroutines[rocketIndex] != null)
            {
                StopCoroutine(rocketAnimCoroutines[rocketIndex]);
                rocketAnimCoroutines[rocketIndex] = null;
            }
        }

        rocket.gameObject.SetActive(false);
    }
    IEnumerator RevealWarByRocketPath()
    {
        yield return new WaitForSeconds(rocketFlyTime * warRevealStartRatio);

        SetImageAlpha(warGirlImage, 0f);
        SetImageAlpha(autumnGirlImage, 0f);
       



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

        StartCoroutine(FadeImageAlpha(warGirlImage, 0f, 1f, girlFadeTime));

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


            ChangeAmbience(autumnClip, autumnVolume);
            ChangeMusic(sadClip, sadVolume);

            StartRainAnimator();

            yield return new WaitForSeconds(afterAutumnRainTime);

            StopRainAnimator();

            yield return StartCoroutine(TimeRewindSequence());
        }
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

            SetAllAutumnAlpha(t);

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

        // 秋天出现时，秋天小女孩出现
        SetImageAlpha(autumnGirlImage, alpha);

        // 秋天退回战争时，战时小女孩回来
        SetImageAlpha(warGirlImage, 1f - alpha);
    }

    IEnumerator TimeRewindSequence()
    {
        phase = PreludePhase.Busy;
        inputLocked = true;
        HidePulse();
        SetAllButtons(false);


        // 开始拉时间轴：秋天环境音停止
        StopAmbienceWithFade();

        // 悲伤音乐不要直接停，而是慢慢变弱
        FadeMusicVolume(sadTimelineVolume, timelineMoveTime * 2f);

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

        yield return StartCoroutine(FadeAutumnToWarWorld(timelineSceneFadeTime));

        yield return new WaitForSeconds(timelineMiddlePauseTime);

        // 战时 -> 春天
        if (timelineKnob != null && timelineSpringPoint != null)
        {
            yield return StartCoroutine(MoveTimelineKnob(
                timelineKnob.anchoredPosition,
                timelineSpringPoint.anchoredPosition
            ));
        }

        yield return StartCoroutine(FadeWarToSpringWorld(timelineSceneFadeTime));
        StartWindmillAnimation();

        StopMusicWithFade();
        ChangeAmbience(springClip, springVolume);


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

        SetImageAlpha(warGirlImage, 1f);
        SetImageAlpha(autumnGirlImage, 0f);
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

        SetImageAlpha(warGirlImage, 0f);
        SetImageAlpha(autumnGirlImage, 0f);
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
        if (pulseHint == null || target == null) return;

        pulseHint.gameObject.SetActive(true);

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (hintLayer == null)
        {
            hintLayer = pulseHint.parent as RectTransform;
        }

        hintLayer.SetAsLastSibling();

        Camera uiCamera = null;

        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = rootCanvas.worldCamera;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            hintLayer,
            screenPoint,
            uiCamera,
            out localPoint
        );

        pulseHint.SetParent(hintLayer, false);
        pulseHint.anchorMin = new Vector2(0.5f, 0.5f);
        pulseHint.anchorMax = new Vector2(0.5f, 0.5f);
        pulseHint.pivot = new Vector2(0.5f, 0.5f);
        pulseHint.anchoredPosition = localPoint;
        pulseHint.localScale = Vector3.one;
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
            pulseCoroutine = null;
        }

        if (pulseHint != null)
        {
            pulseHint.gameObject.SetActive(false);
        }
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

    

    void ChangeAmbience(AudioClip clip, float targetVolume)
    {
        if (ambienceSource == null || clip == null) return;

        if (ambienceAudioCoroutine != null)
        {
            StopCoroutine(ambienceAudioCoroutine);
        }

        ambienceAudioCoroutine = StartCoroutine(ChangeLoopAudioRoutine(
            ambienceSource,
            clip,
            targetVolume,
            audioFadeTime
        ));
    }

    void ChangeMusic(AudioClip clip, float targetVolume)
    {
        if (musicSource == null || clip == null) return;

        if (musicAudioCoroutine != null)
        {
            StopCoroutine(musicAudioCoroutine);
        }

        musicAudioCoroutine = StartCoroutine(ChangeLoopAudioRoutine(
            musicSource,
            clip,
            targetVolume,
            audioFadeTime
        ));
    }

    void FadeMusicVolume(float targetVolume, float duration)
    {
        if (musicSource == null) return;

        if (musicAudioCoroutine != null)
        {
            StopCoroutine(musicAudioCoroutine);
        }

        musicAudioCoroutine = StartCoroutine(FadeAudioVolumeRoutine(
            musicSource,
            targetVolume,
            duration
        ));
    }

    void StopAmbienceWithFade()
    {
        if (ambienceSource == null) return;

        if (ambienceAudioCoroutine != null)
        {
            StopCoroutine(ambienceAudioCoroutine);
        }

        ambienceAudioCoroutine = StartCoroutine(StopAudioWithFadeRoutine(
            ambienceSource,
            audioFadeTime
        ));
    }

    void StopMusicWithFade()
    {
        if (musicSource == null) return;

        if (musicAudioCoroutine != null)
        {
            StopCoroutine(musicAudioCoroutine);
        }

        musicAudioCoroutine = StartCoroutine(StopAudioWithFadeRoutine(
            musicSource,
            audioFadeTime
        ));
    }

    IEnumerator ChangeLoopAudioRoutine(AudioSource source, AudioClip newClip, float targetVolume, float duration)
    {
        if (source == null || newClip == null)
        {
            yield break;
        }

        if (source.isPlaying && source.clip == newClip)
        {
            yield return StartCoroutine(FadeAudioVolumeRoutine(source, targetVolume, duration));
            yield break;
        }

        if (source.isPlaying)
        {
            yield return StartCoroutine(FadeAudioVolumeRoutine(source, 0f, duration * 0.5f));
            source.Stop();
        }

        source.clip = newClip;
        source.loop = true;
        source.volume = 0f;
        source.Play();

        yield return StartCoroutine(FadeAudioVolumeRoutine(source, targetVolume, duration * 0.5f));
    }

    IEnumerator FadeAudioVolumeRoutine(AudioSource source, float targetVolume, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        float startVolume = source.volume;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            source.volume = Mathf.Lerp(startVolume, targetVolume, t);

            yield return null;
        }

        source.volume = targetVolume;
    }

    IEnumerator StopAudioWithFadeRoutine(AudioSource source, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        yield return StartCoroutine(FadeAudioVolumeRoutine(source, 0f, duration));

        source.Stop();
    }

    void PlayArrowSfxOnce()
    {
        if (arrowPlayed) return;
        if (arrowSource == null || arrowClip == null) return;

        arrowPlayed = true;

        arrowSource.Stop();
        arrowSource.clip = arrowClip;
        arrowSource.loop = false;
        arrowSource.volume = arrowVolume;
        arrowSource.pitch = arrowPitch;
        arrowSource.Play();
    }

    void PlayGlassBreakSfxOnce()
    {
        if (glassBreakPlayed) return;
        if (glassBreakSource == null || glassBreakClip == null) return;

        glassBreakPlayed = true;

        glassBreakSource.Stop();
        glassBreakSource.clip = glassBreakClip;
        glassBreakSource.loop = false;
        glassBreakSource.volume = glassBreakVolume;
        glassBreakSource.pitch = glassBreakPitch;
        glassBreakSource.Play();
    }

    void InitFrameAnimations()
    {
        if (windmillImage != null)
        {
            windmillImage.gameObject.SetActive(false);
            windmillImage.raycastTarget = false;
        }

        if (rainImage != null)
        {
            rainImage.gameObject.SetActive(false);
            rainImage.raycastTarget = false;
        }

        if (rainAnimator != null)
        {
            rainAnimator.enabled = false;
        }

        rocketAnimCoroutines = new Coroutine[rockets.Length];

        for (int i = 0; i < rocketImages.Length; i++)
        {
            if (rocketImages[i] != null)
            {
                rocketImages[i].raycastTarget = false;
            }
        }
    }

    IEnumerator PlayFrameAnimation(Image targetImage, Sprite[] frames, float fps, bool loop)
    {
        if (targetImage == null || frames == null || frames.Length == 0)
        {
            yield break;
        }

        targetImage.gameObject.SetActive(true);

        float frameTime = 1f / fps;
        int index = 0;

        while (true)
        {
            if (frames[index] != null)
            {
                targetImage.sprite = frames[index];
            }

            yield return new WaitForSeconds(frameTime);

            index++;

            if (index >= frames.Length)
            {
                if (loop)
                {
                    index = 0;
                }
                else
                {
                    break;
                }
            }
        }
    }

    void StartWindmillAnimation()
    {
        if (windmillImage == null || windmillFrames == null || windmillFrames.Length == 0) return;

        if (windmillAnimCoroutine != null)
        {
            StopCoroutine(windmillAnimCoroutine);
        }

        windmillAnimCoroutine = StartCoroutine(
            PlayFrameAnimation(windmillImage, windmillFrames, windmillFps, true)
        );
    }

    void StopWindmillAnimation()
    {
        if (windmillAnimCoroutine != null)
        {
            StopCoroutine(windmillAnimCoroutine);
            windmillAnimCoroutine = null;
        }

        if (windmillImage != null)
        {
            windmillImage.gameObject.SetActive(false);
        }
    }

    void StartRainAnimator()
    {
        if (rainImage != null)
        {
            rainImage.gameObject.SetActive(true);
            rainImage.raycastTarget = false;
            SetImageAlpha(rainImage, 1f);
            rainImage.transform.SetAsLastSibling();
        }

        if (rainAnimator != null)
        {
            rainAnimator.enabled = true;
            rainAnimator.Play(0, 0, 0f);
        }
    }

    void StopRainAnimator()
    {
        if (rainAnimator != null)
        {
            rainAnimator.enabled = false;
        }

        if (rainImage != null)
        {
            SetImageAlpha(rainImage, 0f);
            rainImage.gameObject.SetActive(false);
        }
    }

    IEnumerator FadeAutumnToWarWorld(float duration)
    {
        // 保证底层春天图一直存在
        for (int i = 0; i < springImages.Length; i++)
        {
            SetImageAlpha(springImages[i], 1f);
        }

        // 保证战时图已经在下面准备好
        for (int i = 0; i < warOverlayImages.Length; i++)
        {
            SetImageAlpha(warOverlayImages[i], 1f);
        }

        // 一开始先确保战时小女孩不出现
        SetImageAlpha(warGirlImage, 0f);

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            // 秋天场景慢慢消失，露出战时场景
            for (int i = 0; i < autumnOverlayImages.Length; i++)
            {
                SetImageAlpha(autumnOverlayImages[i], Mathf.Lerp(1f, 0f, t));
            }

            // 秋天小女孩跟着秋天一起消失
            SetImageAlpha(autumnGirlImage, Mathf.Lerp(1f, 0f, t));

            // 战时小女孩始终不显示
            SetImageAlpha(warGirlImage, 0f);

            yield return null;
        }

        for (int i = 0; i < autumnOverlayImages.Length; i++)
        {
            SetImageAlpha(autumnOverlayImages[i], 0f);
        }

        // 最后停在战时：两个小女孩都不显示
        SetImageAlpha(autumnGirlImage, 0f);
        SetImageAlpha(warGirlImage, 0f);
    }

    IEnumerator FadeWarToSpringWorld(float duration)
    {
        // 春天图保持显示，战时图慢慢消失后，自然就露出春天
        for (int i = 0; i < springImages.Length; i++)
        {
            SetImageAlpha(springImages[i], 1f);
        }

        for (int i = 0; i < autumnOverlayImages.Length; i++)
        {
            SetImageAlpha(autumnOverlayImages[i], 0f);
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            // 战时图慢慢消失
            for (int i = 0; i < warOverlayImages.Length; i++)
            {
                SetImageAlpha(warOverlayImages[i], Mathf.Lerp(1f, 0f, t));
            }

            // 时间轴战时阶段不显示任何小女孩
            SetImageAlpha(warGirlImage, 0f);
            SetImageAlpha(autumnGirlImage, 0f);

            yield return null;
        }

        for (int i = 0; i < warOverlayImages.Length; i++)
        {
            SetImageAlpha(warOverlayImages[i], 0f);
        }

        SetImageAlpha(warGirlImage, 0f);
        SetImageAlpha(autumnGirlImage, 0f);
    }


}