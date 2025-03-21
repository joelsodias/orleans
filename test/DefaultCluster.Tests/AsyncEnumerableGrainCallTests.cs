﻿using Microsoft.Extensions.Logging;
using Orleans.Internal;
using Orleans.Runtime;
using TestExtensions;
using UnitTests.GrainInterfaces;
using Xunit;

namespace DefaultCluster.Tests;

/// <summary>
/// Tests support for grain methods which return <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
public class AsyncEnumerableGrainCallTests : HostedTestClusterEnsureDefaultStarted
{
    public AsyncEnumerableGrainCallTests(DefaultClusterFixture fixture) : base(fixture)
    {
    }

    [Fact, TestCategory("BVT"), TestCategory("Observable")]
    public async Task ObservableGrain_AsyncEnumerable()
    {
        var grain = GrainFactory.GetGrain<IObservableGrain>(Guid.NewGuid());

        var producer = Task.Run(async () =>
        {
            foreach (var value in Enumerable.Range(0, 5))
            {
                await Task.Delay(200);
                await grain.OnNext(value.ToString());
            }

            await grain.Complete();
        });

        var values = new List<string>();
        await foreach (var entry in grain.GetValues())
        {
            values.Add(entry);
            Logger.LogInformation("ObservableGrain_AsyncEnumerable: {Entry}", entry);
        }

        Assert.Equal(5, values.Count);

        // Check that the enumerator is disposed
        var grainCalls = await grain.GetIncomingCalls();
        Assert.Contains(grainCalls, c => c.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension)) && c.MethodName.Contains(nameof(IAsyncDisposable.DisposeAsync)));
    }

    [Theory, TestCategory("BVT"), TestCategory("Observable")]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(9, false)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(11, true)]
    public async Task ObservableGrain_AsyncEnumerable_Throws(int errorIndex, bool waitAfterYield)
    {
        const string ErrorMessage = "This is my error!";
        var grain = GrainFactory.GetGrain<IObservableGrain>(Guid.NewGuid());

        var values = new List<int>();
        try
        {
            await foreach (var entry in grain.GetValuesWithError(errorIndex, waitAfterYield, ErrorMessage).WithBatchSize(10))
            {
                values.Add(entry);
                Logger.LogInformation("ObservableGrain_AsyncEnumerable: {Entry}", entry);
            }
        }
        catch (InvalidOperationException iox)
        {
            Assert.Equal(ErrorMessage, iox.Message);
        }

        Assert.Equal(errorIndex, values.Count);

        // Check that the enumerator is disposed
        var grainCalls = await grain.GetIncomingCalls();
        Assert.Contains(grainCalls, c => c.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension)) && c.MethodName.Contains(nameof(IAsyncDisposable.DisposeAsync)));
    }

    [Fact, TestCategory("BVT"), TestCategory("Observable")]
    public async Task ObservableGrain_AsyncEnumerable_Batch()
    {
        var grain = GrainFactory.GetGrain<IObservableGrain>(Guid.NewGuid());

        foreach (var value in Enumerable.Range(0, 50))
        {
            await grain.OnNext(value.ToString());
        }

        await grain.Complete();

        var values = new List<string>();
        await foreach (var entry in grain.GetValues())
        {
            values.Add(entry);
            Logger.LogInformation("ObservableGrain_AsyncEnumerable: {Entry}", entry);
        }

        Assert.Equal(50, values.Count);

        var grainCalls = await grain.GetIncomingCalls();
        var moveNextCallCount = grainCalls.Count(element =>
            element.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension))
            && (element.MethodName.Contains(nameof(IAsyncEnumerableGrainExtension.MoveNext)) || element.MethodName.Contains(nameof(IAsyncEnumerableGrainExtension.StartEnumeration))));
        Assert.True(moveNextCallCount < values.Count);

        // Check that the enumerator is disposed
        Assert.Contains(grainCalls, c => c.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension)) && c.MethodName.Contains(nameof(IAsyncDisposable.DisposeAsync)));
    }

    [Fact, TestCategory("BVT"), TestCategory("Observable")]
    public async Task ObservableGrain_AsyncEnumerable_SplitBatch()
    {
        var grain = GrainFactory.GetGrain<IObservableGrain>(Guid.NewGuid());

        foreach (var value in Enumerable.Range(0, 50))
        {
            await grain.OnNext(value.ToString());
        }

        await grain.Complete();

        var values = new List<string>();
        await foreach (var entry in grain.GetValues().WithBatchSize(25))
        {
            values.Add(entry);
            Logger.LogInformation("ObservableGrain_AsyncEnumerable: {Entry}", entry);
        }

        Assert.Equal(50, values.Count);

        var grainCalls = await grain.GetIncomingCalls();
        var moveNextCallCount = grainCalls.Count(element =>
            element.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension))
            && (element.MethodName.Contains(nameof(IAsyncEnumerableGrainExtension.MoveNext)) || element.MethodName.Contains(nameof(IAsyncEnumerableGrainExtension.StartEnumeration))));
        Assert.True(moveNextCallCount < values.Count);

        // Check that the enumerator is disposed
        Assert.Contains(grainCalls, c => c.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension)) && c.MethodName.Contains(nameof(IAsyncDisposable.DisposeAsync)));
    }

    [Fact, TestCategory("BVT"), TestCategory("Observable")]
    public async Task ObservableGrain_AsyncEnumerable_NoBatching()
    {
        var grain = GrainFactory.GetGrain<IObservableGrain>(Guid.NewGuid());

        foreach (var value in Enumerable.Range(0, 50))
        {
            await grain.OnNext(value.ToString());
        }

        await grain.Complete();

        var values = new List<string>();
        await foreach (var entry in grain.GetValues().WithBatchSize(1))
        {
            values.Add(entry);
            Logger.LogInformation("ObservableGrain_AsyncEnumerable: {Entry}", entry);
        }

        Assert.Equal(50, values.Count);

        var grainCalls = await grain.GetIncomingCalls();
        var moveNextCallCount = grainCalls.Count(element =>
            element.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension))
            && (element.MethodName.Contains(nameof(IAsyncEnumerableGrainExtension.MoveNext)) || element.MethodName.Contains(nameof(IAsyncEnumerableGrainExtension.StartEnumeration))));

        // One call for every value and one final call to complete the enumeration
        Assert.Equal(values.Count + 1, moveNextCallCount);

        // Check that the enumerator is disposed
        Assert.Contains(grainCalls, c => c.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension)) && c.MethodName.Contains(nameof(IAsyncDisposable.DisposeAsync)));
    }

    [Fact, TestCategory("BVT"), TestCategory("Observable")]
    public async Task ObservableGrain_AsyncEnumerable_WithCancellation()
    {
        var grain = GrainFactory.GetGrain<IObservableGrain>(Guid.NewGuid());

        var producer = Task.Run(async () =>
        {
            foreach (var value in Enumerable.Range(0, 5))
            {
                await Task.Delay(200);
                await grain.OnNext(value.ToString());
            }

            await grain.Complete();
        });

        var values = new List<string>();
        using var cts = new CancellationTokenSource();
        await foreach (var entry in grain.GetValues().WithCancellation(cts.Token))
        {
            values.Add(entry);
            if (values.Count == 3)
            {
                cts.Cancel();
            }

            Logger.LogInformation("ObservableGrain_AsyncEnumerable: {Entry}", entry);
        }

        Assert.Equal(3, values.Count);

        // Check that the enumerator is disposed
        var grainCalls = await grain.GetIncomingCalls();
        Assert.Contains(grainCalls, c => c.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension)) && c.MethodName.Contains(nameof(IAsyncDisposable.DisposeAsync)));
    }

    [Fact, TestCategory("BVT"), TestCategory("Observable")]
    public async Task ObservableGrain_AsyncEnumerable_SlowProducer()
    {
        var grain = GrainFactory.GetGrain<IObservableGrain>(Guid.NewGuid());

        var producer = Task.Run(async () =>
        {
            foreach (var value in Enumerable.Range(0, 5))
            {
                await Task.Delay(2000);
                await grain.OnNext(value.ToString());
            }

            await grain.Complete();
        });

        var values = new List<string>();
        await foreach (var entry in grain.GetValues())
        {
            values.Add(entry);
            if (values.Count == 2)
            {
                break;
            }

            Logger.LogInformation("ObservableGrain_AsyncEnumerable: {Entry}", entry);
        }

        Assert.Equal(2, values.Count);

        // Check that the enumerator is disposed
        var grainCalls = await grain.GetIncomingCalls();
        Assert.Contains(grainCalls, c => c.InterfaceName.Contains(nameof(IAsyncEnumerableGrainExtension)) && c.MethodName.Contains(nameof(IAsyncDisposable.DisposeAsync)));
    }

    [Fact, TestCategory("BVT"), TestCategory("Observable")]
    public async Task ObservableGrain_AsyncEnumerable_Deactivate()
    {
        var grain = GrainFactory.GetGrain<IObservableGrain>(Guid.NewGuid());

        var producer = Task.Run(async () =>
        {
            foreach (var value in Enumerable.Range(0, 2))
            {
                await Task.Delay(200);
                await grain.OnNext(value.ToString());
            }

            await grain.Deactivate();
        });

        var values = new List<string>();
        await Assert.ThrowsAsync<EnumerationAbortedException>(async () =>
        {
            await foreach (var entry in grain.GetValues())
            {
                values.Add(entry);
                Logger.LogInformation("ObservableGrain_AsyncEnumerable: {Entry}", entry);
            }
        });

        Assert.Equal(2, values.Count);
    }
}
