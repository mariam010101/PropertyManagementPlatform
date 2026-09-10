using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PMP.Modules.Auth.Contracts;

namespace PMP.Tests.Integration;

/// <summary>
/// IMP-006: boots the real PMP API (Kestrel + SQLite + EF migrations + seeding) as a
/// child process against a throwaway database and drives it over HTTP, so the MVP
/// slice is verified end-to-end through its public interface (controllers, JWT
/// authorization policies, services, database) rather than through in-process shims.
///
/// No test-only server hooks are added to <c>PMP.Api</c>: configuration is supplied
/// through environment variables and the working directory is a temp folder, so
/// uploads and the SQLite file never touch the repository.
/// </summary>
public sealed class PmpApiFixture : IAsyncLifetime
{
    internal static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private const string JwtTestKey = "PMP_INTEGRATION_TEST_SIGNING_KEY_0123456789ABCDEFG";

    private readonly List<string> _serverLog = new();
    private string _tempDirectory = string.Empty;
    private Process? _process;

    public HttpClient Client { get; private set; } = null!;

    public string DatabasePath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = ResolveBuildConfiguration();
        var apiDll = Path.Combine(repositoryRoot, "src", "PMP.Api", "bin", configuration, "net9.0", "PMP.Api.dll");

        if (!File.Exists(apiDll))
        {
            throw new InvalidOperationException(
                $"The API host was not found at '{apiDll}'. Build the solution before running the integration suite: " +
                "dotnet build PropertyManagement.sln");
        }

        _tempDirectory = Path.Combine(Path.GetTempPath(), $"pmp-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        DatabasePath = Path.Combine(_tempDirectory, "pmp-it.db");

        var port = ReserveFreePort();
        var startInfo = new ProcessStartInfo("dotnet", $"\"{apiDll}\"")
        {
            WorkingDirectory = _tempDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        startInfo.Environment["ConnectionStrings__DefaultConnection"] = $"Data Source={DatabasePath};Foreign Keys=True";
        startInfo.Environment["Jwt__Issuer"] = "PMP";
        startInfo.Environment["Jwt__Audience"] = "PMP";
        startInfo.Environment["Jwt__Key"] = JwtTestKey;
        startInfo.Environment["Logging__LogLevel__Default"] = "Warning";
        startInfo.Environment["Logging__LogLevel__Microsoft.AspNetCore"] = "Warning";

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the PMP API process.");

        _process.OutputDataReceived += (_, e) => Capture(e.Data);
        _process.ErrorDataReceived += (_, e) => Capture(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        Client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = TimeSpan.FromSeconds(60),
        };

        await WaitUntilReadyAsync();
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(10_000);
            }
            catch (Exception ex)
            {
                _serverLog.Add($"Failed to stop the API process: {ex.Message}");
            }
        }

        _process?.Dispose();

        try
        {
            if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            // Temp files are disposable; a locked file must not fail the suite.
            _serverLog.Add($"Failed to delete temp directory: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    // ---------- HTTP helpers ----------

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var result = await PostAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        if (result.Status != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Login for '{email}' failed with {(int)result.Status}. Body: {result.Body}{Environment.NewLine}" +
                $"Server log:{Environment.NewLine}{ServerLog()}");
        }

        return Deserialize<AuthResponse>(result.Body);
    }

    public async Task<ApiResult> GetAsync(string path, string? accessToken = null)
        => await SendAsync(new HttpRequestMessage(HttpMethod.Get, path), accessToken);

    public async Task<ApiResult> PostAsync(string path, object? body = null, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body ?? new { }, Json),
                Encoding.UTF8,
                "application/json"),
        };

        return await SendAsync(request, accessToken);
    }

    public T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Json)
           ?? throw new InvalidOperationException($"Could not deserialize response payload: {json}");

    public string ServerLog() => string.Join(Environment.NewLine, _serverLog);

    private async Task<ApiResult> SendAsync(HttpRequestMessage request, string? accessToken)
    {
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return new ApiResult(response.StatusCode, body);
    }

    // ---------- host lifecycle ----------

    private async Task WaitUntilReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            if (_process!.HasExited)
            {
                throw new InvalidOperationException(
                    $"The API process exited with code {_process.ExitCode} during startup.{Environment.NewLine}" +
                    $"Server log:{Environment.NewLine}{ServerLog()}");
            }

            try
            {
                using var probe = await Client.GetAsync("/api/properties");
                // Any HTTP status proves the host is listening; authorization is asserted by the tests.
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(500);
            }
        }

        throw new InvalidOperationException(
            $"The API did not become ready within 2 minutes. Last error: {lastError?.Message}{Environment.NewLine}" +
            $"Server log:{Environment.NewLine}{ServerLog()}");
    }

    private void Capture(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            lock (_serverLog)
            {
                _serverLog.Add(line);
            }
        }
    }

    /// <summary>Mirrors the API host serialization configuration (Program.cs): enums travel as strings.</summary>
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static int ReserveFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PropertyManagement.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root (PropertyManagement.sln) above the test output directory.");
    }

    private static string ResolveBuildConfiguration()
    {
        var segments = AppContext.BaseDirectory.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var binIndex = Array.IndexOf(segments, "bin");

        return binIndex >= 0 && binIndex + 1 < segments.Length ? segments[binIndex + 1] : "Debug";
    }
}

/// <summary>Raw HTTP result used by assertions so failures can show the response body.</summary>
public readonly record struct ApiResult(HttpStatusCode Status, string Body)
{
    public int Code => (int)Status;

    public override string ToString() => $"{(int)Status} {Status}: {Body}";
}
