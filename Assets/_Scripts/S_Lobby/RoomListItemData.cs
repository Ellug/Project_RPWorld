using Fusion;

// 방 목록 항목 데이터. SessionInfo 래핑 + 호스트명/비밀번호 정보
public sealed class RoomListItemData
{
    public SessionInfo SessionInfo { get; }
    public string HostName { get; }
    public string Password { get; }

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public RoomListItemData(SessionInfo sessionInfo, string hostName, string password)
    {
        SessionInfo = sessionInfo;
        HostName = hostName;
        Password = password;
    }
}
