using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic; // Added missing import
using System.Threading;
using System.Windows; // Added for Application.Current access

namespace SochoPutty.Models
{
    public class PuttySession : IDisposable
    {
        private Process? _puttyProcess;
        private readonly ConnectionInfo _connectionInfo;
        private IntPtr _puttyWindowHandle;
        private bool _disposed = false;
        private Timer? _processMonitorTimer;


        // Windows API imports
        [DllImport("user32.dll")]
        private static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int SW_SHOW = 5;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        // Window styles
        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_BORDER = 0x00800000;


        public ConnectionInfo ConnectionInfo => _connectionInfo;
        public bool IsConnected => _puttyProcess != null && !_puttyProcess.HasExited;
        public IntPtr PuttyWindowHandle => _puttyWindowHandle;

        // PuTTY 프로세스 종료 이벤트
        public event Action? ProcessExited;

        public PuttySession(ConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
        }

        public async Task<bool> StartPutty(IntPtr parentHandle)
        {
            DebugLogger.LogInfo($"PuTTY 시작 요청: {_connectionInfo.Name} -> {_connectionInfo.Hostname}:{_connectionInfo.Port}");
            DebugLogger.LogDebug($"부모 핸들: {parentHandle}");
            
            try
            {
                var puttyPath = FindPuttyExecutable();
                if (string.IsNullOrEmpty(puttyPath))
                {
                    var errorMessage = "PuTTY 실행 파일을 찾을 수 없습니다.\n\n" +
                        "putty.exe 파일이 실행 파일과 같은 디렉토리에 있는지 확인해주세요.\n" +
                        $"예상 경로: {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "putty.exe")}";
                    DebugLogger.LogError(errorMessage);
                    throw new FileNotFoundException(errorMessage);
                }

                DebugLogger.LogInfo($"PuTTY 실행 파일 발견: {puttyPath}");

                var arguments = BuildPuttyArguments();
                DebugLogger.LogInfo($"PuTTY 실행 명령: {puttyPath} {arguments}");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = puttyPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                _puttyProcess = Process.Start(startInfo);
                
                if (_puttyProcess == null)
                {
                    var errorMessage = "PuTTY 프로세스를 시작할 수 없습니다.";
                    DebugLogger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                DebugLogger.LogInfo($"PuTTY 프로세스 시작됨. PID: {_puttyProcess.Id}");

                // PuTTY 창이 생성될 때까지 대기
                await WaitForPuttyWindow();

                // PuTTY 창을 부모 컨테이너에 임베드
                if (_puttyWindowHandle != IntPtr.Zero)
                {
                    DebugLogger.LogDebug($"PuTTY 창 핸들 발견: {_puttyWindowHandle}");
                    EmbedPuttyWindow(parentHandle);
                    DebugLogger.LogInfo("PuTTY 창 임베딩 완료");
                    
                    // 프로세스 모니터링 시작
                    StartProcessMonitoring();
                    
                    return true;
                }

                DebugLogger.LogError("PuTTY 창 핸들을 찾을 수 없음");
                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"PuTTY 시작 중 오류 발생: {_connectionInfo.Name}", ex);
                throw new InvalidOperationException($"PuTTY 세션 시작 중 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        private string FindPuttyExecutable()
        {
            DebugLogger.LogDebug("PuTTY 실행 파일 찾기 시작");
            
            // 실행 파일과 같은 디렉토리에서 putty.exe 찾기
            var puttyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "putty.exe");
            DebugLogger.LogDebug($"PuTTY 경로 확인: {puttyPath}");
            
            if (File.Exists(puttyPath))
            {
                DebugLogger.LogInfo($"PuTTY 실행 파일 발견: {puttyPath}");
                return puttyPath;
            }
            
            DebugLogger.LogError($"PuTTY 실행 파일이 존재하지 않음: {puttyPath}");
            return string.Empty;
        }

        private string BuildPuttyArguments()
        {
            DebugLogger.LogDebug("PuTTY 인수 생성 시작");
            
            var args = new List<string>();

            // 연결 타입
            var connectionTypeArg = _connectionInfo.ConnectionType switch
            {
                ConnectionType.SSH => "-ssh",
                ConnectionType.Telnet => "-telnet",
                ConnectionType.Raw => "-raw",
                ConnectionType.Rlogin => "-rlogin",
                _ => "-ssh" // 기본값
            };
            args.Add(connectionTypeArg);
            DebugLogger.LogDebug($"연결 타입: {connectionTypeArg}");

            // 호스트명과 포트
            args.Add($"{_connectionInfo.Hostname}");
            args.Add($"-P {_connectionInfo.Port}");
            DebugLogger.LogDebug($"대상: {_connectionInfo.Hostname}:{_connectionInfo.Port}");

            // 사용자명
            if (!string.IsNullOrEmpty(_connectionInfo.Username))
            {
                args.Add($"-l {_connectionInfo.Username}");
                DebugLogger.LogDebug($"사용자명: {_connectionInfo.Username}");
            }

            // 비밀번호 (보안상 권장하지 않음, but 편의를 위해)
            if (!string.IsNullOrEmpty(_connectionInfo.Password))
            {
                args.Add($"-pw {_connectionInfo.Password}");
                DebugLogger.LogDebug("비밀번호: [설정됨]");
            }

            // 개인키 파일
            if (!string.IsNullOrEmpty(_connectionInfo.PrivateKeyPath))
            {
                if (File.Exists(_connectionInfo.PrivateKeyPath))
                {
                    args.Add($"-i \"{_connectionInfo.PrivateKeyPath}\"");
                    DebugLogger.LogDebug($"개인키: {_connectionInfo.PrivateKeyPath}");
                }
                else
                {
                    DebugLogger.LogWarning($"개인키 파일이 존재하지 않음: {_connectionInfo.PrivateKeyPath}");
                }
            }

            var argumentString = string.Join(" ", args);
            DebugLogger.LogDebug($"최종 인수: {argumentString}");
            return argumentString;
        }

        private async Task WaitForPuttyWindow()
        {
            var maxAttempts = 50; // 5초 대기 (100ms * 50)
            var attempts = 0;

            DebugLogger.LogDebug($"PuTTY 창 대기 시작 (최대 {maxAttempts * 100}ms)");

            while (attempts < maxAttempts)
            {
                if (_puttyProcess?.HasExited == true)
                {
                    var errorMessage = "PuTTY 프로세스가 예기치 않게 종료되었습니다.";
                    DebugLogger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                var windowHandle = FindPuttyWindowByProcess();
                if (windowHandle != IntPtr.Zero)
                {
                    _puttyWindowHandle = windowHandle;
                    DebugLogger.LogInfo($"PuTTY 창 발견: {windowHandle} (시도 횟수: {attempts + 1})");
                    return;
                }

                attempts++;
                if (attempts % 10 == 0) // 매 1초마다 로그
                {
                    DebugLogger.LogDebug($"PuTTY 창 대기 중... (시도: {attempts}/{maxAttempts})");
                }

                await Task.Delay(100);
            }

            var timeoutMessage = "PuTTY 창을 찾을 수 없습니다.";
            DebugLogger.LogError(timeoutMessage);
            throw new TimeoutException(timeoutMessage);
        }

        private IntPtr FindPuttyWindowByProcess()
        {
            if (_puttyProcess == null) return IntPtr.Zero;

            // PuTTY 창 클래스명으로 찾기
            var puttyWindows = new List<IntPtr>();
            var currentWindow = IntPtr.Zero;

            do
            {
                currentWindow = FindWindow("PuTTY", null);
                if (currentWindow != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(currentWindow, out uint processId);
                    if (processId == _puttyProcess.Id)
                    {
                        return currentWindow;
                    }
                }
            } while (currentWindow != IntPtr.Zero);

            return IntPtr.Zero;
        }




        private void EmbedPuttyWindow(IntPtr parentHandle)
        {
            DebugLogger.LogDebug($"PuTTY 창 임베딩 시작: 창={_puttyWindowHandle}, 부모={parentHandle}");
            
            if (_puttyWindowHandle == IntPtr.Zero || parentHandle == IntPtr.Zero)
            {
                DebugLogger.LogWarning("PuTTY 창 임베딩 실패: 핸들이 유효하지 않음");
                return;
            }

            try
            {
                // 현재 창 스타일 가져오기
                var currentStyle = GetWindowLong(_puttyWindowHandle, GWL_STYLE);
                DebugLogger.LogDebug($"현재 PuTTY 창 스타일: 0x{currentStyle:X8}");

                // 제목 표시줄, 테두리, 시스템 메뉴 등 제거
                var newStyle = currentStyle;
                newStyle &= ~WS_CAPTION;      // 제목 표시줄 제거
                newStyle &= ~WS_THICKFRAME;   // 크기 조정 가능한 테두리 제거
                newStyle &= ~WS_SYSMENU;      // 시스템 메뉴 (닫기 버튼 등) 제거
                newStyle &= ~WS_MINIMIZEBOX;  // 최소화 버튼 제거
                newStyle &= ~WS_MAXIMIZEBOX;  // 최대화 버튼 제거
                newStyle &= ~WS_BORDER;       // 얇은 테두리 제거

                DebugLogger.LogDebug($"새로운 PuTTY 창 스타일: 0x{newStyle:X8}");

                // 새로운 창 스타일 적용
                var setStyleResult = SetWindowLong(_puttyWindowHandle, GWL_STYLE, newStyle);
                DebugLogger.LogDebug($"SetWindowLong 결과: 0x{setStyleResult:X8}");

                // PuTTY 창을 부모 컨테이너의 자식으로 설정
                var setParentResult = SetParent(_puttyWindowHandle, parentHandle);
                DebugLogger.LogDebug($"SetParent 결과: {setParentResult}");
                
                // 창 표시
                var showWindowResult = ShowWindow(_puttyWindowHandle, SW_SHOW);
                DebugLogger.LogDebug($"ShowWindow 결과: {showWindowResult}");
                
                // 간단한 접근: 기본 크기로 PuTTY 창 설정 (나중에 ResizePuttyWindow에서 정확히 조정됨)
                var offsetX = -8;  // 좌측 테두리 가리기
                var offsetY = -30; // 상단 제목표시줄 가리기
                var expandedWidth = 800 + 16;   // 좌우 테두리 가리기
                var expandedHeight = 600 + 38;  // 상하 테두리 가리기
                
                DebugLogger.LogDebug($"PuTTY 창 임베딩 - 기본 크기: {expandedWidth}x{expandedHeight} @ ({offsetX},{offsetY})");
                
                // 창 위치와 크기 설정 (제목 표시줄과 테두리 가리기)
                var setWindowPosResult = SetWindowPos(_puttyWindowHandle, IntPtr.Zero, 
                    offsetX, offsetY, expandedWidth, expandedHeight, 
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                DebugLogger.LogDebug($"SetWindowPos 결과: {setWindowPosResult}");
                
                DebugLogger.LogInfo($"PuTTY 창 임베딩 완료");
                DebugLogger.LogDebug($"→ SetParent: {setParentResult}, ShowWindow: {showWindowResult}, SetWindowPos: {setWindowPosResult}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("PuTTY 창 임베딩 중 예외 발생", ex);
            }
        }



        public void ResizePuttyWindow(int width, int height)
        {
            if (_puttyWindowHandle == IntPtr.Zero || _disposed)
            {
                DebugLogger.LogDebug($"PuTTY 창 크기 조정 건너뜀 - 핸들: {_puttyWindowHandle}, 폐기됨: {_disposed}");
                return;
            }

            try
            {
                // 간단한 접근: 활성 세션 크기에 맞춰 PuTTY 창을 정확히 맞춤
                // PuTTY 창의 테두리를 완전히 가리기 위해 약간 확장하고 오프셋 적용
                var offsetX = -8;  // 좌측 테두리 가리기
                var offsetY = -30; // 상단 제목표시줄 가리기
                var expandedWidth = width + 16;   // 좌우 테두리 가리기 (8 * 2)
                var expandedHeight = height + 38; // 상하 테두리 가리기 (30 + 8)
                
                DebugLogger.LogDebug($"PuTTY 창 크기 조정 - 요청: {width}x{height} → 최종: {expandedWidth}x{expandedHeight} @ ({offsetX},{offsetY})");
                
                // PuTTY 창 크기와 위치 설정
                var setWindowPosResult = SetWindowPos(_puttyWindowHandle, IntPtr.Zero, 
                    offsetX, offsetY, expandedWidth, expandedHeight, 
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                
                if (setWindowPosResult)
                {
                    DebugLogger.LogDebug($"PuTTY 창 크기 조정 성공: {width}x{height}");
                }
                else
                {
                    DebugLogger.LogWarning($"PuTTY 창 크기 조정 실패: {width}x{height}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"PuTTY 창 크기 조정 중 예외 발생: {width}x{height}", ex);
            }
        }

        public void FocusPuttyWindow()
        {
            if (_puttyWindowHandle == IntPtr.Zero || _disposed)
                return;

            try
            {
                // PuTTY 창에 포커스 설정
                var bringToTopResult = BringWindowToTop(_puttyWindowHandle);
                var setForegroundResult = SetForegroundWindow(_puttyWindowHandle);
                var setFocusResult = SetFocus(_puttyWindowHandle);
                
                DebugLogger.LogDebug($"PuTTY 창 포커스 설정 - BringToTop: {bringToTopResult}, SetForeground: {setForegroundResult}, SetFocus: {setFocusResult}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("PuTTY 창 포커스 설정 중 오류", ex);
            }
        }

        private void StartProcessMonitoring()
        {
            DebugLogger.LogInfo($"PuTTY 프로세스 모니터링 시작: {_connectionInfo.Name}");
            
            // 1초마다 프로세스 상태 확인
            _processMonitorTimer = new Timer(OnProcessMonitorTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void OnProcessMonitorTimer(object? state)
        {
            try
            {
                if (_disposed || _puttyProcess == null)
                {
                    StopProcessMonitoring();
                    return;
                }

                // 프로세스가 종료되었는지 확인
                if (_puttyProcess.HasExited)
                {
                    DebugLogger.LogInfo($"PuTTY 프로세스 종료 감지: {_connectionInfo.Name} (Exit Code: {_puttyProcess.ExitCode})");
                    
                    StopProcessMonitoring();
                    
                    // 프로세스 종료 이벤트 발생
                    ProcessExited?.Invoke();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"PuTTY 프로세스 모니터링 중 오류: {_connectionInfo.Name}", ex);
                StopProcessMonitoring();
            }
        }

        private void StopProcessMonitoring()
        {
            try
            {
                _processMonitorTimer?.Dispose();
                _processMonitorTimer = null;
                DebugLogger.LogDebug($"PuTTY 프로세스 모니터링 중지: {_connectionInfo.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"PuTTY 프로세스 모니터링 중지 중 오류: {_connectionInfo.Name}", ex);
            }
        }



        public void Disconnect()
        {
            try
            {
                DebugLogger.LogInfo($"PuTTY 연결 해제 시작: {_connectionInfo.Name}");
                
                // 프로세스 모니터링 중지
                StopProcessMonitoring();
                
                
                if (_puttyProcess != null && !_puttyProcess.HasExited)
                {
                    DebugLogger.LogDebug($"PuTTY 프로세스 종료 시도: {_connectionInfo.Name} (PID: {_puttyProcess.Id})");
                    
                    // 비동기로 프로세스 종료 처리
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _puttyProcess.CloseMainWindow();
                            
                            // 백그라운드에서 정상 종료를 기다림 (UI 블로킹 방지)
                            var waitTask = Task.Run(() => _puttyProcess.WaitForExit(3000));
                            var completed = await waitTask.ConfigureAwait(false);
                            
                            if (!completed)
                            {
                                DebugLogger.LogWarning($"PuTTY 프로세스 강제 종료: {_connectionInfo.Name}");
                                try
                                {
                                    _puttyProcess.Kill();
                                    await Task.Run(() => _puttyProcess.WaitForExit(1000)).ConfigureAwait(false);
                                }
                                catch (Exception killEx)
                                {
                                    DebugLogger.LogError($"PuTTY 프로세스 강제 종료 실패: {_connectionInfo.Name}", killEx);
                                }
                            }
                            else
                            {
                                DebugLogger.LogInfo($"PuTTY 프로세스 정상 종료: {_connectionInfo.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogError($"PuTTY 프로세스 비동기 종료 중 오류: {_connectionInfo.Name}", ex);
                        }
                        finally
                        {
                            try
                            {
                                _puttyProcess?.Dispose();
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.LogError($"PuTTY 프로세스 리소스 해제 중 예외: {_connectionInfo.Name}", ex);
                            }
                        }
                    });
                    
                    // 즉시 리소스 정리 (UI 블로킹 방지)
                    _puttyProcess = null;
                    _puttyWindowHandle = IntPtr.Zero;
                }
                else if (_puttyProcess != null)
                {
                    DebugLogger.LogDebug($"PuTTY 프로세스 이미 종료됨: {_connectionInfo.Name}");
                    try
                    {
                        _puttyProcess?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogError($"PuTTY 프로세스 리소스 해제 중 예외: {_connectionInfo.Name}", ex);
                    }
                    _puttyProcess = null;
                    _puttyWindowHandle = IntPtr.Zero;
                }
                
                
                DebugLogger.LogDebug($"PuTTY 리소스 정리 완료: {_connectionInfo.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"PuTTY 연결 해제 중 예외 발생: {_connectionInfo.Name}", ex);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Disconnect();
                }
                _disposed = true;
            }
        }

        ~PuttySession()
        {
            Dispose(false);
        }
    }
} 