using System.Text;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CodeTitans.Odysseus;

/// <summary>
/// Periodically uploads batches of entries to the Odysseus Logging Platform over HTTP.
///
/// Entry storage/persistence is entirely delegated to <see cref="OdysseusStore"/> - this class only
/// decides when to attempt an upload and does the actual network call, handing a batch to the
/// store's <see cref="OdysseusStore.TakeBatch"/> and reporting the outcome back via
/// <see cref="OdysseusStore.ConfirmSent"/>/<see cref="OdysseusStore.Requeue"/>.
/// </summary>
sealed class OdysseusCollection<T>
{
    private const int MaxBackoffDelaySeconds = 3 * 60;

    private readonly IHttpClientFactory _clientFactory;
    private readonly Uri _baseUri;
    private readonly string _endPoint;
    private readonly string _entityName;
    private readonly int _delaySeconds;
    private readonly int _maxDelaySeconds;
    private readonly Action<string>? _internalLog;
    private readonly OdysseusStore _store;

    private readonly object _lock = new();
    // Doubles on every failed upload (no connectivity, ...) up to _maxDelaySeconds, so a prolonged
    // outage doesn't keep hammering the network on the original cadence; reset back to
    // _delaySeconds as soon as an upload succeeds again.
    private int _currentDelaySeconds;
    private Timer? _timer;

    public OdysseusCollection(IHttpClientFactory clientFactory, string? host, string endPoint, string entityName,
        int delay = 5, string? walFilePath = null, int maxEntries = OdysseusClient.DefaultMaxEntries, Action<string>? internalLog = null)
    {
        _clientFactory = clientFactory;
        _baseUri = string.IsNullOrEmpty(host) ? new Uri("https://odysseus.codetitans.dev") : new Uri(host);
        _endPoint = endPoint;
        _entityName = entityName;
        // delay <= 0 is a deliberate "flush almost immediately" mode (used by tests) and is kept as-is
        // rather than clamped to a minimum, so it keeps behaving the same once an upload succeeds.
        _delaySeconds = delay;
        // never cap backoff below the configured base delay, in case that's already > 3 minutes
        _maxDelaySeconds = Math.Max(_delaySeconds, MaxBackoffDelaySeconds);
        _currentDelaySeconds = _delaySeconds;
        _internalLog = internalLog;
        _store = new OdysseusStore(walFilePath, maxEntries, internalLog);

        // pick up anything the store recovered from a previous session
        if (_store.HasPending)
        {
            ScheduleFlush();
        }
    }

    public OdysseusCollection<T> Add(T item)
    {
        _store.Add(JsonSerializer.Serialize(item));
        ScheduleFlush();
        return this;
    }

    /// <summary>
    /// Forces everything currently buffered to durable storage right now, bypassing the normal
    /// "only persist if the upload didn't go through" flow. Meant to be called explicitly, e.g.
    /// right before intentionally shutting the process down.
    /// </summary>
    public void PersistPendingNow()
    {
        _store.PersistNow();
    }

    private void ScheduleFlush()
    {
        lock (_lock)
        {
            if (_timer != null)
            {
                return;
            }

            var delayMillis = _currentDelaySeconds <= 0 ? 50 : _currentDelaySeconds * 1000;
            _timer = new Timer(delayMillis);
            _timer.Elapsed += ExecuteUploadAsync;
            _timer.AutoReset = false;
            _timer.Start();
        }
    }

    private async void ExecuteUploadAsync(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        var batch = _store.TakeBatch();
        if (batch.IsEmpty)
        {
            return;
        }

        bool success;
        try
        {
            success = await PerformUploadAsync(batch.Items);
        }
        catch (Exception ex)
        {
            _internalLog?.Invoke($"Failed to upload {_entityName} to Odysseus: {ex.Message}");
            success = false;
        }

        if (success)
        {
            // never touched disk on a healthy connection - _store.ConfirmSent() is then a no-op
            _store.ConfirmSent(batch);
            lock (_lock)
            {
                _currentDelaySeconds = _delaySeconds;
            }
        }
        else
        {
            // retried on a later flush - the store persists whatever wasn't already durable
            _store.Requeue(batch);
            int nextDelaySeconds;
            lock (_lock)
            {
                // A base delay <= 0 is the "flush almost immediately" mode - doubling that stays
                // 0 forever, so give backoff a 1s floor to start from once failures begin.
                var baseline = _currentDelaySeconds <= 0 ? 1 : _currentDelaySeconds;
                _currentDelaySeconds = Math.Min(baseline * 2, _maxDelaySeconds);
                nextDelaySeconds = _currentDelaySeconds;
            }
            _internalLog?.Invoke($"Upload of {_entityName} failed, backing off to {nextDelaySeconds}s before the next attempt");
        }

        // restart the timer if needed to upload something again
        if (_store.HasPending)
        {
            ScheduleFlush();
        }
    }

    private async Task<bool> PerformUploadAsync(IReadOnlyList<string> items)
    {
        var client = _clientFactory.CreateClient();
        var json = ToJsonArray(items);
        var content = new StringContent(json, Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);

        var apiUri = new Uri(_baseUri, _endPoint);
        var response = await client.PostAsync(apiUri, content);

        if (response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            _internalLog?.Invoke($"Successfully uploaded {_entityName} to Odysseus: {response.StatusCode} (\"{text}\")");
            return true;
        }
        else
        {
            var text = await response.Content.ReadAsStringAsync();
            _internalLog?.Invoke($"Failed to upload {_entityName} to Odysseus: {response.StatusCode} (\"{text}\")");
            return false;
        }
    }

    private static string ToJsonArray(IReadOnlyList<string> items)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append(items[i]);
        }
        sb.Append(']');
        return sb.ToString();
    }
}
