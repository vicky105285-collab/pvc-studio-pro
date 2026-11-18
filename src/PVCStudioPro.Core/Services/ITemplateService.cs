using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.Core.Services
{
    public interface ITemplateService
    {
        Task InitializeTemplatesAsync();
        IReadOnlyList<CardTemplate> GetAllTemplates();
        CardTemplate? GetTemplateById(string templateId);
        TemplateMatchResult MatchTemplate(string extractedText, double pageWidthMm = 0, double pageHeightMm = 0);
        TemplateMatchResult MatchTemplateByText(string extractedText);
        Task SaveCustomTemplateAsync(CardTemplate template);
        bool ValidateTemplate(CardTemplate template, out List<string> validationErrors);
    }
}
