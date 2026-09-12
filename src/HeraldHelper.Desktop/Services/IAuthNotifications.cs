namespace HeraldHelper.Desktop.Services;

internal interface IAuthNotifications
{
    void Log(string message);

    void OnRefreshed();
}
