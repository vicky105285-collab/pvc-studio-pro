using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class TemplateManagerViewModel : ObservableObject
    {
        private readonly ITemplateService _templateService;

        [ObservableProperty]
        private CardTemplate? _selectedTemplate;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public ObservableCollection<CardTemplate> Templates { get; } = new ObservableCollection<CardTemplate>();

        public ICommand RefreshCommand { get; }
        public ICommand ExportTemplateCommand { get; }
        public ICommand ImportTemplateCommand { get; }
        public ICommand ValidateTemplateCommand { get; }

        public TemplateManagerViewModel(ITemplateService templateService)
        {
            _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));

            RefreshCommand = new AsyncRelayCommand(LoadTemplatesAsync);
            ExportTemplateCommand = new RelayCommand(ExportTemplate);
            ImportTemplateCommand = new AsyncRelayCommand(ImportTemplateAsync);
            ValidateTemplateCommand = new RelayCommand(ValidateCurrentTemplate);

            _ = LoadTemplatesAsync();
        }

        public async Task LoadTemplatesAsync()
        {
            try
            {
                await _templateService.InitializeTemplatesAsync();
                var list = _templateService.GetAllTemplates();
                Templates.Clear();
                foreach (var t in list)
                {
                    Templates.Add(t);
                }

                if (Templates.Count > 0 && SelectedTemplate == null)
                {
                    SelectedTemplate = Templates[0];
                }

                StatusMessage = $"Loaded {Templates.Count} Indian Government ID card templates.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private void ValidateCurrentTemplate()
        {
            if (SelectedTemplate == null) return;

            bool isValid = _templateService.ValidateTemplate(SelectedTemplate, out var errors);
            if (isValid)
            {
                StatusMessage = $"Template '{SelectedTemplate.DisplayName}' is VALID. All CR80 dimensional constraints satisfied.";
            }
            else
            {
                StatusMessage = $"Validation Error: {string.Join("; ", errors)}";
            }
        }

        private void ExportTemplate()
        {
            if (SelectedTemplate == null) return;

            var dlg = new SaveFileDialog
            {
                FileName = $"{SelectedTemplate.TemplateId}.json",
                Filter = "JSON Template (*.json)|*.json",
                Title = "Export Card Template Definition"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(SelectedTemplate, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dlg.FileName, json);
                    StatusMessage = $"Exported template to {dlg.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        private async Task ImportTemplateAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON Template (*.json)|*.json",
                Title = "Import Custom Card Template"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = await System.IO.File.ReadAllTextAsync(dlg.FileName);
                    var template = JsonSerializer.Deserialize<CardTemplate>(json);
                    if (template != null)
                    {
                        await _templateService.SaveCustomTemplateAsync(template);
                        await LoadTemplatesAsync();
                        SelectedTemplate = Templates.FirstOrDefault(t => t.TemplateId == template.TemplateId);
                        StatusMessage = $"Imported template: {template.DisplayName}";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Import failed: {ex.Message}";
                }
            }
        }
    }
}
