using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public sealed partial class LinuxAdminSession : IAdminSession
{
    private const string PkexecPath = "/usr/bin/pkexec";
    private const string EndMarker = "END";
    private const int FieldsPerEntry = 6;
    private const int PkexecDismissed = 126;
    private const int PkexecRefused = 127;

    internal const string Script =
        "export PATH=/usr/bin:/bin LC_ALL=C; " +
        "IFS= read -r -d '' secret || exit 1; " +
        "while IFS= read -r -d '' token && IFS= read -r -d '' verb " +
        "&& IFS= read -r -d '' path && IFS= read -r -d '' limit; do " +
        "[ \"$token\" = \"$secret\" ] || exit 1; " +
        "case \"$path\" in /*) ;; *) exit 1;; esac; " +
        "case \"$verb\" in " +
        "list) " +
        "find -H \"$path\" -mindepth 1 -maxdepth 1 " +
        "-printf '%y\\0%Y\\0%s\\0%T@\\0%f\\0%l\\0' 2>/dev/null; " +
        "printf 'END\\0%s\\0' \"$?\";; " +
        "read) " +
        "case \"$limit\" in ''|*[!0-9]*) exit 1;; esac; " +
        "if [ -f \"$path\" ]; then " +
        "printf 'OK\\0'; head -c \"$limit\" -- \"$path\" 2>/dev/null | base64 -w0; printf '\\0'; " +
        "else printf 'FAIL\\0'; fi;; " +
        "*) exit 1;; " +
        "esac; " +
        "done";

    private readonly string _program;
    private readonly string[] _arguments;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly byte[] _buffer = new byte[16 * 1024];
    private readonly List<byte> _token = [];
    private readonly StringBuilder _errors = new();
    private readonly string _secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private Process? _process;
    private Stream? _output;
    private string? _copyRoot;
    private int _copyCount;
    private int _bufferPosition;
    private int _bufferLength;

    internal LinuxAdminSession(string program, string[] arguments)
    {
        _program = program;
        _arguments = arguments;
    }

    public static LinuxAdminSession? ForPkexec()
    {
        string? bash = FindBash();
        if (bash is null || !File.Exists(PkexecPath))
            return null;
        return new LinuxAdminSession(PkexecPath, [bash, "-c", Script]);
    }

    private static string? FindBash() =>
        new[] { "/usr/bin/bash", "/bin/bash" }.FirstOrDefault(File.Exists);

    public bool IsRunning => _process is { HasExited: false };

    public void Dispose()
    {
        if (_copyRoot is not null)
            DeleteQuietly(_copyRoot);
        Process? process = _process;
        _process = null;
        if (process is null)
            return;
        try
        {
            process.StandardInput.Close();
            process.WaitForExit(500);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
        }
        process.Dispose();
    }
}
