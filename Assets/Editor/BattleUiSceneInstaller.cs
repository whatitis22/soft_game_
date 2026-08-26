using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleUiSceneInstaller
{
    private const string BattleScenePath = "Assets/Scenes/GameScene.unity";
    private const string NormalUiName = "전투 UI - 일반";
    private const string FocusUiName = "전투 UI - 확대";

    [MenuItem("Tools/Battle/전투 UI 설치 또는 갱신")]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        BattleHudUI normalUi = GetOrCreateUi<BattleHudUI>(canvas.transform, NormalUiName);
        normalUi.BuildPreviewInEditor();
        normalUi.gameObject.SetActive(true);

        BattleStandingFocusController focusUi = GetOrCreateUi<BattleStandingFocusController>(canvas.transform, FocusUiName);
        focusUi.BuildPreviewInEditor(true);
        focusUi.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Battle UI installed. Toggle '전투 UI - 일반' and '전투 UI - 확대' in the Hierarchy.");
    }

    private static T GetOrCreateUi<T>(Transform parent, string objectName) where T : Component
    {
        T[] existingComponents = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        for (int i = 0; i < existingComponents.Length; i++)
        {
            if (existingComponents[i] != null && existingComponents[i].gameObject.name == objectName)
            {
                Stretch(existingComponents[i].GetComponent<RectTransform>());
                existingComponents[i].transform.SetParent(parent, false);
                return existingComponents[i];
            }
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        return go.AddComponent<T>();
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
