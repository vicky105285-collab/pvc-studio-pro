using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using PVCStudioPro.Core.Services;

namespace PVCStudioPro.App.Converters
{
    public class FeatureVisibilityConverter : IValueConverter
    {
        private static IFeatureFlagsService? _featureFlagsService;

        public static void Initialize(IFeatureFlagsService service)
        {
            _featureFlagsService = service;
        }

        private static IFeatureFlagsService GetService()
        {
            if (_featureFlagsService != null) return _featureFlagsService;
            if (App.ServiceProvider != null)
            {
                _featureFlagsService = App.ServiceProvider.GetService<IFeatureFlagsService>();
            }
            return _featureFlagsService ?? new PVCStudioPro.Core.Services.Implementations.FeatureFlagsService();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string featureId = parameter as string ?? value as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(featureId))
            {
                return Visibility.Visible;
            }

            var service = GetService();
            bool isEnabled = service.IsFeatureEnabled(featureId);

            return isEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
