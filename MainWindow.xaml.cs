using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using SochoPutty.Models;
using SochoPutty.Windows;

namespace SochoPutty
{
    public partial class MainWindow : Window
    {
        private ConnectionManager connectionManager = null!;
        private SettingsManager settingsManager = null!;
        private List<PuttySession> activeSessions = null!;
        private SplitManager splitManager = null!;

        public MainWindow()
        {
            InitializeComponent();
            InitializeManagers();
            LoadQuickConnections();
        }

        private void InitializeManagers()
        {
            connectionManager = new ConnectionManager();
            settingsManager = new SettingsManager();
            activeSessions = new List<PuttySession>();
            
            // SplitManager 초기화 - 단일 모드로 시작
            splitManager = new SplitManager(splitContainer, connectionManager);
            splitManager.TabControlSelectionChanged += TabControl_SelectionChanged;
            splitManager.QuickConnectRequested += OnQuickConnectRequested;
            
            // 초기에 단일 모드로 시작
            splitManager.ApplySplit(SplitMode.Single);
        }



        private void LoadQuickConnections()
        {
            var connections = connectionManager.GetAllConnections();
            cmbQuickConnect.ItemsSource = connections;
            cmbQuickConnect.DisplayMemberPath = "Name";
            
            // 빠른 연결 목록은 이제 SplitManager의 "시작" 탭에서 자동 처리됨
        }

        private void NewConnection_Click(object sender, RoutedEventArgs e)
        {
            DebugLogger.LogDebug("새 연결 대화상자 열기");
            
            try
            {
                var connectionDialog = new ConnectionDialog();
                if (connectionDialog.ShowDialog() == true)
                {
                    var connection = connectionDialog.Connection;
                    if (connection != null)
                    {
                        DebugLogger.LogInfo($"새 연결 정보 입력 완료: {connection}");
                        CreateNewSession(connection);
                    }
                    else
                    {
                        DebugLogger.LogWarning("연결 대화상자에서 null 연결 정보 반환");
                    }
                }
                else
                {
                    DebugLogger.LogDebug("연결 대화상자가 취소됨");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("새 연결 생성 중 예외 발생", ex);
                MessageBox.Show($"연결 대화상자를 열 수 없습니다: {ex.Message}", "오류", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageConnections_Click(object sender, RoutedEventArgs e)
        {
            var manageDialog = new ManageConnectionsDialog(connectionManager);
            if (manageDialog.ShowDialog() == true && manageDialog.SelectedConnectionToConnect != null)
            {
                // 연결 관리에서 [연결] 버튼을 눌렀을 때
                CreateNewSession(manageDialog.SelectedConnectionToConnect);
            }
            LoadQuickConnections(); // 변경사항 반영
        }

        private void QuickConnect_Click(object sender, RoutedEventArgs e)
        {
            if (cmbQuickConnect.SelectedItem is ConnectionInfo connection)
            {
                CreateNewSession(connection);
            }
        }

        private void QuickConnect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 선택 변경 시 처리할 로직 (필요시 구현)
        }

        private void QuickConnect_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ConnectionInfo connection)
            {
                DebugLogger.LogInfo($"빠른 연결 버튼 클릭: {connection.Name}");
                CreateNewSession(connection);
            }
        }

        private void QuickConnectionsList_DoubleClick(object sender, RoutedEventArgs e)
        {
            // 이 메서드는 더 이상 사용되지 않음 (SplitManager의 "시작" 탭에서 처리)
        }

        private async void CreateNewSession(ConnectionInfo connection)
        {
            DebugLogger.LogInfo($"새 세션 생성 시작: {connection.Name} ({connection.Hostname}:{connection.Port})");
            
            try
            {
                // 연결 정보 검증
                if (string.IsNullOrEmpty(connection.Hostname))
                {
                    throw new ArgumentException("호스트명이 지정되지 않았습니다.");
                }

                var session = new PuttySession(connection);
                
                // PuTTY 프로세스 종료 이벤트 연결
                session.ProcessExited += () => OnPuttyProcessExited(session);
                
                activeSessions.Add(session);
                DebugLogger.LogDebug($"PuttySession 생성 완료. 활성 세션 수: {activeSessions.Count}");

                // 탭을 추가할 TabControl 결정 - 항상 활성 분할 영역 사용
                TabControl targetTabControl = splitManager.GetActiveTabControl() ?? throw new InvalidOperationException("활성 분할 영역을 찾을 수 없습니다.");

                // 새 탭 생성
                var tabItem = new TabItem();
                tabItem.Header = CreateTabHeader(connection.Name, session);
                tabItem.Tag = session;

                // PuTTY가 임베드될 컨테이너
                var windowsFormsHost = new System.Windows.Forms.Integration.WindowsFormsHost();
                var panel = new System.Windows.Forms.Panel();
                panel.Dock = System.Windows.Forms.DockStyle.Fill; // 패널이 전체 영역을 채우도록
                windowsFormsHost.Child = panel;

                // 크기 변경 이벤트 처리 (유일하고 정확한 크기 조정 지점)
                windowsFormsHost.SizeChanged += (sender, e) =>
                {
                    var newSize = e.NewSize;
                    if (newSize.Width > 0 && newSize.Height > 0)
                    {
                        session.ResizePuttyWindow((int)newSize.Width, (int)newSize.Height);
                        DebugLogger.LogDebug($"SizeChanged로 정확한 크기 조정: {newSize.Width}x{newSize.Height}");
                    }
                };

                tabItem.Content = windowsFormsHost;
                targetTabControl.Items.Add(tabItem);
                targetTabControl.SelectedItem = tabItem;

                DebugLogger.LogDebug($"탭 생성 완료. Panel Handle: {panel.Handle}");

                // PuTTY 프로세스 시작
                var success = await session.StartPutty(panel.Handle).ConfigureAwait(false);
                
                if (success)
                {
                    DebugLogger.LogInfo($"세션 '{connection.Name}' 생성 성공");
                    
                    // PuTTY 시작 후 즉시 현재 탭 크기로 강제 크기 조정 (최초 1회)
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 현재 WindowsFormsHost의 실제 크기 가져오기
                            var currentWidth = windowsFormsHost.ActualWidth;
                            var currentHeight = windowsFormsHost.ActualHeight;
                            
                            if (currentWidth > 0 && currentHeight > 0)
                            {
                                session.ResizePuttyWindow((int)currentWidth, (int)currentHeight);
                                DebugLogger.LogInfo($"PuTTY 시작 후 최초 크기 조정 완료: {currentWidth}x{currentHeight}");
                            }
                            else
                            {
                                // ActualWidth/Height가 아직 0인 경우 RenderSize 시도
                                var renderWidth = windowsFormsHost.RenderSize.Width;
                                var renderHeight = windowsFormsHost.RenderSize.Height;
                                
                                if (renderWidth > 0 && renderHeight > 0)
                                {
                                    session.ResizePuttyWindow((int)renderWidth, (int)renderHeight);
                                    DebugLogger.LogInfo($"PuTTY 시작 후 RenderSize로 크기 조정 완료: {renderWidth}x{renderHeight}");
                                }
                                else
                                {
                                    // 마지막 시도: 기본 크기로 설정 후 SizeChanged가 자동 호출되도록
                                    DebugLogger.LogWarning("PuTTY 시작 후 크기 정보 없음, SizeChanged 이벤트 대기");
                                }
                            }
                            
                            // 포커스 설정
                            session.FocusPuttyWindow();
                            DebugLogger.LogDebug($"새 세션 포커스 설정 완료: {connection.Name}");
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogError("PuTTY 시작 후 크기 조정 및 포커스 설정 실패", ex);
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
                else
                {
                    DebugLogger.LogError($"세션 '{connection.Name}' 생성 실패");
                    MessageBox.Show($"PuTTY 연결을 시작할 수 없습니다.\n\n연결 정보를 확인하거나 디버그 로그를 확인해주세요.", 
                                  "연결 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"세션 생성 중 오류 발생: {connection.Name}", ex);
                MessageBox.Show($"연결 생성 중 오류가 발생했습니다: {ex.Message}\n\n자세한 정보는 디버그 로그를 확인해주세요.", 
                              "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SplitVertical_Click(object sender, RoutedEventArgs e)
        {
            DebugLogger.LogInfo("새로 분할(수직 분할) 실행");
            ApplySplitMode(SplitMode.Vertical);
        }

        private void SplitHorizontal_Click(object sender, RoutedEventArgs e)
        {
            DebugLogger.LogInfo("가로 분할(수평 분할) 실행");
            ApplySplitMode(SplitMode.Horizontal);
        }

        private void SplitQuad_Click(object sender, RoutedEventArgs e)
        {
            DebugLogger.LogInfo("4화면 분할 실행");
            ApplySplitMode(SplitMode.Quad);
        }

        private void UnsplitAll_Click(object sender, RoutedEventArgs e)
        {
            DebugLogger.LogInfo("분할 해제 실행 (단일 화면으로 변경)");
            ApplySplitMode(SplitMode.Single);
        }

        private void ApplySplitMode(SplitMode mode)
        {
            try
            {
                // 모든 활성 세션을 수집
                var existingPuttyTabs = splitManager.GetAllPuttyTabs();
                DebugLogger.LogInfo($"분할 모드 변경 시작: {splitManager.CurrentMode} → {mode}, 기존 PuTTY 탭 수: {existingPuttyTabs.Count}");
                
                // 새로운 분할 모드 적용
                splitManager.ApplySplit(mode);
                
                // 모든 활성 세션을 첫 번째 영역으로 자동 이동
                if (existingPuttyTabs.Count > 0 && splitManager.SplitPanes.Count > 0)
                {
                    var firstPane = splitManager.SplitPanes[0];
                    MovePuttyTabsToPane(existingPuttyTabs, firstPane);
                    DebugLogger.LogInfo($"PuTTY 탭 이동 완료: {existingPuttyTabs.Count}개 → {firstPane.Name}");
                    
                    // 첫 번째 영역을 활성화
                    splitManager.SetActivePane(firstPane);
                }
                
                DebugLogger.LogInfo($"분할 모드 적용 완료: {mode}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"분할 모드 적용 중 오류: {mode}", ex);
                MessageBox.Show($"분할 기능 실행 중 오류가 발생했습니다: {ex.Message}", 
                              "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void MovePuttyTabsToPane(List<TabItem> puttyTabs, SplitPane targetPane)
        {
            try
            {
                DebugLogger.LogInfo($"PuTTY 탭들을 {targetPane.Name} 영역으로 이동 시작: {puttyTabs.Count}개");
                
                // 탭들의 정보를 저장하고 UI 요소에서 분리
                var tabsInfo = new List<(PuttySession session, object? header, object? content)>();
                foreach (var tab in puttyTabs)
                {
                    if (tab.Tag is PuttySession session)
                    {
                        // UI 요소에서 분리하기 전에 정보 저장
                        var header = tab.Header;
                        var content = tab.Content;
                        
                        // 기존 탭에서 Header와 Content 분리
                        tab.Header = null;
                        tab.Content = null;
                        
                        tabsInfo.Add((session, header, content));
                        DebugLogger.LogDebug($"탭 정보 저장: {session.ConnectionInfo.Name}");
                    }
                }
                
                // 새 탭들을 대상 영역에 추가
                foreach (var (session, header, content) in tabsInfo)
                {
                    var newTab = new TabItem
                    {
                        Header = header,
                        Content = content,
                        Tag = session
                    };
                    targetPane.TabControl.Items.Add(newTab);
                    DebugLogger.LogDebug($"탭 이동 완료: {session.ConnectionInfo.Name} → {targetPane.Name}");
                }
                
                DebugLogger.LogInfo($"{targetPane.Name} 영역으로 탭 이동 완료: {tabsInfo.Count}개");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"{targetPane.Name} 영역으로 탭 이동 중 오류", ex);
            }
        }

        private void CloseAllPuttyTabs(List<TabItem> puttyTabs)
        {
            try
            {
                DebugLogger.LogInfo($"PuTTY 탭들 종료 시작: {puttyTabs.Count}개");
                
                foreach (var tab in puttyTabs)
                {
                    if (tab.Tag is PuttySession session)
                    {
                        session.Dispose();
                        activeSessions.Remove(session);
                        DebugLogger.LogDebug($"세션 종료 완료: {session.ConnectionInfo.Name}");
                    }
                }
                
                DebugLogger.LogInfo($"PuTTY 탭들 종료 완료: {puttyTabs.Count}개, 남은 활성 세션: {activeSessions.Count}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("PuTTY 탭들 종료 중 오류", ex);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsDialog = new SettingsDialog(settingsManager);
            settingsDialog.ShowDialog();
        }

        private void ShowActiveSessionStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var statusMessage = "=== 활성 세션 상태 ===\n\n";
                statusMessage += $"• 총 활성 세션 수: {activeSessions.Count}개\n";
                statusMessage += $"• 현재 분할 모드: {splitManager.CurrentMode}\n\n";
                
                // 각 분할 영역의 탭 확인
                for (int i = 0; i < splitManager.SplitPanes.Count; i++)
                {
                    var pane = splitManager.SplitPanes[i];
                    var puttyTabs = 0;
                    
                    foreach (var item in pane.TabControl.Items)
                    {
                        if (item is TabItem tab && tab.Tag is PuttySession)
                        {
                            puttyTabs++;
                        }
                    }
                    
                    var activeIndicator = pane.IsActive ? " (활성)" : "";
                    statusMessage += $"• {pane.Name} 영역{activeIndicator}: {puttyTabs}개 PuTTY 탭\n";
                }
                
                statusMessage += "\n=== 세션 상세 정보 ===\n";
                if (activeSessions.Count == 0)
                {
                    statusMessage += "활성 세션이 없습니다.";
                }
                else
                {
                    for (int i = 0; i < activeSessions.Count; i++)
                    {
                        var session = activeSessions[i];
                        statusMessage += $"{i + 1}. {session.ConnectionInfo.Name} ({session.ConnectionInfo.Hostname}:{session.ConnectionInfo.Port})\n";
                    }
                }
                
                MessageBox.Show(statusMessage, "활성 세션 상태", MessageBoxButton.OK, MessageBoxImage.Information);
                DebugLogger.LogInfo($"활성 세션 상태 확인 - 총 {activeSessions.Count}개 세션");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("활성 세션 상태 확인 중 오류", ex);
                MessageBox.Show($"세션 상태 확인 중 오류가 발생했습니다: {ex.Message}", 
                              "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Socho Putty Manager v1.3.0 \n\nPuTTY 연결을 편리하게 관리하는 도구입니다.\n\nmade by socho", 
                          "정보", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowDebugLog_Click(object sender, RoutedEventArgs e)
        {
            DebugLogger.ShowDebugWindow();
        }

        private void ClearDebugLog_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("디버그 로그를 모두 삭제하시겠습니까?", "확인", 
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                DebugLogger.ClearLog();
                MessageBox.Show("디버그 로그가 삭제되었습니다.", "정보", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logFolder = DebugLogger.GetLogDirectoryPath();
                Process.Start("explorer.exe", logFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로그 폴더를 열 수 없습니다: {ex.Message}", "오류", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 탭 변경 시 포커스 설정 및 크기 재조정
            if (e.Source is TabControl sourceTabControl && sourceTabControl.SelectedItem is TabItem selectedTab)
            {
                // 선택된 탭이 PuTTY 세션을 포함하는 탭인지 확인
                if (selectedTab.Tag is PuttySession session)
                {
                    // 약간의 지연 후 포커스 설정 및 크기 조정 (UI 업데이트 완료 후)
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // WindowsFormsHost 찾기
                            if (selectedTab.Content is System.Windows.Forms.Integration.WindowsFormsHost windowsFormsHost)
                            {
                                // 현재 탭 크기로 PuTTY 창 크기 조정
                                var currentWidth = windowsFormsHost.ActualWidth;
                                var currentHeight = windowsFormsHost.ActualHeight;
                                
                                if (currentWidth > 0 && currentHeight > 0)
                                {
                                    session.ResizePuttyWindow((int)currentWidth, (int)currentHeight);
                                    DebugLogger.LogDebug($"탭 활성화 시 크기 조정: {session.ConnectionInfo.Name} → {currentWidth}x{currentHeight}");
                                }
                                else
                                {
                                    DebugLogger.LogDebug($"탭 활성화 시 크기 정보 없음: {session.ConnectionInfo.Name}");
                                }
                            }
                            
                            // 포커스 설정
                            session.FocusPuttyWindow();
                            DebugLogger.LogDebug($"탭 변경 시 포커스 설정: {session.ConnectionInfo.Name}");
                            
                            // 드래그앤드롭으로 이동된 탭인 경우 해당 분할 영역을 활성화
                            UpdateActivePaneForTabControl(sourceTabControl);
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogError("탭 변경 시 크기 조정 및 포커스 설정 실패", ex);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void TabControl_RightClick(object sender, RoutedEventArgs e)
        {
            // 우클릭 컨텍스트 메뉴 (향후 구현 가능)
        }

        private StackPanel CreateTabHeader(string title, PuttySession session)
        {
            var stackPanel = new StackPanel();
            stackPanel.Orientation = Orientation.Horizontal;

            // 탭 제목
            var titleText = new TextBlock();
            titleText.Text = title;
            titleText.VerticalAlignment = VerticalAlignment.Center;
            titleText.Margin = new Thickness(0, 0, 8, 0);

            // 닫기 버튼
            var closeButton = new Button();
            closeButton.Content = "✕";
            closeButton.Width = 16;
            closeButton.Height = 16;
            closeButton.FontSize = 10;
            closeButton.Padding = new Thickness(0);
            closeButton.Margin = new Thickness(0);
            closeButton.Background = System.Windows.Media.Brushes.Transparent;
            closeButton.BorderThickness = new Thickness(0);
            closeButton.ToolTip = "탭 닫기";
            closeButton.Tag = session;
            closeButton.Click += CloseTab_Click;

            // 마우스 오버 효과
            closeButton.MouseEnter += (s, e) => closeButton.Background = System.Windows.Media.Brushes.LightGray;
            closeButton.MouseLeave += (s, e) => closeButton.Background = System.Windows.Media.Brushes.Transparent;

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(closeButton);

            return stackPanel;
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PuttySession session)
            {
                CloseSession(session);
            }
        }

        private void CloseSession(PuttySession session)
        {
            try
            {
                DebugLogger.LogInfo($"탭 닫기 시작: {session.ConnectionInfo.Name}");
                
                // 해당 탭 찾기 - 모든 TabControl에서 검색
                TabItem? tabToRemove = null;
                TabControl? parentTabControl = null;

                // 모든 분할 영역의 TabControl에서 검색
                foreach (var splitPane in splitManager.SplitPanes)
                {
                    foreach (var item in splitPane.TabControl.Items)
                    {
                        if (item is TabItem tab && tab.Tag == session)
                        {
                            tabToRemove = tab;
                            parentTabControl = splitPane.TabControl;
                            break;
                        }
                    }
                    if (tabToRemove != null) break;
                }

                if (tabToRemove != null && parentTabControl != null)
                {
                    // 즉시 탭 제거 (UI 반응성 향상)
                    parentTabControl.Items.Remove(tabToRemove);
                    activeSessions.Remove(session);
                    DebugLogger.LogDebug($"탭 제거 완료: {session.ConnectionInfo.Name}");
                    
                    // 백그라운드에서 세션 정리 (UI 블로킹 방지)
                    Task.Run(() =>
                    {
                        try
                        {
                            session.Dispose();
                            DebugLogger.LogInfo($"세션 정리 완료: {session.ConnectionInfo.Name}");
                        }
                        catch (Exception disposeEx)
                        {
                            DebugLogger.LogError($"세션 정리 중 오류: {session.ConnectionInfo.Name}", disposeEx);
                        }
                    });
                    
                    DebugLogger.LogInfo($"탭 닫기 완료: {session.ConnectionInfo.Name}, 남은 활성 세션: {activeSessions.Count}");
                }
                else
                {
                    DebugLogger.LogWarning($"닫을 탭을 찾을 수 없음: {session.ConnectionInfo.Name}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"탭 닫기 중 오류 발생: {session.ConnectionInfo.Name}", ex);
            }
        }

        private void OnPuttyProcessExited(PuttySession session)
        {
            DebugLogger.LogInfo($"PuTTY 프로세스 종료 감지: {session.ConnectionInfo.Name}");
            
            // UI 스레드에서 탭 닫기 실행
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    CloseSession(session);
                    DebugLogger.LogInfo($"PuTTY 프로세스 종료로 인한 자동 탭 닫기 완료: {session.ConnectionInfo.Name}");
                }
                catch (Exception ex)
                {
                    DebugLogger.LogError($"PuTTY 프로세스 종료 후 자동 탭 닫기 실패: {session.ConnectionInfo.Name}", ex);
                }
            }));
        }

        private void OnQuickConnectRequested(object? sender, ConnectionInfo connection)
        {
            DebugLogger.LogInfo($"분할 영역에서 빠른 연결 요청: {connection.Name}");
            CreateNewSession(connection);
        }

        private void UpdateActivePaneForTabControl(TabControl tabControl)
        {
            try
            {
                // 해당 TabControl을 가진 분할 영역 찾기
                foreach (var splitPane in splitManager.SplitPanes)
                {
                    if (splitPane.TabControl == tabControl)
                    {
                        splitManager.SetActivePane(splitPane);
                        DebugLogger.LogDebug($"드래그앤드롭으로 인한 활성 영역 변경: {splitPane.Name}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("활성 영역 업데이트 중 오류", ex);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                DebugLogger.LogInfo($"메인 윈도우 종료 시작 - 활성 세션: {activeSessions.Count}개");
                
                // 모든 활성 세션을 복사하여 백그라운드에서 정리
                var sessionsToClose = activeSessions.ToList();
                activeSessions.Clear();
                
                // 백그라운드에서 모든 세션 정리 (UI 종료 블로킹 방지)
                Task.Run(() =>
                {
                    foreach (var session in sessionsToClose)
                    {
                        try
                        {
                            session.Dispose();
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogError($"프로그램 종료 시 세션 정리 중 오류: {session.ConnectionInfo.Name}", ex);
                        }
                    }
                    DebugLogger.LogInfo($"프로그램 종료 시 모든 세션 정리 완료: {sessionsToClose.Count}개");
                });
                
                DebugLogger.LogInfo("메인 윈도우 종료 완료");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("메인 윈도우 종료 중 오류", ex);
            }
            
            base.OnClosed(e);
        }
    }
} 