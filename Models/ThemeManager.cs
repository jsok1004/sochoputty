using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SochoPutty.Models
{
    public static class ThemeManager
    {
        public static event EventHandler<string>? ThemeChanged;
        
        private static string _currentTheme = "Dark";
        
        public static string CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ThemeChanged?.Invoke(null, value);
                }
            }
        }

        // 다크 테마 색상
        private static readonly Dictionary<string, object> DarkTheme = new Dictionary<string, object>
        {
            // 기본 배경/전경 색상
            ["WindowBackground"] = new SolidColorBrush(Color.FromRgb(32, 32, 32)), // #202020 - 어두운 회색
            ["PanelBackground"] = new SolidColorBrush(Color.FromRgb(42, 42, 42)), // #2A2A2A - 조금 더 밝은 어두운 회색
            ["ControlBackground"] = new SolidColorBrush(Color.FromRgb(64, 64, 64)), // #404040 - 버튼용 더 밝은 회색 (대비 개선)
            ["TextForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // #FFFFFF - 흰색 텍스트
            
            // 강조 색상
            ["AccentBrush"] = new SolidColorBrush(Color.FromRgb(51, 234, 255)), // #33EAFF - 청록색 액센트
            ["AccentForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)), // #000000 - 검은색 텍스트 (대비)
            
            // 테두리 색상
            ["BorderBrush"] = new SolidColorBrush(Color.FromRgb(128, 128, 128)), // #808080 - 더 밝은 회색 테두리 (대비 개선)
            
            // 호버 효과
            ["HoverBackground"] = new SolidColorBrush(Color.FromRgb(80, 80, 80)), // #505050 - 호버 시 더 밝은 회색 (대비 개선)
            ["HoverForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // #FFFFFF - 흰색 텍스트
            
            // 비활성화 상태
            ["DisabledBackground"] = new SolidColorBrush(Color.FromRgb(56, 56, 56)), // #383838 - 비활성화 배경 (더 어둡게)
            ["DisabledForeground"] = new SolidColorBrush(Color.FromRgb(128, 128, 128)), // #808080 - 비활성화 텍스트 (더 밝은 회색)
            
            // 선택된 상태
            ["SelectedBackground"] = new SolidColorBrush(Color.FromRgb(51, 234, 255)), // #33EAFF - 청록색 액센트
            ["SelectedForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)), // #000000 - 검은색 텍스트 (대비)
            
            // 탭 관련
            ["TabBackground"] = new SolidColorBrush(Color.FromRgb(42, 42, 42)), // #2A2A2A - 어두운 회색
            ["TabSelectedBackground"] = new SolidColorBrush(Color.FromRgb(64, 64, 64)), // #404040 - 선택된 탭은 조금 밝게
            ["TabHoverBackground"] = new SolidColorBrush(Color.FromRgb(56, 56, 56)), // #383838 - 호버 시 중간 회색
            
            // 데이터그리드
            ["DataGridBackground"] = new SolidColorBrush(Color.FromRgb(48, 48, 48)), // #303030 - 어두운 회색
            ["DataGridAlternateBackground"] = new SolidColorBrush(Color.FromRgb(56, 56, 56)), // #383838 - 교대 행
            ["DataGridHeaderBackground"] = new SolidColorBrush(Color.FromRgb(51, 234, 255)), // #33EAFF - 청록색 헤더 배경
            ["DataGridHeaderForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)), // #000000 - 검은색 헤더 텍스트
            
            // 그룹박스
            ["GroupBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(51, 234, 255)), // #33EAFF - 청록색 테두리
            ["GroupBoxForeground"] = new SolidColorBrush(Color.FromRgb(51, 234, 255)), // #33EAFF - 청록색 텍스트
            
            // 메뉴
            ["MenuBackground"] = new SolidColorBrush(Color.FromRgb(32, 32, 32)), // #202020 - 어두운 회색
            ["MenuForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // #FFFFFF - 흰색 텍스트
            ["MenuHoverBackground"] = new SolidColorBrush(Color.FromRgb(64, 64, 64)), // #404040 - 호버 시 밝은 회색
            ["MenuHoverForeground"] = new SolidColorBrush(Color.FromRgb(51, 234, 255)), // #33EAFF - 청록색
            
            // 툴바
            ["ToolBarBackground"] = new SolidColorBrush(Color.FromRgb(32, 32, 32)), // #202020 - 어두운 회색
            ["ToolBarForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)) // #FFFFFF - 흰색 텍스트
        };

        // 라이트 테마 색상
        private static readonly Dictionary<string, object> LightTheme = new Dictionary<string, object>
        {
            // 기본 배경/전경 색상
            ["WindowBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            ["PanelBackground"] = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
            ["ControlBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            ["TextForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
            
            // 강조 색상
            ["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            ["AccentForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            
            // 테두리 색상
            ["BorderBrush"] = new SolidColorBrush(Color.FromRgb(173, 173, 173)),
            
            // 호버 효과
            ["HoverBackground"] = new SolidColorBrush(Color.FromRgb(229, 241, 251)),
            ["HoverForeground"] = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            
            // 비활성화 상태
            ["DisabledBackground"] = new SolidColorBrush(Color.FromRgb(240, 240, 240)), // #F0F0F0 - 비활성화 배경
            ["DisabledForeground"] = new SolidColorBrush(Color.FromRgb(128, 128, 128)), // #808080 - 비활성화 텍스트 (회색)
            
            // 선택된 상태
            ["SelectedBackground"] = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            ["SelectedForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            
            // 탭 관련
            ["TabBackground"] = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            ["TabSelectedBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            ["TabHoverBackground"] = new SolidColorBrush(Color.FromRgb(229, 241, 251)),
            
            // 데이터그리드
            ["DataGridBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            ["DataGridAlternateBackground"] = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
            ["DataGridHeaderBackground"] = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            ["DataGridHeaderForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            
            // 그룹박스
            ["GroupBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            ["GroupBoxForeground"] = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            
            // 메뉴
            ["MenuBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            ["MenuForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
            ["MenuHoverBackground"] = new SolidColorBrush(Color.FromRgb(229, 241, 251)),
            ["MenuHoverForeground"] = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            
            // 툴바
            ["ToolBarBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            ["ToolBarForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0))
        };

        public static void ApplyTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
                themeName = "Dark";

            var themeResources = themeName.ToLower() switch
            {
                "light" => LightTheme,
                "dark" => DarkTheme,
                _ => DarkTheme
            };

            var app = Application.Current;
            if (app?.Resources != null)
            {
                // 기존 테마 리소스 제거
                var keysToRemove = new List<string>();
                foreach (var key in app.Resources.Keys)
                {
                    if (key is string strKey && (DarkTheme.ContainsKey(strKey) || LightTheme.ContainsKey(strKey)))
                    {
                        keysToRemove.Add(strKey);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    app.Resources.Remove(key);
                }

                // 새 테마 리소스 적용
                foreach (var kvp in themeResources)
                {
                    app.Resources[kvp.Key] = kvp.Value;
                }
            }

            CurrentTheme = themeName;
        }

        public static void Initialize(string? themeName = null)
        {
            ApplyTheme(themeName ?? "Dark");
        }

        public static SolidColorBrush GetBrush(string resourceKey)
        {
            var app = Application.Current;
            if (app?.Resources?.Contains(resourceKey) == true && 
                app.Resources[resourceKey] is SolidColorBrush brush)
            {
                return brush;
            }

            // 기본값 반환 (다크 테마)
            return DarkTheme.TryGetValue(resourceKey, out var defaultBrush) && defaultBrush is SolidColorBrush sb 
                ? sb 
                : new SolidColorBrush(Colors.White);
        }
    }
}
