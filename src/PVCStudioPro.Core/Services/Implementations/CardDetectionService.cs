using System;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class CardDetectionService : ICardDetectionService, ICardDetectionEngine
    {
        private const double MmPerInch = 25.4;
        private readonly ITemplateService _templateService;
        private readonly IDocumentTypeDetectionService _detectionService;

        public CardDetectionService(
            ITemplateService? templateService = null,
            IDocumentTypeDetectionService? detectionService = null)
        {
            _templateService = templateService ?? new TemplateService();
            _detectionService = detectionService ?? new DocumentTypeDetectionService(_templateService);
        }

        public async Task<CardExtractionResult> DetectAndExtractAdaptiveAsync(
            SKBitmap pageBitmap,
            string extractedText = "",
            double pageWidthMm = 210.0,
            double pageHeightMm = 297.0,
            string? preferredTemplateId = null,
            DocumentKind preferredKind = DocumentKind.Unknown,
            AadhaarGeneration preferredAadhaarGen = AadhaarGeneration.Unknown,
            bool applyMask = false)
        {
            return await Task.Run(async () =>
            {
                var result = new CardExtractionResult();

                if (pageBitmap == null || pageBitmap.Width <= 0 || pageBitmap.Height <= 0)
                {
                    result.DetectionNotes = "Invalid or empty source bitmap.";
                    result.Confidence = new DetectionConfidenceScore(0.0, 0.0, 0.0, 0.0, 0.0);
                    return result;
                }

                // =========================================================================
                // STAGE 1: Geometry & Point Inspection
                // =========================================================================
                double effectiveHeightMm = pageHeightMm > 10.0 ? pageHeightMm : 297.0;
                double effectiveWidthMm = pageWidthMm > 10.0 ? pageWidthMm : 210.0;
                double deducedDpi = (pageBitmap.Height * MmPerInch) / effectiveHeightMm;
                if (deducedDpi < 72.0 || deducedDpi > 1200.0)
                {
                    deducedDpi = 300.0;
                }

                double expectedA4Aspect = 297.0 / 210.0; // 1.4142
                double pageAspect = (pageHeightMm > 0 && pageWidthMm > 0)
                    ? (effectiveHeightMm / effectiveWidthMm)
                    : ((double)pageBitmap.Height / Math.Max(1, pageBitmap.Width));

                double geometryScore;
                if (Math.Abs(pageAspect - expectedA4Aspect) <= 0.08)
                {
                    geometryScore = 1.0;
                }
                else if (Math.Abs(((double)pageBitmap.Width / pageBitmap.Height) - 1.5857) <= 0.10)
                {
                    // Pre-cropped direct CR80 single card scan
                    geometryScore = 0.95;
                }
                else
                {
                    geometryScore = Math.Max(0.40, 1.0 - (Math.Abs(pageAspect - expectedA4Aspect) * 0.60));
                }

                // =========================================================================
                // STAGE 2: OCR Keyword Anchor Matching
                // =========================================================================
                DocumentKind detectedKind = preferredKind != DocumentKind.Unknown ? preferredKind : DocumentKind.Aadhaar;
                AadhaarGeneration aadhaarGen = preferredAadhaarGen != AadhaarGeneration.Unknown ? preferredAadhaarGen : AadhaarGeneration.Unknown;
                CardTemplate? matchedTemplate = null;
                double keywordScore = 0.40;

                if (!string.IsNullOrWhiteSpace(preferredTemplateId))
                {
                    matchedTemplate = _templateService.GetTemplateById(preferredTemplateId);
                }

                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    var docDetection = await _detectionService.DetectDocumentTypeAsync(pageBitmap, extractedText, effectiveWidthMm, effectiveHeightMm);
                    if (preferredKind == DocumentKind.Unknown)
                    {
                        detectedKind = docDetection.DetectedKind;
                        aadhaarGen = docDetection.AadhaarGen;
                    }
                    matchedTemplate ??= docDetection.MatchedTemplate;
                    keywordScore = Math.Clamp(docDetection.Confidence, 0.40, 1.0);
                    result.DetectionNotes = docDetection.DetectionNotes;
                }
                else
                {
                    if (preferredKind != DocumentKind.Unknown)
                    {
                        keywordScore = 0.80; // Explicitly instructed document kind
                        if (preferredKind == DocumentKind.Pan)
                            matchedTemplate = _templateService.GetTemplateById("pan_nsdl");
                        else if (preferredKind == DocumentKind.AyushmanBharat)
                            matchedTemplate = _templateService.GetTemplateById("ayushman_pmjay_v2");
                    }
                    else if (matchedTemplate == null)
                    {
                        if (geometryScore >= 0.90 && ((double)pageBitmap.Width / pageBitmap.Height) >= 1.40)
                        {
                            detectedKind = DocumentKind.GenericId;
                            result.DetectionNotes = "Direct CR80 single-card raster scan detected.";
                        }
                        else
                        {
                            detectedKind = DocumentKind.Aadhaar;
                            result.DetectionNotes = "Raster page without OCR text; applying standard calibrated Aadhaar template.";
                        }
                    }
                }

                result.DetectedKind = detectedKind;
                result.AadhaarGen = aadhaarGen;
                result.MatchedTemplate = matchedTemplate;

                // =========================================================================
                // STAGE 3: Calibrated Template Seed from Registry
                // =========================================================================
                double frontXMm;
                double frontYMm;
                double frontWMm;
                double frontHMm;

                double backXMm;
                double backYMm;
                double backWMm;
                double backHMm;
                bool hasBack;
                double templateSeedScore;

                if (matchedTemplate != null)
                {
                    templateSeedScore = 1.0;
                    frontXMm = matchedTemplate.Faces.Front.CropRectMm.X;
                    frontYMm = matchedTemplate.Faces.Front.CropRectMm.Y;
                    frontWMm = matchedTemplate.Faces.Front.CropRectMm.Width;
                    frontHMm = matchedTemplate.Faces.Front.CropRectMm.Height;

                    if (matchedTemplate.Faces.Back != null)
                    {
                        hasBack = true;
                        backXMm = matchedTemplate.Faces.Back.CropRectMm.X;
                        backYMm = matchedTemplate.Faces.Back.CropRectMm.Y;
                        backWMm = matchedTemplate.Faces.Back.CropRectMm.Width;
                        backHMm = matchedTemplate.Faces.Back.CropRectMm.Height;
                    }
                    else
                    {
                        hasBack = false;
                        backXMm = 0; backYMm = 0; backWMm = 0; backHMm = 0;
                    }
                }
                else
                {
                    // Calibrated Indian ID card template defaults
                    GetCalibratedCoordinates(detectedKind, aadhaarGen,
                        out frontXMm, out frontYMm, out frontWMm, out frontHMm,
                        out backXMm, out backYMm, out backWMm, out backHMm,
                        out hasBack);
                    templateSeedScore = preferredKind != DocumentKind.Unknown ? 0.90 : 0.70;
                }

                // Compute pixel bounds for front seed
                int fx = (int)Math.Round((frontXMm * deducedDpi) / MmPerInch);
                int fy = (int)Math.Round((frontYMm * deducedDpi) / MmPerInch);
                int fw = (int)Math.Round((frontWMm * deducedDpi) / MmPerInch);
                int fh = (int)Math.Round((frontHMm * deducedDpi) / MmPerInch);

                fx = Math.Clamp(fx, 0, Math.Max(0, pageBitmap.Width - fw));
                fy = Math.Clamp(fy, 0, Math.Max(0, pageBitmap.Height - fh));
                fw = Math.Min(fw, pageBitmap.Width - fx);
                fh = Math.Min(fh, pageBitmap.Height - fy);
                var frontSeedRect = new SKRectI(fx, fy, fx + fw, fy + fh);

                SKRectI? backSeedRect = null;
                if (hasBack)
                {
                    int bx = (int)Math.Round((backXMm * deducedDpi) / MmPerInch);
                    int by = (int)Math.Round((backYMm * deducedDpi) / MmPerInch);
                    int bw = (int)Math.Round((backWMm * deducedDpi) / MmPerInch);
                    int bh = (int)Math.Round((backHMm * deducedDpi) / MmPerInch);

                    bx = Math.Clamp(bx, 0, Math.Max(0, pageBitmap.Width - bw));
                    by = Math.Clamp(by, 0, Math.Max(0, pageBitmap.Height - bh));
                    bw = Math.Min(bw, pageBitmap.Width - bx);
                    bh = Math.Min(bh, pageBitmap.Height - by);
                    backSeedRect = new SKRectI(bx, by, bx + bw, by + bh);
                }

                // =========================================================================
                // STAGE 4: OpenCV Canny Edge & Hough Line Refinement (±15mm window)
                // =========================================================================
                var frontRefinedRect = RefineCropBoundsWithOpenCv(pageBitmap, frontSeedRect, searchWindowMm: 15.0, deducedDpi);
                SKRectI? backRefinedRect = null;
                if (hasBack && backSeedRect.HasValue)
                {
                    backRefinedRect = RefineCropBoundsWithOpenCv(pageBitmap, backSeedRect.Value, searchWindowMm: 15.0, deducedDpi);
                }

                double edgeScore;
                bool frontShifted = frontRefinedRect != frontSeedRect;
                bool backShifted = backRefinedRect.HasValue && backRefinedRect.Value != backSeedRect;

                if (frontShifted || backShifted)
                {
                    edgeScore = 0.95;
                }
                else
                {
                    edgeScore = 0.65;
                }

                result.FrontRect = frontRefinedRect;
                result.BackRect = backRefinedRect;

                // =========================================================================
                // STAGE 5: Confidence Scoring & Normalization
                // =========================================================================
                double overallConfidence;
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    overallConfidence = (geometryScore * 0.15) + (keywordScore * 0.40) + (templateSeedScore * 0.25) + (edgeScore * 0.20);
                }
                else
                {
                    overallConfidence = (geometryScore * 0.30) + (templateSeedScore * 0.35) + (edgeScore * 0.35);
                }

                result.Confidence = new DetectionConfidenceScore(
                    overallConfidence,
                    geometryScore,
                    keywordScore,
                    templateSeedScore,
                    edgeScore);

                // Extract and normalize to CR80 target resolution: 2022 x 1275 px (600 DPI)
                int targetW = 2022;
                int targetH = 1275;

                using var frontSub = new SKBitmap(frontRefinedRect.Width, frontRefinedRect.Height, pageBitmap.ColorType, pageBitmap.AlphaType);
                using (var canvas = new SKCanvas(frontSub))
                {
                    canvas.DrawBitmap(pageBitmap, frontRefinedRect, new SKRect(0, 0, frontRefinedRect.Width, frontRefinedRect.Height), SKSamplingOptions.Default);
                }

                var finalFront = new SKBitmap(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(finalFront))
                {
                    canvas.Clear(SKColors.White);
                    canvas.DrawBitmap(frontSub, new SKRect(0, 0, targetW, targetH), new SKSamplingOptions(SKCubicResampler.Mitchell));

                    if (applyMask)
                    {
                        // MaskZone coordinates calibration:
                        // UIDAI 2022-2024 Modern Cutline e-Aadhaar format: card size 85.60 x 53.98 mm.
                        // Primary 12-digit Aadhaar UID number is positioned at relative X=28.0 mm, Y=46.5 mm,
                        // Width=24.0 mm, Height=4.5 mm (or fallback range X=20.0, Y=42.0, W=45.0, H=6.0 mm).
                        // Masking replaces the first 8 digits with "XXXX XXXX " and paints an opaque white block.
                        // Note: Legacy pre-2018 large format or mAadhaar mobile app screenshots do not share this
                        // geometry and must be cropped/masked with the interactive crop drawer.
                        var maskZones = matchedTemplate?.Faces?.Front?.MaskZones;
                        if (maskZones == null || maskZones.Count == 0)
                        {
                            if (detectedKind == DocumentKind.Aadhaar)
                            {
                                maskZones = new List<MaskZone>
                                {
                                    new MaskZone
                                    {
                                        Name = "DefaultAadhaarMask",
                                        RelativeXMm = 20.0,
                                        RelativeYMm = 42.0,
                                        WidthMm = 45.0,
                                        HeightMm = 6.0,
                                        OverlayText = "XXXX XXXX "
                                    }
                                };
                            }
                        }

                        if (maskZones != null && maskZones.Count > 0)
                        {
                            using var maskPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                            using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                            using var font = new SKFont(SKTypeface.Default, 28);

                            foreach (var mz in maskZones)
                            {
                                float mx = (float)((mz.RelativeXMm * 600.0) / MmPerInch);
                                float my = (float)((mz.RelativeYMm * 600.0) / MmPerInch);
                                float mw = (float)((mz.WidthMm * 600.0) / MmPerInch);
                                float mh = (float)((mz.HeightMm * 600.0) / MmPerInch);

                                canvas.DrawRect(new SKRect(mx, my, mx + mw, my + mh), maskPaint);
                                if (!string.IsNullOrEmpty(mz.OverlayText))
                                {
                                    canvas.DrawText(mz.OverlayText, mx + 15, my + (mh * 0.75f), SKTextAlign.Left, font, textPaint);
                                }
                            }
                            result.IsMaskApplied = true;
                        }
                    }
                }
                result.FrontCard = finalFront;

                if (hasBack && backRefinedRect.HasValue)
                {
                    using var backSub = new SKBitmap(backRefinedRect.Value.Width, backRefinedRect.Value.Height, pageBitmap.ColorType, pageBitmap.AlphaType);
                    using (var canvas = new SKCanvas(backSub))
                    {
                        canvas.DrawBitmap(pageBitmap, backRefinedRect.Value, new SKRect(0, 0, backRefinedRect.Value.Width, backRefinedRect.Value.Height), SKSamplingOptions.Default);
                    }

                    var finalBack = new SKBitmap(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using (var canvas = new SKCanvas(finalBack))
                    {
                        canvas.Clear(SKColors.White);
                        canvas.DrawBitmap(backSub, new SKRect(0, 0, targetW, targetH), new SKSamplingOptions(SKCubicResampler.Mitchell));
                    }
                    result.BackCard = finalBack;
                }

                return result;
            });
        }

        public async Task<(SKBitmap? Front, SKBitmap? Back, SKRectI? FrontRect, SKRectI? BackRect)> DetectAndExtractDualCardsAsync(
            SKBitmap pageBitmap,
            DocumentKind kind = DocumentKind.Aadhaar,
            AadhaarGeneration generation = AadhaarGeneration.Unknown)
        {
            var result = await DetectAndExtractAdaptiveAsync(
                pageBitmap,
                extractedText: string.Empty,
                pageWidthMm: 210.0,
                pageHeightMm: 297.0,
                preferredKind: kind,
                preferredAadhaarGen: generation);

            return (result.FrontCard, result.BackCard, result.FrontRect, result.BackRect);
        }

        public Task<SKRectI> RefineCropBoundsWithOpenCvAsync(
            SKBitmap pageBitmap,
            SKRectI seedRect,
            double searchWindowMm = 15.0,
            double dpi = 300.0)
        {
            return Task.Run(() => RefineCropBoundsWithOpenCv(pageBitmap, seedRect, searchWindowMm, dpi));
        }

        public SKRectI RefineCropBoundsWithOpenCv(
            SKBitmap pageBitmap,
            SKRectI seedRect,
            double searchWindowMm = 15.0,
            double dpi = 300.0)
        {
            if (pageBitmap == null || pageBitmap.Width <= 0 || pageBitmap.Height <= 0)
                return seedRect;

            int windowPx = (int)Math.Round((searchWindowMm * dpi) / MmPerInch);
            if (windowPx <= 0) windowPx = 100;

            int searchLeft = Math.Max(0, seedRect.Left - windowPx);
            int searchTop = Math.Max(0, seedRect.Top - windowPx);
            int searchRight = Math.Min(pageBitmap.Width, seedRect.Right + windowPx);
            int searchBottom = Math.Min(pageBitmap.Height, seedRect.Bottom + windowPx);
            int searchWidth = searchRight - searchLeft;
            int searchHeight = searchBottom - searchTop;

            if (searchWidth <= 50 || searchHeight <= 50)
                return seedRect;

            using var searchSubset = new SKBitmap(searchWidth, searchHeight, pageBitmap.ColorType, pageBitmap.AlphaType);
            var searchRectI = new SKRectI(searchLeft, searchTop, searchRight, searchBottom);
            using (var canvas = new SKCanvas(searchSubset))
            {
                canvas.DrawBitmap(pageBitmap, searchRectI, new SKRect(0, 0, searchWidth, searchHeight), SKSamplingOptions.Default);
            }

            using var srcMat = SkBitmapToMat(searchSubset);
            using var gray = new Mat();
            Cv2.CvtColor(srcMat, gray, ColorConversionCodes.BGRA2GRAY);

            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(3, 3), 0);

            using var edges = new Mat();
            Cv2.Canny(blurred, edges, 50, 150);

            Cv2.FindContours(edges, out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            double expectedArea = (double)seedRect.Width * seedRect.Height;
            Rect? bestRect = null;
            double bestDistance = double.MaxValue;
            Point2f seedCenterInRoi = new Point2f(seedRect.MidX - searchLeft, seedRect.MidY - searchTop);

            foreach (var c in contours)
            {
                var bRect = Cv2.BoundingRect(c);
                double area = (double)bRect.Width * bRect.Height;

                // Check if area is within 50% to 150% of expected card area
                if (area < expectedArea * 0.50 || area > expectedArea * 1.50)
                    continue;

                double aspect = (double)bRect.Width / Math.Max(1, bRect.Height);
                // CR80 aspect ratio is ~1.586. Allow 1.25 to 1.95
                if (aspect < 1.25 || aspect > 1.95)
                    continue;

                Point2f cCenter = new Point2f(bRect.X + bRect.Width / 2f, bRect.Y + bRect.Height / 2f);
                double dist = Math.Sqrt(Math.Pow(cCenter.X - seedCenterInRoi.X, 2) + Math.Pow(cCenter.Y - seedCenterInRoi.Y, 2));

                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestRect = bRect;
                }
            }

            if (bestRect != null)
            {
                int refinedLeft = Math.Clamp(searchLeft + bestRect.Value.X, 0, pageBitmap.Width - 10);
                int refinedTop = Math.Clamp(searchTop + bestRect.Value.Y, 0, pageBitmap.Height - 10);
                int refinedW = Math.Clamp(bestRect.Value.Width, 10, pageBitmap.Width - refinedLeft);
                int refinedH = Math.Clamp(bestRect.Value.Height, 10, pageBitmap.Height - refinedTop);
                return new SKRectI(refinedLeft, refinedTop, refinedLeft + refinedW, refinedTop + refinedH);
            }

            // Secondary check: Look for distinct Hough lines for cutlines
            var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, threshold: 40, minLineLength: (int)(seedRect.Width * 0.35), maxLineGap: 20);
            if (lines != null && lines.Length >= 2)
            {
                int topY = -1, bottomY = -1, leftX = -1, rightX = -1;
                int seedTopInRoi = seedRect.Top - searchTop;
                int seedBottomInRoi = seedRect.Bottom - searchTop;
                int seedLeftInRoi = seedRect.Left - searchLeft;
                int seedRightInRoi = seedRect.Right - searchLeft;

                foreach (var l in lines)
                {
                    bool isHorizontal = Math.Abs(l.P1.Y - l.P2.Y) <= 5;
                    bool isVertical = Math.Abs(l.P1.X - l.P2.X) <= 5;

                    if (isHorizontal)
                    {
                        int avgY = (l.P1.Y + l.P2.Y) / 2;
                        if (Math.Abs(avgY - seedTopInRoi) < windowPx && (topY == -1 || Math.Abs(avgY - seedTopInRoi) < Math.Abs(topY - seedTopInRoi)))
                            topY = avgY;
                        else if (Math.Abs(avgY - seedBottomInRoi) < windowPx && (bottomY == -1 || Math.Abs(avgY - seedBottomInRoi) < Math.Abs(bottomY - seedBottomInRoi)))
                            bottomY = avgY;
                    }
                    else if (isVertical)
                    {
                        int avgX = (l.P1.X + l.P2.X) / 2;
                        if (Math.Abs(avgX - seedLeftInRoi) < windowPx && (leftX == -1 || Math.Abs(avgX - seedLeftInRoi) < Math.Abs(leftX - seedLeftInRoi)))
                            leftX = avgX;
                        else if (Math.Abs(avgX - seedRightInRoi) < windowPx && (rightX == -1 || Math.Abs(avgX - seedRightInRoi) < Math.Abs(rightX - seedRightInRoi)))
                            rightX = avgX;
                    }
                }

                int finalLeft = (leftX != -1) ? searchLeft + leftX : seedRect.Left;
                int finalTop = (topY != -1) ? searchTop + topY : seedRect.Top;
                int finalRight = (rightX != -1) ? searchLeft + rightX : seedRect.Right;
                int finalBottom = (bottomY != -1) ? searchTop + bottomY : seedRect.Bottom;

                if (finalRight > finalLeft + 50 && finalBottom > finalTop + 50)
                {
                    return new SKRectI(finalLeft, finalTop, finalRight, finalBottom);
                }
            }

            return seedRect;
        }

        public Task<SKBitmap> RectifyCardPerspectiveAsync(
            SKBitmap sourceBitmap,
            QuadrilateralPoints quad,
            int targetWidthPx = 2022,
            int targetHeightPx = 1275)
        {
            return Task.Run(() =>
            {
                using var srcMat = SkBitmapToMat(sourceBitmap);

                var srcPts = new Point2f[]
                {
                    new Point2f(quad.TopLeft.X, quad.TopLeft.Y),
                    new Point2f(quad.TopRight.X, quad.TopRight.Y),
                    new Point2f(quad.BottomRight.X, quad.BottomRight.Y),
                    new Point2f(quad.BottomLeft.X, quad.BottomLeft.Y)
                };

                var destPts = new Point2f[]
                {
                    new Point2f(0, 0),
                    new Point2f(targetWidthPx - 1, 0),
                    new Point2f(targetWidthPx - 1, targetHeightPx - 1),
                    new Point2f(0, targetHeightPx - 1)
                };

                using var transformMatrix = Cv2.GetPerspectiveTransform(srcPts, destPts);
                using var warped = new Mat();
                Cv2.WarpPerspective(srcMat, warped, transformMatrix, new OpenCvSharp.Size(targetWidthPx, targetHeightPx), InterpolationFlags.Cubic);

                return MatToSkBitmap(warped);
            });
        }

        private static void GetCalibratedCoordinates(
            DocumentKind kind,
            AadhaarGeneration generation,
            out double frontXMm, out double frontYMm, out double frontWMm, out double frontHMm,
            out double backXMm, out double backYMm, out double backWMm, out double backHMm,
            out bool hasBack)
        {
            frontWMm = 88.48;
            frontHMm = 56.30;
            frontXMm = 17.78;
            frontYMm = 219.71;

            backWMm = 88.48;
            backHMm = 56.30;
            backXMm = 110.06;
            backYMm = 219.71;
            hasBack = true;

            switch (kind)
            {
                case DocumentKind.Aadhaar:
                    if (generation == AadhaarGeneration.PvcOrderLetter)
                    {
                        frontXMm = 62.2;
                        frontYMm = 95.0;
                        frontWMm = 85.60;
                        frontHMm = 53.98;
                        hasBack = false;
                    }
                    else
                    {
                        frontXMm = 17.78;
                        frontYMm = 219.71;
                        frontWMm = 88.48;
                        frontHMm = 56.30;

                        backXMm = 110.06;
                        backYMm = 219.71;
                        backWMm = 88.48;
                        backHMm = 56.30;
                        hasBack = true;
                    }
                    break;

                case DocumentKind.Pan:
                    frontXMm = 24.55;
                    frontYMm = 229.79;
                    frontWMm = 82.13;
                    frontHMm = 51.22;

                    backXMm = 108.80;
                    backYMm = 229.79;
                    backWMm = 82.55;
                    backHMm = 51.22;
                    hasBack = true;
                    break;

                case DocumentKind.VoterId:
                    frontXMm = 18.0;
                    frontYMm = 160.0;
                    frontWMm = 85.60;
                    frontHMm = 53.98;

                    backXMm = 108.0;
                    backYMm = 160.0;
                    backWMm = 85.60;
                    backHMm = 53.98;
                    hasBack = true;
                    break;

                case DocumentKind.AyushmanBharat:
                    frontXMm = 10.16;
                    frontYMm = 22.86;
                    frontWMm = 88.05;
                    frontHMm = 56.30;

                    backXMm = 114.30;
                    backYMm = 22.86;
                    backWMm = 88.05;
                    backHMm = 56.30;
                    hasBack = true;
                    break;

                case DocumentKind.EShram:
                    frontXMm = 40.64;
                    frontYMm = 35.13;
                    frontWMm = 80.43;
                    frontHMm = 51.22;

                    backXMm = 40.64;
                    backYMm = 87.63;
                    backWMm = 80.43;
                    backHMm = 50.80;
                    hasBack = true;
                    break;

                case DocumentKind.DrivingLicence:
                    frontXMm = 20.0;
                    frontYMm = 80.0;
                    frontWMm = 85.60;
                    frontHMm = 53.98;

                    backXMm = 110.0;
                    backYMm = 80.0;
                    backWMm = 85.60;
                    backHMm = 53.98;
                    hasBack = true;
                    break;

                case DocumentKind.VehicleRc:
                    frontXMm = 20.0;
                    frontYMm = 90.0;
                    frontWMm = 85.60;
                    frontHMm = 53.98;

                    backXMm = 110.0;
                    backYMm = 90.0;
                    backWMm = 85.60;
                    backHMm = 53.98;
                    hasBack = true;
                    break;

                case DocumentKind.Abha:
                    frontXMm = 17.0;
                    frontYMm = 75.0;
                    frontWMm = 85.60;
                    frontHMm = 53.98;

                    backXMm = 108.0;
                    backYMm = 75.0;
                    backWMm = 85.60;
                    backHMm = 53.98;
                    hasBack = true;
                    break;

                case DocumentKind.TamilNaduRation:
                    frontXMm = 15.0;
                    frontYMm = 140.0;
                    frontWMm = 85.60;
                    frontHMm = 53.98;

                    backXMm = 105.0;
                    backYMm = 140.0;
                    backWMm = 85.60;
                    backHMm = 53.98;
                    hasBack = true;
                    break;

                default:
                    frontXMm = 17.78;
                    frontYMm = 219.71;
                    frontWMm = 88.48;
                    frontHMm = 56.30;

                    backXMm = 110.06;
                    backYMm = 219.71;
                    backWMm = 88.48;
                    backHMm = 56.30;
                    hasBack = true;
                    break;
            }
        }

        private static Mat SkBitmapToMat(SKBitmap bitmap)
        {
            var mat = new Mat(bitmap.Height, bitmap.Width, MatType.CV_8UC4);
            int bytesPerRow = bitmap.Width * 4;
            int stride = bitmap.RowBytes;
            IntPtr srcPtr = bitmap.GetPixels();

            unsafe
            {
                byte* src = (byte*)srcPtr;
                byte* dst = (byte*)mat.Data;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Buffer.MemoryCopy(src + (y * stride), dst + (y * bytesPerRow), bytesPerRow, bytesPerRow);
                }
            }
            return mat;
        }

        private static SKBitmap MatToSkBitmap(Mat mat)
        {
            var bitmap = new SKBitmap(mat.Width, mat.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            int bytesPerRow = mat.Width * 4;
            int stride = bitmap.RowBytes;
            IntPtr destPtr = bitmap.GetPixels();

            unsafe
            {
                byte* src = (byte*)mat.Data;
                byte* dst = (byte*)destPtr;
                for (int y = 0; y < mat.Height; y++)
                {
                    Buffer.MemoryCopy(src + (y * bytesPerRow), dst + (y * stride), bytesPerRow, bytesPerRow);
                }
            }
            return bitmap;
        }
    }
}
