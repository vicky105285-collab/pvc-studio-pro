using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PVCStudioPro.App.ViewModels;

namespace PVCStudioPro.App.Views
{
    public partial class DocumentPageDesignerView : UserControl
    {
        public DocumentPageDesignerView()
        {
            InitializeComponent();
            this.Loaded += OnViewLoaded;
            this.SizeChanged += (s, e) => DrawRulers();
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            DrawRulers();
        }

        private void DrawRulers()
        {
            try
            {
                // Horizontal ruler: draw ticks every 10 mm (major) and 2 mm (minor)
                HorizontalRulerCanvas.Children.Clear();
                double hWidth = HorizontalRulerCanvas.ActualWidth;
                if (hWidth <= 0) hWidth = 800;

                // Scale factor ~ 3.78 pixels per millimeter at standard 96 DPI screen
                double pxPerMm = 3.0;

                for (double mm = 0; mm * pxPerMm < hWidth; mm += 2)
                {
                    double x = mm * pxPerMm;
                    bool isMajor = (int)mm % 10 == 0;

                    var line = new Line
                    {
                        X1 = x,
                        Y1 = isMajor ? 6 : 14,
                        X2 = x,
                        Y2 = 24,
                        Stroke = isMajor ? Brushes.Gray : Brushes.LightGray,
                        StrokeThickness = 1
                    };
                    HorizontalRulerCanvas.Children.Add(line);

                    if (isMajor && mm > 0 && mm % 20 == 0)
                    {
                        var text = new TextBlock
                        {
                            Text = $"{mm:0}",
                            FontSize = 8,
                            Foreground = Brushes.DimGray
                        };
                        Canvas.SetLeft(text, x + 2);
                        Canvas.SetTop(text, 1);
                        HorizontalRulerCanvas.Children.Add(text);
                    }
                }

                // Vertical ruler: draw ticks down left side
                VerticalRulerCanvas.Children.Clear();
                double vHeight = VerticalRulerCanvas.ActualHeight;
                if (vHeight <= 0) vHeight = 800;

                for (double mm = 0; mm * pxPerMm < vHeight; mm += 2)
                {
                    double y = mm * pxPerMm;
                    bool isMajor = (int)mm % 10 == 0;

                    var line = new Line
                    {
                        X1 = isMajor ? 10 : 18,
                        Y1 = y,
                        X2 = 28,
                        Y2 = y,
                        Stroke = isMajor ? Brushes.Gray : Brushes.LightGray,
                        StrokeThickness = 1
                    };
                    VerticalRulerCanvas.Children.Add(line);

                    if (isMajor && mm > 0 && mm % 20 == 0)
                    {
                        var text = new TextBlock
                        {
                            Text = $"{mm:0}",
                            FontSize = 8,
                            Foreground = Brushes.DimGray
                        };
                        Canvas.SetLeft(text, 2);
                        Canvas.SetTop(text, y - 10);
                        VerticalRulerCanvas.Children.Add(text);
                    }
                }
            }
            catch { }
        }
    }
}
