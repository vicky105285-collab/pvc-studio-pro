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

            // Populate all 10 standard Indian Government templates synchronously
            RegisterDefaultTemplates();
        }

        private void RegisterDefaultTemplates()
        {
            _templates.Clear();
            _templates.Add(CreateDefaultAadhaarTemplate());
            _templates.Add(CreateDefaultPanNsdlTemplate());
            _templates.Add(CreateDefaultPanUtiitslTemplate());
            _templates.Add(CreateDefaultVoterTemplate());
            _templates.Add(CreateDefaultAyushmanTemplate());
            _templates.Add(CreateDefaultTamilNaduRationTemplate());
            _templates.Add(CreateDefaultDrivingLicenceTemplate());
            _templates.Add(CreateDefaultVehicleRcTemplate());
            _templates.Add(CreateDefaultEShramTemplate());
            _templates.Add(CreateDefaultAbhaTemplate());
        }

        public async Task InitializeTemplatesAsync()
        {
            RegisterDefaultTemplates();

            try
            {
                if (Directory.Exists(_templatesDirectory))
                {
                    var files = Directory.GetFiles(_templatesDirectory, "*.json");
                    foreach (var file in files)
                    {
                        string json = await File.ReadAllTextAsync(file);
                        var template = JsonSerializer.Deserialize<CardTemplate>(json);
                        if (template != null && ValidateTemplate(template, out _))
                        {
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

        public TemplateMatchResult MatchTemplateByText(string extractedText) =>
            MatchTemplate(extractedText, 0, 0);

        public TemplateMatchResult MatchTemplate(string extractedText, double pageWidthMm = 0, double pageHeightMm = 0)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return TemplateMatchResult.Unknown("Document contains no readable text or is an un-rasterized image scan.");
            }

            var scoredMatches = new List<(CardTemplate Template, double Score, List<string> MatchedKeywords)>();

            foreach (var template in _templates)
            {
                var matched = new List<string>();
                foreach (var keyword in template.Identification.AnchorKeywords)
                {
                    if (extractedText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(keyword);
                    }
                }

                if (matched.Count >= template.Identification.MinKeywordMatches)
                {
                    double keywordScore = (double)matched.Count / Math.Max(1, template.Identification.AnchorKeywords.Count);
                    double sizeScore = 1.0;

                    // If physical page dimensions were provided, verify A4 or card bounds
                    if (pageWidthMm > 0 && pageHeightMm > 0)
                    {
                        // Check if within A4 dimensions (210 x 297 mm)
                        bool isA4 = Math.Abs(pageWidthMm - 210.0) <= template.Identification.PageSizeToleranceMm &&
                                    Math.Abs(pageHeightMm - 297.0) <= template.Identification.PageSizeToleranceMm;
                        if (!isA4 && template.Identification.ExpectedPages == 1)
                        {
                            sizeScore = 0.85;
                        }
                    }

                    double totalConfidence = (keywordScore * 0.7) + (sizeScore * 0.3);
                    scoredMatches.Add((template, totalConfidence, matched));
                }
            }

            if (scoredMatches.Count == 0)
            {
                return TemplateMatchResult.Unknown("Document keywords did not match any supported Indian Government identity template.");
            }

            // Order by score descending
            scoredMatches = scoredMatches.OrderByDescending(m => m.Score).ThenByDescending(m => m.MatchedKeywords.Count).ToList();
            var topMatch = scoredMatches[0];

            bool isAmbiguous = false;
            string matchReason = $"Matched {topMatch.MatchedKeywords.Count} keywords ({string.Join(", ", topMatch.MatchedKeywords.Take(3))})";

            if (scoredMatches.Count > 1)
            {
                var secondMatch = scoredMatches[1];
                if (Math.Abs(topMatch.Score - secondMatch.Score) < 0.10)
                {
                    isAmbiguous = true;
                    matchReason += $" [Ambiguous with {secondMatch.Template.DisplayName}]";
                }
            }

            bool requiresConfirmation = isAmbiguous || topMatch.Score < 0.70;

            return TemplateMatchResult.Success(
                topMatch.Template,
                topMatch.Score,
                topMatch.MatchedKeywords,
                requiresConfirmation,
                matchReason);
        }

        public bool ValidateTemplate(CardTemplate template, out List<string> validationErrors)
        {
            validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(template.TemplateId))
                validationErrors.Add("TemplateId cannot be empty.");
            if (string.IsNullOrWhiteSpace(template.DisplayName))
                validationErrors.Add("DisplayName cannot be empty.");
            if (template.PhysicalCard.WidthMm < 50 || template.PhysicalCard.WidthMm > 150)
                validationErrors.Add($"Invalid PhysicalCard.WidthMm: {template.PhysicalCard.WidthMm}");
            if (template.PhysicalCard.HeightMm < 30 || template.PhysicalCard.HeightMm > 100)
                validationErrors.Add($"Invalid PhysicalCard.HeightMm: {template.PhysicalCard.HeightMm}");
            if (template.Faces.Front.CropRectMm.Width <= 0 || template.Faces.Front.CropRectMm.Height <= 0)
                validationErrors.Add("Front face crop rectangle dimensions must be positive.");

            return validationErrors.Count == 0;
        }

        public async Task SaveCustomTemplateAsync(CardTemplate template)
        {
            if (!ValidateTemplate(template, out var errors))
            {
                throw new ArgumentException($"Template validation failed: {string.Join("; ", errors)}");
            }

            Directory.CreateDirectory(_templatesDirectory);
            string filePath = Path.Combine(_templatesDirectory, $"{template.TemplateId}.json");
            string json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            _templates.RemoveAll(t => t.TemplateId == template.TemplateId);
            _templates.Add(template);
        }

        #region Default Template Factories (10 Indian Identity Templates)

        private static CardTemplate CreateDefaultAadhaarTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "aadhaar_v2024",
                DisplayName = "UIDAI e-Aadhaar (Letter / Download)",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "AADHAAR",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "Unique Identification Authority of India",
                        "Enrollment No",
                        "मेरा आधार, मेरी पहचान",
                        "VID:",
                        "आधार"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 17.78, Y = 219.71, Width = 88.48, Height = 56.30 },
                        MaskZones = new List<MaskZone>
                        {
                            new MaskZone
                            {
                                Name = "AadhaarFirst8Digits",
                                RelativeXMm = 25.0,
                                RelativeYMm = 44.5,
                                WidthMm = 32.0,
                                HeightMm = 6.0,
                                OverlayText = "XXXX XXXX "
                            }
                        }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 110.06, Y = 219.71, Width = 88.48, Height = 56.30 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultPanNsdlTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "pan_nsdl",
                DisplayName = "NSDL Income Tax PAN Card",
                Category = "TAX_IDENTITY",
                DocumentType = "PAN",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "INCOME TAX DEPARTMENT",
                        "GOVT. OF INDIA",
                        "Permanent Account Number",
                        "National Securities Depository"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 24.55, Y = 229.79, Width = 82.13, Height = 51.22 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 108.80, Y = 229.79, Width = 82.55, Height = 51.22 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultPanUtiitslTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "pan_utiitsl",
                DisplayName = "UTIITSL PAN Card",
                Category = "TAX_IDENTITY",
                DocumentType = "PAN",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "INCOME TAX DEPARTMENT",
                        "UTI Infrastructure Technology",
                        "Permanent Account Number Card",
                        "UTIITSL"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 12.70, Y = 232.75, Width = 86.36, Height = 53.34 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 111.35, Y = 232.75, Width = 85.51, Height = 53.34 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultVoterTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "voter_epic_v2",
                DisplayName = "ECI e-EPIC (Voter ID Card)",
                Category = "ELECTION_IDENTITY",
                DocumentType = "VOTER",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "ELECTION COMMISSION OF INDIA",
                        "ELECTOR PHOTO IDENTITY CARD",
                        "வாக்காளர் அடையாள அட்டை",
                        "भारत निर्वाचन आयोग"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 18.0, Y = 160.0, Width = 85.60, Height = 53.98 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 108.0, Y = 160.0, Width = 85.60, Height = 53.98 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultAyushmanTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "ayushman_pmjay",
                DisplayName = "Ayushman Bharat PM-JAY Golden Card",
                Category = "HEALTH_IDENTITY",
                DocumentType = "AYUSHMAN",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "Ayushman Bharat",
                        "PM-JAY",
                        "National Health Authority",
                        "Pradhan Mantri Jan Arogya Yojana"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 10.16, Y = 22.86, Width = 88.05, Height = 56.30 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 114.30, Y = 22.86, Width = 88.05, Height = 56.30 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultTamilNaduRationTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "tn_smart_ration",
                DisplayName = "Tamil Nadu Smart Family Card (TNPDS)",
                Category = "STATE_RATION",
                DocumentType = "RATION",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "உணவுப்பொருள் வழங்கல்",
                        "உணவுப் பொருள்",
                        "Civil Supplies",
                        "TNPDS",
                        "குடும்ப அட்டை"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 15.0, Y = 140.0, Width = 85.60, Height = 53.98 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 105.0, Y = 140.0, Width = 85.60, Height = 53.98 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultDrivingLicenceTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "parivahan_dl",
                DisplayName = "Parivahan Driving Licence (DL)",
                Category = "TRANSPORT_IDENTITY",
                DocumentType = "DRIVING_LICENCE",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "UNION OF INDIA",
                        "DRIVING LICENCE",
                        "FORM 7",
                        "Transport Department",
                        "Licence No"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 20.0, Y = 80.0, Width = 85.60, Height = 53.98 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 110.0, Y = 80.0, Width = 85.60, Height = 53.98 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultVehicleRcTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "parivahan_rc",
                DisplayName = "Parivahan Certificate of Registration (RC)",
                Category = "TRANSPORT_IDENTITY",
                DocumentType = "VEHICLE_RC",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "CERTIFICATE OF REGISTRATION",
                        "FORM 23",
                        "Motor Vehicles Act",
                        "Chassis No",
                        "Engine No"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 20.0, Y = 90.0, Width = 85.60, Height = 53.98 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 110.0, Y = 90.0, Width = 85.60, Height = 53.98 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultEShramTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "eshram_card",
                DisplayName = "e-Shram National Worker Card",
                Category = "LABOUR_IDENTITY",
                DocumentType = "ESHRAM",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "e-Shram",
                        "Ministry of Labour and Employment",
                        "Universal Account Number",
                        "UAN"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 40.64, Y = 35.13, Width = 80.43, Height = 51.22 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 40.64, Y = 87.63, Width = 80.43, Height = 50.80 }
                    }
                }
            };
        }

        private static CardTemplate CreateDefaultAbhaTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "abha_health_id",
                DisplayName = "ABHA Health ID Card (ABDM)",
                Category = "HEALTH_IDENTITY",
                DocumentType = "ABHA",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string>
                    {
                        "Ayushman Bharat Digital Mission",
                        "ABHA Number",
                        "National Health Authority",
                        "ABHA Address"
                    },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 20.0, Y = 75.0, Width = 85.60, Height = 53.98 }
                    },
                    Back = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 110.0, Y = 75.0, Width = 85.60, Height = 53.98 }
                    }
                }
            };
        }

        #endregion
    }
}
