namespace StreamStruct.Tests;

public static class TaskExtensions
{
    public static async Task<T> WithTimeout<T>(this Task<T> task, int timeoutMs = 1000)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        try
        {
            return await task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeoutMs}ms");
        }
    }

    public static async Task WithTimeout(this Task task, int timeoutMs = 1000)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        try
        {
            await task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeoutMs}ms");
        }
    }

    public static async Task<T> WithTimeout<T>(this ValueTask<T> valueTask, int timeoutMs = 1000)
    {
        return await valueTask.AsTask().WithTimeout(timeoutMs);
    }

    public static async Task WithTimeout(this ValueTask valueTask, int timeoutMs = 1000)
    {
        await valueTask.AsTask().WithTimeout(timeoutMs);
    }
}