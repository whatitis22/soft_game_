public enum TypePlayerInteractionState
{
    None = 0,

    // 선택
    SelectingCard = 1,
    SelectingItem = 2,
    SelectingTarget = 3,

    // 보기
    ViewingInfo = 4,
    WatchingAnimation = 5,

    // UI
    PopupOpen = 6,
    InputLocked = 7
}
