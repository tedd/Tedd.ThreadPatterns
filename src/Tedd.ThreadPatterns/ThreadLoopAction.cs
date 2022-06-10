using Microsoft.Extensions.Logging;

namespace Tedd;
public class ThreadLoopAction
{
    private readonly ILogger _logger;
    private readonly string _name;
    private readonly Action<CancellationToken> _action;
    private readonly int _retryDelayMs;
    private readonly Action<Exception>? _exceptionAction;
    private Thread? _thread;

    public ThreadLoopAction(ILogger logger, string name, Action<CancellationToken> action, int retryDelayMs, Action<Exception>? exceptionAction = null)
    {
        _logger = logger;
        _name = name;
        _action = action;
        _retryDelayMs = retryDelayMs;
        _exceptionAction = exceptionAction;
    }

    public static ThreadLoopAction StartNewBackgroundThread(ILogger logger, string name, Action<CancellationToken> action, CancellationToken cancellationToken, int retryDelayMs = 60_000, Action<Exception>? exceptionAction = null)
    {
        var ret = new ThreadLoopAction(logger, name, action, retryDelayMs, exceptionAction);
        ret.StartBackgroundThread(cancellationToken);
        return ret;
    }

    public void StartBackgroundThread(CancellationToken cancellationToken)
    {
        _thread = new Thread(ActionThreadLoop)
        {
            Name = _name,
            IsBackground = true
        };
        _thread.Start(cancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
    private void ActionThreadLoop(object? obj)
    {
        var threadId = Environment.CurrentManagedThreadId;
        _logger.LogDebug($"[ThreadLoop #{threadId}] Starting.");
        var cancellationToken = (CancellationToken)obj!;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _action(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ThreadLoop #{threadId}] Exception.");
                _exceptionAction?.Invoke(ex);
            }
            _logger.LogDebug("[ThreadLoop #{threadId}] Sleeping for {retryDelayMs}ms before re-executing action.", threadId, _retryDelayMs);
            Thread.Sleep(_retryDelayMs);
        }
    }
}