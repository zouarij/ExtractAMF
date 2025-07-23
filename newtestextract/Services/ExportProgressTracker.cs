using System.Collections.Concurrent;

public class ExportProgressTracker
{
    private readonly ConcurrentDictionary<string, ExportProgress> _progress = new();

    public void Initialize(string operationId, int totalSteps)
    {
        _progress[operationId] = new ExportProgress
        {
            TotalSteps = totalSteps,
            CurrentStep = 0,
            StartTime = DateTime.UtcNow
        };
    }

    public void UpdateProgress(string operationId, string message)
    {
        if (_progress.TryGetValue(operationId, out var progress))
        {
            progress.CurrentStep++;
            progress.Message = message;
            progress.Percentage = (int)((double)progress.CurrentStep / progress.TotalSteps * 100);

            var elapsed = DateTime.UtcNow - progress.StartTime;
            progress.EstimatedRemaining = TimeSpan.FromTicks(elapsed.Ticks * progress.TotalSteps / progress.CurrentStep);
        }
    }

    public ExportProgress GetProgress(string operationId)
    {
        return _progress.TryGetValue(operationId, out var progress) ? progress : null;
    }

    public void Complete(string operationId) => _progress.TryRemove(operationId, out _);
}

public class ExportProgress
{
    public int TotalSteps { get; set; }
    public int CurrentStep { get; set; }
    public int Percentage { get; set; }
    public string Message { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan EstimatedRemaining { get; set; }
}