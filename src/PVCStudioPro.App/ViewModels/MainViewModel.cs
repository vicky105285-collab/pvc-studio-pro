using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PVCStudioPro.App.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _currentLanguage = "en";

        [ObservableProperty]
        private string _languageButtonText = "தமிழ் (Tamil)";

        public CardPrintViewModel CardPrintVM { get; }
        public CalibrationViewModel CalibrationVM { get; }
        public BusinessLedgerViewModel LedgerVM { get; }

        public MainViewModel(
            CardPrintViewModel cardPrintVM,
            CalibrationViewModel calibrationVM,
            BusinessLedgerViewModel ledgerVM)
        {
            CardPrintVM = cardPrintVM;
            CalibrationVM = calibrationVM;
            LedgerVM = ledgerVM;

            CurrentView = CardPrintVM;
        }

        [RelayCommand]
        public void Navigate(string destination)
        {
            switch (destination)
            {
                case "Cards":
                    CurrentView = CardPrintVM;
                    break;
                case "Calibration":
                    CurrentView = CalibrationVM;
                    break;
                case "Ledger":
                    _ = LedgerVM.LoadLedgerAsync();
                    CurrentView = LedgerVM;
                    break;
                default:
                    CurrentView = CardPrintVM;
                    break;
            }
        }

        [RelayCommand]
        public void ToggleLanguage()
        {
            if (CurrentLanguage == "en")
            {
                CurrentLanguage = "ta";
                LanguageButtonText = "English";
                ApplyLanguageDictionary("Strings.ta.xaml");
            }
            else
            {
                CurrentLanguage = "en";
                LanguageButtonText = "தமிழ் (Tamil)";
                ApplyLanguageDictionary("Strings.en.xaml");
            }
        }

        private static void ApplyLanguageDictionary(string dictFile)
        {
            var dictUri = new Uri($"pack://application:,,,/I18n/{dictFile}", UriKind.Absolute);
            var newDict = new ResourceDictionary { Source = dictUri };

            var appResources = Application.Current.Resources.MergedDictionaries;
            for (int i = appResources.Count - 1; i >= 0; i--)
            {
                if (appResources[i].Source != null && appResources[i].Source.OriginalString.Contains("/I18n/"))
                {
                    appResources.RemoveAt(i);
                }
            }
            appResources.Add(newDict);
        }
    }
}
