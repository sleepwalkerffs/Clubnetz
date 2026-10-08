namespace Bookennis.Client.Services.Store.Base;

public interface ISemaphoreStore
{
    bool IsLoading { get; }
    Task<bool> WaitUntilLoaded();
    bool IsSaving { get; }
    Task<bool> WaitUntilSaved();
}
