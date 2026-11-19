using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class TemplateServiceTests
    {
        [Fact]
        public async Task InitializeTemplates_ShouldLoadAll10AgreedIndianGovernmentTemplates()
        {
            var service = new TemplateService();
            await service.InitializeTemplatesAsync();

            var templates = service.GetAllTemplates().ToList();
            templates.Should().HaveCountGreaterThanOrEqualTo(25);

            // Verify each specific agreed template is present
            templates.Should().Contain(t => t.TemplateId == "aadhaar_v2024");
            templates.Should().Contain(t => t.TemplateId == "pan_nsdl");
            templates.Should().Contain(t => t.TemplateId == "pan_utiitsl");
            templates.Should().Contain(t => t.TemplateId == "voter_epic_v2");
            templates.Should().Contain(t => t.TemplateId == "ayushman_pmjay");
            templates.Should().Contain(t => t.TemplateId == "tn_smart_ration");
            templates.Should().Contain(t => t.TemplateId == "parivahan_dl");
            templates.Should().Contain(t => t.TemplateId == "parivahan_rc");
            templates.Should().Contain(t => t.TemplateId == "eshram_card");
            templates.Should().Contain(t => t.TemplateId == "abha_health_id");
        }

        [Theory]
        [InlineData("Government of India Unique Identification Authority of India Enrollment No 1234/5678", "aadhaar_v2024")]
        [InlineData("INCOME TAX DEPARTMENT GOVT OF INDIA Permanent Account Number NSDL e-Gov", "pan_nsdl")]
        [InlineData("INCOME TAX DEPARTMENT UTIITSL UTI Infrastructure Technology Services Ltd", "pan_utiitsl")]
        [InlineData("ELECTION COMMISSION OF INDIA ELECTOR PHOTO IDENTITY CARD EPIC NO ABC1234567", "voter_epic_v2")]
        [InlineData("Ayushman Bharat Pradhan Mantri Jan Arogya Yojana PM-JAY NHA National Health Authority", "ayushman_pmjay")]
        [InlineData("தமிழ்நாடு அரசு உணவுப் பொருள் வழங்கல் துறை குடும்ப அட்டை TNPDS Civil Supplies", "tn_smart_ration")]
        [InlineData("UNION OF INDIA DRIVING LICENCE FORM 7 Transport Department Authorisation to Drive", "parivahan_dl")]
        [InlineData("CERTIFICATE OF REGISTRATION FORM 23 Chassis Number Engine Number Fuel", "parivahan_rc")]
        [InlineData("e-Shram Card Ministry of Labour and Employment Universal Account Number UAN", "eshram_card")]
        [InlineData("Ayushman Bharat Health Account ABHA Number ABHA Address National Health Authority", "abha_health_id")]
        public async Task MatchTemplate_ShouldCorrectlyIdentifySpecificTemplates(string text, string expectedTemplateId)
        {
            var service = new TemplateService();
            await service.InitializeTemplatesAsync();

            var match = service.MatchTemplate(text);

            match.IsSuccess.Should().BeTrue();
            match.Template.Should().NotBeNull();
            match.Template!.TemplateId.Should().Be(expectedTemplateId);
            match.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.5);
        }

        [Fact]
        public async Task MatchTemplate_UnknownDocument_ShouldReturnExplicitFailureWithoutSilentAadhaarFallback()
        {
            var service = new TemplateService();
            await service.InitializeTemplatesAsync();

            string unrelatedText = "Electricity Board Monthly Consumption Bill Payment Receipt Consumer No 98765 Units 240 Total Amount 1200";

            var match = service.MatchTemplate(unrelatedText);

            match.IsSuccess.Should().BeFalse();
            match.Template.Should().BeNull();
            match.ConfidenceScore.Should().Be(0.0);
            match.MatchReason.Should().Contain("did not match any supported");
        }

        [Fact]
        public async Task MatchTemplate_AmbiguousText_ShouldFlagRequiresOperatorConfirmation()
        {
            var service = new TemplateService();
            await service.InitializeTemplatesAsync();

            // Text with only a single weak keyword match
            string ambiguousText = "India Form Government Office Document";

            var match = service.MatchTemplate(ambiguousText);

            // Either not matched or marked requiring operator confirmation
            if (match.IsSuccess)
            {
                match.RequiresOperatorConfirmation.Should().BeTrue();
                match.ConfidenceScore.Should().BeLessThan(0.70);
            }
            else
            {
                match.Template.Should().BeNull();
            }
        }

        [Fact]
        public void ValidateTemplate_ShouldAcceptValidCr80Template()
        {
            var service = new TemplateService();
            var validTemplate = new CardTemplate
            {
                TemplateId = "custom_test_template",
                DisplayName = "Custom Test",
                DocumentType = "CUSTOM",
                PhysicalCard = new PhysicalCardSpec { WidthMm = 85.60, HeightMm = 53.98 },
                Faces = new CardFaces
                {
                    Front = new FaceDefinition
                    {
                        SourcePage = 0,
                        CropRectMm = new MillimeterRect { X = 10, Y = 10, Width = 85.6, Height = 53.98 }
                    }
                }
            };

            bool isValid = service.ValidateTemplate(validTemplate, out var errors);

            isValid.Should().BeTrue();
            errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData("", 85.60, 53.98, true, "TemplateId cannot be empty")]
        [InlineData("valid_id", 0.0, 53.98, true, "Invalid PhysicalCard.WidthMm")]
        [InlineData("valid_id", 85.60, -10.0, true, "Invalid PhysicalCard.HeightMm")]
        public void ValidateTemplate_ShouldRejectInvalidTemplates(string id, double w, double h, bool hasFront, string expectedErrorSubstring)
        {
            var service = new TemplateService();
            var template = new CardTemplate
            {
                TemplateId = id,
                DisplayName = "Test",
                DocumentType = "TEST",
                PhysicalCard = new PhysicalCardSpec { WidthMm = w, HeightMm = h },
                Faces = hasFront ? new CardFaces
                {
                    Front = new FaceDefinition { SourcePage = 0, CropRectMm = new MillimeterRect { X = 10, Y = 10, Width = 85.6, Height = 53.98 } }
                } : new CardFaces()
            };

            bool isValid = service.ValidateTemplate(template, out var errors);

            isValid.Should().BeFalse();
            errors.Should().Contain(e => e.Contains(expectedErrorSubstring));
        }
    }
}
