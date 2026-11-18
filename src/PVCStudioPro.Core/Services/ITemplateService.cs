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
        CardTemplate MatchTemplateByText(string extractedText);
        Task SaveCustomTemplateAsync(CardTemplate template);
    }
}
