public enum TypeGamePhaze
{
    None = 0,

    // 게임 시작
    LeaderSelection = 1,

    // 스테이지 준비
    QuestSelection = 2,
    MealSelection = 3,

    // 라운드 진행
    EventSelection = 4,
    EventResolution = 5,

    // 전투
    BattlePreparation = 6,
    BattleSelection = 7,
    BattleResolution = 8,
    BattleResult = 9,

    // 정산
    RoundResult = 10,
    StageResult = 11,

    // 게임 종료
    GameResult = 12
}
