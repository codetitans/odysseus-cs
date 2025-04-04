using System.Text;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CodeTitans.Odysseus;

public sealed class OdysseusCollection<T>
{
    private static readonly Uri BaseUri = new Uri("https://odysseus.codetitans.dev");

    private readonly IHttpClientFactory _clientFactory;
    private readonly string _endPoint;
    private readonly string _entityName;
    private readonly int _delay;
    private readonly Action<string>? _internalLog;

    private readonly object _lock = new object();
    private List<T> _entries;
    private List<T> _toUpload;
    private Timer? _timer;

    public OdysseusCollection(IHttpClientFactory clientFactory, string endPoint, string entityName, int delay = 5, Action<string>? internalLog = null)
    {
        _clientFactory = clientFactory;
        _endPoint = endPoint;
        _entityName = entityName;
        _delay = delay;
        _internalLog = internalLog;

        _entries = new List<T>();
        _toUpload = new List<T>();
    }

    public OdysseusCollection<T> Add(T item)
    {
        lock (_lock)
        {
            _entries.Add(item);
            if (_timer == null)
            {
                StartTimer();
            }
        }

        return this;
    }

    private void StartTimer()
    {
        _timer = new Timer( _delay <= 0 ? 50 : _delay * 1000);
        _timer.Elapsed += ExecuteUploadAsync;
        _timer.AutoReset = false;
        _timer.Start();
    }

    private async void ExecuteUploadAsync(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            _toUpload = _entries;
            _entries = new List<T>();
        }

        bool success = false;
        try
        {
            success = await PerformUploadAsync();
            if (success)
            {
                lock (_lock)
                {
                    _toUpload = new List<T>();
                    _timer?.Dispose();
                    _timer = null;
                }
            }
        }
        catch (Exception ex)
        {
            _internalLog?.Invoke($"Failed to upload {_entityName} to Odysseus: {ex.Message}");
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        // restart the timer if needed
        if (!success || _entries.Count > 0)
        {
            lock (_lock)
            {
                _toUpload.AddRange(_entries);
                _entries.Clear();
                StartTimer();
            }
        }
    }

    private async Task<bool> PerformUploadAsync()
    {
        var client = _clientFactory.CreateClient();
        var json = JsonSerializer.Serialize(_toUpload);
        var content = new StringContent(json, Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);

        var apiUri = new Uri(BaseUri, _endPoint);
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
}
