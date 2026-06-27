using System.Collections.Concurrent;

namespace SignalRDemo.Api.Hubs;

/// <summary>
/// Rastreia presença em memória: cada usuário pode ter várias conexões (multi-aba/dispositivo).
/// Singleton — o estado é por nó. Em scale-out com Redis, a presença "verdadeira" deveria
/// ser consolidada no backplane; aqui mantemos por nó e o README explica o trade-off.
/// </summary>
public class PresenceTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();
    private readonly ConcurrentDictionary<Guid, string> _displayNames = new();

    /// <returns>true se foi a PRIMEIRA conexão do usuário (acabou de ficar online).</returns>
    public bool Connect(Guid userId, string displayName, string connectionId)
    {
        _displayNames[userId] = displayName;
        var set = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (set)
        {
            var wasOffline = set.Count == 0;
            set.Add(connectionId);
            return wasOffline;
        }
    }

    /// <returns>true se foi a ÚLTIMA conexão do usuário (ficou offline).</returns>
    public bool Disconnect(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set))
            return false;
        lock (set)
        {
            set.Remove(connectionId);
            return set.Count == 0;
        }
    }

    public bool IsOnline(Guid userId) =>
        _connections.TryGetValue(userId, out var set) && set.Count > 0;

    /// <summary>Conexões ativas de um usuário (para client results direcionados a uma conexão).</summary>
    public IReadOnlyCollection<string> ConnectionsOf(Guid userId)
    {
        if (!_connections.TryGetValue(userId, out var set))
            return Array.Empty<string>();
        lock (set)
            return set.ToArray();
    }

    public IReadOnlyCollection<(Guid UserId, string DisplayName)> OnlineUsers() =>
        _connections
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => (kv.Key, _displayNames.GetValueOrDefault(kv.Key, "?")))
            .ToList();
}
