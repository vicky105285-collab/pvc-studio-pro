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
            // Core Identity Templates
            _templates.Add(CreateDefaultAadhaarTemplate());
            _templates.Add(CreateDefaultChildAadhaarTemplate());
            _templates.Add(CreateDefaultMAadhaarTemplate());
            _templates.Add(CreateDefaultPanNsdlTemplate());
            _templates.Add(CreateDefaultPanUtiitslTemplate());
            _templates.Add(CreateDefaultPanInstantTemplate());
            _templates.Add(CreateDefaultVoterTemplate());

            // Healthcare & Welfare Templates
            _templates.Add(CreateDefaultAyushmanTemplate());
            _templates.Add(CreateDefaultAyushmanPmjayTemplate());
            _templates.Add(CreateDefaultAyushmanNewTemplate());
            _templates.Add(CreateDefaultAbhaTemplate());
            _templates.Add(CreateDefaultCmInsuranceTemplate());
            _templates.Add(CreateDefaultUmidTemplate());

            // Labour & Pension Templates
            _templates.Add(CreateDefaultEShramTemplate());
            _templates.Add(CreateDefaultPmSymTemplate());
            _templates.Add(CreateDefaultPmKmyTemplate());
            _templates.Add(CreateDefaultPfInsuranceTemplate());
            _templates.Add(CreateDefaultUanTemplate());
            _templates.Add(CreateDefaultEPranTemplate());

            // Civil, Social & State Identity Templates
            _templates.Add(CreateDefaultUdidTemplate());
            _templates.Add(CreateDefaultSeniorCitizenTemplate());
            _templates.Add(CreateDefaultJanAadhaarTemplate());
            _templates.Add(CreateDefaultPravasiCardTemplate());
            _templates.Add(CreateDefaultVaccinationTemplate());
            _templates.Add(CreateDefaultTamilNaduRationTemplate());
            _templates.Add(CreateDefaultKeralaRationTemplate());
            _templates.Add(CreateDefaultWbRationTemplate());

            // Transport & Vehicle
            _templates.Add(CreateDefaultDrivingLicenceTemplate());
            _templates.Add(CreateDefaultVehicleRcTemplate());
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

        public IReadOnlyList<TemplateCategoryGroup> GetCategorizedTemplates()
        {
            var identityTemplates = new List<CardTemplate>();
            var healthTemplates = new List<CardTemplate>();
            var labourTemplates = new List<CardTemplate>();
            var stateTemplates = new List<CardTemplate>();
            var transportTemplates = new List<CardTemplate>();

            foreach (var t in _templates)
            {
                string id = t.TemplateId.ToLowerInvariant();
                string cat = t.Category.ToUpperInvariant();

                if (id.Contains("dl") || id.Contains("driving") || id.Contains("rc") || cat.Contains("TRANSPORT"))
                {
                    transportTemplates.Add(t);
                }
                else if (id.Contains("ayushman") || id.Contains("abha") || id.Contains("insurance") || id.Contains("umid") || cat == "HEALTHCARE_WELFARE" || cat == "HEALTH_IDENTITY")
                {
                    healthTemplates.Add(t);
                }
                else if (id.Contains("shram") || id.Contains("sym") || id.Contains("kmy") || id.Contains("uan") || id.Contains("pran") || id.Contains("pf") || cat == "LABOUR_PENSION")
                {
                    labourTemplates.Add(t);
                }
                else if (id.Contains("ration") || id.Contains("jan_aadhaar") || id.Contains("udid") || id.Contains("senior") || id.Contains("pravasi") || id.Contains("vaccination") || cat == "STATE_CIVIL_WELFARE")
                {
                    stateTemplates.Add(t);
                }
                else
                {
                    identityTemplates.Add(t);
                }
            }

            var groups = new List<TemplateCategoryGroup>
            {
                new TemplateCategoryGroup
                {
                    CategoryKey = "IDENTITY",
                    CategoryDisplayName = "Identity",
                    Templates = identityTemplates
                },
                new TemplateCategoryGroup
                {
                    CategoryKey = "HEALTHCARE",
                    CategoryDisplayName = "Health",
                    Templates = healthTemplates
                },
                new TemplateCategoryGroup
                {
                    CategoryKey = "LABOUR",
                    CategoryDisplayName = "Labour",
                    Templates = labourTemplates
                },
                new TemplateCategoryGroup
                {
                    CategoryKey = "STATE_RATIONS",
                    CategoryDisplayName = "State Rations",
                    Templates = stateTemplates
                },
                new TemplateCategoryGroup
                {
                    CategoryKey = "TRANSPORT",
                    CategoryDisplayName = "Transport",
                    Templates = transportTemplates
                }
            };

            return groups.AsReadOnly();
        }

        public IReadOnlyList<TemplateOptionItem> GetTemplateOptions()
        {
            var options = new List<TemplateOptionItem>();
            var groups = GetCategorizedTemplates();

            foreach (var g in groups)
            {
                foreach (var t in g.Templates)
                {
                    options.Add(new TemplateOptionItem
                    {
                        TemplateId = t.TemplateId,
                        CategoryDisplayName = g.CategoryDisplayName,
                        DisplayName = t.DisplayName,
                        Template = t
                    });
                }
            }

            return options.AsReadOnly();
        }

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

        
        private static CardTemplate CreateDefaultChildAadhaarTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "child_aadhaar",
                DisplayName = "Baal Aadhaar (Child Aadhaar)",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Unique Identification Authority of India", "Baal Aadhaar", "Child", "बाल आधार" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 12.28, Y = 204.05, Width = 91.02, Height = 57.15 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 107.10, Y = 204.05, Width = 91.02, Height = 58.42 } }
                }
            };
        }

        private static CardTemplate CreateDefaultMAadhaarTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "m_aadhaar",
                DisplayName = "mAadhaar Mobile Card",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "mAadhaar", "Unique Identification Authority of India", "UIDAI" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 10.58, Y = 163.83, Width = 84.24, Height = 55.88 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 98.64, Y = 163.83, Width = 84.24, Height = 55.88 } }
                }
            };
        }

        private static CardTemplate CreateDefaultPanInstantTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "pan_instant",
                DisplayName = "Instant e-PAN Card",
                Category = "TAX_IDENTITY",
                DocumentType = "PAN",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "INCOME TAX DEPARTMENT", "Permanent Account Number", "e-PAN", "Instant PAN" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 12.70, Y = 220.98, Width = 88.90, Height = 56.73 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 107.95, Y = 220.98, Width = 88.90, Height = 56.73 } }
                }
            };
        }

        private static CardTemplate CreateDefaultCmInsuranceTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "cm_insurance",
                DisplayName = "Chief Minister Comprehensive Health Insurance (CMCHIS)",
                Category = "HEALTH_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Chief Minister", "Comprehensive Health Insurance", "Amma", "Maruthuva Kapitu" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 12.87, Y = 13.12, Width = 84.50, Height = 55.71 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 99.65, Y = 12.28, Width = 85.09, Height = 57.15 } }
                }
            };
        }

        private static CardTemplate CreateDefaultPmSymTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "pm_sym",
                DisplayName = "PM Shram Yogi Maan-dhan (PM-SYM)",
                Category = "LABOUR_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Pradhan Mantri Shram Yogi", "PM-SYM", "Maan-dhan", "Labour and Employment" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 25.99, Y = 37.25, Width = 78.32, Height = 51.22 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 115.32, Y = 37.25, Width = 78.32, Height = 51.22 } }
                }
            };
        }

        private static CardTemplate CreateDefaultPmKmyTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "pm_kmy",
                DisplayName = "PM Kisan Maan-dhan Yojana (PM-KMY)",
                Category = "AGRICULTURE_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Pradhan Mantri Kisan", "PM-KMY", "Kisan Maan-dhan", "Agriculture" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 15.41, Y = 31.75, Width = 93.13, Height = 60.54 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 110.24, Y = 31.92, Width = 93.13, Height = 60.54 } }
                }
            };
        }

        private static CardTemplate CreateDefaultPfInsuranceTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "pf_insurance",
                DisplayName = "EPFO Member Card",
                Category = "LABOUR_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Employees' Provident Fund", "EPFO", "Member ID", "Provident Fund" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 12.70, Y = 30.90, Width = 97.79, Height = 60.11 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 12.70, Y = 120.23, Width = 97.79, Height = 60.11 } }
                }
            };
        }

        private static CardTemplate CreateDefaultUanTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "uan_card",
                DisplayName = "EPFO Universal Account Number (UAN)",
                Category = "LABOUR_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Universal Account Number", "UAN", "Employees' Provident Fund", "EPFO" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 7.62, Y = 8.47, Width = 136.74, Height = 92.29 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 152.40, Y = 9.31, Width = 136.74, Height = 91.02 } }
                }
            };
        }

        private static CardTemplate CreateDefaultAyushmanPmjayTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "ayushman_pmjay_state",
                DisplayName = "Ayushman Bharat PM-JAY (State Scheme)",
                Category = "HEALTH_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "PM-JAY", "Pradhan Mantri Jan Arogya Yojana", "Ayushman Bharat", "NHA" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 37.68, Y = 33.02, Width = 107.53, Height = 54.61 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 158.33, Y = 33.02, Width = 105.83, Height = 55.46 } }
                }
            };
        }

        private static CardTemplate CreateDefaultAyushmanNewTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "ayushman_new",
                DisplayName = "Ayushman Card (BIS 2.0 / New Format)",
                Category = "HEALTH_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "National Health Authority", "Pradhan Mantri", "Jan Arogya Yojana", "5 Lakh" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 10.16, Y = 22.86, Width = 88.05, Height = 56.30 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 114.30, Y = 22.86, Width = 88.05, Height = 56.30 } }
                }
            };
        }

        private static CardTemplate CreateDefaultUmidTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "umid_railways",
                DisplayName = "Indian Railways UMID Medical Card",
                Category = "HEALTH_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Unique Medical Identification", "UMID", "Indian Railways", "Railway Employee" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 0.00, Y = 242.57, Width = 86.36, Height = 54.19 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 0.00, Y = 242.57, Width = 86.36, Height = 54.19 } }
                }
            };
        }

        private static CardTemplate CreateDefaultEPranTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "e_pran",
                DisplayName = "NPS e-PRAN Card",
                Category = "PENSION_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Permanent Retirement Account Number", "e-PRAN", "National Pension System", "PFRDA" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 19.47, Y = 22.01, Width = 146.47, Height = 93.13 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 169.76, Y = 22.01, Width = 146.47, Height = 93.13 } }
                }
            };
        }

        private static CardTemplate CreateDefaultUdidTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "udid_card",
                DisplayName = "Unique Disability ID (UDID/Swavlamban)",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Unique Disability ID", "Department of Empowerment of Persons with Disabilities", "Swavlamban", "Disability" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 17.78, Y = 17.78, Width = 89.32, Height = 55.88 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 17.78, Y = 96.52, Width = 89.32, Height = 55.88 } }
                }
            };
        }

        private static CardTemplate CreateDefaultPravasiCardTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "pravasi_card",
                DisplayName = "Pravasi Bharatiya Card",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Pravasi", "Non-Resident", "Overseas", "Pravasi Card" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 18.20, Y = 158.75, Width = 88.90, Height = 57.57 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 113.03, Y = 158.75, Width = 88.90, Height = 57.57 } }
                }
            };
        }

        private static CardTemplate CreateDefaultSeniorCitizenTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "senior_citizen",
                DisplayName = "Senior Citizen Identity Card",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Senior Citizen", "Identity Card", "Social Welfare", "Age Proof" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 11.26, Y = 27.52, Width = 191.77, Height = 120.23 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 12.11, Y = 162.98, Width = 191.77, Height = 101.60 } }
                }
            };
        }

        private static CardTemplate CreateDefaultJanAadhaarTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "jan_aadhaar",
                DisplayName = "Rajasthan Jan-Aadhaar Card",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Jan Aadhaar", "जन आधार", "Rajasthan", "Jan-Aadhaar" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 12.28, Y = 183.30, Width = 91.02, Height = 57.57 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 106.26, Y = 183.30, Width = 92.29, Height = 57.57 } }
                }
            };
        }

        private static CardTemplate CreateDefaultVaccinationTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "vaccination_card",
                DisplayName = "Co-WIN Vaccination Certificate Card",
                Category = "HEALTH_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "COVID-19 Vaccination", "Co-WIN", "Ministry of Health", "Beneficiary Details" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 4.23, Y = 2.96, Width = 205.32, Height = 119.38 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 4.23, Y = 117.26, Width = 205.32, Height = 179.07 } }
                }
            };
        }

        private static CardTemplate CreateDefaultKeralaRationTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "kerala_ration",
                DisplayName = "Kerala Civil Supplies Digital Ration Card",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Civil Supplies", "Kerala", "Ration Card", "Priority Household" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 5.50, Y = 96.52, Width = 110.49, Height = 66.46 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 5.50, Y = 196.85, Width = 110.91, Height = 66.46 } }
                }
            };
        }

        private static CardTemplate CreateDefaultWbRationTemplate()
        {
            return new CardTemplate
            {
                TemplateId = "wb_ration",
                DisplayName = "West Bengal Digital Ration Card",
                Category = "GOVERNMENT_IDENTITY",
                DocumentType = "PDF",
                Identification = new TemplateIdentification
                {
                    AnchorKeywords = new List<string> { "Department of Food and Supplies", "West Bengal", "Digital Ration Card", "Ration Card" },
                    MinKeywordMatches = 2,
                    ExpectedPages = 1
                },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 19.05, Y = 235.37, Width = 82.55, Height = 49.95 } },
                    Back = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 108.80, Y = 235.37, Width = 83.40, Height = 49.95 } }
                }
            };
        }

        #endregion
    }
}
