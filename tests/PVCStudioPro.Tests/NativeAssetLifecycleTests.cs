using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Tests
{
    public class NativeAssetLifecycleTests
    {
        private static SKBitmap CreateTestBitmap(int width = 100, int height = 100, SKColor? color = null)
        {
            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(color ?? SKColors.CornflowerBlue);
            return bitmap;
        }

        #region Test 1: Double Disposal Of Asset Owner Is Idempotent

        [Fact]
        public void DoubleDisposal_OfAssetOwner_IsIdempotent()
        {
            var rawBitmap = CreateTestBitmap(80, 80);
            var safeHandle = new SafeNativeBitmap(rawBitmap);

            safeHandle.IsOwnerDisposed.Should().BeFalse();
            safeHandle.IsNativeDisposed.Should().BeFalse();
            safeHandle.ReferenceCount.Should().Be(1);

            // First disposal releases owner reference
            safeHandle.Dispose();
            safeHandle.IsOwnerDisposed.Should().BeTrue();
            safeHandle.ReferenceCount.Should().Be(0);
            safeHandle.IsNativeDisposed.Should().BeTrue();

            // Second disposal must be completely harmless and idempotent
            var act = () => safeHandle.Dispose();
            act.Should().NotThrow();
            safeHandle.IsOwnerDisposed.Should().BeTrue();
            safeHandle.ReferenceCount.Should().Be(0);
            safeHandle.IsNativeDisposed.Should().BeTrue();
        }

        #endregion

        #region Test 2: Double Disposal Of Lease Is Idempotent

        [Fact]
        public void DoubleDisposal_OfLease_IsIdempotent()
        {
            var rawBitmap = CreateTestBitmap(80, 80);
            using var safeHandle = new SafeNativeBitmap(rawBitmap);

            safeHandle.ReferenceCount.Should().Be(1);

            // Acquire lease
            var lease = safeHandle.AcquireLease("asset-test", 0, 300);
            safeHandle.ReferenceCount.Should().Be(2);
            lease.IsDisposed.Should().BeFalse();

            // First lease disposal
            lease.Dispose();
            lease.IsDisposed.Should().BeTrue();
            safeHandle.ReferenceCount.Should().Be(1);

            // Second lease disposal must be idempotent and not decrement ref count again
            var act = () => lease.Dispose();
            act.Should().NotThrow();
            lease.IsDisposed.Should().BeTrue();
            safeHandle.ReferenceCount.Should().Be(1);
        }

        #endregion

        #region Test 3: Acquire Lease After Owner Disposal Throws ObjectDisposedException

        [Fact]
        public void AcquireLease_AfterOwnerDisposal_ThrowsObjectDisposedException()
        {
            var rawBitmap = CreateTestBitmap(50, 50);
            var safeHandle = new SafeNativeBitmap(rawBitmap);
            safeHandle.Dispose();

            safeHandle.IsOwnerDisposed.Should().BeTrue();

            var act = () => safeHandle.AcquireLease("asset-expired", 0, 300);
            act.Should().Throw<ObjectDisposedException>()
                .WithMessage("*disposed*");
        }

        #endregion

        #region Test 4: Concurrent Acquisition And Owner Disposal Is ThreadSafe

        [Fact]
        public async Task ConcurrentAcquisition_AndOwnerDisposal_IsThreadSafe()
        {
            const int workerCount = 20;
            const int iterationsPerWorker = 50;

            for (int run = 0; run < 5; run++)
            {
                var rawBitmap = CreateTestBitmap(100, 100);
                var safeHandle = new SafeNativeBitmap(rawBitmap);

                var successLeases = new ConcurrentBag<IAssetLease>();
                var disposedExceptions = new ConcurrentBag<ObjectDisposedException>();
                using var startBarrier = new ManualResetEventSlim(false);

                var tasks = new Task[workerCount];
                for (int i = 0; i < workerCount; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        startBarrier.Wait();
                        for (int j = 0; j < iterationsPerWorker; j++)
                        {
                            try
                            {
                                var lease = safeHandle.AcquireLease($"worker-asset", j, 300);
                                successLeases.Add(lease);
                                // Quickly perform a read under lock
                                lease.Surface.ReadPixels((span, row) =>
                                {
                                    _ = span.Length;
                                });
                            }
                            catch (ObjectDisposedException ex)
                            {
                                disposedExceptions.Add(ex);
                                break;
                            }
                        }
                    });
                }

                // Start workers and concurrently dispose the owner after slight jitter
                startBarrier.Set();
                await Task.Delay(2);
                safeHandle.Dispose();

                await Task.WhenAll(tasks);

                // Dispose all leases that were acquired
                foreach (var lease in successLeases)
                {
                    lease.Dispose();
                }

                // After all leases are disposed, ref count must be 0 and native bitmap disposed
                safeHandle.ReferenceCount.Should().Be(0);
                safeHandle.IsNativeDisposed.Should().BeTrue();
            }
        }

        #endregion

        #region Test 5: Owner Disposal While Lease Held Keeps Resource Alive

        [Fact]
        public void OwnerDisposal_WhileLeaseHeld_KeepsResourceAlive()
        {
            var rawBitmap = CreateTestBitmap(120, 90, SKColors.Crimson);
            var safeHandle = new SafeNativeBitmap(rawBitmap);

            // Acquire lease before owner disposal
            var lease = safeHandle.AcquireLease("retained-asset", 0, 300);
            safeHandle.ReferenceCount.Should().Be(2);

            // Dispose owner
            safeHandle.Dispose();
            safeHandle.IsOwnerDisposed.Should().BeTrue();
            safeHandle.ReferenceCount.Should().Be(1);
            safeHandle.IsNativeDisposed.Should().BeFalse(); // Still alive because lease is held!

            // Verify surface operations still succeed completely
            lease.Surface.Width.Should().Be(120);
            lease.Surface.Height.Should().Be(90);

            // Render to offscreen canvas
            using var destBitmap = new SKBitmap(120, 90);
            using var canvas = new SKCanvas(destBitmap);
            var renderAct = () => lease.Surface.RenderTo(canvas, new SKRect(0, 0, 120, 90));
            renderAct.Should().NotThrow();

            // Create immutable snapshot
            using var snapshot = lease.Surface.CreateImmutableSnapshot();
            snapshot.Should().NotBeNull();
            snapshot.Width.Should().Be(120);

            // Inspect pixel bytes
            int byteCount = 0;
            lease.Surface.ReadPixels((span, rowBytes) =>
            {
                byteCount = span.Length;
            });
            byteCount.Should().BeGreaterThan(0);

            // Now release the lease
            lease.Dispose();
            safeHandle.ReferenceCount.Should().Be(0);
            safeHandle.IsNativeDisposed.Should().BeTrue(); // Now native resource is destroyed
        }

        #endregion

        #region Test 6: Exception And Cancellation In Rendering Pipeline Releases Lease Cleanly

        [Fact]
        public async Task ExceptionAndCancellation_InRenderingPipeline_ReleasesLeaseCleanly()
        {
            var rawBitmap = CreateTestBitmap(100, 100);
            var asset = SourceImageAsset.Create("test_doc.pdf", rawBitmap, 300);

            asset.ReferenceCount.Should().Be(1);

            // Scenario A: Exception in rendering block
            var exceptionAct = async () =>
            {
                using var lease = asset.AcquireLease();
                await Task.Yield();
                throw new InvalidOperationException("Simulated rendering failure.");
            };

            await exceptionAct.Should().ThrowAsync<InvalidOperationException>();
            asset.ReferenceCount.Should().Be(1); // Lease was disposed in using block

            // Scenario B: Task cancellation in pipeline
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var cancelAct = async () =>
            {
                using var lease = asset.AcquireLease();
                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(100, cts.Token);
            };

            await cancelAct.Should().ThrowAsync<OperationCanceledException>();
            asset.ReferenceCount.Should().Be(1); // Lease was cleanly disposed

            asset.Dispose();
            asset.ReferenceCount.Should().Be(0);
            asset.IsDisposed.Should().BeTrue();
        }

        #endregion

        #region Test 7: Use After Lease Disposal Throws ObjectDisposedException

        [Fact]
        public void UseAfterLeaseDisposal_ThrowsObjectDisposedException()
        {
            var rawBitmap = CreateTestBitmap(100, 60);
            using var safeHandle = new SafeNativeBitmap(rawBitmap);
            var lease = safeHandle.AcquireLease("asset-dispose-test", 0, 300);
            var surface = lease.Surface;

            // Verify operations work before disposal
            surface.Width.Should().Be(100);
            surface.Height.Should().Be(60);

            // Dispose the lease
            lease.Dispose();
            lease.IsDisposed.Should().BeTrue();

            // Attempting any surface operation after lease disposal must throw ObjectDisposedException
            Action widthAccess = () => _ = surface.Width;
            widthAccess.Should().Throw<ObjectDisposedException>();

            Action heightAccess = () => _ = surface.Height;
            heightAccess.Should().Throw<ObjectDisposedException>();

            using var targetBitmap = new SKBitmap(100, 60);
            using var canvas = new SKCanvas(targetBitmap);
            Action renderCall = () => surface.RenderTo(canvas, new SKRect(0, 0, 100, 60));
            renderCall.Should().Throw<ObjectDisposedException>();

            Action snapshotCall = () => _ = surface.CreateImmutableSnapshot();
            snapshotCall.Should().Throw<ObjectDisposedException>();

            Action readPixelsCall = () => surface.ReadPixels((span, rowBytes) => { });
            readPixelsCall.Should().Throw<ObjectDisposedException>();

            Action readPixelsFuncCall = () => surface.ReadPixels((span, rowBytes) => span.Length);
            readPixelsFuncCall.Should().Throw<ObjectDisposedException>();
        }

        #endregion

        #region Test 8: Concurrent Rendering And Lease Disposal Is ThreadSafe

        [Fact]
        public async Task ConcurrentRendering_AndLeaseDisposal_IsThreadSafe()
        {
            for (int iteration = 0; iteration < 5; iteration++)
            {
                var rawBitmap = CreateTestBitmap(200, 200, SKColors.DarkGreen);
                using var safeHandle = new SafeNativeBitmap(rawBitmap);
                var lease = safeHandle.AcquireLease("concurrent-render", 0, 300);
                var surface = lease.Surface;

                using var renderStarted = new ManualResetEventSlim(false);
                using var destBitmap = new SKBitmap(200, 200);
                using var canvas = new SKCanvas(destBitmap);

                var renderTask = Task.Run(() =>
                {
                    renderStarted.Set();
                    int completedRenders = 0;
                    for (int i = 0; i < 200; i++)
                    {
                        try
                        {
                            surface.RenderTo(canvas, new SKRect(0, 0, 200, 200));
                            completedRenders++;
                        }
                        catch (ObjectDisposedException)
                        {
                            // Expected once lease is disposed
                            break;
                        }
                    }
                    return completedRenders;
                });

                renderStarted.Wait();
                await Task.Delay(1); // Let some renders execute
                lease.Dispose(); // Concurrently dispose the lease

                int rendered = await renderTask;
                rendered.Should().BeGreaterThanOrEqualTo(0);
                lease.IsDisposed.Should().BeTrue();
            }
        }

        #endregion

        #region Test 9: No Use After Free During Stress Testing

        [Fact]
        public void NoUseAfterFree_DuringStressTesting()
        {
            const int threadCount = 4;
            const int iterationsPerThread = 250;

            var rawBitmap = CreateTestBitmap(150, 100, SKColors.Navy);
            var sourceAsset = SourceImageAsset.Create("stress_source.png", rawBitmap, 300);

            var errors = new ConcurrentBag<Exception>();

            Parallel.For(0, threadCount, threadIdx =>
            {
                using var targetBitmap = new SKBitmap(150, 100);
                using var canvas = new SKCanvas(targetBitmap);

                for (int i = 0; i < iterationsPerThread; i++)
                {
                    try
                    {
                        using var lease = sourceAsset.AcquireLease();
                        
                        // 1. Render operation
                        lease.Surface.RenderTo(canvas, new SKRect(0, 0, 150, 100));

                        // 2. Scoped pixel inspection
                        int totalBytes = lease.Surface.ReadPixels((span, rowBytes) =>
                        {
                            int sum = 0;
                            // Read first 16 bytes safely
                            int sample = Math.Min(span.Length, 16);
                            for (int b = 0; b < sample; b++)
                            {
                                sum += span[b];
                            }
                            return sum;
                        });
                        totalBytes.Should().BeGreaterThanOrEqualTo(0);

                        // 3. Snapshot creation every 50 iterations
                        if (i % 50 == 0)
                        {
                            using var snapshot = lease.Surface.CreateImmutableSnapshot();
                            snapshot.Width.Should().Be(150);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                }
            });

            errors.Should().BeEmpty();
            sourceAsset.ReferenceCount.Should().Be(1);

            // Verify derived asset creation and disposal lineage
            using (var lease = sourceAsset.AcquireLease())
            {
                var derivedBitmap = CreateTestBitmap(1011, 638, SKColors.Gold);
                var derivedAsset = DerivedImageAsset.Create(
                    sourceAsset.AssetId,
                    revision: 1,
                    dpi: 300,
                    transformDescription: "Crop CR80",
                    derivedBitmap,
                    parentLease: lease);

                derivedAsset.IsCr80Standard.Should().BeTrue();
                derivedAsset.Width.Should().Be(1011);
                derivedAsset.Height.Should().Be(638);
                derivedAsset.PhysicalWidthMm.Should().BeApproximately(85.60, 0.1);
                derivedAsset.PhysicalHeightMm.Should().BeApproximately(53.98, 0.1);

                derivedAsset.Dispose();
                derivedAsset.IsDisposed.Should().BeTrue();
            }

            sourceAsset.Dispose();
            sourceAsset.IsDisposed.Should().BeTrue();
            sourceAsset.ReferenceCount.Should().Be(0);
        }

        #endregion

        #region Re-entrancy Protection Test

        [Fact]
        public void ReentrantDisposal_DuringPixelRead_DefersDestructionUntilCallbackCompletes()
        {
            var rawBitmap = CreateTestBitmap(50, 50, SKColors.MediumPurple);
            var safeHandle = new SafeNativeBitmap(rawBitmap);
            var lease = safeHandle.AcquireLease("reentrant-test", 0, 300);

            // Dispose owner first so handle only has 1 reference (the lease)
            safeHandle.Dispose();
            safeHandle.ReferenceCount.Should().Be(1);
            safeHandle.IsNativeDisposed.Should().BeFalse();

            bool callbackFinished = false;
            int bytesInspected = 0;

            // Inside ReadPixels, we invoke lease.Dispose()!
            // Re-entrancy protection must ensure _nativeBitmap is NOT destroyed while the callback is still running.
            lease.Surface.ReadPixels((span, rowBytes) =>
            {
                // Re-entrant lease disposal
                lease.Dispose();
                lease.IsDisposed.Should().BeTrue();

                // Native memory must still be valid for reading!
                bytesInspected = span.Length;
                span[0].Should().Be(span[0]); // Safe access without access violation
                callbackFinished = true;
            });

            callbackFinished.Should().BeTrue();
            bytesInspected.Should().BeGreaterThan(0);

            // Now that the callback has completed, destruction has taken place
            safeHandle.ReferenceCount.Should().Be(0);
            safeHandle.IsNativeDisposed.Should().BeTrue();
        }

        #endregion
    }
}
