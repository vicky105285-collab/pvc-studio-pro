using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class DocumentSessionManagerTests : IDisposable
    {
        private readonly string _testDirectory;

        public DocumentSessionManagerTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PVCStudioPro_SessionTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static SKBitmap CreateBitmap(int width, int height, SKColor color)
        {
            var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(color);
            return bmp;
        }

        private string CreateTestImageFile(string fileName, int width = 200, int height = 200)
        {
            string path = Path.Combine(_testDirectory, fileName);
            using var bmp = CreateBitmap(width, height, SKColors.DodgerBlue);
            using var image = SKImage.FromBitmap(bmp);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
            return path;
        }

        #region Test 1: Multi-Document Session Isolation

        [Fact]
        public async Task MultiDocument_Isolation_MaintainsIndependentRevisions()
        {
            using var manager = new DocumentSessionManager();

            string fileA = CreateTestImageFile("docA.png", 300, 200);
            string fileB = CreateTestImageFile("docB.png", 400, 300);

            var sessionA = await manager.OpenDocumentAsync(fileA);
            var sessionB = await manager.OpenDocumentAsync(fileB);

            manager.Sessions.Count.Should().Be(2);
            manager.ActiveSession.Should().BeSameAs(sessionB);

            // Edit Document A
            var cropA = new CropSettings { X = 10, Y = 10, Width = 85, Height = 54 };
            using (var leaseA = sessionA.SourceAsset.AcquireLease())
            {
                var derivedBmpA = CreateBitmap(1011, 638, SKColors.DarkRed);
                var derivedAssetA = DerivedImageAsset.Create(sessionA.SourceAsset.AssetId, 1, 300, "Crop Front A", derivedBmpA, leaseA);
                sessionA.CommitRevision("Crop Front A", frontAsset: derivedAssetA, frontCrop: cropA);
            }

            // Edit Document B differently
            var cropB = new CropSettings { X = 50, Y = 50, Width = 100, Height = 60 };
            using (var leaseB = sessionB.SourceAsset.AcquireLease())
            {
                var derivedBmpB = CreateBitmap(1011, 638, SKColors.DarkGreen);
                var derivedAssetB = DerivedImageAsset.Create(sessionB.SourceAsset.AssetId, 1, 300, "Crop Front B", derivedBmpB, leaseB);
                sessionB.CommitRevision("Crop Front B", frontAsset: derivedAssetB, frontCrop: cropB);
            }

            // Assert isolation
            sessionA.ActiveRevision.Should().Be(1);
            sessionA.FrontCropSettings.X.Should().Be(10);
            sessionA.FrontCropSettings.Width.Should().Be(85);
            sessionA.FrontCardAsset.Should().NotBeNull();
            sessionA.FrontCardAsset!.TransformDescription.Should().Be("Crop Front A");

            sessionB.ActiveRevision.Should().Be(1);
            sessionB.FrontCropSettings.X.Should().Be(50);
            sessionB.FrontCropSettings.Width.Should().Be(100);
            sessionB.FrontCardAsset.Should().NotBeNull();
            sessionB.FrontCardAsset!.TransformDescription.Should().Be("Crop Front B");

            // Undo on Session A must NOT affect Session B
            sessionA.Undo().Should().BeTrue();
            sessionA.ActiveRevision.Should().Be(0);
            sessionB.ActiveRevision.Should().Be(1);
            sessionB.FrontCropSettings.X.Should().Be(50);
        }

        #endregion

        #region Test 2: Closing Session Disposes Only Target Session Assets

        [Fact]
        public async Task ClosingSession_DisposesOnlyTargetSessionAssets()
        {
            using var manager = new DocumentSessionManager();

            string fileA = CreateTestImageFile("close_docA.png", 200, 200);
            string fileB = CreateTestImageFile("close_docB.png", 200, 200);

            var sessionA = await manager.OpenDocumentAsync(fileA);
            var sessionB = await manager.OpenDocumentAsync(fileB);

            // Close session A
            manager.CloseSession(sessionA.SessionId);

            // Session A must be disposed and removed
            manager.Sessions.Count.Should().Be(1);
            manager.ActiveSession.Should().BeSameAs(sessionB);
            sessionA.IsDisposed.Should().BeTrue();
            sessionA.SourceAsset.IsDisposed.Should().BeTrue();

            // Session B must remain completely intact and active
            sessionB.IsDisposed.Should().BeFalse();
            sessionB.SourceAsset.IsDisposed.Should().BeFalse();

            // Accessing Session B surface must succeed
            using var lease = sessionB.SourceAsset.AcquireLease();
            lease.Surface.Width.Should().Be(200);
        }

        #endregion

        #region Test 3: Undo/Redo Retention And Eviction Disposes Evicted Native Buffers

        [Fact]
        public void UndoRedo_RetentionAndEviction_DisposesEvictedNativeBuffers()
        {
            using var rawRoot = CreateBitmap(100, 100, SKColors.Blue);
            using var rootAsset = SourceImageAsset.Create("root.png", rawRoot, 300);
            using var lease = rootAsset.AcquireLease();

            using var undoManager = new UndoRedoManager();
            var createdAssets = new DerivedImageAsset[18];

            // Push 18 revisions (MaxUndoDepth = 15)
            for (int i = 0; i < 18; i++)
            {
                var bmp = CreateBitmap(50, 50, SKColors.Crimson);
                var asset = DerivedImageAsset.Create(rootAsset.AssetId, i + 1, 300, $"Rev {i + 1}", bmp, lease);
                createdAssets[i] = asset;

                undoManager.PushRevision(
                    revisionNumber: i + 1,
                    description: $"Revision {i + 1}",
                    frontCardAsset: asset,
                    backCardAsset: null,
                    frontCropSettings: new CropSettings { X = i },
                    backCropSettings: new CropSettings());
            }

            // Undo stack can hold at most 15 entries. The oldest entries (Revisions 0, 1, 2) must be evicted!
            undoManager.HistoryCount.Should().BeLessThanOrEqualTo(UndoRedoManager.MaxUndoDepth);

            // Revisions 0 and 1 must be evicted and disposed
            createdAssets[0].IsDisposed.Should().BeTrue();
            createdAssets[1].IsDisposed.Should().BeTrue();

            // The latest revisions must still be alive
            createdAssets[17].IsDisposed.Should().BeFalse();
            createdAssets[16].IsDisposed.Should().BeFalse();
        }

        #endregion

        #region Test 4: Memory Quota Eviction When Exceeding 250 MB

        [Fact]
        public void MemoryQuota_Enforcement_EvictsOldestRevisionsWhenExceeding250MB()
        {
            using var rawRoot = CreateBitmap(100, 100, SKColors.Blue);
            using var rootAsset = SourceImageAsset.Create("root.png", rawRoot, 300);
            using var lease = rootAsset.AcquireLease();

            using var undoManager = new UndoRedoManager();

            // Create large bitmaps (5000 x 5000 x 4 bytes = 100 MB each)
            // 3 revisions = 300 MB > 250 MB quota
            var largeAssets = new DerivedImageAsset[4];

            for (int i = 0; i < 4; i++)
            {
                var bmp = CreateBitmap(5000, 5000, SKColors.YellowGreen);
                var asset = DerivedImageAsset.Create(rootAsset.AssetId, i + 1, 300, $"Large Rev {i + 1}", bmp, lease);
                largeAssets[i] = asset;

                undoManager.PushRevision(
                    i + 1,
                    $"Large Revision {i + 1}",
                    asset,
                    null,
                    new CropSettings(),
                    new CropSettings());
            }

            // Memory quota enforcement must have evicted the oldest revisions to stay under 250 MB
            largeAssets[0].IsDisposed.Should().BeTrue();
            undoManager.TotalNativeMemoryBytes.Should().BeLessThanOrEqualTo(UndoRedoManager.MaxNativeMemoryQuotaBytes);
        }

        #endregion

        #region Test 5: Abandoned Branch On Divergent Edit Disposes Redo States

        [Fact]
        public void AbandonedBranch_OnDivergentEdit_DisposesRedoStates()
        {
            using var rawRoot = CreateBitmap(100, 100, SKColors.Blue);
            using var rootAsset = SourceImageAsset.Create("root.png", rawRoot, 300);
            using var lease = rootAsset.AcquireLease();

            using var undoManager = new UndoRedoManager();

            // Create Revision 1, 2, 3
            var bmp1 = CreateBitmap(50, 50, SKColors.Red);
            var asset1 = DerivedImageAsset.Create(rootAsset.AssetId, 1, 300, "Rev 1", bmp1, lease);
            undoManager.PushRevision(1, "Rev 1", asset1, null, new CropSettings(), new CropSettings());

            var bmp2 = CreateBitmap(50, 50, SKColors.Green);
            var asset2 = DerivedImageAsset.Create(rootAsset.AssetId, 2, 300, "Rev 2", bmp2, lease);
            undoManager.PushRevision(2, "Rev 2", asset2, null, new CropSettings(), new CropSettings());

            var bmp3 = CreateBitmap(50, 50, SKColors.Blue);
            var asset3 = DerivedImageAsset.Create(rootAsset.AssetId, 3, 300, "Rev 3", bmp3, lease);
            undoManager.PushRevision(3, "Rev 3", asset3, null, new CropSettings(), new CropSettings());

            // Undo twice back to Revision 1
            undoManager.Undo(); // back to 2
            undoManager.Undo(); // back to 1

            undoManager.CanRedo.Should().BeTrue();
            asset2.IsDisposed.Should().BeFalse();
            asset3.IsDisposed.Should().BeFalse();

            // Commit a new divergent Revision 4
            var bmp4 = CreateBitmap(50, 50, SKColors.Purple);
            var asset4 = DerivedImageAsset.Create(rootAsset.AssetId, 4, 300, "Rev 4", bmp4, lease);
            undoManager.PushRevision(4, "Rev 4", asset4, null, new CropSettings(), new CropSettings());

            // Redo stack is now abandoned and all its assets must be disposed!
            undoManager.CanRedo.Should().BeFalse();
            asset2.IsDisposed.Should().BeTrue();
            asset3.IsDisposed.Should().BeTrue();
            asset4.IsDisposed.Should().BeFalse();
        }

        #endregion

        #region Test 6: Cancelled Document Loading Cleans Up Resources Without Leaking

        [Fact]
        public async Task CancelledDocumentLoading_CleansUpNativeMemory_WithoutLeaking()
        {
            using var manager = new DocumentSessionManager();
            string testFile = CreateTestImageFile("cancel_doc.png", 200, 200);

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancelled

            var act = async () => await manager.OpenDocumentAsync(testFile, cancellationToken: cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();

            manager.Sessions.Should().BeEmpty();
            manager.ActiveSession.Should().BeNull();
        }

        #endregion

        #region Test 7: Subscription Token Ceases Events After Disposal

        [Fact]
        public async Task Subscription_Lifecycle_CeasesEvents_AfterDispose()
        {
            using var manager = new DocumentSessionManager();
            string testFile = CreateTestImageFile("sub_doc.png", 100, 100);

            var session = await manager.OpenDocumentAsync(testFile);

            int eventCount = 0;
            var subscription = session.Subscribe(change =>
            {
                eventCount++;
            });

            // Update crop settings -> event fired
            session.UpdateCropSettings(new CropSettings { X = 5 }, null);
            eventCount.Should().Be(1);

            // Dispose subscription token
            subscription.Dispose();

            // Subsequent update -> event must NOT be delivered
            session.UpdateCropSettings(new CropSettings { X = 10 }, null);
            eventCount.Should().Be(1);
        }

        #endregion

        #region Test 8: SwitchActiveSession Atomically Updates Active Session

        [Fact]
        public async Task SwitchActiveSession_AtomicallyUpdatesActiveSession()
        {
            using var manager = new DocumentSessionManager();

            string file1 = CreateTestImageFile("tab1.png", 100, 100);
            string file2 = CreateTestImageFile("tab2.png", 100, 100);
            string file3 = CreateTestImageFile("tab3.png", 100, 100);

            var s1 = await manager.OpenDocumentAsync(file1);
            var s2 = await manager.OpenDocumentAsync(file2);
            var s3 = await manager.OpenDocumentAsync(file3);

            manager.ActiveSession.Should().BeSameAs(s3);

            manager.SwitchActiveSession(s1.SessionId);
            manager.ActiveSession.Should().BeSameAs(s1);

            manager.SwitchActiveSession(s2.SessionId);
            manager.ActiveSession.Should().BeSameAs(s2);

            manager.SwitchActiveSession("non-existent-id");
            manager.ActiveSession.Should().BeSameAs(s2); // Unchanged
        }

        #endregion
    }
}
