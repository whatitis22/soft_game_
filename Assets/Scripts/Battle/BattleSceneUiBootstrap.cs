using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleSceneUiBootstrap
{
    private const string BattleSceneName = "GameScene";
    private static bool sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!sceneHooked)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneHooked = true;
        }

        RestoreIfBattleScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RestoreIfBattleScene(scene);
    }

    private static void RestoreIfBattleScene(Scene scene)
    {
        if (!string.Equals(scene.name, BattleSceneName, System.StringComparison.Ordinal))
            return;

        BattleSceneUiBootstrapRunner.Run(scene);
    }

    private sealed class BattleSceneUiBootstrapRunner : MonoBehaviour
    {
        private static BattleSceneUiBootstrapRunner instance;

        public static void Run(Scene scene)
        {
            if (instance == null)
            {
                GameObject runnerObject = new GameObject("Battle Scene UI Bootstrap");
                Object.DontDestroyOnLoad(runnerObject);
                instance = runnerObject.AddComponent<BattleSceneUiBootstrapRunner>();
            }

            instance.StartCoroutine(instance.RestoreAfterSceneLoad(scene));
        }

        private IEnumerator RestoreAfterSceneLoad(Scene scene)
        {
            yield return null;
            yield return null;

            if (!string.Equals(SceneManager.GetActiveScene().name, BattleSceneName, System.StringComparison.Ordinal))
                yield break;

            BattleHudUI.EnsureForScene(scene);
            if (BattleHudUI.Instance != null)
                BattleHudUI.Instance.SetFocusHidden(false);

            CardDrawPopupController cardPopup = CardDrawPopupController.GetAny();
            if (cardPopup != null)
                cardPopup.RestoreForBattleSceneEntry();

            BattlePlayerSlots[] slotControllers =
                Object.FindObjectsByType<BattlePlayerSlots>(FindObjectsInactive.Include);

            for (int i = 0; i < slotControllers.Length; i++)
            {
                if (slotControllers[i] != null)
                    slotControllers[i].EnsureSpawnedForBattleSceneEntry();
            }
        }
    }
}
