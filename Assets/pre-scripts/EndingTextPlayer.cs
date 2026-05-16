using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingTextPlayer : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI endingText;
    public CanvasGroup textCanvasGroup;

    [Header("Text Content")]
    [TextArea(2, 5)]
    public string[] endingLines;

    [Header("Time Settings")]
    public float fadeInTime = 1.5f;
    public float stayTime = 2.5f;
    public float fadeOutTime = 1.5f;

    [Header("Next Scene")]
    public string nextSceneName;

    private void Start()
    {
        StartCoroutine(PlayEndingText());
    }

    private IEnumerator PlayEndingText()
    {
        textCanvasGroup.alpha = 0f;

        for (int i = 0; i < endingLines.Length; i++)
        {
            endingText.text = endingLines[i];

            yield return StartCoroutine(FadeText(0f, 1f, fadeInTime));

            yield return new WaitForSeconds(stayTime);

            yield return StartCoroutine(FadeText(1f, 0f, fadeOutTime));
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator FadeText(float startAlpha, float endAlpha, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            textCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

            yield return null;
        }

        textCanvasGroup.alpha = endAlpha;
    }
}