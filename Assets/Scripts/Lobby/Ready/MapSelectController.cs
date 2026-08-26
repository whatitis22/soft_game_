using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MapSelectController : MonoBehaviour
{
    private const int SlotCount = 3;

    [Header("Data")]
    [SerializeField] private TextAsset monsterJsonFile;

    [Header("Scene")]
    [SerializeField] private string targetSceneName = "GameScene";

    [Header("Slots")]
    [SerializeField] private Button[] stageButtons = new Button[SlotCount];
    [SerializeField] private TMP_Text[] stageTexts = new TMP_Text[SlotCount];
    [SerializeField] private string emptySlotText = string.Empty;
    [SerializeField] private string fallbackDescription = "설명";

    private readonly StageOption[] assignedStages = new StageOption[SlotCount];
    private NetworkSessionState subscribedSession;

    private void Awake()
    {
        AutoWireSlots();
    }

    private void Start()
    {
        SubscribeToSelectionSeed();
        RefreshSlots();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSelectionSeed();

        for (int i = 0; i < stageButtons.Length; i++)
        {
            if (stageButtons[i] != null)
                stageButtons[i].onClick.RemoveAllListeners();
        }
    }

    private void RefreshSlots()
    {
        List<StageOption> stages = LoadStageOptions();
        Shuffle(stages, GetSelectionSeed());

        for (int i = 0; i < SlotCount; i++)
        {
            StageOption stage = i < stages.Count ? stages[i] : null;
            assignedStages[i] = stage;
            ApplySlot(i, stage);
        }
    }

    private void SubscribeToSelectionSeed()
    {
        subscribedSession = NetworkSessionState.Instance;
        if (subscribedSession != null)
            subscribedSession.CurrentMapSelectionSeed.OnValueChanged += HandleSelectionSeedChanged;
    }

    private void UnsubscribeFromSelectionSeed()
    {
        if (subscribedSession != null)
            subscribedSession.CurrentMapSelectionSeed.OnValueChanged -= HandleSelectionSeedChanged;

        subscribedSession = null;
    }

    private void HandleSelectionSeedChanged(int previousValue, int newValue)
    {
        if (newValue != 0 && previousValue != newValue)
            RefreshSlots();
    }

    private List<StageOption> LoadStageOptions()
    {
        Dictionary<string, StageOption> uniqueStages =
            new Dictionary<string, StageOption>(StringComparer.OrdinalIgnoreCase);

        if (monsterJsonFile == null)
        {
            Debug.LogWarning("[MapSelectController] monsterJsonFile is empty.");
            return new List<StageOption>();
        }

        MonsterData[] monsters = JsonArrayHelper.FromJson<MonsterData>(monsterJsonFile.text);
        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterData monster = monsters[i];
            if (monster == null || string.IsNullOrWhiteSpace(monster.spawn_stage))
                continue;

            string stageName = monster.spawn_stage.Trim();
            if (IsStageAlreadySelected(stageName))
                continue;

            string description = string.IsNullOrWhiteSpace(monster.stage_description)
                ? fallbackDescription
                : monster.stage_description.Trim();

            if (!uniqueStages.TryGetValue(stageName, out StageOption option))
            {
                uniqueStages.Add(stageName, new StageOption(stageName, description));
                continue;
            }

            if (option.Description == fallbackDescription && description != fallbackDescription)
                option.Description = description;
        }

        List<StageOption> result = new List<StageOption>(uniqueStages.Values);
        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private bool IsStageAlreadySelected(string stageName)
    {
        NetworkSessionState session = NetworkSessionState.Instance;
        return session != null && session.HasSelectedMonsterStageName(stageName);
    }

    private int GetSelectionSeed()
    {
        NetworkSessionState session = NetworkSessionState.Instance;
        if (session != null && session.CurrentMapSelectionSeed.Value != 0)
            return session.CurrentMapSelectionSeed.Value;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
            return 1;

        return Environment.TickCount;
    }

    private void Shuffle<T>(List<T> list, int seed)
    {
        System.Random random = new System.Random(seed);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }

    private void ApplySlot(int index, StageOption stage)
    {
        Button button = GetSlotButton(index);
        TMP_Text text = GetSlotText(index);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = stage != null && CanLocalPlayerSelectStage();

            int capturedIndex = index;
            button.onClick.AddListener(() => OnClickSlot(capturedIndex));
        }

        if (text == null)
            return;

        if (stage == null)
        {
            text.text = emptySlotText;
            return;
        }

        text.text = $"{stage.Name}\n\n{stage.Description}";
    }

    private bool CanLocalPlayerSelectStage()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    private void OnClickSlot(int index)
    {
        if (index < 0 || index >= assignedStages.Length)
            return;

        StageOption stage = assignedStages[index];
        if (stage == null)
            return;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer)
        {
            GameStatusMessageRouter.Show("방장이 전투 스테이지를 선택합니다.");
            return;
        }

        ResetAllPlayerNumberCards();
        NetworkSessionState.Instance?.ServerSetCurrentMonsterStageName(stage.Name);
        NetworkSessionState.Instance?.ServerMarkMonsterStageSelected(stage.Name);
        LoadBattleScene();
    }

    private void ResetAllPlayerNumberCards()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer)
            return;

        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
            player.ServerResetNumberCards();

        NetworkSessionState.Instance?.ServerResetRoundLock();
    }

    private void LoadBattleScene()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            GameStatusMessageRouter.Show("이동할 씬 이름이 비어 있습니다.");
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
        {
            if (!manager.NetworkConfig.EnableSceneManagement)
            {
                GameStatusMessageRouter.Show("Network Scene Management가 비활성화되어 있습니다.");
                return;
            }

            var status = manager.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                GameStatusMessageRouter.Show($"씬 이동에 실패했습니다. ({status})");

            return;
        }

        SceneManager.LoadSceneAsync(targetSceneName);
    }

    private void AutoWireSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            string objectName = $"Map {i + 1}";
            GameObject slotObject = GameObject.Find(objectName);
            if (slotObject == null)
                continue;

            if (GetSlotButton(i) == null)
                stageButtons[i] = slotObject.GetComponent<Button>();

            if (GetSlotText(i) == null)
                stageTexts[i] = slotObject.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private Button GetSlotButton(int index)
    {
        return stageButtons != null && index < stageButtons.Length ? stageButtons[index] : null;
    }

    private TMP_Text GetSlotText(int index)
    {
        return stageTexts != null && index < stageTexts.Length ? stageTexts[index] : null;
    }

    private class StageOption
    {
        public StageOption(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; set; }
    }
}
