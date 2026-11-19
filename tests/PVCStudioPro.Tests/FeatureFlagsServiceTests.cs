using System.Globalization;
using System.Windows;
using FluentAssertions;
using PVCStudioPro.App.Converters;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class FeatureFlagsServiceTests
    {
        private readonly IFeatureFlagsService _service;

        public FeatureFlagsServiceTests()
        {
            _service = new FeatureFlagsService();
        }

        [Theory]
        [InlineData("F-COLOR-BGRA")]
        [InlineData("F-AADHAAR-MASK")]
        [InlineData("F-TEMPLATES-29")]
        [InlineData("F-CROP-STRAIGHTEN")]
        [InlineData("F-TRAY-PRINT")]
        [InlineData("F-LEDGER-ACID")]
        public void FeatureFlags_ShouldEnable_WorkingAndVerifiedFeatures(string featureId)
        {
            _service.IsFeatureEnabled(featureId).Should().BeTrue($"Feature {featureId} is verified or working and must be enabled");
        }

        [Theory]
        [InlineData("F-THERMAL-RECEIPT")]
        [InlineData("F-BATCH-SPOOL")]
        [InlineData("P-AUTO-UPDATE")]
        [InlineData("P-CROP-MAGNIFIER")]
        [InlineData("NON_EXISTENT_FEATURE")]
        public void FeatureFlags_ShouldDisable_PartialUiOnlyAndPlannedFeatures(string featureId)
        {
            _service.IsFeatureEnabled(featureId).Should().BeFalse($"Feature {featureId} is not fully working/verified and must be disabled/hidden in the UI");
        }

        [Fact]
        public void FeatureVisibilityConverter_ShouldReturnExpectedVisibility()
        {
            var converter = new FeatureVisibilityConverter();
            FeatureVisibilityConverter.Initialize(_service);

            // Enabled feature -> Visible
            var visibleResult = converter.Convert(null!, typeof(Visibility), "F-COLOR-BGRA", CultureInfo.InvariantCulture);
            visibleResult.Should().Be(Visibility.Visible);

            // Unfinished / UI-only feature -> Collapsed
            var collapsedResult = converter.Convert(null!, typeof(Visibility), "F-THERMAL-RECEIPT", CultureInfo.InvariantCulture);
            collapsedResult.Should().Be(Visibility.Collapsed);

            var batchCollapsed = converter.Convert(null!, typeof(Visibility), "F-BATCH-SPOOL", CultureInfo.InvariantCulture);
            batchCollapsed.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void FeatureFlags_LoadsFromEmbeddedResource_WhenDocsFolderUnavailable()
        {
            // Point to a non-existent path so custom filesystem path fails
            string nonExistentPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NonExistentDir_" + System.Guid.NewGuid().ToString("N"), "FEATURES.json");
            var service = new FeatureFlagsService(nonExistentPath);

            // Should still successfully load statuses from embedded assembly resource
            service.IsFeatureEnabled("F-COLOR-BGRA").Should().BeTrue();
            service.IsFeatureEnabled("F-LEDGER-ACID").Should().BeTrue();
            service.GetFeatureStatus("F-COLOR-BGRA").Should().Be("Verified");
        }
    }
}
