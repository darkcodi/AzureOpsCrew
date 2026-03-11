using Serilog;
using System.Collections.Concurrent;

namespace AzureOpsCrew.Api.Background;

public class AgentSignalManager
{
    private readonly ConcurrentDictionary<(Guid agentId, Guid chatId), AutoResetEvent> _events = new();

    public void Signal(Guid agentId, Guid chatId)
    {
        var key = (agentId, chatId);
        var evt = _events.GetOrAdd(key, _ => new AutoResetEvent(false));
        evt.Set();
        Log.Debug("[BACKGROUND] Signal for agent {AgentId} and chat {ChatId}", agentId, chatId);
    }

    public void WaitForSignal(Guid agentId, Guid chatId, CancellationToken ct)
    {
        var key = (agentId, chatId);
        var evt = _events.GetOrAdd(key, _ => new AutoResetEvent(false));

        // Wait on either the signal event OR the cancellation token
        WaitHandle.WaitAny(new[] { evt, ct.WaitHandle });
    }
}
