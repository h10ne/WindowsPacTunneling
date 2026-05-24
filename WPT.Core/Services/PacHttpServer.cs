using System.Net;
using System.Text;

namespace WPT.Core.Services;

public sealed class PacHttpServer : IDisposable
{
    private readonly object _sync = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private string _pacContent = string.Empty;
    private int _port = 1080;
    private bool _disposed;

    public int Port
    {
        get
        {
            lock (_sync)
            {
                return _port;
            }
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _cts != null;
            }
        }
    }

    public void SetPort(int port)
    {
        lock (_sync)
        {
            if (_port == port)
            {
                return;
            }

            _port = port;
            StopInternal();
        }
    }

    public void SetPacContent(string content) => _pacContent = content;

    public void Restart()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            StopInternal();
            StartInternal();
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_cts != null)
            {
                return;
            }

            StopInternal();
            StartInternal();
        }
    }

    private void StartInternal()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        listener.Prefixes.Add($"http://localhost:{_port}/");

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            listener.Close();
            throw new InvalidOperationException(
                $"Не удалось запустить PAC-сервер на порту {_port}. " +
                "Возможно, порт занят другим приложением (например, Shadowsocks).",
                ex);
        }

        var cts = new CancellationTokenSource();
        _listener = listener;
        _cts = cts;
        _listenTask = Task.Run(() => ListenLoopAsync(listener, cts.Token));
    }

    public void Stop()
    {
        lock (_sync)
        {
            StopInternal();
        }
    }

    public string GetPacUrl(string hash) => $"http://127.0.0.1:{Port}/pac?hash={hash}";

    private void StopInternal()
    {
        if (_cts == null)
        {
            return;
        }

        _cts.Cancel();

        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
        _cts = null;
        _listenTask = null;

        if (_listener != null)
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Close();
            _listener = null;
        }
    }

    private async Task ListenLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context), CancellationToken.None);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";

            if (path.Equals("/pac", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/proxy.pac", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = Encoding.UTF8.GetBytes(_pacContent);
                context.Response.ContentType = "application/x-ns-proxy-autoconfig";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }
        catch
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            context.Response.Close();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
