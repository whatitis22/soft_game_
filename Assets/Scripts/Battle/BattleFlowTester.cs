using UnityEngine;

[DisallowMultipleComponent]
public class BattleFlowTester : MonoBehaviour
{
    [SerializeField] private BattleFlowController controller;
    [SerializeField] private string stageName = "Grassland";

    public BattleFlowController Controller
    {
        get
        {
            if (controller == null)
                controller = GetComponent<BattleFlowController>();

            if (controller == null)
                controller = FindAnyObjectByType<BattleFlowController>();

            return controller;
        }
    }

    public string StageName
    {
        get => stageName;
        set => stageName = string.IsNullOrWhiteSpace(value) ? "Grassland" : value.Trim();
    }

    public bool StartBattle()
    {
        if (Controller == null)
        {
            Debug.LogWarning("BattleFlowTester: BattleFlowController 참조가 없습니다.");
            return false;
        }

        // 테스트 버튼은 서버/호스트에서 전투 시작 함수를 직접 호출한다.
        ApplyStageName();
        return Controller.ServerStartBattle();
    }

    public bool ResolveCurrentRoundNow()
    {
        if (Controller == null)
        {
            Debug.LogWarning("BattleFlowTester: BattleFlowController 참조가 없습니다.");
            return false;
        }

        return Controller.ServerResolveCurrentRoundNow();
    }

    public void ApplyStageName()
    {
        if (Controller == null)
        {
            Debug.LogWarning("BattleFlowTester: BattleFlowController 참조가 없습니다.");
            return;
        }

        Controller.ServerSetCurrentMonsterStageName(stageName);
    }

    public void ResetAppearedMonsterHistory()
    {
        if (Controller == null)
        {
            Debug.LogWarning("BattleFlowTester: BattleFlowController 참조가 없습니다.");
            return;
        }

        Controller.ServerResetAppearedMonsterHistory();
    }

    private void Reset()
    {
        controller = GetComponent<BattleFlowController>();
    }
}
