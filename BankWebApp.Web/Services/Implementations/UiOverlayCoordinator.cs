using BankWebApp.Web.Services.Interfaces;

namespace BankWebApp.Web.Services.Implementations;

public sealed class UiOverlayCoordinator : IUiOverlayCoordinator
{
    public event Action<string>? OverlayActivated;

    public void Activate(string overlayKey)
    {
        OverlayActivated?.Invoke(overlayKey);
    }
}
