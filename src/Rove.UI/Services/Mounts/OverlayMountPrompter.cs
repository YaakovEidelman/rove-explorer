using Avalonia.Threading;
using Rove.Core.Services;
using Rove.UI.ViewModels;

namespace Rove.UI.Services;

public sealed class OverlayMountPrompter(PromptViewModel prompt, ConfirmViewModel confirm) : IMountPrompter
{
    public Task<string?> AskTextAsync(string message, string field, bool secret, string? suggested) =>
        Dispatcher.UIThread.InvokeAsync(() => prompt.AskAsync(message, field, secret, suggested));

    public Task<int?> ChooseAsync(string message, string[] choices) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            TaskCompletionSource<int?> picked = new(TaskCreationOptions.RunContinuationsAsynchronously);
            string question = choices.Length > 0 ? $"{message}\n\nConfirm = {choices[0]}" : message;
            confirm.Request(question, () => picked.TrySetResult(0), () => picked.TrySetResult(null));
            return picked.Task;
        });
}
