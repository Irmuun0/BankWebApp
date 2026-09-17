namespace BankWebApp.Web.Services.Interfaces;

public interface IUiOverlayCoordinator
{
    event Action<string>? OverlayActivated;

    void Activate(string overlayKey);
}
