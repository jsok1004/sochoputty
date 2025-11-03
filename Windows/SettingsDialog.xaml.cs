using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SochoPutty.Models;

namespace SochoPutty.Windows
{
    public partial class SettingsDialog : Window
    {
        private readonly SettingsManager _settingsManager;
        private AppSettings _currentSettings;

        public SettingsDialog(SettingsManager settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _currentSettings = _settingsManager.GetSettings();
            
            InitializeComboBoxes();
            LoadSettings();
        }

        private void InitializeComboBoxes()
        {
            // 기본 연결 타입 초기화
            cmbDefaultConnectionType.ItemsSource = Enum.GetValues(typeof(ConnectionType));
            
            // 글꼴 패밀리 초기화
            var fontFamilies = Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();
            cmbFontFamily.ItemsSource = fontFamilies;
            cmbFontFamily.DisplayMemberPath = "Source";
            
            // 글꼴 크기 초기화
            var fontSizes = new[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };
            cmbFontSize.ItemsSource = fontSizes;
        }

        private void LoadSettings()
        {
            // 애플리케이션 테마 설정
            foreach (ComboBoxItem item in cmbApplicationTheme.Items)
            {
                if (item.Tag.ToString() == _currentSettings.Theme)
                {
                    cmbApplicationTheme.SelectedItem = item;
                    break;
                }
            }
            
            // 기본값 설정
            if (Enum.TryParse<ConnectionType>(_currentSettings.DefaultConnectionType, out var connectionType))
            {
                cmbDefaultConnectionType.SelectedValue = connectionType;
            }
            
            
            // 글꼴 설정
            var fontFamily = Fonts.SystemFontFamilies.FirstOrDefault(f => f.Source == _currentSettings.FontFamily);
            if (fontFamily != null)
            {
                cmbFontFamily.SelectedItem = fontFamily;
            }
            cmbFontSize.SelectedItem = _currentSettings.FontSize;
        }

        private AppSettings CreateSettingsFromInput()
        {
            var settings = new AppSettings();
            
            // 애플리케이션 테마
            if (cmbApplicationTheme.SelectedItem is ComboBoxItem themeItem)
            {
                settings.Theme = themeItem.Tag?.ToString() ?? "Dark";
            }
            
            // 기본 연결 타입
            if (cmbDefaultConnectionType.SelectedValue is ConnectionType defaultConnectionType)
            {
                settings.DefaultConnectionType = defaultConnectionType.ToString();
            }
            
            
            // 글꼴
            if (cmbFontFamily.SelectedItem is FontFamily fontFamily)
            {
                settings.FontFamily = fontFamily.Source;
            }
            
            if (cmbFontSize.SelectedItem is int fontSize)
            {
                settings.FontSize = fontSize;
            }
            
            return settings;
        }




        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newSettings = CreateSettingsFromInput();
                _settingsManager.UpdateSettings(newSettings);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 중 오류가 발생했습니다: {ex.Message}", "오류", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 