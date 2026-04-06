using DungeonProject.Core.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonProject.UI
{
    public class PrototypeNumberButton : MonoBehaviour
    {
        [SerializeField] private PrototypeGameManager gameManager;    // 프로토타입 게임 매니저
        [SerializeField] private int numberValue = 1;                 // 이 버튼이 제출할 숫자
        [SerializeField] private Button targetButton;                 // 버튼 컴포넌트 참조

        private void Reset()
        {
            targetButton = GetComponent<Button>();
        }

        private void Awake()
        {
            if (targetButton == null)
                targetButton = GetComponent<Button>();

            if (targetButton != null)
            {
                targetButton.onClick.AddListener(OnClickButton);
            }
        }

        private void Update()
        {
            if (targetButton == null || gameManager == null)
                return;

            targetButton.interactable = gameManager.CanCurrentPlayerSubmit(numberValue);
        }

        private void OnClickButton()
        {
            if (gameManager == null)
                return;

            gameManager.SubmitNumberForCurrentPlayer(numberValue);
        }
    }
}