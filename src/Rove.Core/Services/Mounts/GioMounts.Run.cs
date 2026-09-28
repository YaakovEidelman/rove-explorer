using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Rove.Core.Services;

public sealed partial class GioMounts
{
    public const string RetryNote = "That didn't work. Try again, or Esc to give up.";

    private async Task<GioRun> RunAsync(string[] args, IMountPrompter? prompter, CancellationToken ct)
    {
        Process process;
        try
        {
            process = Process.Start(Start(args)) ?? throw new InvalidOperationException("gio did not start.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new GioRun(-1, "", ex.Message, Prompted: false);
        }

        using (process)
        await using (ct.Register(() => Kill(process)))
        {
            if (prompter is null)
                process.StandardInput.Close();

            Task<string> errors = process.StandardError.ReadToEndAsync(CancellationToken.None);
            StringBuilder output = new();
            bool prompted = false;
            try
            {
                prompted = await ReadAnsweringAsync(process, prompter, output).ConfigureAwait(false);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                Kill(process);
            }

            string error = await errors.ConfigureAwait(false);
            int exit = process.HasExited ? process.ExitCode : -1;
            return new GioRun(ct.IsCancellationRequested ? -1 : exit, output.ToString(), error, prompted);
        }
    }

    private static async Task<bool> ReadAnsweringAsync(Process process, IMountPrompter? prompter, StringBuilder output)
    {
        char[] buffer = new char[4096];
        StringBuilder pending = new();
        string lastMessage = "";
        HashSet<string> askedFields = [];
        bool prompted = false;
        int read;
        while ((read = await process.StandardOutput.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            output.Append(buffer, 0, read);
            if (prompter is null)
                continue;

            pending.Append(buffer, 0, read);
            if (GioPrompt.TryRead(pending.ToString()) is not { } prompt)
                continue;

            pending.Clear();
            prompted = true;
            string message = prompt.Message.Length > 0 ? prompt.Message : lastMessage;
            lastMessage = message;
            if (!askedFields.Add(prompt.Field) && !prompt.IsChoice)
                message = RetryNote + "\n" + message;

            string? answer = await AnswerAsync(prompt, message, prompter).ConfigureAwait(false);
            if (answer is null)
            {
                process.StandardInput.Close();
                continue;
            }
            await process.StandardInput.WriteLineAsync(answer).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
        }
        return prompted;
    }

    private static async Task<string?> AnswerAsync(GioPrompt prompt, string message, IMountPrompter prompter)
    {
        if (!prompt.IsChoice)
            return await prompter.AskTextAsync(message, prompt.Field, prompt.IsSecret, prompt.Suggested).ConfigureAwait(false);

        int? picked = await prompter.ChooseAsync(message, prompt.Choices).ConfigureAwait(false);
        return picked is { } index && index >= 0 && index < prompt.Choices.Length
            ? (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
        }
    }
}
