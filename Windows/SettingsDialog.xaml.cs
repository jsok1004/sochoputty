using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SochoPutty.Models;

namespace SochoPutty.Windows
{
    public partial class SettingsDialog : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        public struct Margins
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        private readonly SettingsManager _settingsManager;
        private AppSettings _currentSettings;

        public SettingsDialog(SettingsManager settingsManager)
        {
            InitializeComponent();
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _currentSettings = _settingsManager.GetSettings();
            
            InitializeComboBoxes();
            LoadSettings();
            
            // 테마 변경 이벤트 구독
            ThemeManager.ThemeChanged += OnThemeChanged;
        }
        
        private void OnThemeChanged(object? sender, string themeName)
        {
            // 현재 윈도우에 테마 적용 (이미 DynamicResource로 연결되어 있어서 자동 적용됨)
        }
        
        private void ApplicationTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 테마 선택이 변경되면 즉시 미리보기 적용
            if (cmbApplicationTheme.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string themeName)
            {
                ThemeManager.ApplyTheme(themeName);
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // 이벤트 구독 해제
            ThemeManager.ThemeChanged -= OnThemeChanged;
            base.OnClosed(e);
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
            // 취소 시 원래 테마로 되돌리기
            ThemeManager.ApplyTheme(_currentSettings.Theme);
            DialogResult = false;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            // 닫기 버튼 클릭 시 취소와 동일하게 처리
            Cancel_Click(sender, e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            // Enable shadow
            int val = 2;
            DwmSetWindowAttribute(hwnd, 2, ref val, sizeof(int)); // DWMWA_NCRENDERING_POLICY = 2

            Margins margins = new Margins
            {
                cxLeftWidth = 1,
                cxRightWidth = 1,
                cyTopHeight = 1,
                cyBottomHeight = 1
            };

            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
    }
} 