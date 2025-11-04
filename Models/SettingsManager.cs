using System;
using System.IO;
using Newtonsoft.Json;

namespace SochoPutty.Models
{
    public class SettingsManager
    {
        private const string SettingsFileName = "settings.json";
        private readonly string _dataDirectory;
        private readonly string _settingsFilePath;
        private AppSettings _settings;

        public SettingsManager()
        {
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SochoPutty");
            _settingsFilePath = Path.Combine(_dataDirectory, SettingsFileName);
            _settings = new AppSettings();
            
            EnsureDataDirectoryExists();
            LoadSettings();
        }

        private void EnsureDataDirectoryExists()
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        public AppSettings GetSettings()
        {
            return _settings;
        }

        public void UpdateSettings(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            SaveSettings();
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    _settings = settings ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                    SaveSettings();
                }
            }
            catch (Exception)
            {
                _settings = new AppSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("설정을 저장하는 중 오류가 발생했습니다.", ex);
            }
        }
    }

    public class AppSettings
    {
        public bool AlwaysOnTop { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool SavePasswordsEncrypted { get; set; } = true;
        public bool AutoReconnect { get; set; } = false;
        public int AutoReconnectInterval { get; set; } = 30;
        public string DefaultConnectionType { get; set; } = "SSH";
        public string Theme { get; set; } = "Dark";
        public bool ShowToolbar { get; set; } = true;
        public bool ShowStatusBar { get; set; } = true;
        public int TabPosition { get; set; } = 0; // 0=Top, 1=Bottom, 2=Left, 3=Right
        public bool ConfirmBeforeClosing { get; set; } = true;
        public string FontFamily { get; set; } = "Consolas";
        public int FontSize { get; set; } = 12;
    }
} 