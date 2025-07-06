using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Linq;

public class CivSelectionUI : MonoBehaviour
{
    [Tooltip("The parent under which buttons will be instantiated")]
    public Transform container;       // assign: CivListPanel

    [Tooltip("A simple button prefab with a Text child called \"Label\"")]
    public GameObject buttonPrefab;   // assign: Prefabs/CivButton

    void Start()
    {
        // Load all your CivConfiguration assets from Resources/CivConfigs/
        var configs = Resources.LoadAll<CivConfiguration>("CivConfigs");
        if (configs == null || configs.Length == 0)
        {
            Debug.LogError("No CivConfigs found in Resources/CivConfigs!");
            return;
        }

        // Sort alphabetically and create one button per config
        foreach (var cfg in configs.OrderBy(c => c.name))
        {
            var btnGO = Instantiate(buttonPrefab, container);
            var txt = btnGO.GetComponentInChildren<Text>();
            txt.text = cfg.name;

            btnGO.GetComponent<Button>().onClick.AddListener(() => OnSelectCiv(cfg));
        }
    }

    void OnSelectCiv(CivConfiguration chosen)
    {
        // Store the choice for both sides (for now)
        MenuController.SelectedWhiteCiv = chosen;
        MenuController.SelectedBlackCiv = chosen;

        // Load the GameScene, where GameController.Start() will call:
        // NewGame(SelectedWhiteCiv, SelectedBlackCiv);
        SceneManager.LoadScene("GameScene");
    }
}
