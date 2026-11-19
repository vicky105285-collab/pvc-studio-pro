using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using PVCStudioPro.App.Converters;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.ViewModels
{
    public partial class DocumentPageDesignerViewModel : ObservableObject
    {
        private readonly IPhysicalPageRenderService _renderService;

        public Action<PageLayoutModel>? OpenInPrintPreviewAction { get; set; }
        public Action? CloseAction { get; set; }

        [ObservableProperty]
        private PageLayoutModel _layout = PageLayoutModel.CreateDefaultA4();

        [ObservableProperty]
        private ObservableCollection<PageLayoutItem> _items = new();

        [ObservableProperty]
        private PageLayoutItem? _selectedItem;

        [ObservableProperty]
        private ObservableCollection<string> _paperPresets = new()
        {
            "A4 Sheet (210 x 297 mm)",
            "A5 Sheet (148 x 210 mm)",
            "4x6 Photo Paper (101.6 x 152.4 mm)",
            "Epson 2-Card Direct Tray (120 x 180 mm)",
            "Canon Multi-Card Tray (130 x 220 mm)",
            "Direct CR80 PVC Card (85.60 x 53.98 mm)"
        };

        [ObservableProperty]
        private string _selectedPaperPreset = "A4 Sheet (210 x 297 mm)";

        [ObservableProperty]
        private BitmapSource? _canvasPreviewImage;

        [ObservableProperty]
        private double _canvasDisplayWidth = 600;

        [ObservableProperty]
        private double _canvasDisplayHeight = 848;

        // Numeric mm properties for selected item
        [ObservableProperty]
        private double _itemXPositionMm = 10.0;

        [ObservableProperty]
        private double _itemYPositionMm = 10.0;

        [ObservableProperty]
        private double _itemWidthMm = 85.60;

        [ObservableProperty]
        private double _itemHeightMm = 53.98;

        [ObservableProperty]
        private double _itemRotation = 0.0;

        [ObservableProperty]
        private bool _itemLockAspect = true;

        // View options
        [ObservableProperty]
        private bool _showGrid = true;

        [ObservableProperty]
        private double _snapGridMm = 5.0;

        [ObservableProperty]
        private bool _showRulers = true;

        [ObservableProperty]
        private bool _showCutMarks = true;

        [ObservableProperty]
        private bool _showFoldLine = false;

        [ObservableProperty]
        private bool _mirrorMode = false;

        [ObservableProperty]
        private double _zoomFactor = 1.0;

        [ObservableProperty]
        private string _statusMessage = "Document Page Designer Ready | Physical mm layout engine active";

        [ObservableProperty]
        private bool _isBusy = false;

        private readonly Stack<string> _undoStack = new();
        private readonly Stack<string> _redoStack = new();
        private bool _isApplyingUndoRedo = false;

        public DocumentPageDesignerViewModel(IPhysicalPageRenderService renderService)
        {
            _renderService = renderService;
            RefreshCanvas();
        }

        public void LoadCardsIntoDesigner(SKBitmap? front, SKBitmap? back, string title = "Document Cards")
        {
            PushUndoState();
            Layout = PageLayoutModel.CreateDefaultA4();
            Layout.Title = title;
            Items.Clear();

            if (front != null)
            {
                var frontItem = new PageLayoutItem
                {
                    Label = "Front Card Face",
                    Kind = LayoutItemKind.FrontCard,
                    XPositionMm = 15.0,
                    YPositionMm = 20.0,
                    WidthMm = 85.60,
                    HeightMm = 53.98,
                    Bitmap = front.Copy()
                };
                Items.Add(frontItem);
                SelectedItem = frontItem;
            }

            if (back != null)
            {
                var backItem = new PageLayoutItem
                {
                    Label = "Back Card Face",
                    Kind = LayoutItemKind.BackCard,
                    XPositionMm = 15.0 + 85.60 + 5.0, // Placed adjacent horizontally
                    YPositionMm = 20.0,
                    WidthMm = 85.60,
                    HeightMm = 53.98,
                    Bitmap = back.Copy()
                };
                Items.Add(backItem);
            }

            Layout.Items = Items.ToList();
            StatusMessage = $"Imported cards into Page Designer: {title}";
            RefreshCanvas();
        }

        [RelayCommand]
        public void ApplyPaperPreset(string presetName)
        {
            PushUndoState();
            if (presetName.StartsWith("A4", StringComparison.OrdinalIgnoreCase))
            {
                Layout.PageWidthMm = 210.0;
                Layout.PageHeightMm = 297.0;
                Layout.Preset = PaperPreset.A4;
            }
            else if (presetName.StartsWith("A5", StringComparison.OrdinalIgnoreCase))
            {
                Layout.PageWidthMm = 148.0;
                Layout.PageHeightMm = 210.0;
                Layout.Preset = PaperPreset.A5;
            }
            else if (presetName.StartsWith("4x6", StringComparison.OrdinalIgnoreCase))
            {
                Layout.PageWidthMm = 101.6;
                Layout.PageHeightMm = 152.4;
                Layout.Preset = PaperPreset.Photo4x6;
            }
            else if (presetName.StartsWith("Epson", StringComparison.OrdinalIgnoreCase))
            {
                Layout.PageWidthMm = 120.0;
                Layout.PageHeightMm = 180.0;
                Layout.Preset = PaperPreset.EpsonTray;
            }
            else if (presetName.StartsWith("Canon", StringComparison.OrdinalIgnoreCase))
            {
                Layout.PageWidthMm = 130.0;
                Layout.PageHeightMm = 220.0;
                Layout.Preset = PaperPreset.CanonTray;
            }
            else if (presetName.StartsWith("Direct", StringComparison.OrdinalIgnoreCase))
            {
                Layout.PageWidthMm = 85.60;
                Layout.PageHeightMm = 53.98;
                Layout.Preset = PaperPreset.DirectCr80;
            }

            StatusMessage = $"Canvas resized to {Layout.PageWidthMm:F1} x {Layout.PageHeightMm:F1} mm";
            RefreshCanvas();
        }

        partial void OnSelectedItemChanged(PageLayoutItem? value)
        {
            if (value != null)
            {
                ItemXPositionMm = value.XPositionMm;
                ItemYPositionMm = value.YPositionMm;
                ItemWidthMm = value.WidthMm;
                ItemHeightMm = value.HeightMm;
                ItemRotation = value.RotationDegrees;
                ItemLockAspect = value.LockAspectRatio;
            }
        }

        [RelayCommand]
        public void UpdateSelectedItemProperties()
        {
            if (SelectedItem == null) return;
            PushUndoState();

            SelectedItem.XPositionMm = ItemXPositionMm;
            SelectedItem.YPositionMm = ItemYPositionMm;
            SelectedItem.WidthMm = ItemWidthMm;
            SelectedItem.HeightMm = ItemHeightMm;
            SelectedItem.RotationDegrees = ItemRotation;
            SelectedItem.LockAspectRatio = ItemLockAspect;

            RefreshCanvas();
        }

        [RelayCommand]
        public void DuplicateSelectedItem()
        {
            if (SelectedItem == null) return;
            PushUndoState();

            var clone = SelectedItem.Clone();
            clone.Label = $"{SelectedItem.Label} (Copy)";
            clone.XPositionMm += 5.0; // Offset copy slightly
            clone.YPositionMm += 5.0;

            Items.Add(clone);
            SelectedItem = clone;
            Layout.Items = Items.ToList();
            StatusMessage = $"Duplicated item: {clone.Label}";
            RefreshCanvas();
        }

        [RelayCommand]
        public void DeleteSelectedItem()
        {
            if (SelectedItem == null) return;
            PushUndoState();

            var label = SelectedItem.Label;
            Items.Remove(SelectedItem);
            SelectedItem = Items.LastOrDefault();
            Layout.Items = Items.ToList();
            StatusMessage = $"Removed item: {label}";
            RefreshCanvas();
        }

        [RelayCommand]
        public void BringForward()
        {
            if (SelectedItem == null) return;
            PushUndoState();
            SelectedItem.ZIndex++;
            Layout.Items = Items.OrderBy(i => i.ZIndex).ToList();
            RefreshCanvas();
        }

        [RelayCommand]
        public void SendBackward()
        {
            if (SelectedItem == null) return;
            PushUndoState();
            SelectedItem.ZIndex--;
            Layout.Items = Items.OrderBy(i => i.ZIndex).ToList();
            RefreshCanvas();
        }

        #region Alignment Tools

        [RelayCommand]
        public void AlignLeft()
        {
            if (SelectedItem == null) return;
            PushUndoState();
            SelectedItem.XPositionMm = Layout.MarginLeftMm;
            ItemXPositionMm = SelectedItem.XPositionMm;
            RefreshCanvas();
        }

        [RelayCommand]
        public void AlignCenter()
        {
            if (SelectedItem == null) return;
            PushUndoState();
            SelectedItem.XPositionMm = (Layout.PageWidthMm - SelectedItem.WidthMm) / 2.0;
            ItemXPositionMm = SelectedItem.XPositionMm;
            RefreshCanvas();
        }

        [RelayCommand]
        public void AlignRight()
        {
            if (SelectedItem == null) return;
            PushUndoState();
            SelectedItem.XPositionMm = Layout.PageWidthMm - Layout.MarginRightMm - SelectedItem.WidthMm;
            ItemXPositionMm = SelectedItem.XPositionMm;
            RefreshCanvas();
        }

        [RelayCommand]
        public void AlignTop()
        {
            if (SelectedItem == null) return;
            PushUndoState();
            SelectedItem.YPositionMm = Layout.MarginTopMm;
            ItemYPositionMm = SelectedItem.YPositionMm;
            RefreshCanvas();
        }

        [RelayCommand]
        public void AlignMiddle()
        {
            if (SelectedItem == null) return;
            PushUndoState();
            SelectedItem.YPositionMm = (Layout.PageHeightMm - SelectedItem.HeightMm) / 2.0;
            ItemYPositionMm = SelectedItem.YPositionMm;
            RefreshCanvas();
        }

        [RelayCommand]
        public void AlignBottom()
        {
            if (SelectedItem == null) return;
            PushUndoState();
            SelectedItem.YPositionMm = Layout.PageHeightMm - Layout.MarginBottomMm - SelectedItem.HeightMm;
            ItemYPositionMm = SelectedItem.YPositionMm;
            RefreshCanvas();
        }

        [RelayCommand]
        public void AutoArrangeA4()
        {
            PushUndoState();
            Layout.PageWidthMm = 210.0;
            Layout.PageHeightMm = 297.0;
            Layout.Preset = PaperPreset.A4;

            // Arrange cards in 2 columns x N rows (typical 10-card A4 sheet layout)
            double col1X = 14.0;
            double col2X = 14.0 + 85.60 + 10.0; // 109.6 mm
            double startY = 15.0;
            double rowSpacing = 53.98 + 4.0; // 57.98 mm

            for (int i = 0; i < Items.Count; i++)
            {
                int col = i % 2;
                int row = i / 2;
                Items[i].XPositionMm = col == 0 ? col1X : col2X;
                Items[i].YPositionMm = startY + (row * rowSpacing);
                Items[i].WidthMm = 85.60;
                Items[i].HeightMm = 53.98;
                Items[i].RotationDegrees = 0;
            }

            StatusMessage = $"Auto-arranged {Items.Count} cards in 2-column A4 grid.";
            RefreshCanvas();
        }

        #endregion

        #region Undo / Redo

        private void PushUndoState()
        {
            if (_isApplyingUndoRedo) return;
            Layout.Items = Items.ToList();
            string json = _renderService.SerializeProject(Layout);
            _undoStack.Push(json);
            _redoStack.Clear();
        }

        [RelayCommand]
        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            _isApplyingUndoRedo = true;
            try
            {
                string current = _renderService.SerializeProject(Layout);
                _redoStack.Push(current);

                string previous = _undoStack.Pop();
                RestoreLayoutFromJson(previous);
                StatusMessage = "Undo applied.";
            }
            finally
            {
                _isApplyingUndoRedo = false;
            }
        }

        [RelayCommand]
        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            _isApplyingUndoRedo = true;
            try
            {
                string current = _renderService.SerializeProject(Layout);
                _undoStack.Push(current);

                string next = _redoStack.Pop();
                RestoreLayoutFromJson(next);
                StatusMessage = "Redo applied.";
            }
            finally
            {
                _isApplyingUndoRedo = false;
            }
        }

        private void RestoreLayoutFromJson(string json)
        {
            var model = _renderService.DeserializeProject(json);
            Layout = model;
            Items.Clear();
            foreach (var item in model.Items)
            {
                Items.Add(item);
            }
            SelectedItem = Items.FirstOrDefault();
            RefreshCanvas();
        }

        #endregion

        #region File Persistence & Export

        [RelayCommand]
        public async Task SaveProjectAsync()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PVC Studio Pro Project (*.pvcproj)|*.pvcproj|All Files (*.*)|*.*",
                Title = "Save PVC Page Layout Project",
                FileName = $"{Layout.Title.Replace(' ', '_')}.pvcproj"
            };

            if (dlg.ShowDialog() == true)
            {
                Layout.Items = Items.ToList();
                await _renderService.SaveProjectAsync(Layout, dlg.FileName);
                StatusMessage = $"Project saved successfully to {Path.GetFileName(dlg.FileName)}";
            }
        }

        [RelayCommand]
        public async Task OpenProjectAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "PVC Studio Pro Project (*.pvcproj)|*.pvcproj|All Files (*.*)|*.*",
                Title = "Open PVC Page Layout Project"
            };

            if (dlg.ShowDialog() == true)
            {
                PushUndoState();
                var loaded = await _renderService.LoadProjectAsync(dlg.FileName);
                Layout = loaded;
                Items.Clear();
                foreach (var it in loaded.Items)
                {
                    Items.Add(it);
                }
                SelectedItem = Items.FirstOrDefault();
                RefreshCanvas();
                StatusMessage = $"Loaded project: {Path.GetFileName(dlg.FileName)} ({Items.Count} items)";
            }
        }

        [RelayCommand]
        public async Task ExportPdfAsync()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "High-Resolution Vector PDF (*.pdf)|*.pdf",
                Title = "Export Layout to Print-Ready PDF",
                FileName = $"{Layout.Title.Replace(' ', '_')}_600DPI.pdf"
            };

            if (dlg.ShowDialog() == true)
            {
                IsBusy = true;
                StatusMessage = "Generating high-resolution 600 DPI vector PDF...";
                try
                {
                    Layout.Items = Items.ToList();
                    byte[] pdfBytes = _renderService.ExportLayoutToPdf(Layout);
                    await File.WriteAllBytesAsync(dlg.FileName, pdfBytes);
                    StatusMessage = $"Exported PDF: {Path.GetFileName(dlg.FileName)} ({(pdfBytes.Length / 1024.0):F1} KB)";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"PDF export error: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        public async Task ExportPngAsync()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "High-Resolution PNG Image (*.png)|*.png",
                Title = "Export Layout to PNG",
                FileName = $"{Layout.Title.Replace(' ', '_')}_600DPI.png"
            };

            if (dlg.ShowDialog() == true)
            {
                IsBusy = true;
                StatusMessage = "Rendering 600 DPI raster bitmap...";
                try
                {
                    Layout.Items = Items.ToList();
                    await Task.Run(() =>
                    {
                        using var bitmap = _renderService.RenderLayoutToBitmap(Layout, dpi: 600);
                        using var image = SKImage.FromBitmap(bitmap);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var stream = File.OpenWrite(dlg.FileName);
                        data.SaveTo(stream);
                    });
                    StatusMessage = $"Exported PNG: {Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"PNG export error: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        public void SendToPrintPreview()
        {
            Layout.Items = Items.ToList();
            OpenInPrintPreviewAction?.Invoke(Layout);
        }

        [RelayCommand]
        public void Close()
        {
            CloseAction?.Invoke();
        }

        #endregion

        public void RefreshCanvas()
        {
            try
            {
                Layout.Items = Items.ToList();
                Layout.ShowCutMarks = ShowCutMarks;
                Layout.ShowFoldLine = ShowFoldLine;
                Layout.MirrorMode = MirrorMode;

                // Render at 150 DPI for smooth, responsive UI display
                using var bmp = _renderService.RenderLayoutToBitmap(Layout, dpi: 150);
                CanvasPreviewImage = bmp.ToBitmapSource();

                // Compute aspect-fit display dimensions inside viewer
                double aspect = Layout.PageWidthMm / Math.Max(1.0, Layout.PageHeightMm);
                CanvasDisplayHeight = 700 * ZoomFactor;
                CanvasDisplayWidth = CanvasDisplayHeight * aspect;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Canvas render notice: {ex.Message}";
            }
        }
    }
}
