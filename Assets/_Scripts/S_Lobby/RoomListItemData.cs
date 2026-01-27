using Fusion;

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
