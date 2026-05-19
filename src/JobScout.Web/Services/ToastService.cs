namespace JobScout.Web.Services;

public record ToastMessage(string Text, string Type, Guid Id);

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Show(string message, string type = "success")
        => OnShow?.Invoke(new ToastMessage(message, type, Guid.NewGuid()));

    public void ShowError(string message) => Show(message, "error");
    public void ShowInfo(string message) => Show(message, "info");
}
