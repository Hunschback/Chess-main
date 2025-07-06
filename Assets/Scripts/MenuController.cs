using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    public CivConfiguration chosenCiv;
    public static CivConfiguration SelectedWhiteCiv;
    public static CivConfiguration SelectedBlackCiv;


    public static void PlayGame()
    {
        // 1) Go to selection scene
        SceneManager.LoadScene("CivSelectionScene");
    }

    // (Optional) Hook this up to an Exit/Quit button
    public void QuitGame()
    {
        Application.Quit();
    }
}
