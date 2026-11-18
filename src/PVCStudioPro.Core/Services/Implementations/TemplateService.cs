using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class TemplateService : ITemplateService
    {
        private readonly List<CardTemplate> _templates = new();
        private readonly string _templatesDirectory;

        public TemplateService(string? customTemplatesDir = null)
        {
            _templatesDirectory = customTemplatesDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PVCStudioPro", "templates"
            );

            // Populate built-in defaults synchronously so templates are immediately available
            _templates.Add(CreateDefaultAadhaarTemplate());
            _templates.Add(CreateDefaultPanTemplate());
            _templates.Add(CreateDefaultVoterTemplate());
            _templates.Add(CreateDefaultAyushmanTemplate());
            _templates.Add(CreateDefaultTamilNaduRationTemplate());
        }

        public async Task InitializeTemplatesAsync()
        {
            // Load Disk Templates to allow overrides or custom templates
            try
            {
                if (Directory.Exists(_templatesDirectory))
                {
                    var files = Directory.GetFiles(_templatesDirectory, "*.json");
                    foreach (var file in files)
                    {
                        string json = await File.ReadAllTextAsync(file);
                        var template = JsonSerializer.Deserialize<CardTemplate>(json);
                        if (template != null)
                        {
                            // Override or add
                            _templates.RemoveAll(t => t.TemplateId == template.TemplateId);
                            _templates.Add(template);
                        }
                    }
                }
            }
            catch
            {
                // Resilient fallback to built-in templates
            }
        }

        public IReadOnlyList<CardTemplate> GetAllTemplates() => _templates.AsReadOnly();

        public CardTemplate? GetTemplateById(string templateId) =>
            _templates.FirstOrDefault(t => t.TemplateId == templateId);

        public CardTemplate MatchTemplateByText(string extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
                return _templates.First(); // Default to Aadhaar

            CardTemplate? bestMatch = null;
            int highestMatches = 0;

            foreach (var template in _templates)
            {
                int matches = 0;
                foreach (var keyword in template.Identification.AnchorKeywords)
                {
                    if (extractedText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                    }
                }

                if (matches >= template.Identification.MinKeywordMatches && matches > highestMatches)
                {
                    highestMatches = matches;
                    bestMatch = template;
                }
            }

            return bestMatch ?? _templates.First();
        }

        public async Task SaveCustomTemplateAsync(CardTemplate template)
        {
            Directory.CreateDirectory(_templatesDirectory);
            string filePath = Path.Combine(_templatesDirectory, $"{template.TemplateId}.json");
            string json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            _templates.RemoveAll(t => t.TemplateId == template.TemplateId);
            _templates.Add(template);
        }

        private static CardTemplate CreateDefaultAadhaarTemplate() => new()
        {
            TemplateId = "in_gov_aadhaar_standard",
            DisplayName = "Aadhaar Card (UIDAI Standard)",
            Category = "AADHAAR",
            Identification = new TemplateIdentification
            {
                AnchorKeywords = new List<string> { "Unique Identification Authority of India", "Government of India", "Enrollment No", "Help@uidai.gov.in" },
                MinKeywordMatches = 2
            },
            Faces = new CardFaces
            {
                Front = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 18.0, Y = 204.0, Width = 85.60, Height = 53.98 },
                    MaskZones = new List<MaskZone>
                    {
                        new() { Name = "Aadhaar First 8 Digits", RelativeXMm = 28.0, RelativeYMm = 46.5, WidthMm = 24.0, HeightMm = 4.5, OverlayText = "XXXX XXXX " }
                    }
                },
                Back = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 108.0, Y = 204.0, Width = 85.60, Height = 53.98 }
                }
            }
        };

        private static CardTemplate CreateDefaultPanTemplate() => new()
        {
            TemplateId = "in_gov_pan_standard",
            DisplayName = "PAN Card (NSDL / UTIITSL)",
            Category = "PAN",
            Identification = new TemplateIdentification
            {
                AnchorKeywords = new List<string> { "INCOME TAX DEPARTMENT", "Permanent Account Number", "GOVT. OF INDIA" },
                MinKeywordMatches = 2
            },
            Faces = new CardFaces
            {
                Front = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 18.0, Y = 210.0, Width = 85.60, Height = 53.98 }
                },
                Back = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 108.0, Y = 210.0, Width = 85.60, Height = 53.98 }
                }
            }
        };

        private static CardTemplate CreateDefaultVoterTemplate() => new()
        {
            TemplateId = "in_gov_voter_epic",
            DisplayName = "Voter ID (e-EPIC)",
            Category = "VOTER",
            Identification = new TemplateIdentification
            {
                AnchorKeywords = new List<string> { "ELECTION COMMISSION OF INDIA", "ELECTOR PHOTO IDENTITY CARD", "EPIC NO" },
                MinKeywordMatches = 2
            },
            Faces = new CardFaces
            {
                Front = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 15.0, Y = 195.0, Width = 85.60, Height = 53.98 }
                },
                Back = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 105.0, Y = 195.0, Width = 85.60, Height = 53.98 }
                }
            }
        };

        private static CardTemplate CreateDefaultAyushmanTemplate() => new()
        {
            TemplateId = "in_gov_ayushman_pmjay",
            DisplayName = "Ayushman Bharat (PM-JAY)",
            Category = "AYUSHMAN",
            Identification = new TemplateIdentification
            {
                AnchorKeywords = new List<string> { "Ayushman Bharat", "PM-JAY", "National Health Authority" },
                MinKeywordMatches = 2
            },
            Faces = new CardFaces
            {
                Front = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 18.0, Y = 180.0, Width = 85.60, Height = 53.98 }
                },
                Back = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 108.0, Y = 180.0, Width = 85.60, Height = 53.98 }
                }
            }
        };

        private static CardTemplate CreateDefaultTamilNaduRationTemplate() => new()
        {
            TemplateId = "in_tn_smart_ration",
            DisplayName = "Tamil Nadu Smart Ration Card (e-PDS)",
            Category = "STATE_GOVT",
            Identification = new TemplateIdentification
            {
                AnchorKeywords = new List<string> { "உணவுப் பொருள் வழங்கல்", "Civil Supplies", "Tamil Nadu Smart Family Card" },
                MinKeywordMatches = 1
            },
            Faces = new CardFaces
            {
                Front = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 12.0, Y = 140.0, Width = 85.60, Height = 53.98 }
                },
                Back = new FaceDefinition
                {
                    SourcePage = 0,
                    CropRectMm = new MillimeterRect { X = 102.0, Y = 140.0, Width = 85.60, Height = 53.98 }
                }
            }
        };
    }
}
