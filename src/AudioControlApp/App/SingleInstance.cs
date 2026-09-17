using System.IO.Pipes;
using System.Text;

namespace AudioControlApp.App;

/// <summary>
/// Guarantees a single running instance and lets later invocations hand their command line to it
/// (e.g. <c>AudioControlApp.exe --set 5.1</c>).
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\AudioControlApp.SingleInstance.v2";
    private const string PipeName = "AudioControlApp.Command.v2";

    private readonly Mutex _mutex;
    private readonly bool _isFirst;
    private CancellationTokenSource? _cts;
    private Thread? _serverThread;

    public bool IsFirstInstance => _isFirst;

    /// <summary>Raised on a thread-pool thread with the command line sent by another instance.</summary>
    public event Action<string[]>? CommandReceived;

    public SingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out _isFirst);
    }

    public void StartServer()
    {
        if (!_isFirst)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _serverThread = new Thread(() => ServerLoop(_cts.Token))
        {
            IsBackground = true,
            Name = "SingleInstancePipe",
        };
        _serverThread.Start();
    }

    private void ServerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                using var reader = new StreamReader(server, Encoding.UTF8);
                string? payload = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(payload))
                {
                    string[] args = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(a => a.TrimEnd('\r')).ToArray();
                    CommandReceived?.Invoke(args);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Warn("Pipe server error: " + ex.Message);
                Thread.Sleep(250);
            }
        }
    }

    /// <summary>Sends a command line to the running instance. Returns false if none answered.</summary>
    public static bool SendToRunningInstance(string[] args, int timeoutMs = 2000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            writer.Write(string.Join('\n', args));
            writer.Flush();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not reach the running instance: " + ex.Message);
            return false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        if (_isFirst)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
            }
        }

        _mutex.Dispose();
    }
}
