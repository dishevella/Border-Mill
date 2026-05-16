using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayButtonLoadScene : MonoBehaviour
{
    [Header("要加载的场景名字")]
    public string sceneName;

    // Play按钮调用这个
    public void LoadScene()
    {
        SceneManager.LoadScene(sceneName);
    }

    // Quit按钮调用这个
    public void QuitGame()
    {
        Debug.Log("退出游戏");

        Application.Quit();
    }
}