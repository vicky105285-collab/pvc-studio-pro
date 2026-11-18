using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PVCStudioPro.Core.Models
{
    public class CardTemplate
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "1.0.0";

        [JsonPropertyName("template_id")]
        public string TemplateId { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "GOVERNMENT_IDENTITY";

        [JsonPropertyName("document_type")]
        public string DocumentType { get; set; } = "PDF";

        [JsonPropertyName("identification")]
        public TemplateIdentification Identification { get; set; } = new();

        [JsonPropertyName("physical_card")]
        public PhysicalCardSpec PhysicalCard { get; set; } = new();

        [JsonPropertyName("faces")]
        public CardFaces Faces { get; set; } = new();

        [JsonPropertyName("tray_layout")]
        public Dictionary<string, TrayOffsets> TrayLayout { get; set; } = new();
    }

    public class TemplateIdentification
    {
        [JsonPropertyName("anchor_keywords")]
        public List<string> AnchorKeywords { get; set; } = new();

        [JsonPropertyName("min_keyword_matches")]
        public int MinKeywordMatches { get; set; } = 2;

        [JsonPropertyName("expected_pages")]
        public int ExpectedPages { get; set; } = 1;

        [JsonPropertyName("page_size_tolerance_mm")]
        public double PageSizeToleranceMm { get; set; } = 5.0;
    }

    public class PhysicalCardSpec
    {
        [JsonPropertyName("standard")]
        public string Standard { get; set; } = "CR80";

        [JsonPropertyName("width_mm")]
        public double WidthMm { get; set; } = 85.60;

        [JsonPropertyName("height_mm")]
        public double HeightMm { get; set; } = 53.98;

        [JsonPropertyName("corner_radius_mm")]
        public double CornerRadiusMm { get; set; } = 3.18;

        [JsonPropertyName("target_dpi")]
        public int TargetDpi { get; set; } = 600;
    }

    public class CardFaces
    {
        [JsonPropertyName("front")]
        public FaceDefinition Front { get; set; } = new();

        [JsonPropertyName("back")]
        public FaceDefinition? Back { get; set; }
    }

    public class FaceDefinition
    {
        [JsonPropertyName("source_page")]
        public int SourcePage { get; set; } = 0;

        [JsonPropertyName("crop_rect_mm")]
        public MillimeterRect CropRectMm { get; set; } = new();

        [JsonPropertyName("rotation_degrees")]
        public int RotationDegrees { get; set; } = 0;

        [JsonPropertyName("mask_zones")]
        public List<MaskZone> MaskZones { get; set; } = new();
    }

    public class MillimeterRect
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }
    }

    public class MaskZone
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("relative_x_mm")]
        public double RelativeXMm { get; set; }

        [JsonPropertyName("relative_y_mm")]
        public double RelativeYMm { get; set; }

        [JsonPropertyName("width_mm")]
        public double WidthMm { get; set; }

        [JsonPropertyName("height_mm")]
        public double HeightMm { get; set; }

        [JsonPropertyName("fill_color")]
        public string FillColor { get; set; } = "#FFFFFF";

        [JsonPropertyName("overlay_text")]
        public string OverlayText { get; set; } = "XXXX XXXX ";
    }

    public class TrayOffsets
    {
        [JsonPropertyName("card_1_pos_mm")]
        public PointMm Card1PosMm { get; set; } = new();

        [JsonPropertyName("card_2_pos_mm")]
        public PointMm Card2PosMm { get; set; } = new();
    }

    public class PointMm
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }

    public class TemplateMatchResult
    {
        public bool IsSuccess { get; set; }
        public CardTemplate? Template { get; set; }
        public double ConfidenceScore { get; set; }
        public int MatchedKeywordsCount { get; set; }
        public List<string> MatchedKeywords { get; set; } = new();
        public bool RequiresOperatorConfirmation { get; set; }
        public string MatchReason { get; set; } = string.Empty;

        public static TemplateMatchResult Success(CardTemplate template, double confidence, List<string> matched, bool requiresConfirmation, string reason)
        {
            return new TemplateMatchResult
            {
                IsSuccess = true,
                Template = template,
                ConfidenceScore = Math.Round(confidence, 2),
                MatchedKeywordsCount = matched.Count,
                MatchedKeywords = matched,
                RequiresOperatorConfirmation = requiresConfirmation,
                MatchReason = reason
            };
        }

        public static TemplateMatchResult Unknown(string reason)
        {
            return new TemplateMatchResult
            {
                IsSuccess = false,
                Template = null,
                ConfidenceScore = 0.0,
                MatchedKeywordsCount = 0,
                RequiresOperatorConfirmation = true,
                MatchReason = reason
            };
        }
    }
}
