using System.IO;
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
        public async Task InitializeTemplates_ShouldLoadAllDefaultIndianGovernmentTemplates()
        {
            var service = new TemplateService();
            await service.InitializeTemplatesAsync();

            var templates = service.GetAllTemplates();
            templates.Should().NotBeEmpty();
            templates.Should().Contain(t => t.Category == "AADHAAR");
            templates.Should().Contain(t => t.Category == "PAN");
            templates.Should().Contain(t => t.Category == "VOTER");
            templates.Should().Contain(t => t.Category == "AYUSHMAN");
            templates.Should().Contain(t => t.Category == "STATE_GOVT");
        }

        [Fact]
        public async Task MatchTemplateByText_ShouldIdentifyAadhaarFromKeywords()
        {
            var service = new TemplateService();
            await service.InitializeTemplatesAsync();

            string sampleText = "Government of India Unique Identification Authority of India Enrollment No 1234/5678";
            var matched = service.MatchTemplateByText(sampleText);

            matched.Should().NotBeNull();
            matched.Category.Should().Be("AADHAAR");
        }

        [Fact]
        public async Task MatchTemplateByText_ShouldIdentifyTamilNaduRationCard()
        {
            var service = new TemplateService();
            await service.InitializeTemplatesAsync();

            string sampleText = "தமிழ்நாடு அரசு உணவுப் பொருள் வழங்கல் துறை குடும்ப அட்டை Civil Supplies";
            var matched = service.MatchTemplateByText(sampleText);

            matched.Should().NotBeNull();
            matched.Category.Should().Be("STATE_GOVT");
            matched.TemplateId.Should().Be("in_tn_smart_ration");
        }
    }
}
