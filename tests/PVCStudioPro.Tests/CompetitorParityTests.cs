using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.Tests
{
    public class CompetitorParityTests
    {
        private readonly CardDetectionService _cardDetection;
        private readonly ManualCropService _cropService;
        private readonly TemplateService _templateService;

        public CompetitorParityTests()
        {
            _cardDetection = new CardDetectionService();
            _cropService = new ManualCropService();
            _templateService = new TemplateService();
        }

        #region 1. Calibrated Coordinate Assertions (Aadhaar, PAN, Ayushman, e-Shram)

        [Fact]
        public async Task CardDetection_CalibratedAadhaarCoordinates_MatchesGroundTruth()
        {
            // Standard A4 at 300 DPI is 2480 x 3508 px
            using var a4Bitmap = new SKBitmap(2480, 3508, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(a4Bitmap))
            {
                canvas.Clear(SKColors.White);
            }

            var extract = await _cardDetection.DetectAndExtractDualCardsAsync(
                a4Bitmap,
                DocumentKind.Aadhaar,
                AadhaarGeneration.Modern2022Plus);

            extract.Front.Should().NotBeNull();
            extract.Back.Should().NotBeNull();
            extract.FrontRect.Should().NotBeNull();
            extract.BackRect.Should().NotBeNull();

            // Ground truth from Real.db at 300 DPI:
            // Front: X = 210 px (17.78 mm), Y = 2595 px (219.71 mm), W = 1045 px, H = 665 px
            // Back:  X = 1300 px (110.06 mm), Y = 2595 px (219.71 mm), W = 1045 px, H = 665 px
            var fRect = extract.FrontRect!.Value;
            var bRect = extract.BackRect!.Value;

            fRect.Left.Should().BeInRange(200, 220); // ~210 px
            fRect.Top.Should().BeInRange(2580, 2610); // ~2595 px
            fRect.Width.Should().BeInRange(1035, 1055); // ~1045 px
            fRect.Height.Should().BeInRange(655, 675); // ~665 px

            bRect.Left.Should().BeInRange(1290, 1310); // ~1300 px
            bRect.Top.Should().BeInRange(2580, 2610); // ~2595 px

            // Output cards normalized to 600 DPI CR80: 2022 x 1275 px
            extract.Front!.Width.Should().Be(2022);
            extract.Front!.Height.Should().Be(1275);
            extract.Back!.Width.Should().Be(2022);
            extract.Back!.Height.Should().Be(1275);
        }

        [Fact]
        public async Task CardDetection_CalibratedPanCoordinates_MatchesGroundTruth()
        {
            using var a4Bitmap = new SKBitmap(2480, 3508, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(a4Bitmap)) { canvas.Clear(SKColors.White); }

            var extract = await _cardDetection.DetectAndExtractDualCardsAsync(
                a4Bitmap,
                DocumentKind.Pan);

            extract.FrontRect.Should().NotBeNull();
            extract.BackRect.Should().NotBeNull();

            // Ground truth from Real.db PAN-NSDL at 300 DPI:
            // Front: X = 290 px (24.55 mm), Y = 2714 px (229.79 mm)
            var fRect = extract.FrontRect!.Value;
            fRect.Left.Should().BeInRange(280, 300);
            fRect.Top.Should().BeInRange(2700, 2730);
        }

        [Fact]
        public async Task CardDetection_CalibratedAyushmanCoordinates_MatchesGroundTruth()
        {
            using var a4Bitmap = new SKBitmap(2480, 3508, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(a4Bitmap)) { canvas.Clear(SKColors.White); }

            var extract = await _cardDetection.DetectAndExtractDualCardsAsync(
                a4Bitmap,
                DocumentKind.AyushmanBharat);

            extract.FrontRect.Should().NotBeNull();
            extract.BackRect.Should().NotBeNull();

            // Ground truth from Real.db AYUSHMAN-NEW at 300 DPI:
            // Front: X = 120 px (10.16 mm), Y = 270 px (22.86 mm)
            var fRect = extract.FrontRect!.Value;
            fRect.Left.Should().BeInRange(110, 130);
            fRect.Top.Should().BeInRange(260, 280);
        }

        [Fact]
        public void TemplateService_CalibratedBuiltInTemplates_MatchPhysicalGroundTruth()
        {
            var aadhaar = _templateService.GetTemplateById("aadhaar_v2024");
            aadhaar.Should().NotBeNull();
            aadhaar!.Faces.Front.CropRectMm.X.Should().BeApproximately(17.78, 0.05);
            aadhaar.Faces.Front.CropRectMm.Y.Should().BeApproximately(219.71, 0.05);
            aadhaar.Faces.Back!.CropRectMm.X.Should().BeApproximately(110.06, 0.05);
            aadhaar.Faces.Back!.CropRectMm.Y.Should().BeApproximately(219.71, 0.05);

            var pan = _templateService.GetTemplateById("pan_nsdl");
            pan.Should().NotBeNull();
            pan!.Faces.Front.CropRectMm.X.Should().BeApproximately(24.55, 0.05);
            pan.Faces.Front.CropRectMm.Y.Should().BeApproximately(229.79, 0.05);

            var ayushman = _templateService.GetTemplateById("ayushman_pmjay");
            ayushman.Should().NotBeNull();
            ayushman!.Faces.Front.CropRectMm.X.Should().BeApproximately(10.16, 0.05);
            ayushman.Faces.Front.CropRectMm.Y.Should().BeApproximately(22.86, 0.05);
        }

        #endregion

        #region 2. Image Enhancement & Adjustments Pipeline

        [Fact]
        public void ManualCropService_ApplyAdjustments_ModifiesBrightnessAndContrast()
        {
            using var src = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(src))
            {
                canvas.Clear(new SKColor(128, 128, 128)); // Mid-gray
            }

            // Increase brightness
            using var bright = _cropService.ApplyAdjustments(src, brightness: 50f, contrast: 1.0f, saturation: 1.0f, sharpness: 0f);
            bright.Width.Should().Be(100);
            bright.Height.Should().Be(100);

            var brightPixel = bright.GetPixel(50, 50);
            brightPixel.Red.Should().BeGreaterThan(128);
            brightPixel.Green.Should().BeGreaterThan(128);
            brightPixel.Blue.Should().BeGreaterThan(128);
        }

        [Fact]
        public void ManualCropService_ApplyAdjustments_ConvertsToGrayscaleWhenSaturationZero()
        {
            using var src = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(src))
            {
                canvas.Clear(SKColors.Crimson); // Bright color
            }

            using var desaturated = _cropService.ApplyAdjustments(src, brightness: 0f, contrast: 1.0f, saturation: 0.0f, sharpness: 0f);
            var pixel = desaturated.GetPixel(50, 50);

            // In pure grayscale, R, G, and B must be within 2 intensity levels
            Math.Abs(pixel.Red - pixel.Green).Should().BeLessThanOrEqualTo(2);
            Math.Abs(pixel.Green - pixel.Blue).Should().BeLessThanOrEqualTo(2);
        }

        #endregion

        #region 3. Hardware Tray Profile Parity (12 Factory Profiles)

        [Fact]
        public async Task PrinterTrayService_ShouldLoadAll12FactoryProfiles()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PVCStudioPro_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string profilePath = Path.Combine(tempDir, "profiles.json");

            try
            {
                var fakeDiscovery = new PrinterDiscoveryService();
                var trayService = new PrinterTrayService(fakeDiscovery, profilePath);

                var profiles = await trayService.GetAllProfilesAsync();
                profiles.Count.Should().BeGreaterThanOrEqualTo(12);

                var ids = profiles.Select(p => p.ProfileId).ToList();
                ids.Should().Contain("epson_l805_aftermarket_tray");
                ids.Should().Contain("epson_l8050_aftermarket_tray");
                ids.Should().Contain("epson_pvc_factory_tray");
                ids.Should().Contain("canon_pvc_tray");
                ids.Should().Contain("brother_pvc_tray");
                ids.Should().Contain("direct_cr80_card_printer");
                ids.Should().Contain("a4_single_side_5_card");
                ids.Should().Contain("a4_dragon_sheet_mirror");
                ids.Should().Contain("a4_double_side_half_a4");
                ids.Should().Contain("photo_4x6_card_sheet");
                ids.Should().Contain("postal_aadhaar_card_sheet");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task AllCompetitorTemplates_LoadedAndVerified_FromRealDbCatalogue()
        {
            var templateService = new TemplateService();
            await templateService.InitializeTemplatesAsync();
            var allTemplates = templateService.GetAllTemplates();

            allTemplates.Count.Should().BeGreaterThanOrEqualTo(25);

            string[] expectedIds = new[]
            {
                "aadhaar_v2024",
                "child_aadhaar",
                "m_aadhaar",
                "pan_nsdl",
                "pan_utiitsl",
                "pan_instant",
                "voter_epic_v2",
                "ayushman_pmjay",
                "ayushman_new",
                "cm_insurance",
                "umid_railways",
                "pm_sym",
                "pm_kmy",
                "pf_insurance",
                "uan_card",
                "e_pran",
                "udid_card",
                "pravasi_card",
                "senior_citizen",
                "jan_aadhaar",
                "vaccination_card",
                "kerala_ration",
                "wb_ration",
                "tn_smart_ration",
                "parivahan_dl",
                "parivahan_rc"
            };

            foreach (var expectedId in expectedIds)
            {
                var tmpl = allTemplates.FirstOrDefault(t => t.TemplateId == expectedId);
                tmpl.Should().NotBeNull($"Template {expectedId} from competitor Real.db should be registered");
                tmpl!.Faces.Front.CropRectMm.Width.Should().BeGreaterThan(0);
                tmpl.Faces.Front.CropRectMm.Height.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void PhysicalPageLayout_SwitchingModes_CalculatesAccuratePhysicalGeometry()
        {
            var renderService = new PhysicalPageRenderService();

            // 1. Single Side A4 (210 x 297 mm)
            var layoutA4 = PageLayoutModel.CreateDefaultA4();
            layoutA4.Items.Add(new PageLayoutItem
            {
                Label = "Front",
                Kind = LayoutItemKind.FrontCard,
                XPositionMm = 15.0,
                YPositionMm = 20.0,
                WidthMm = 85.60,
                HeightMm = 53.98
            });

            var sheetBitmapA4 = renderService.RenderLayoutToBitmap(layoutA4, 150);
            sheetBitmapA4.Should().NotBeNull();
            sheetBitmapA4.Width.Should().BeInRange(1230, 1250); // 210mm @ 150 DPI ~ 1240 px
            sheetBitmapA4.Height.Should().BeInRange(1740, 1765); // 297mm @ 150 DPI ~ 1754 px
            sheetBitmapA4.Dispose();

            // 2. Epson 2-Card Tray (120 x 180 mm)
            var layoutEpson = PageLayoutModel.CreateEpsonTray();
            var sheetBitmapEpson = renderService.RenderLayoutToBitmap(layoutEpson, 150);
            sheetBitmapEpson.Should().NotBeNull();
            sheetBitmapEpson.Width.Should().BeInRange(700, 720); // 120mm @ 150 DPI ~ 709 px
            sheetBitmapEpson.Height.Should().BeInRange(1050, 1075); // 180mm @ 150 DPI ~ 1063 px
            sheetBitmapEpson.Dispose();
        }

        #endregion
    }
}
