using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class DocumentTypeDetectionService : IDocumentTypeDetectionService
    {
        private readonly ITemplateService _templateService;

        public DocumentTypeDetectionService(ITemplateService templateService)
        {
            _templateService = templateService;
        }

        public Task<DocumentDetectionResult> DetectDocumentTypeAsync(
            SKBitmap? pageBitmap,
            string extractedText,
            double widthMm = 210.0,
            double heightMm = 297.0)
        {
            return Task.Run(() =>
            {
                var result = new DocumentDetectionResult();
                string text = extractedText?.ToLowerInvariant() ?? string.Empty;

                // 1. Check for Aadhaar
                var aadhaarKeywords = new[]
                {
                    "unique identification authority of india",
                    "uidai",
                    "enrolment no",
                    "enrollment no",
                    "mera aadhaar",
                    "meri pehchan",
                    "आधार",
                    "ஆதார்",
                    "vid:",
                    "virtual id",
                    "help@uidai.gov.in",
                    "myaadhaar"
                };

                int aadhaarMatches = aadhaarKeywords.Count(k => text.Contains(k));
                if (aadhaarMatches >= 1 || text.Contains("1947") && (text.Contains("aadhaar") || text.Contains("आधार")))
                {
                    result.DetectedKind = DocumentKind.Aadhaar;
                    result.Confidence = Math.Min(1.0, 0.6 + (aadhaarMatches * 0.1));
                    result.MatchedTokens = aadhaarKeywords.Where(k => text.Contains(k)).ToList();

                    // Detect Aadhaar Generation
                    if (text.Contains("order aadhaar pvc card") || text.Contains("pvc card request"))
                    {
                        result.AadhaarGen = AadhaarGeneration.PvcOrderLetter;
                        result.DisplayName = "UIDAI Aadhaar PVC Order Letter";
                        result.DetectionNotes = "Single-card reprint order format detected.";
                    }
                    else if (text.Contains("xxxx xxxx") || text.Contains("masked aadhaar"))
                    {
                        result.AadhaarGen = AadhaarGeneration.Masked2018To2022;
                        result.DisplayName = "UIDAI Masked e-Aadhaar (2018–2022)";
                        result.DetectionNotes = "Masked VID format detected with masked digit zones.";
                    }
                    else if (text.Contains("proof of identity, not of citizenship") || text.Contains("not a proof of date of birth"))
                    {
                        result.AadhaarGen = AadhaarGeneration.Modern2022Plus;
                        result.DisplayName = "UIDAI Modern e-Aadhaar (2022+)";
                        result.DetectionNotes = "Modern high-density QR layout detected with updated legal disclaimer.";
                    }
                    else
                    {
                        result.AadhaarGen = AadhaarGeneration.Pre2018;
                        result.DisplayName = "UIDAI Standard e-Aadhaar";
                        result.DetectionNotes = "Standard classic e-Aadhaar cut-out card detected.";
                    }

                    result.MatchedTemplate = _templateService.GetTemplateById("aadhaar_v2024");
                    return result;
                }

                // 2. Check for PAN Card
                var panKeywords = new[]
                {
                    "income tax department",
                    "govt. of india",
                    "permanent account number",
                    "incometaxindia",
                    "nsdl",
                    "utiitsl",
                    " आयकर विभाग"
                };
                int panMatches = panKeywords.Count(k => text.Contains(k));
                if (panMatches >= 1 || text.Contains("permanent account number card") || (text.Contains("income tax") && text.Contains("pan")))
                {
                    result.DetectedKind = DocumentKind.Pan;
                    result.DisplayName = text.Contains("utiitsl") ? "Income Tax PAN Card (UTIITSL)" : "Income Tax PAN Card (NSDL)";
                    result.Confidence = Math.Min(1.0, 0.6 + (panMatches * 0.15));
                    result.MatchedTokens = panKeywords.Where(k => text.Contains(k)).ToList();
                    result.MatchedTemplate = _templateService.GetTemplateById(text.Contains("utiitsl") ? "pan_utiitsl" : "pan_nsdl");
                    result.DetectionNotes = "National PAN card vector layout detected.";
                    return result;
                }

                // 3. Check for Voter ID (EPIC)
                var voterKeywords = new[]
                {
                    "election commission of india",
                    "elector's photo identity card",
                    "epic no",
                    "electoral registration officer",
                    "bharat nirvachan aayog"
                };
                int voterMatches = voterKeywords.Count(k => text.Contains(k));
                if (voterMatches >= 1 || text.Contains("voter id") || text.Contains("epic"))
                {
                    result.DetectedKind = DocumentKind.VoterId;
                    result.DisplayName = "Election Commission Voter ID (EPIC)";
                    result.Confidence = Math.Min(1.0, 0.6 + (voterMatches * 0.15));
                    result.MatchedTokens = voterKeywords.Where(k => text.Contains(k)).ToList();
                    result.MatchedTemplate = _templateService.GetTemplateById("voter_epic_v2023");
                    result.DetectionNotes = "ECI official Voter ID card layout detected.";
                    return result;
                }

                // 4. Check for Driving Licence
                var dlKeywords = new[]
                {
                    "driving licence",
                    "driving license",
                    "union of india",
                    "form 7",
                    "licensing authority",
                    "sarathi",
                    "transport department"
                };
                int dlMatches = dlKeywords.Count(k => text.Contains(k));
                if (dlMatches >= 1 || text.Contains("dl no:") || text.Contains("motor vehicle"))
                {
                    result.DetectedKind = DocumentKind.DrivingLicence;
                    result.DisplayName = "Indian Driving Licence (Sarathi / Form 7)";
                    result.Confidence = Math.Min(1.0, 0.6 + (dlMatches * 0.15));
                    result.MatchedTokens = dlKeywords.Where(k => text.Contains(k)).ToList();
                    result.MatchedTemplate = _templateService.GetTemplateById("driving_licence_mparivahan");
                    result.DetectionNotes = "MoRTH Sarathi smart driving licence layout.";
                    return result;
                }

                // 5. Check for Vehicle RC
                var rcKeywords = new[]
                {
                    "certificate of registration",
                    "form 23",
                    "vahan",
                    "chassis no",
                    "engine no",
                    "registration no"
                };
                int rcMatches = rcKeywords.Count(k => text.Contains(k));
                if (rcMatches >= 1 || (text.Contains("vahan") && text.Contains("rc")))
                {
                    result.DetectedKind = DocumentKind.VehicleRc;
                    result.DisplayName = "Vehicle Registration Certificate (RC / Form 23)";
                    result.Confidence = Math.Min(1.0, 0.6 + (rcMatches * 0.15));
                    result.MatchedTokens = rcKeywords.Where(k => text.Contains(k)).ToList();
                    result.MatchedTemplate = _templateService.GetTemplateById("vehicle_rc_v2023");
                    result.DetectionNotes = "Vahan motor vehicle RC certificate layout.";
                    return result;
                }

                // 6. Check for Ayushman Bharat / PM-JAY
                if (text.Contains("ayushman") || text.Contains("pm-jay") || text.Contains("pradhan mantri jan arogya yojana"))
                {
                    result.DetectedKind = DocumentKind.AyushmanBharat;
                    result.DisplayName = "Ayushman Bharat / PM-JAY Golden Card";
                    result.Confidence = 0.90;
                    result.MatchedTemplate = _templateService.GetTemplateById("ayushman_pmjay_v2");
                    result.DetectionNotes = "NHA PM-JAY healthcare beneficiary card.";
                    return result;
                }

                // 7. Check for ABHA
                if (text.Contains("abha number") || text.Contains("abha address") || text.Contains("health id"))
                {
                    result.DetectedKind = DocumentKind.Abha;
                    result.DisplayName = "ABHA (Ayushman Bharat Health Account)";
                    result.Confidence = 0.90;
                    result.MatchedTemplate = _templateService.GetTemplateById("abha_health_id");
                    result.DetectionNotes = "Digital India ABHA health card.";
                    return result;
                }

                // 8. Check for e-Shram
                if (text.Contains("e-shram") || text.Contains("eshram") || text.Contains("universal account number"))
                {
                    result.DetectedKind = DocumentKind.EShram;
                    result.DisplayName = "Ministry of Labour e-Shram Card";
                    result.Confidence = 0.90;
                    result.MatchedTemplate = _templateService.GetTemplateById("eshram_labour");
                    result.DetectionNotes = "National unorganized worker identity card.";
                    return result;
                }

                // 9. Check for Tamil Nadu Ration Card
                if (text.Contains("tnepds") || text.Contains("civil supplies") || text.Contains("குடும்ப அட்டை") || text.Contains("ration card"))
                {
                    result.DetectedKind = DocumentKind.TamilNaduRation;
                    result.DisplayName = "Tamil Nadu Smart Family Card (TNEPDS)";
                    result.Confidence = 0.85;
                    result.MatchedTemplate = _templateService.GetTemplateById("tn_smart_ration_card");
                    result.DetectionNotes = "Tamil Nadu civil supplies food security smart card.";
                    return result;
                }

                // 10. Fallback: match via template service keywords
                var tm = _templateService.MatchTemplate(extractedText ?? string.Empty, widthMm, heightMm);
                if (tm.IsSuccess && tm.Template != null)
                {
                    result.DetectedKind = DocumentKind.GenericId;
                    result.DisplayName = tm.Template.DisplayName;
                    result.Confidence = tm.ConfidenceScore;
                    result.MatchedTemplate = tm.Template;
                    result.MatchedTokens = tm.MatchedKeywords.ToList();
                    result.DetectionNotes = $"Matched via keyword index: {tm.MatchReason}";
                    return result;
                }

                // If text is not empty, label as general Office/Print document
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    result.DetectedKind = DocumentKind.OfficeDocument;
                    result.DisplayName = "General Text / Office Document";
                    result.Confidence = 0.60;
                    result.DetectionNotes = "Standard document with printable text.";
                    return result;
                }

                result.DetectedKind = DocumentKind.Unknown;
                result.DisplayName = "Unknown / Unclassified Document";
                result.Confidence = 0.0;
                result.DetectionNotes = "No recognized identity anchors or text extracted. Use Manual Crop Studio.";
                return result;
            });
        }
    }
}
