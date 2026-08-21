namespace SpotiTube.Kiosk.Resilience;

public static class RetryPolicy
{
    public static async Task<T?> RunWithRetryAsync<T>(Func<Task<T>> action, int maxAttempts, Action<Exception>? onError = null)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
                }
            }
        }
        return default;
    }
}
