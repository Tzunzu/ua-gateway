using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UAGateway.Core.Ipc;
using UAGateway.UI.Services;

namespace UAGateway.UI.Controls;

public sealed partial class LiveOutputViewer : UserControl
{
    private const int MaxDisplayedLines = 1000;

    private readonly IpcEventStreamClient _eventStreamClient = new();
    private readonly Queue<string> _lineBuffer = new();
    private bool _isPaused;
    private bool _eventStreamConnected;
    private bool _eventStreamConnectionKnown;

    public event Action? StreamConnectionStateChanged;

    public bool IsEventStreamConnected => _eventStreamConnected;

    public bool IsEventStreamConnectionKnown => _eventStreamConnectionKnown;

    public LiveOutputViewer()
    {
        InitializeComponent();

        Loaded += LiveOutputViewer_Loaded;
        Unloaded += LiveOutputViewer_Unloaded;
    }

    public void StartMonitoring()
    {
        _eventStreamClient.Start(HandleServiceEvent, HandleEventStreamConnectionStateChanged);
    }

    public void StopMonitoring()
    {
        _ = _eventStreamClient.StopAsync();
    }

    private void LiveOutputViewer_Loaded(object sender, RoutedEventArgs e)
    {
        StartMonitoring();
    }

    private void LiveOutputViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        StopMonitoring();
    }

    private void HandleServiceEvent(IpcEventEnvelope<IpcServiceEventPayload> evt)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isPaused)
            {
                return;
            }

            var endpoint = string.IsNullOrWhiteSpace(evt.Payload.EndpointId)
                ? string.Empty
                : $" endpoint={evt.Payload.EndpointId}";

            var detail = string.IsNullOrWhiteSpace(evt.Payload.Detail)
                ? string.Empty
                : $" detail={evt.Payload.Detail}";

            var line = $"[{evt.TimestampUtc:O}] [{evt.Severity}] [{evt.Category}/{evt.Name}] {evt.Payload.Message}{endpoint}{detail}";
            AppendLine(line);
            LiveStatusText.Text = $"Live events: connected | Total shown: {_lineBuffer.Count}";
        });
    }

    private void HandleEventStreamConnectionStateChanged(bool connected, string status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _eventStreamConnectionKnown = true;
            _eventStreamConnected = connected;
            LiveStatusText.Text = status;
            StreamConnectionStateChanged?.Invoke();
        });
    }

    private void AppendLine(string line)
    {
        _lineBuffer.Enqueue(line);

        if (_lineBuffer.Count <= MaxDisplayedLines)
        {
            if (string.IsNullOrEmpty(LiveOutputText.Text))
            {
                LiveOutputText.Text = line;
            }
            else
            {
                LiveOutputText.Text += Environment.NewLine + line;
            }
        }
        else
        {
            while (_lineBuffer.Count > MaxDisplayedLines)
            {
                _lineBuffer.Dequeue();
            }

            LiveOutputText.Text = string.Join(Environment.NewLine, _lineBuffer);
        }

        if (AutoScrollToggle.IsOn)
        {
            LiveOutputScrollViewer.UpdateLayout();
            LiveOutputScrollViewer.ChangeView(horizontalOffset: null, verticalOffset: LiveOutputScrollViewer.ScrollableHeight, zoomFactor: null);
        }
    }

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        PauseResumeButton.Content = _isPaused ? "Resume" : "Pause";
        LiveStatusText.Text = _isPaused
            ? "Live output paused."
            : _eventStreamConnected
                ? "Live event stream connected."
                : "Waiting for live event stream...";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _lineBuffer.Clear();
        LiveOutputText.Text = string.Empty;
    }
}