using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PVCStudioPro.App.ViewModels;
using PVCStudioPro.Core.Models;

namespace PVCStudioPro.App.Views
{
    public partial class CropStudioView : UserControl
    {
        private enum DragTarget
        {
            None,
            Pan,
            MoveBox,
            HandleC1,
            HandleC2,
            HandleC3,
            HandleC4,
            HandleTop,
            HandleBottom,
            HandleLeft,
            HandleRight
        }

        private DragTarget _activeDrag = DragTarget.None;
        private Point _lastMouseScreen;
        private CropStudioViewModel? _vm;

        public CropStudioView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _vm = DataContext as CropStudioViewModel;

            if (_vm != null)
            {
                _vm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateVisualElements();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CropStudioViewModel.CropX):
                case nameof(CropStudioViewModel.CropY):
                case nameof(CropStudioViewModel.CropWidth):
                case nameof(CropStudioViewModel.CropHeight):
                case nameof(CropStudioViewModel.Corner1X):
                case nameof(CropStudioViewModel.Corner1Y):
                case nameof(CropStudioViewModel.Corner2X):
                case nameof(CropStudioViewModel.Corner2Y):
                case nameof(CropStudioViewModel.Corner3X):
                case nameof(CropStudioViewModel.Corner3Y):
                case nameof(CropStudioViewModel.Corner4X):
                case nameof(CropStudioViewModel.Corner4Y):
                case nameof(CropStudioViewModel.IsPerspectiveMode):
                case nameof(CropStudioViewModel.SourceImage):
                    UpdateVisualElements();
                    break;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null && ViewportContainer.ActualWidth > 0 && ViewportContainer.ActualHeight > 0)
            {
                _vm.ViewportWidth = ViewportContainer.ActualWidth;
                _vm.ViewportHeight = ViewportContainer.ActualHeight;
                _vm.FitToWindow();
                UpdateVisualElements();
            }
        }

        private void ViewportContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_vm != null && e.NewSize.Width > 50 && e.NewSize.Height > 50)
            {
                _vm.ViewportWidth = e.NewSize.Width;
                _vm.ViewportHeight = e.NewSize.Height;
            }
        }

        private void UpdateVisualElements()
        {
            if (_vm == null || _vm.ImageWidth <= 0 || _vm.ImageHeight <= 0)
            {
                CropBoxRect.Visibility = Visibility.Collapsed;
                DimmedMaskPath.Visibility = Visibility.Collapsed;
                SetHandlesVisibility(Visibility.Collapsed);
                return;
            }

            double imgW = _vm.ImageWidth;
            double imgH = _vm.ImageHeight;

            DocImage.Width = imgW;
            DocImage.Height = imgH;

            if (_vm.IsPerspectiveMode)
            {
                // Perspective Quad Mode
                CropBoxRect.Visibility = Visibility.Collapsed;
                SetEdgeHandlesVisibility(Visibility.Collapsed);
                GridLineH1.Visibility = Visibility.Collapsed;
                GridLineH2.Visibility = Visibility.Collapsed;
                GridLineV1.Visibility = Visibility.Collapsed;
                GridLineV2.Visibility = Visibility.Collapsed;
                PerspectivePoly.Visibility = Visibility.Visible;

                PerspectivePoly.Points = new PointCollection
                {
                    new Point(_vm.Corner1X, _vm.Corner1Y),
                    new Point(_vm.Corner2X, _vm.Corner2Y),
                    new Point(_vm.Corner3X, _vm.Corner3Y),
                    new Point(_vm.Corner4X, _vm.Corner4Y)
                };

                PositionHandle(HandleC1, _vm.Corner1X, _vm.Corner1Y);
                PositionHandle(HandleC2, _vm.Corner2X, _vm.Corner2Y);
                PositionHandle(HandleC3, _vm.Corner3X, _vm.Corner3Y);
                PositionHandle(HandleC4, _vm.Corner4X, _vm.Corner4Y);
                SetCornerHandlesVisibility(Visibility.Visible);

                // Perspective Dimmed Mask
                var fullRect = new RectangleGeometry(new Rect(0, 0, imgW, imgH));
                var polyFig = new PathFigure { StartPoint = new Point(_vm.Corner1X, _vm.Corner1Y), IsClosed = true };
                polyFig.Segments.Add(new LineSegment(new Point(_vm.Corner2X, _vm.Corner2Y), true));
                polyFig.Segments.Add(new LineSegment(new Point(_vm.Corner3X, _vm.Corner3Y), true));
                polyFig.Segments.Add(new LineSegment(new Point(_vm.Corner4X, _vm.Corner4Y), true));
                var pathGeom = new PathGeometry();
                pathGeom.Figures.Add(polyFig);

                var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, pathGeom);
                DimmedMaskPath.Data = combined;
                DimmedMaskPath.Visibility = Visibility.Visible;
            }
            else
            {
                // Standard Rectangular Crop Mode
                PerspectivePoly.Visibility = Visibility.Collapsed;
                CropBoxRect.Visibility = Visibility.Visible;
                SetHandlesVisibility(Visibility.Visible);

                double cx = _vm.CropX;
                double cy = _vm.CropY;
                double cw = _vm.CropWidth;
                double ch = _vm.CropHeight;

                Canvas.SetLeft(CropBoxRect, cx);
                Canvas.SetTop(CropBoxRect, cy);
                CropBoxRect.Width = cw;
                CropBoxRect.Height = ch;

                // Position 4 corner handles
                PositionHandle(HandleC1, cx, cy);
                PositionHandle(HandleC2, cx + cw, cy);
                PositionHandle(HandleC3, cx + cw, cy + ch);
                PositionHandle(HandleC4, cx, cy + ch);

                // Position 4 edge handles
                PositionHandle(HandleTop, cx + (cw / 2), cy);
                PositionHandle(HandleBottom, cx + (cw / 2), cy + ch);
                PositionHandle(HandleLeft, cx, cy + (ch / 2));
                PositionHandle(HandleRight, cx + cw, cy + (ch / 2));

                // Position Grid Lines
                GridLineH1.X1 = cx; GridLineH1.Y1 = cy + (ch / 3.0);
                GridLineH1.X2 = cx + cw; GridLineH1.Y2 = cy + (ch / 3.0);
                GridLineH1.Visibility = Visibility.Visible;

                GridLineH2.X1 = cx; GridLineH2.Y1 = cy + (2.0 * ch / 3.0);
                GridLineH2.X2 = cx + cw; GridLineH2.Y2 = cy + (2.0 * ch / 3.0);
                GridLineH2.Visibility = Visibility.Visible;

                GridLineV1.X1 = cx + (cw / 3.0); GridLineV1.Y1 = cy;
                GridLineV1.X2 = cx + (cw / 3.0); GridLineV1.Y2 = cy + ch;
                GridLineV1.Visibility = Visibility.Visible;

                GridLineV2.X1 = cx + (2.0 * cw / 3.0); GridLineV2.Y1 = cy;
                GridLineV2.X2 = cx + (2.0 * cw / 3.0); GridLineV2.Y2 = cy + ch;
                GridLineV2.Visibility = Visibility.Visible;

                // Dimmed Mask outside the crop rectangle
                var fullRect = new RectangleGeometry(new Rect(0, 0, imgW, imgH));
                var cropRectGeom = new RectangleGeometry(new Rect(cx, cy, cw, ch));
                DimmedMaskPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, cropRectGeom);
                DimmedMaskPath.Visibility = Visibility.Visible;
            }
        }

        private static void PositionHandle(FrameworkElement element, double x, double y)
        {
            Canvas.SetLeft(element, x - (element.Width / 2.0));
            Canvas.SetTop(element, y - (element.Height / 2.0));
        }

        private void SetHandlesVisibility(Visibility v)
        {
            SetCornerHandlesVisibility(v);
            SetEdgeHandlesVisibility(v);
        }

        private void SetCornerHandlesVisibility(Visibility v)
        {
            HandleC1.Visibility = v;
            HandleC2.Visibility = v;
            HandleC3.Visibility = v;
            HandleC4.Visibility = v;
        }

        private void SetEdgeHandlesVisibility(Visibility v)
        {
            HandleTop.Visibility = v;
            HandleBottom.Visibility = v;
            HandleLeft.Visibility = v;
            HandleRight.Visibility = v;
        }

        #region Mouse Interaction & Viewport Pan/Zoom

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_vm == null || _vm.ImageWidth <= 0) return;

            Point mousePos = e.GetPosition(InteractionCanvas);
            double oldZoom = _vm.ZoomFactor;
            double factor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
            double newZoom = Math.Clamp(oldZoom * factor, 0.05, 5.0);

            // Keep the image point under mouse stable
            double imgX = (mousePos.X - _vm.PanOffsetX) / oldZoom;
            double imgY = (mousePos.Y - _vm.PanOffsetY) / oldZoom;

            _vm.ZoomFactor = newZoom;
            _vm.PanOffsetX = mousePos.X - (imgX * newZoom);
            _vm.PanOffsetY = mousePos.Y - (imgY * newZoom);

            e.Handled = true;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm == null) return;

            _lastMouseScreen = e.GetPosition(InteractionCanvas);

            // Pan with Middle Click or Right Click or Space+LeftClick
            if (e.MiddleButton == MouseButtonState.Pressed ||
                e.RightButton == MouseButtonState.Pressed ||
                (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space)))
            {
                _activeDrag = DragTarget.Pan;
                InteractionCanvas.CaptureMouse();
                InteractionCanvas.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point imagePt = ScreenToImagePoint(_lastMouseScreen);
                _activeDrag = HitTestHandles(imagePt);

                if (_activeDrag != DragTarget.None)
                {
                    InteractionCanvas.CaptureMouse();
                }
            }
        }

        private DragTarget HitTestHandles(Point imgPt)
        {
            if (_vm == null) return DragTarget.None;

            double tolerance = 18.0 / Math.Max(0.1, _vm.ZoomFactor);

            if (_vm.IsPerspectiveMode)
            {
                if (IsNear(imgPt, _vm.Corner1X, _vm.Corner1Y, tolerance)) return DragTarget.HandleC1;
                if (IsNear(imgPt, _vm.Corner2X, _vm.Corner2Y, tolerance)) return DragTarget.HandleC2;
                if (IsNear(imgPt, _vm.Corner3X, _vm.Corner3Y, tolerance)) return DragTarget.HandleC3;
                if (IsNear(imgPt, _vm.Corner4X, _vm.Corner4Y, tolerance)) return DragTarget.HandleC4;
                return DragTarget.None;
            }

            double cx = _vm.CropX;
            double cy = _vm.CropY;
            double cw = _vm.CropWidth;
            double ch = _vm.CropHeight;

            if (IsNear(imgPt, cx, cy, tolerance)) return DragTarget.HandleC1;
            if (IsNear(imgPt, cx + cw, cy, tolerance)) return DragTarget.HandleC2;
            if (IsNear(imgPt, cx + cw, cy + ch, tolerance)) return DragTarget.HandleC3;
            if (IsNear(imgPt, cx, cy + ch, tolerance)) return DragTarget.HandleC4;

            if (IsNear(imgPt, cx + (cw / 2), cy, tolerance)) return DragTarget.HandleTop;
            if (IsNear(imgPt, cx + (cw / 2), cy + ch, tolerance)) return DragTarget.HandleBottom;
            if (IsNear(imgPt, cx, cy + (ch / 2), tolerance)) return DragTarget.HandleLeft;
            if (IsNear(imgPt, cx + cw, cy + (ch / 2), tolerance)) return DragTarget.HandleRight;

            // Check if inside crop box
            if (imgPt.X >= cx && imgPt.X <= cx + cw && imgPt.Y >= cy && imgPt.Y <= cy + ch)
            {
                return DragTarget.MoveBox;
            }

            return DragTarget.None;
        }

        private static bool IsNear(Point p, double targetX, double targetY, double tol)
        {
            return Math.Abs(p.X - targetX) <= tol && Math.Abs(p.Y - targetY) <= tol;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_vm == null) return;

            Point currentScreen = e.GetPosition(InteractionCanvas);
            Point currentImage = ScreenToImagePoint(currentScreen);

            if (_activeDrag == DragTarget.None)
            {
                // Hover cursor updates
                var hover = HitTestHandles(currentImage);
                switch (hover)
                {
                    case DragTarget.HandleC1:
                    case DragTarget.HandleC3:
                        InteractionCanvas.Cursor = Cursors.SizeNWSE;
                        break;
                    case DragTarget.HandleC2:
                    case DragTarget.HandleC4:
                        InteractionCanvas.Cursor = Cursors.SizeNESW;
                        break;
                    case DragTarget.HandleTop:
                    case DragTarget.HandleBottom:
                        InteractionCanvas.Cursor = Cursors.SizeNS;
                        break;
                    case DragTarget.HandleLeft:
                    case DragTarget.HandleRight:
                        InteractionCanvas.Cursor = Cursors.SizeWE;
                        break;
                    case DragTarget.MoveBox:
                        InteractionCanvas.Cursor = Cursors.SizeAll;
                        break;
                    default:
                        InteractionCanvas.Cursor = Keyboard.IsKeyDown(Key.Space) ? Cursors.SizeAll : Cursors.Arrow;
                        break;
                }
                return;
            }

            double dx = (currentScreen.X - _lastMouseScreen.X) / _vm.ZoomFactor;
            double dy = (currentScreen.Y - _lastMouseScreen.Y) / _vm.ZoomFactor;
            _lastMouseScreen = currentScreen;

            if (_activeDrag == DragTarget.Pan)
            {
                _vm.PanOffsetX += dx * _vm.ZoomFactor;
                _vm.PanOffsetY += dy * _vm.ZoomFactor;
                return;
            }

            if (_vm.IsPerspectiveMode)
            {
                switch (_activeDrag)
                {
                    case DragTarget.HandleC1:
                        _vm.UpdateCorner(1, (float)currentImage.X, (float)currentImage.Y);
                        break;
                    case DragTarget.HandleC2:
                        _vm.UpdateCorner(2, (float)currentImage.X, (float)currentImage.Y);
                        break;
                    case DragTarget.HandleC3:
                        _vm.UpdateCorner(3, (float)currentImage.X, (float)currentImage.Y);
                        break;
                    case DragTarget.HandleC4:
                        _vm.UpdateCorner(4, (float)currentImage.X, (float)currentImage.Y);
                        break;
                }
                UpdateVisualElements();
                return;
            }

            // Standard Rectangular Crop Mode
            double cx = _vm.CropX;
            double cy = _vm.CropY;
            double cw = _vm.CropWidth;
            double ch = _vm.CropHeight;

            float targetAspect = GetAspectLockRatio(_vm.SelectedAspectRatio);
            bool lockAspect = targetAspect > 0;

            switch (_activeDrag)
            {
                case DragTarget.MoveBox:
                    cx += dx;
                    cy += dy;
                    break;

                case DragTarget.HandleC1: // Top-Left
                    cx += dx;
                    cy += dy;
                    cw -= dx;
                    ch -= dy;
                    if (lockAspect) ch = cw / targetAspect;
                    break;

                case DragTarget.HandleC2: // Top-Right
                    cy += dy;
                    cw += dx;
                    ch -= dy;
                    if (lockAspect) ch = cw / targetAspect;
                    break;

                case DragTarget.HandleC3: // Bottom-Right
                    cw += dx;
                    ch += dy;
                    if (lockAspect) ch = cw / targetAspect;
                    break;

                case DragTarget.HandleC4: // Bottom-Left
                    cx += dx;
                    cw -= dx;
                    ch += dy;
                    if (lockAspect) ch = cw / targetAspect;
                    break;

                case DragTarget.HandleTop:
                    cy += dy;
                    ch -= dy;
                    break;

                case DragTarget.HandleBottom:
                    ch += dy;
                    break;

                case DragTarget.HandleLeft:
                    cx += dx;
                    cw -= dx;
                    break;

                case DragTarget.HandleRight:
                    cw += dx;
                    break;
            }

            if (cw >= 20 && ch >= 20)
            {
                _vm.UpdateCropRect((float)cx, (float)cy, (float)cw, (float)ch);
                UpdateVisualElements();
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_activeDrag != DragTarget.None)
            {
                _activeDrag = DragTarget.None;
                InteractionCanvas.ReleaseMouseCapture();
                InteractionCanvas.Cursor = Cursors.Arrow;
                _vm?.PerformCrop();
            }
        }

        private Point ScreenToImagePoint(Point screenPt)
        {
            if (_vm == null || _vm.ZoomFactor <= 0) return new Point(0, 0);
            return new Point(
                (screenPt.X - _vm.PanOffsetX) / _vm.ZoomFactor,
                (screenPt.Y - _vm.PanOffsetY) / _vm.ZoomFactor
            );
        }

        private static float GetAspectLockRatio(CropAspectRatio ratio)
        {
            return ratio switch
            {
                CropAspectRatio.Cr80Card => 85.60f / 53.98f, // 1.58577
                CropAspectRatio.PassportPhoto => 35.0f / 45.0f, // 0.77778
                CropAspectRatio.StampPhoto => 25.0f / 30.0f, // 0.83333
                _ => 0f
            };
        }

        #endregion
    }
}
