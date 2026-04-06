using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PressAnyKeyToContinue : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    [SerializeField] private string targetSceneName;

    private bool isLoading = false;

    private void Update()
    {
        if (isLoading) return;

        bool keyboardPressed =
            Keyboard.current != null &&
            Keyboard.current.anyKey.wasPressedThisFrame;

        bool mousePressed =
            Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame);

        if (keyboardPressed || mousePressed)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning("이동할 씬 이름이 비어 있습니다.");
                return;
            }

            isLoading = true;
            SceneManager.LoadSceneAsync(targetSceneName);
        }
    }
}