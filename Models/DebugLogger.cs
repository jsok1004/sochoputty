using System;
using System.IO;
using System.Windows;

namespace SochoPutty.Models
{
    public static class DebugLogger
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SochoPutty", "debug.log");

        static DebugLogger()
        {
            // 로그 디렉토리 생성
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] [{level}] {message}";

            try
            {
                // 파일에 로그 기록
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                
                // 디버그 콘솔에 출력
                System.Diagnostics.Debug.WriteLine(logEntry);
                
                // 콘솔에 출력 (개발 중)
                Console.WriteLine(logEntry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로깅 오류: {ex.Message}");
            }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message} - 예외: {ex}" : message;
            Log(fullMessage, LogLevel.Error);
        }

        public static void LogWarning(string message)
        {
            Log(message, LogLevel.Warning);
        }

        public static void LogInfo(string message)
        {
            Log(message, LogLevel.Info);
        }

        public static void LogDebug(string message)
        {
            Log(message, LogLevel.Debug);
        }

        public static void ShowDebugWindow()
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    var logContent = File.ReadAllText(LogFilePath);
                    var window = new Window
                    {
                        Title = "디버그 로그",
                        Width = 800,
                        Height = 600,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };

                    var textBox = new System.Windows.Controls.TextBox
                    {
                        Text = logContent,
                        IsReadOnly = true,
                        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 12
                    };

                    window.Content = textBox;
                    window.Show();
                }
                else
                {
                    MessageBox.Show("로그 파일이 존재하지 않습니다.", "정보", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로그 창을 열 수 없습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ClearLog()
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    File.Delete(LogFilePath);
                    Log("로그가 초기화되었습니다.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 초기화 오류: {ex.Message}");
            }
        }

        public static string GetLogFilePath() => LogFilePath;
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
} 