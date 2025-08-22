using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace SochoPutty.Models
{
    public static class DebugLogger
    {
        private static readonly string LogDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SochoPutty", "Logs");

        static DebugLogger()
        {
            // 로그 디렉토리 생성
            Directory.CreateDirectory(LogDirectoryPath);
            
            // 애플리케이션 시작 시 오래된 로그 파일 정리
            CleanupOldLogFiles();
        }

        /// <summary>
        /// 현재 날짜의 로그 파일 경로를 반환
        /// </summary>
        private static string GetCurrentLogFilePath()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(LogDirectoryPath, $"debug_{today}.log");
        }

        /// <summary>
        /// 하루가 지난 로그 파일들을 자동으로 삭제
        /// </summary>
        private static void CleanupOldLogFiles()
        {
            try
            {
                if (!Directory.Exists(LogDirectoryPath)) return;

                var logFiles = Directory.GetFiles(LogDirectoryPath, "debug_*.log");
                var yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                
                foreach (var logFile in logFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(logFile);
                    // debug_yyyy-MM-dd 형식에서 날짜 부분 추출
                    if (fileName.StartsWith("debug_") && fileName.Length >= 16)
                    {
                        var dateString = fileName.Substring(6); // "debug_" 제거
                        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", null, 
                                                   System.Globalization.DateTimeStyles.None, out var logDate))
                        {
                            // 하루가 지난 파일 삭제
                            if (logDate < DateTime.Now.Date)
                            {
                                File.Delete(logFile);
                                System.Diagnostics.Debug.WriteLine($"오래된 로그 파일 삭제: {logFile}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 파일 정리 중 오류: {ex.Message}");
            }
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] [{level}] {message}";

            try
            {
                // 현재 날짜의 로그 파일에 기록
                var currentLogFile = GetCurrentLogFilePath();
                File.AppendAllText(currentLogFile, logEntry + Environment.NewLine);
                
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
                var currentLogFile = GetCurrentLogFilePath();
                if (File.Exists(currentLogFile))
                {
                    var logContent = File.ReadAllText(currentLogFile);
                    var today = DateTime.Now.ToString("yyyy-MM-dd");
                    
                    var window = new Window
                    {
                        Title = $"디버그 로그 - {today}",
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
                    MessageBox.Show("오늘 날짜의 로그 파일이 존재하지 않습니다.", "정보", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (Directory.Exists(LogDirectoryPath))
                {
                    var logFiles = Directory.GetFiles(LogDirectoryPath, "debug_*.log");
                    foreach (var logFile in logFiles)
                    {
                        File.Delete(logFile);
                    }
                    Log("모든 로그 파일이 초기화되었습니다.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 초기화 오류: {ex.Message}");
            }
        }

        public static string GetLogDirectoryPath() => LogDirectoryPath;
        
        public static string GetTodayLogFilePath()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(LogDirectoryPath, $"debug_{today}.log");
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
} 