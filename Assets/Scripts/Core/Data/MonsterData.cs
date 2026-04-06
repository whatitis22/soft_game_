using UnityEngine;

namespace DungeonProject.Core.Data
{
    [CreateAssetMenu(fileName = "MonsterData_", menuName = "DungeonProject/Data/Monster Data")]
    public class MonsterData : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string monsterId;          // 몬스터 고유 ID
        [SerializeField] private string monsterName;        // 몬스터 표시 이름
        [SerializeField] private int maxHp = 12;            // 몬스터 최대 체력

        [Header("보상 정보")]
        [SerializeField] private int goldReward = 1;        // 처치 성공 시 지급할 골드 개수
        [SerializeField] private int silverReward = 1;      // 처치 성공 시 지급할 실버 개수
        [SerializeField] private int copperReward = 1;      // 처치 성공 시 지급할 코퍼 개수

        public string MonsterId => monsterId;
        public string MonsterName => monsterName;
        public int MaxHp => maxHp;
        public int GoldReward => goldReward;
        public int SilverReward => silverReward;
        public int CopperReward => copperReward;
    }
}