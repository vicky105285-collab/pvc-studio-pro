using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PVCStudioPro.Core.Models;
using PVCStudioPro.Core.Services;
using PVCStudioPro.Core.Services.Implementations;

namespace PVCStudioPro.App.Controls
{
    public partial class InteractiveCropCanvas : UserControl
    {
        private readonly ICropEngine _cropEngine = new CropEngine();

        private enum DragMode
        {
            None,
            Pan,
            MoveRect,
            HandleNW,
            HandleN,
            HandleNE,
            HandleE,
            HandleSE,
            HandleS,
            HandleSW,
            HandleW,
            Corner1,
            Corner2,
            Corner3,
            Corner4
        }

        private DragMode _activeDrag = DragMode.None;
        private Point _lastMousePos;

        #region Dependency Properties

        public static readonly DependencyProperty SourceImageProperty =
            DependencyProperty.Register(nameof(SourceImage), typeof(BitmapSource), typeof(InteractiveCropCanvas),
                new PropertyMetadata(null, OnSourceImageChanged));

        public static readonly DependencyProperty CropXProperty =
            DependencyProperty.Register(nameof(CropX), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty CropYProperty =
            DependencyProperty.Register(nameof(CropY), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty CropWidthProperty =
            DependencyProperty.Register(nameof(CropWidth), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(100f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty CropHeightProperty =
            DependencyProperty.Register(nameof(CropHeight), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(60f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty ZoomFactorProperty =
            DependencyProperty.Register(nameof(ZoomFactor), typeof(double), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty PanOffsetXProperty =
            DependencyProperty.Register(nameof(PanOffsetX), typeof(double), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty PanOffsetYProperty =
            DependencyProperty.Register(nameof(PanOffsetY), typeof(double), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty RotationAngleProperty =
            DependencyProperty.Register(nameof(RotationAngle), typeof(double), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty IsPerspectiveModeProperty =
            DependencyProperty.Register(nameof(IsPerspectiveMode), typeof(bool), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty SelectedAspectRatioProperty =
            DependencyProperty.Register(nameof(SelectedAspectRatio), typeof(CropAspectRatio), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(CropAspectRatio.Cr80Card, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty Corner1XProperty =
            DependencyProperty.Register(nameof(Corner1X), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));
        public static readonly DependencyProperty Corner1YProperty =
            DependencyProperty.Register(nameof(Corner1Y), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty Corner2XProperty =
            DependencyProperty.Register(nameof(Corner2X), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));
        public static readonly DependencyProperty Corner2YProperty =
            DependencyProperty.Register(nameof(Corner2Y), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty Corner3XProperty =
            DependencyProperty.Register(nameof(Corner3X), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));
        public static readonly DependencyProperty Corner3YProperty =
            DependencyProperty.Register(nameof(Corner3Y), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public static readonly DependencyProperty Corner4XProperty =
            DependencyProperty.Register(nameof(Corner4X), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));
        public static readonly DependencyProperty Corner4YProperty =
            DependencyProperty.Register(nameof(Corner4Y), typeof(float), typeof(InteractiveCropCanvas),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualGeometryChanged));

        public BitmapSource? SourceImage
        {
            get => (BitmapSource?)GetValue(SourceImageProperty);
            set => SetValue(SourceImageProperty, value);
        }

        public float CropX
        {
            get => (float)GetValue(CropXProperty);
            set => SetValue(CropXProperty, value);
        }

        public float CropY
        {
            get => (float)GetValue(CropYProperty);
            set => SetValue(CropYProperty, value);
        }

        public float CropWidth
        {
            get => (float)GetValue(CropWidthProperty);
            set => SetValue(CropWidthProperty, value);
        }

        public float CropHeight
        {
            get => (float)GetValue(CropHeightProperty);
            set => SetValue(CropHeightProperty, value);
        }

        public double ZoomFactor
        {
            get => (double)GetValue(ZoomFactorProperty);
            set => SetValue(ZoomFactorProperty, value);
        }

        public double PanOffsetX
        {
            get => (double)GetValue(PanOffsetXProperty);
            set => SetValue(PanOffsetXProperty, value);
        }

        public double PanOffsetY
        {
            get => (double)GetValue(PanOffsetYProperty);
            set => SetValue(PanOffsetYProperty, value);
        }

        public double RotationAngle
        {
            get => (double)GetValue(RotationAngleProperty);
            set => SetValue(RotationAngleProperty, value);
        }

        public bool IsPerspectiveMode
        {
            get => (bool)GetValue(IsPerspectiveModeProperty);
            set => SetValue(IsPerspectiveModeProperty, value);
        }

        public CropAspectRatio SelectedAspectRatio
        {
            get => (CropAspectRatio)GetValue(SelectedAspectRatioProperty);
            set => SetValue(SelectedAspectRatioProperty, value);
        }

        public float Corner1X { get => (float)GetValue(Corner1XProperty); set => SetValue(Corner1XProperty, value); }
        public float Corner1Y { get => (float)GetValue(Corner1YProperty); set => SetValue(Corner1YProperty, value); }
        public float Corner2X { get => (float)GetValue(Corner2XProperty); set => SetValue(Corner2XProperty, value); }
        public float Corner2Y { get => (float)GetValue(Corner2YProperty); set => SetValue(Corner2YProperty, value); }
        public float Corner3X { get => (float)GetValue(Corner3XProperty); set => SetValue(Corner3XProperty, value); }
        public float Corner3Y { get => (float)GetValue(Corner3YProperty); set => SetValue(Corner3YProperty, value); }
        public float Corner4X { get => (float)GetValue(Corner4XProperty); set => SetValue(Corner4XProperty, value); }
        public float Corner4Y { get => (float)GetValue(Corner4YProperty); set => SetValue(Corner4YProperty, value); }

        #endregion

        public InteractiveCropCanvas()
        {
            InitializeComponent();
        }

        private static void OnSourceImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InteractiveCropCanvas canvas)
            {
                canvas.SourceImg.Source = e.NewValue as BitmapSource;
                canvas.FitToWindow();
                canvas.UpdateVisuals();
            }
        }

        private static void OnVisualGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InteractiveCropCanvas canvas)
            {
                canvas.UpdateVisuals();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            FitToWindow();
            UpdateVisuals();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }

        public void FitToWindow()
        {
            if (SourceImage == null || ActualWidth < 20 || ActualHeight < 20) return;

            PanOffsetX = 0;
            PanOffsetY = 0;
            ZoomFactor = 1.0;
            UpdateVisuals();
        }

        private CropInteractionState GetInteractionState() => new()
        {
            ZoomFactor = ZoomFactor,
            PanOffsetX = PanOffsetX,
            PanOffsetY = PanOffsetY,
            ViewportWidth = ActualWidth > 0 ? ActualWidth : 800,
            ViewportHeight = ActualHeight > 0 ? ActualHeight : 600
        };

        private void UpdateVisuals()
        {
            if (SourceImage == null || ActualWidth < 10 || ActualHeight < 10)
            {
                SourceImg.Visibility = Visibility.Collapsed;
                CropRectVisual.Visibility = Visibility.Collapsed;
                SetHandlesVisibility(Visibility.Collapsed);
                return;
            }

            SourceImg.Visibility = Visibility.Visible;
            var state = GetInteractionState();
            double imgW = SourceImage.PixelWidth;
            double imgH = SourceImage.PixelHeight;

            // 1. Position and size image
            var (sTopLeftX, sTopLeftY) = _cropEngine.ImageToScreen(0, 0, state, imgW, imgH);
            var (sBottomRightX, sBottomRightY) = _cropEngine.ImageToScreen(imgW, imgH, state, imgW, imgH);
            double screenW = Math.Max(1, sBottomRightX - sTopLeftX);
            double screenH = Math.Max(1, sBottomRightY - sTopLeftY);

            Canvas.SetLeft(SourceImg, sTopLeftX);
            Canvas.SetTop(SourceImg, sTopLeftY);
            SourceImg.Width = screenW;
            SourceImg.Height = screenH;
            SourceImgRotateTransform.Angle = RotationAngle;

            // 2. Perspective Mode vs Rectangle Mode
            if (IsPerspectiveMode)
            {
                CropRectVisual.Visibility = Visibility.Collapsed;
                SetStandardHandlesVisibility(Visibility.Collapsed);
                SetPerspectiveHandlesVisibility(Visibility.Visible);
                PerspectivePolygon.Visibility = Visibility.Visible;

                var (p1X, p1Y) = _cropEngine.ImageToScreen(Corner1X, Corner1Y, state, imgW, imgH);
                var (p2X, p2Y) = _cropEngine.ImageToScreen(Corner2X, Corner2Y, state, imgW, imgH);
                var (p3X, p3Y) = _cropEngine.ImageToScreen(Corner3X, Corner3Y, state, imgW, imgH);
                var (p4X, p4Y) = _cropEngine.ImageToScreen(Corner4X, Corner4Y, state, imgW, imgH);

                PerspectivePolygon.Points = new PointCollection { new(p1X, p1Y), new(p2X, p2Y), new(p3X, p3Y), new(p4X, p4Y) };

                PositionHandle(CornerHandle1, p1X, p1Y);
                PositionHandle(CornerHandle2, p2X, p2Y);
                PositionHandle(CornerHandle3, p3X, p3Y);
                PositionHandle(CornerHandle4, p4X, p4Y);
            }
            else
            {
                PerspectivePolygon.Visibility = Visibility.Collapsed;
                SetPerspectiveHandlesVisibility(Visibility.Collapsed);
                CropRectVisual.Visibility = Visibility.Visible;
                SetStandardHandlesVisibility(Visibility.Visible);

                var (cropScreenX, cropScreenY) = _cropEngine.ImageToScreen(CropX, CropY, state, imgW, imgH);
                var (cropScreenEndX, cropScreenEndY) = _cropEngine.ImageToScreen(CropX + CropWidth, CropY + CropHeight, state, imgW, imgH);
                double cropW = Math.Max(1, cropScreenEndX - cropScreenX);
                double cropH = Math.Max(1, cropScreenEndY - cropScreenY);

                Canvas.SetLeft(CropRectVisual, cropScreenX);
                Canvas.SetTop(CropRectVisual, cropScreenY);
                CropRectVisual.Width = cropW;
                CropRectVisual.Height = cropH;

                // Rule of Thirds
                GuideLineH1.X1 = cropScreenX; GuideLineH1.X2 = cropScreenX + cropW;
                GuideLineH1.Y1 = GuideLineH1.Y2 = cropScreenY + cropH / 3.0;

                GuideLineH2.X1 = cropScreenX; GuideLineH2.X2 = cropScreenX + cropW;
                GuideLineH2.Y1 = GuideLineH2.Y2 = cropScreenY + 2.0 * cropH / 3.0;

                GuideLineV1.Y1 = cropScreenY; GuideLineV1.Y2 = cropScreenY + cropH;
                GuideLineV1.X1 = GuideLineV1.X2 = cropScreenX + cropW / 3.0;

                GuideLineV2.Y1 = cropScreenY; GuideLineV2.Y2 = cropScreenY + cropH;
                GuideLineV2.X1 = GuideLineV2.X2 = cropScreenX + 2.0 * cropW / 3.0;

                // 8 Drag Handles
                PositionHandle(HandleNW, cropScreenX, cropScreenY);
                PositionHandle(HandleN, cropScreenX + cropW / 2.0, cropScreenY);
                PositionHandle(HandleNE, cropScreenX + cropW, cropScreenY);
                PositionHandle(HandleE, cropScreenX + cropW, cropScreenY + cropH / 2.0);
                PositionHandle(HandleSE, cropScreenX + cropW, cropScreenY + cropH);
                PositionHandle(HandleS, cropScreenX + cropW / 2.0, cropScreenY + cropH);
                PositionHandle(HandleSW, cropScreenX, cropScreenY + cropH);
                PositionHandle(HandleW, cropScreenX, cropScreenY + cropH / 2.0);
            }

            // Update HUD
            ZoomHudText.Text = $"{(int)(ZoomFactor * 100)}%";
            double mmW = Math.Round(CropWidth * 25.4 / 300.0, 1);
            double mmH = Math.Round(CropHeight * 25.4 / 300.0, 1);
            CoordsHudText.Text = $"X: {(int)CropX} Y: {(int)CropY} | {(int)CropWidth}×{(int)CropHeight} px ({mmW}×{mmH} mm)";
        }

        private static void PositionHandle(FrameworkElement element, double x, double y)
        {
            Canvas.SetLeft(element, x - element.Width / 2.0);
            Canvas.SetTop(element, y - element.Height / 2.0);
        }

        private void SetHandlesVisibility(Visibility v)
        {
            SetStandardHandlesVisibility(v);
            SetPerspectiveHandlesVisibility(v);
        }

        private void SetStandardHandlesVisibility(Visibility v)
        {
            HandleNW.Visibility = v;
            HandleN.Visibility = v;
            HandleNE.Visibility = v;
            HandleE.Visibility = v;
            HandleSE.Visibility = v;
            HandleS.Visibility = v;
            HandleSW.Visibility = v;
            HandleW.Visibility = v;
            GuideLineH1.Visibility = v;
            GuideLineH2.Visibility = v;
            GuideLineV1.Visibility = v;
            GuideLineV2.Visibility = v;
        }

        private void SetPerspectiveHandlesVisibility(Visibility v)
        {
            CornerHandle1.Visibility = v;
            CornerHandle2.Visibility = v;
            CornerHandle3.Visibility = v;
            CornerHandle4.Visibility = v;
        }

        #region Mouse Interaction & Pan/Zoom

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (SourceImage == null) return;

            Point mousePos = e.GetPosition(this);
            double zoomDelta = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
            double newZoom = Math.Clamp(ZoomFactor * zoomDelta, 0.1, 5.0);

            if (Math.Abs(newZoom - ZoomFactor) > 0.001)
            {
                // Anchor-point centered zoom
                double mouseRelX = mousePos.X - ActualWidth / 2.0 - PanOffsetX;
                double mouseRelY = mousePos.Y - ActualHeight / 2.0 - PanOffsetY;

                PanOffsetX -= mouseRelX * (newZoom / ZoomFactor - 1.0);
                PanOffsetY -= mouseRelY * (newZoom / ZoomFactor - 1.0);
                ZoomFactor = newZoom;
            }

            e.Handled = true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SourceImage == null) return;
            Focus();
            _lastMousePos = e.GetPosition(this);

            if (e.MiddleButton == MouseButtonState.Pressed || Keyboard.IsKeyDown(Key.Space))
            {
                _activeDrag = DragMode.Pan;
                CaptureMouse();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var hit = e.OriginalSource as FrameworkElement;
                if (hit == HandleNW) _activeDrag = DragMode.HandleNW;
                else if (hit == HandleN) _activeDrag = DragMode.HandleN;
                else if (hit == HandleNE) _activeDrag = DragMode.HandleNE;
                else if (hit == HandleE) _activeDrag = DragMode.HandleE;
                else if (hit == HandleSE) _activeDrag = DragMode.HandleSE;
                else if (hit == HandleS) _activeDrag = DragMode.HandleS;
                else if (hit == HandleSW) _activeDrag = DragMode.HandleSW;
                else if (hit == HandleW) _activeDrag = DragMode.HandleW;
                else if (hit == CornerHandle1) _activeDrag = DragMode.Corner1;
                else if (hit == CornerHandle2) _activeDrag = DragMode.Corner2;
                else if (hit == CornerHandle3) _activeDrag = DragMode.Corner3;
                else if (hit == CornerHandle4) _activeDrag = DragMode.Corner4;
                else if (hit == CropRectVisual) _activeDrag = DragMode.MoveRect;
                else _activeDrag = DragMode.Pan;

                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (SourceImage == null || _activeDrag == DragMode.None) return;

            Point currentPos = e.GetPosition(this);
            double dx = currentPos.X - _lastMousePos.X;
            double dy = currentPos.Y - _lastMousePos.Y;
            _lastMousePos = currentPos;

            var state = GetInteractionState();
            double imgW = SourceImage.PixelWidth;
            double imgH = SourceImage.PixelHeight;

            if (_activeDrag == DragMode.Pan)
            {
                PanOffsetX += dx;
                PanOffsetY += dy;
                UpdateVisuals();
                return;
            }

            // Convert screen delta to image pixel delta
            double fitScale = Math.Min(state.ViewportWidth / Math.Max(1.0, imgW), state.ViewportHeight / Math.Max(1.0, imgH));
            double totalScale = fitScale * ZoomFactor;
            float imgDx = (float)(dx / totalScale);
            float imgDy = (float)(dy / totalScale);

            if (_activeDrag == DragMode.MoveRect)
            {
                CropX = Math.Clamp(CropX + imgDx, 0, (float)imgW - CropWidth);
                CropY = Math.Clamp(CropY + imgDy, 0, (float)imgH - CropHeight);
            }
            else if (_activeDrag == DragMode.Corner1) { Corner1X += imgDx; Corner1Y += imgDy; }
            else if (_activeDrag == DragMode.Corner2) { Corner2X += imgDx; Corner2Y += imgDy; }
            else if (_activeDrag == DragMode.Corner3) { Corner3X += imgDx; Corner3Y += imgDy; }
            else if (_activeDrag == DragMode.Corner4) { Corner4X += imgDx; Corner4Y += imgDy; }
            else
            {
                // Standard 8 handles
                float newX = CropX, newY = CropY, newW = CropWidth, newH = CropHeight;

                switch (_activeDrag)
                {
                    case DragMode.HandleE: newW += imgDx; break;
                    case DragMode.HandleS: newH += imgDy; break;
                    case DragMode.HandleW: newX += imgDx; newW -= imgDx; break;
                    case DragMode.HandleN: newY += imgDy; newH -= imgDy; break;
                    case DragMode.HandleSE: newW += imgDx; newH += imgDy; break;
                    case DragMode.HandleSW: newX += imgDx; newW -= imgDx; newH += imgDy; break;
                    case DragMode.HandleNE: newW += imgDx; newY += imgDy; newH -= imgDy; break;
                    case DragMode.HandleNW: newX += imgDx; newW -= imgDx; newY += imgDy; newH -= imgDy; break;
                }

                if (newW >= 20 && newH >= 20)
                {
                    var bounds = new CropBounds
                    {
                        X = newX,
                        Y = newY,
                        Width = newW,
                        Height = newH,
                        IsAspectRatioLocked = SelectedAspectRatio != CropAspectRatio.Freeform
                    };
                    var constrained = _cropEngine.ConstrainToAspectRatio(bounds, SelectedAspectRatio, imgW, imgH);
                    CropX = (float)constrained.X;
                    CropY = (float)constrained.Y;
                    CropWidth = (float)constrained.Width;
                    CropHeight = (float)constrained.Height;
                }
            }

            UpdateVisuals();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _activeDrag = DragMode.None;
            ReleaseMouseCapture();
        }

        #endregion
    }
}
