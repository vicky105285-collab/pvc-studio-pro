using System;
using System.Collections.Generic;

namespace PVCStudioPro.Core.Models
{
    public class TemplateCategoryGroup
    {
        public string CategoryKey { get; set; } = string.Empty;
        public string CategoryDisplayName { get; set; } = string.Empty;
        public List<CardTemplate> Templates { get; set; } = new();
    }

    public class TemplateOptionItem
    {
        public string TemplateId { get; set; } = string.Empty;
        public string CategoryDisplayName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public CardTemplate Template { get; set; } = null!;

        public string FormattedLabel => $"[{CategoryDisplayName}] {DisplayName}";

        public override string ToString() => FormattedLabel;
    }

    public class TemplateSelectionEventArgs : EventArgs
    {
        public CardTemplate SelectedTemplate { get; }
        public bool IsManualOverride { get; }

        public TemplateSelectionEventArgs(CardTemplate selectedTemplate, bool isManualOverride = true)
        {
            SelectedTemplate = selectedTemplate ?? throw new ArgumentNullException(nameof(selectedTemplate));
            IsManualOverride = isManualOverride;
        }
    }
}
