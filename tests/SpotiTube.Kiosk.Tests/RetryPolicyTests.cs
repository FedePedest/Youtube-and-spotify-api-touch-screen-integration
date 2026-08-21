using Xunit;
using SpotiTube.Kiosk.Resilience;

namespace SpotiTube.Kiosk.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task RetriesUntilSuccess()
    {
        int calls = 0;
        var result = await RetryPolicy.RunWithRetryAsync(() =>
        {
            calls++;
            if (calls < 3) throw new InvalidOperationException("boom");
            return Task.FromResult(42);
        }, maxAttempts: 5);

        Assert.Equal(42, result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ReturnsDefault_WhenAllAttemptsFail()
    {
        var result = await RetryPolicy.RunWithRetryAsync<int?>(
            () => throw new InvalidOperationException("boom"),
            maxAttempts: 2);

        Assert.Null(result);
    }

    [Fact]
    public async Task InvokesOnErrorForEachFailure()
    {
        var errors = new List<string>();
        await RetryPolicy.RunWithRetryAsync<int?>(
            () => throw new InvalidOperationException("boom"),
            maxAttempts: 3,
            onError: ex => errors.Add(ex.Message));

        Assert.Equal(3, errors.Count);
    }
}
