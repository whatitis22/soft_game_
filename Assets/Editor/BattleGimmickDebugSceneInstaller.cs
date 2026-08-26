using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleGimmickDebugSceneInstaller
{
    private const string BattleScenePath = "Assets/Scenes/GameScene.unity";
    private const string DebugObjectName = "전투 기믹 디버그 설정";

    [MenuItem("Tools/Battle/전투 기믹 디버그 설정 설치")]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        BattleGimmickDebugSettings[] settings =
            Object.FindObjectsByType<BattleGimmickDebugSettings>(FindObjectsInactive.Include);

        GameObject target = settings.Length > 0 ? settings[0].gameObject : new GameObject(DebugObjectName);
        target.name = DebugObjectName;

        if (target.GetComponent<BattleGimmickDebugSettings>() == null)
            target.AddComponent<BattleGimmickDebugSettings>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
