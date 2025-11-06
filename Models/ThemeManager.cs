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
            ["TextForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // #FFFFFF - 흰색 텍스트
            
            // 커스텀 타이틀바
            ["TitleBarBackground"] = new SolidColorBrush(Color.FromRgb(60, 60, 60)), // #3C3C3C - 요청된 배경색
            ["TitleBarForeground"] = new SolidColorBrush(Color.FromRgb(204, 204, 204)), // #CCCCCC - 요청된 글씨색
            ["TitleBarButtonHover"] = new SolidColorBrush(Color.FromRgb(80, 80, 80)), // #505050 - 버튼 호버
            ["TitleBarButtonPressed"] = new SolidColorBrush(Color.FromRgb(100, 100, 100)), // #646464 - 버튼 눌림
            
            // 메뉴 관련 색상
            ["MenuBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 45)), // #2D2D2D - 메뉴 배경
            ["MenuForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // #FFFFFF - 메뉴 텍스트
            ["MenuItemBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 45)), // #2D2D2D - 메뉴 아이템 배경
            ["MenuItemForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // #FFFFFF - 메뉴 아이템 텍스트
            ["MenuItemHover"] = new SolidColorBrush(Color.FromRgb(62, 62, 66)), // #3E3E42 - 메뉴 아이템 호버
            ["MenuSeparator"] = new SolidColorBrush(Color.FromRgb(90, 90, 90)) // #5A5A5A - 메뉴 구분선
        };

        // 라이트 테마 색상
        private static readonly Dictionary<string, object> LightTheme = new Dictionary<string, object>
        {
            // 기본 배경/전경 색상
            ["WindowBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            ["TextForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
            
            // 커스텀 타이틀바 (라이트 테마)
            ["TitleBarBackground"] = new SolidColorBrush(Color.FromRgb(240, 240, 240)), // #F0F0F0 - 밝은 배경
            ["TitleBarForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)), // #000000 - 검은 글씨
            ["TitleBarButtonHover"] = new SolidColorBrush(Color.FromRgb(229, 241, 251)), // #E5F1FB - 호버
            ["TitleBarButtonPressed"] = new SolidColorBrush(Color.FromRgb(204, 228, 247)), // #CCE4F7 - 눌림
            
            // 메뉴 관련 색상 (라이트 테마)
            ["MenuBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // #FFFFFF - 메뉴 배경
            ["MenuForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)), // #000000 - 메뉴 텍스트
            ["MenuItemBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // #FFFFFF - 메뉴 아이템 배경
            ["MenuItemForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)), // #000000 - 메뉴 아이템 텍스트
            ["MenuItemHover"] = new SolidColorBrush(Color.FromRgb(229, 241, 251)), // #E5F1FB - 메뉴 아이템 호버
            ["MenuSeparator"] = new SolidColorBrush(Color.FromRgb(160, 160, 160)) // #A0A0A0 - 메뉴 구분선
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
