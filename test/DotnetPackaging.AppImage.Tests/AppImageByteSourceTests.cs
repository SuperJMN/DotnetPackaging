using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DotnetPackaging;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageByteSourceTests
{
    [Fact]
    public void Single_use_byte_source_is_consumed_only_once()
    {
        var source = SingleUseByteSource.FromBytes(Enumerable.Range(0, 1024).Select(x => (byte)(x % 256)).ToArray());

        var first = source.Array();
        var second = source.Array();

        first.Length.Should().Be(1024);
        second.Length.Should().Be(0, "single-use sources emit data only once");
    }

    [Fact]
    public async Task ToByteSource_concatenates_without_resubscribing_runtime()
    {
        var runtimeBytes = Enumerable.Repeat((byte)0xAA, 256).ToArray();
        var runtime = new SingleUseRuntime(runtimeBytes);
        var container = BuildMinimalContainer();

        var appImage = new AppImageContainer(runtime, container);

        var byteSourceResult = await appImage.ToByteSource();
        byteSourceResult.Should().Succeed();

        var combined = byteSourceResult.Value.Array();

        runtime.SubscriptionCount.Should().Be(1);
        combined.Take(runtimeBytes.Length).Should().Equal(runtimeBytes);
        combined.Length.Should().BeGreaterThan(runtimeBytes.Length);
    }

    private static UnixDirectory BuildMinimalContainer()
    {
        var file = new Resource("dummy", ByteSource.FromBytes(new byte[] { 0x42 }));
        var unixFile = new UnixFile(file, new UnixPermissions(Permission.OwnerAll), ownerId: 0);
        return new UnixDirectory(
            name: string.Empty,
            ownerId: 0,
            permissions: new UnixPermissions(Permission.OwnerAll),
            subdirs: Array.Empty<UnixDirectory>(),
            files: new[] { unixFile });
    }

    private sealed class SingleUseRuntime : IRuntime
    {
        private readonly SingleUseByteSource inner;

        public SingleUseRuntime(byte[] bytes)
        {
            inner = SingleUseByteSource.FromBytes(bytes);
        }

        public Architecture Architecture => Architecture.X64;
        public int SubscriptionCount => inner.SubscriptionCount;
        public IObservable<byte[]> Bytes => inner.Bytes;
        public IDisposable Subscribe(IObserver<byte[]> observer) => inner.Subscribe(observer);
    }

    private sealed class SingleUseByteSource : IByteSource
    {
        private readonly byte[][] chunks;
        private bool consumed;
        private readonly object gate = new();

        private SingleUseByteSource(byte[][] chunks)
        {
            this.chunks = chunks;
        }

        public static SingleUseByteSource FromBytes(params byte[][] chunks)
        {
            return new SingleUseByteSource(chunks);
        }

        public int SubscriptionCount { get; private set; }

        public IObservable<byte[]> Bytes => Observable.Create<byte[]>(observer =>
        {
            bool shouldEmit;
            lock (gate)
            {
                SubscriptionCount++;
                shouldEmit = !consumed;
                if (shouldEmit)
                {
                    consumed = true;
                }
            }

            if (shouldEmit)
            {
                foreach (var chunk in chunks)
                {
                    observer.OnNext(chunk);
                }
            }

            observer.OnCompleted();
            return Disposable.Empty;
        });

        public IDisposable Subscribe(IObserver<byte[]> observer)
        {
            return Bytes.Subscribe(observer);
        }
    }
}
