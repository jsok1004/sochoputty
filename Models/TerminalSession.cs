using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows; // Application.Current access

namespace SochoPutty.Models
{
    /// <summary>
    /// 임베딩된 PowerShell 콘솔에서 Windows 기본 OpenSSH(ssh)를 실행하는 세션.
    /// powershell.exe를 숨김 상태로 직접 실행한 뒤, 콘솔 창(ConsoleWindowClass)을
    /// SetParent로 앱 탭의 WinForms Panel에 리페어런팅(임베딩)한다.
    /// </summary>
    public class TerminalSession : IDisposable
    {
        private Process? _process;
        private readonly ConnectionInfo _connectionInfo;
        private IntPtr _terminalWindowHandle;
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
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        // 다른 프로세스 소유 창에 SetFocus 하려면 입력 큐를 연결해야 함
        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        // 임베드된 콘솔 창에 문자 입력(WM_CHAR)을 전달하기 위한 메시지 전송
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // 다중 세션에서 PID로 콘솔 창을 정확히 찾기 위한 열거/클래스명 조회
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

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
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        // 콘솔 창에 문자 입력을 전달하는 메시지
        private const uint WM_CHAR = 0x0102;

        // 레거시 콘솔 창 클래스명
        private const string ConsoleWindowClass = "ConsoleWindowClass";

        public ConnectionInfo ConnectionInfo => _connectionInfo;
        public bool IsConnected => _process != null && !_process.HasExited;
        public IntPtr TerminalWindowHandle => _terminalWindowHandle;

        // 콘솔 프로세스 종료 이벤트
        public event Action? ProcessExited;

        public TerminalSession(ConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
        }

        public async Task<bool> StartTerminal(IntPtr parentHandle)
        {
            DebugLogger.LogInfo($"터미널 시작 요청: {_connectionInfo.Name} -> {_connectionInfo.Hostname}:{_connectionInfo.Port}");
            DebugLogger.LogDebug($"부모 핸들: {parentHandle}");

            try
            {
                // OpenSSH 클라이언트 존재 확인
                var sshPath = FindSshExecutable();
                if (string.IsNullOrEmpty(sshPath))
                {
                    var errorMessage = "OpenSSH 클라이언트(ssh.exe)를 찾을 수 없습니다.\n\n" +
                        "Windows 설정 > 앱 > 선택적 기능에서 'OpenSSH 클라이언트'를 설치해주세요.\n" +
                        $"예상 경로: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe")}";
                    DebugLogger.LogError(errorMessage);
                    throw new FileNotFoundException(errorMessage);
                }

                DebugLogger.LogInfo($"OpenSSH 클라이언트 발견: {sshPath}");

                // powershell.exe를 직접 실행한다. 콘솔 창(ConsoleWindowClass)은 호환성 계층에 의해
                // 루트 클라이언트(powershell) PID를 보고하므로 _process.Id로 정확히 매칭된다.
                // (conhost.exe 래퍼 경유 시 창이 자식 powershell PID로 보고되어 매칭에 실패했음)
                var powershellPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe");
                var arguments = BuildPowerShellArguments();
                DebugLogger.LogInfo($"터미널 실행 명령: {powershellPath} {arguments}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = powershellPath,
                    Arguments = arguments,
                    // WindowStyle은 ShellExecute 경로에서만 적용된다
                    UseShellExecute = true,
                    // 콘솔 창을 숨김 상태로 생성 → 임베드 전 데스크톱 노출/포커스 탈취 방지.
                    // 숨김 세션은 Win11 기본 터미널(Windows Terminal) 위임도 발생하지 않는다.
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _process = Process.Start(startInfo);

                if (_process == null)
                {
                    var errorMessage = "터미널 프로세스를 시작할 수 없습니다.";
                    DebugLogger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                DebugLogger.LogInfo($"터미널 프로세스 시작됨. PID: {_process.Id}");

                // 콘솔 창이 생성될 때까지 대기
                await WaitForTerminalWindow();

                // 콘솔 창을 부모 컨테이너에 임베드
                if (_terminalWindowHandle != IntPtr.Zero)
                {
                    DebugLogger.LogDebug($"콘솔 창 핸들 발견: {_terminalWindowHandle}");
                    EmbedTerminalWindow(parentHandle);
                    DebugLogger.LogInfo("콘솔 창 임베딩 완료");

                    // 프로세스 모니터링 시작
                    StartProcessMonitoring();

                    return true;
                }

                DebugLogger.LogError("콘솔 창 핸들을 찾을 수 없음");
                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"터미널 시작 중 오류 발생: {_connectionInfo.Name}", ex);
                throw new InvalidOperationException($"터미널 세션 시작 중 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Windows 기본 OpenSSH 클라이언트(ssh.exe) 경로를 반환한다. 없으면 빈 문자열.
        /// PowerShell이 PATH로 ssh를 찾으므로 여기서는 존재 확인 용도.
        /// </summary>
        private string FindSshExecutable()
        {
            DebugLogger.LogDebug("OpenSSH 클라이언트 찾기 시작");

            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var candidates = new[]
            {
                Path.Combine(systemPath, "OpenSSH", "ssh.exe"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    DebugLogger.LogInfo($"OpenSSH 클라이언트 발견: {candidate}");
                    return candidate;
                }
            }

            // PATH 상의 ssh.exe도 허용
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), "ssh.exe");
                    if (File.Exists(candidate))
                    {
                        DebugLogger.LogInfo($"PATH에서 OpenSSH 클라이언트 발견: {candidate}");
                        return candidate;
                    }
                }
                catch { /* 잘못된 PATH 항목 무시 */ }
            }

            DebugLogger.LogError("OpenSSH 클라이언트(ssh.exe)를 찾을 수 없음");
            return string.Empty;
        }

        /// <summary>
        /// powershell.exe에 전달할 인자를 생성한다.
        /// 형태: -NoLogo -NoExit -Command "& { ssh ... }"
        /// </summary>
        private string BuildPowerShellArguments()
        {
            var sshCommand = BuildSshCommand();
            DebugLogger.LogDebug($"SSH 명령: {sshCommand}");

            // PowerShell -Command 내부에서 실행할 문자열. 작은따옴표로 감싸고 내부 작은따옴표는 이스케이프.
            var escaped = sshCommand.Replace("'", "''");
            return $"-NoLogo -NoExit -Command \"& {{ {escaped} }}\"";
        }

        /// <summary>
        /// ConnectionInfo를 OpenSSH ssh 명령줄로 매핑한다.
        /// 비밀번호는 ssh 명령줄로 전달할 수 없으므로 포함하지 않는다(키 인증 또는 대화형 입력).
        /// </summary>
        private string BuildSshCommand()
        {
            DebugLogger.LogDebug("SSH 명령 생성 시작");

            var args = new List<string> { "ssh" };

            // 포트 (OpenSSH는 소문자 -p)
            args.Add($"-p {_connectionInfo.Port}");

            // 개인키 파일 (OpenSSH 형식)
            if (!string.IsNullOrEmpty(_connectionInfo.PrivateKeyPath))
            {
                if (File.Exists(_connectionInfo.PrivateKeyPath))
                {
                    args.Add($"-i '{_connectionInfo.PrivateKeyPath}'");
                    DebugLogger.LogDebug($"개인키: {_connectionInfo.PrivateKeyPath}");
                }
                else
                {
                    DebugLogger.LogWarning($"개인키 파일이 존재하지 않음: {_connectionInfo.PrivateKeyPath}");
                }
            }

            // 최초 접속 시 호스트키 프롬프트로 막히지 않도록(자동 수락, 변경 시엔 거부)
            args.Add("-o StrictHostKeyChecking=accept-new");

            // 사용자@호스트 (사용자명 없으면 호스트만)
            var target = string.IsNullOrEmpty(_connectionInfo.Username)
                ? _connectionInfo.Hostname
                : $"{_connectionInfo.Username}@{_connectionInfo.Hostname}";
            args.Add(target);

            var command = string.Join(" ", args);
            DebugLogger.LogDebug($"최종 SSH 명령: {command}");
            return command;
        }

        private async Task WaitForTerminalWindow()
        {
            var maxAttempts = 50; // 5초 대기 (100ms * 50)
            var attempts = 0;

            DebugLogger.LogDebug($"콘솔 창 대기 시작 (최대 {maxAttempts * 100}ms)");

            while (attempts < maxAttempts)
            {
                if (_process?.HasExited == true)
                {
                    var errorMessage = "터미널 프로세스가 예기치 않게 종료되었습니다.";
                    DebugLogger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                var windowHandle = FindTerminalWindowByProcess();
                if (windowHandle != IntPtr.Zero)
                {
                    _terminalWindowHandle = windowHandle;
                    DebugLogger.LogInfo($"콘솔 창 발견: {windowHandle} (시도 횟수: {attempts + 1})");
                    return;
                }

                attempts++;
                if (attempts % 10 == 0) // 매 1초마다 로그
                {
                    DebugLogger.LogDebug($"콘솔 창 대기 중... (시도: {attempts}/{maxAttempts})");
                }

                await Task.Delay(100);
            }

            var timeoutMessage = "콘솔 창을 찾을 수 없습니다.";
            DebugLogger.LogError(timeoutMessage);
            throw new TimeoutException(timeoutMessage);
        }

        /// <summary>
        /// 현재 세션의 콘솔 창(ConsoleWindowClass)을 EnumWindows + PID 매칭으로 찾는다.
        /// 콘솔 창은 호환성 계층에 의해 루트 클라이언트(powershell) PID를 보고하므로 _process.Id와 일치한다.
        /// 창은 숨김 상태로 생성되므로 가시성 검사는 하지 않는다. 다중 세션에서도 각각 정확히 구분된다.
        /// </summary>
        private IntPtr FindTerminalWindowByProcess()
        {
            if (_process == null) return IntPtr.Zero;

            var targetPid = (uint)_process.Id;
            var found = IntPtr.Zero;
            var classNameBuffer = new StringBuilder(256);

            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != targetPid)
                    return true;

                classNameBuffer.Clear();
                GetClassName(hWnd, classNameBuffer, classNameBuffer.Capacity);
                if (classNameBuffer.ToString() == ConsoleWindowClass)
                {
                    found = hWnd;
                    return false; // 열거 중단
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }

        private void EmbedTerminalWindow(IntPtr parentHandle)
        {
            DebugLogger.LogDebug($"콘솔 창 임베딩 시작: 창={_terminalWindowHandle}, 부모={parentHandle}");

            if (_terminalWindowHandle == IntPtr.Zero || parentHandle == IntPtr.Zero)
            {
                DebugLogger.LogWarning("콘솔 창 임베딩 실패: 핸들이 유효하지 않음");
                return;
            }

            try
            {
                // 현재 창 스타일 가져오기
                var currentStyle = GetWindowLong(_terminalWindowHandle, GWL_STYLE);
                DebugLogger.LogDebug($"현재 콘솔 창 스타일: 0x{currentStyle:X8}");

                // 제목 표시줄, 테두리, 시스템 메뉴 등 제거
                var newStyle = currentStyle;
                newStyle &= ~WS_CAPTION;      // 제목 표시줄 제거
                newStyle &= ~WS_THICKFRAME;   // 크기 조정 가능한 테두리 제거
                newStyle &= ~WS_SYSMENU;      // 시스템 메뉴 (닫기 버튼 등) 제거
                newStyle &= ~WS_MINIMIZEBOX;  // 최소화 버튼 제거
                newStyle &= ~WS_MAXIMIZEBOX;  // 최대화 버튼 제거
                newStyle &= ~WS_BORDER;       // 얇은 테두리 제거
                newStyle &= ~WS_POPUP;        // 최상위 팝업 스타일 제거
                newStyle |= WS_CHILD;         // SetParent 전에 자식 스타일 지정(MSDN SetParent 권고)

                DebugLogger.LogDebug($"새로운 콘솔 창 스타일: 0x{newStyle:X8}");

                // 새로운 창 스타일 적용
                var setStyleResult = SetWindowLong(_terminalWindowHandle, GWL_STYLE, newStyle);
                DebugLogger.LogDebug($"SetWindowLong 결과: 0x{setStyleResult:X8}");

                // 콘솔 창을 부모 컨테이너의 자식으로 설정
                var setParentResult = SetParent(_terminalWindowHandle, parentHandle);
                DebugLogger.LogDebug($"SetParent 결과: {setParentResult}");

                // 기본 크기로 콘솔 창 설정 (이후 ResizeTerminalWindow에서 정확히 조정됨)
                DebugLogger.LogDebug("콘솔 창 임베딩 - 기본 크기 800x600 @ (0,0)");
                var setWindowPosResult = SetWindowPos(_terminalWindowHandle, IntPtr.Zero,
                    0, 0, 800, 600,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                DebugLogger.LogDebug($"SetWindowPos 결과: {setWindowPosResult}");

                // 숨김 상태로 생성된 콘솔 창을 임베딩 완료 후 표시
                var showWindowResult = ShowWindow(_terminalWindowHandle, SW_SHOW);
                DebugLogger.LogDebug($"ShowWindow 결과: {showWindowResult}");

                DebugLogger.LogInfo("콘솔 창 임베딩 완료");
                DebugLogger.LogDebug($"→ SetParent: {setParentResult}, ShowWindow: {showWindowResult}, SetWindowPos: {setWindowPosResult}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("콘솔 창 임베딩 중 예외 발생", ex);
            }
        }

        public void ResizeTerminalWindow(int width, int height)
        {
            if (_terminalWindowHandle == IntPtr.Zero || _disposed)
            {
                DebugLogger.LogDebug($"콘솔 창 크기 조정 건너뜀 - 핸들: {_terminalWindowHandle}, 폐기됨: {_disposed}");
                return;
            }

            try
            {
                // 콘솔 창을 호스트 패널 크기에 맞춤. 콘솔은 제목표시줄/테두리를 제거했으므로
                // 오프셋 없이 (0,0)에서 전체 크기로 채운다.
                DebugLogger.LogDebug($"콘솔 창 크기 조정 - 요청: {width}x{height}");

                var setWindowPosResult = SetWindowPos(_terminalWindowHandle, IntPtr.Zero,
                    0, 0, width, height,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

                if (setWindowPosResult)
                {
                    DebugLogger.LogDebug($"콘솔 창 크기 조정 성공: {width}x{height}");
                }
                else
                {
                    DebugLogger.LogWarning($"콘솔 창 크기 조정 실패: {width}x{height}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"콘솔 창 크기 조정 중 예외 발생: {width}x{height}", ex);
            }
        }

        public void FocusTerminalWindow()
        {
            if (_terminalWindowHandle == IntPtr.Zero || _disposed)
                return;

            try
            {
                // 콘솔 창은 다른 프로세스 스레드 소유이므로 AttachThreadInput으로
                // 입력 큐를 연결해야 SetFocus가 동작한다.
                var targetThreadId = GetWindowThreadProcessId(_terminalWindowHandle, out _);
                var currentThreadId = GetCurrentThreadId();

                if (targetThreadId == 0 || targetThreadId == currentThreadId)
                {
                    SetFocus(_terminalWindowHandle);
                    return;
                }

                AttachThreadInput(currentThreadId, targetThreadId, true);
                try
                {
                    var previousFocus = SetFocus(_terminalWindowHandle);
                    DebugLogger.LogDebug($"콘솔 창 포커스 설정 - 이전 포커스: {previousFocus}");
                }
                finally
                {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("콘솔 창 포커스 설정 중 오류", ex);
            }
        }

        /// <summary>
        /// 임베드된 콘솔 창에 텍스트를 문자 단위(WM_CHAR)로 전송한다.
        /// 비밀번호가 포함될 수 있으므로 내용은 절대 로그에 남기지 않는다(길이만 기록).
        /// </summary>
        public void SendTextToTerminal(string text)
        {
            if (_terminalWindowHandle == IntPtr.Zero || _disposed || string.IsNullOrEmpty(text))
            {
                DebugLogger.LogDebug($"터미널 텍스트 전송 건너뜀 - 핸들: {_terminalWindowHandle}, 폐기됨: {_disposed}");
                return;
            }

            try
            {
                var sent = 0;
                for (var i = 0; i < text.Length; i++)
                {
                    var ch = text[i];

                    // 개행 정규화: "\r\n"은 '\r' 1회로, 단독 '\n'도 '\r'로 전송 (Enter 중복 방지)
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    else if (ch == '\n')
                    {
                        ch = '\r';
                    }

                    if (!PostMessage(_terminalWindowHandle, WM_CHAR, (IntPtr)ch, IntPtr.Zero))
                    {
                        // 메시지 큐 포화 등 일시 실패 시 잠시 대기 후 1회 재시도
                        Thread.Sleep(10);
                        if (!PostMessage(_terminalWindowHandle, WM_CHAR, (IntPtr)ch, IntPtr.Zero))
                        {
                            DebugLogger.LogWarning($"터미널 텍스트 전송 중단: {sent}자 전송 후 PostMessage 실패");
                            return;
                        }
                    }

                    sent++;

                    // 대량 전송 시 메시지 큐 포화 방지
                    if (sent % 512 == 0)
                    {
                        Thread.Sleep(1);
                    }
                }

                DebugLogger.LogDebug($"터미널 텍스트 전송 완료: {sent}자");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("터미널 텍스트 전송 중 오류", ex);
            }
        }

        private void StartProcessMonitoring()
        {
            DebugLogger.LogInfo($"터미널 프로세스 모니터링 시작: {_connectionInfo.Name}");

            // 1초마다 프로세스 상태 확인
            _processMonitorTimer = new Timer(OnProcessMonitorTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void OnProcessMonitorTimer(object? state)
        {
            try
            {
                if (_disposed || _process == null)
                {
                    StopProcessMonitoring();
                    return;
                }

                // 프로세스가 종료되었는지 확인
                if (_process.HasExited)
                {
                    DebugLogger.LogInfo($"터미널 프로세스 종료 감지: {_connectionInfo.Name} (Exit Code: {_process.ExitCode})");

                    StopProcessMonitoring();

                    // 프로세스 종료 이벤트 발생
                    ProcessExited?.Invoke();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"터미널 프로세스 모니터링 중 오류: {_connectionInfo.Name}", ex);
                StopProcessMonitoring();
            }
        }

        private void StopProcessMonitoring()
        {
            try
            {
                _processMonitorTimer?.Dispose();
                _processMonitorTimer = null;
                DebugLogger.LogDebug($"터미널 프로세스 모니터링 중지: {_connectionInfo.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"터미널 프로세스 모니터링 중지 중 오류: {_connectionInfo.Name}", ex);
            }
        }

        public void Disconnect()
        {
            try
            {
                DebugLogger.LogInfo($"터미널 연결 해제 시작: {_connectionInfo.Name}");

                // 프로세스 모니터링 중지
                StopProcessMonitoring();

                if (_process != null && !_process.HasExited)
                {
                    DebugLogger.LogDebug($"터미널 프로세스 종료 시도: {_connectionInfo.Name} (PID: {_process.Id})");

                    var processToClose = _process;

                    // 비동기로 프로세스 종료 처리
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 임베드된 콘솔은 자식 창이라 MainWindowHandle이 0이므로 CloseMainWindow가 통하지 않는다.
                            // 이 경우 3초 대기를 생략하고 곧바로 프로세스 트리 종료로 진행한다.
                            var completed = false;
                            if (processToClose.MainWindowHandle != IntPtr.Zero && processToClose.CloseMainWindow())
                            {
                                // 백그라운드에서 정상 종료를 기다림 (UI 블로킹 방지)
                                var waitTask = Task.Run(() => processToClose.WaitForExit(3000));
                                completed = await waitTask.ConfigureAwait(false);
                            }

                            if (!completed)
                            {
                                DebugLogger.LogWarning($"터미널 프로세스 강제 종료: {_connectionInfo.Name}");
                                try
                                {
                                    // 자식 powershell/ssh까지 함께 종료
                                    processToClose.Kill(entireProcessTree: true);
                                    await Task.Run(() => processToClose.WaitForExit(1000)).ConfigureAwait(false);
                                }
                                catch (Exception killEx)
                                {
                                    DebugLogger.LogError($"터미널 프로세스 강제 종료 실패: {_connectionInfo.Name}", killEx);
                                }
                            }
                            else
                            {
                                DebugLogger.LogInfo($"터미널 프로세스 정상 종료: {_connectionInfo.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogError($"터미널 프로세스 비동기 종료 중 오류: {_connectionInfo.Name}", ex);
                        }
                        finally
                        {
                            try
                            {
                                processToClose.Dispose();
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.LogError($"터미널 프로세스 리소스 해제 중 예외: {_connectionInfo.Name}", ex);
                            }
                        }
                    });

                    // 즉시 리소스 정리 (UI 블로킹 방지)
                    _process = null;
                    _terminalWindowHandle = IntPtr.Zero;
                }
                else if (_process != null)
                {
                    DebugLogger.LogDebug($"터미널 프로세스 이미 종료됨: {_connectionInfo.Name}");
                    try
                    {
                        _process?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogError($"터미널 프로세스 리소스 해제 중 예외: {_connectionInfo.Name}", ex);
                    }
                    _process = null;
                    _terminalWindowHandle = IntPtr.Zero;
                }

                DebugLogger.LogDebug($"터미널 리소스 정리 완료: {_connectionInfo.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"터미널 연결 해제 중 예외 발생: {_connectionInfo.Name}", ex);
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

        ~TerminalSession()
        {
            Dispose(false);
        }
    }
}
