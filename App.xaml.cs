using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SochoPutty.Models;

namespace SochoPutty
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 저장된 설정에서 테마 로드
            var settingsManager = new SettingsManager();
            var settings = settingsManager.GetSettings();
            ThemeManager.ApplyTheme(settings.Theme);
        }
    }
} 