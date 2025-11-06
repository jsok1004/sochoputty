using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SochoPutty.Models
{
    public enum SplitMode
    {
        Single,         // 단일 화면 (기본 1개 탭)
        Horizontal,     // 가로 분할 (위/아래)
        Vertical,       // 세로 분할 (좌/우)
        Quad           // 4화면 분할
    }

    public class SplitPane
    {
        public TabControl TabControl { get; set; }
        public Border Container { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        
        // 드래그앤드롭 관련 속성
        private TabItem? draggingTab = null;
        private Point dragStartPoint;

        public SplitPane(string name, SelectionChangedEventHandler? selectionChangedHandler = null, 
                        ConnectionManager? connectionManager = null, 
                        EventHandler<ConnectionInfo>? quickConnectHandler = null)
        {
            Name = name;
            IsActive = false;
            
            // 컨테이너 Border 생성
            Container = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2)
            };

            // TabControl 생성
            TabControl = new TabControl
            {
                TabStripPlacement = Dock.Top,
                AllowDrop = true  // 드롭 허용
            };

            // 이벤트 핸들러 연결
            if (selectionChangedHandler != null)
            {
                TabControl.SelectionChanged += selectionChangedHandler;
            }
            
            // 드래그앤드롭 이벤트 연결
            TabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
            TabControl.PreviewMouseMove += TabControl_PreviewMouseMove;
            TabControl.Drop += TabControl_Drop;
            TabControl.DragEnter += TabControl_DragEnter;
            TabControl.DragOver += TabControl_DragOver;
            TabControl.DragLeave += TabControl_DragLeave;

            // 기본 시작 탭 추가
            AddWelcomeTab(connectionManager, quickConnectHandler);

            Container.Child = TabControl;
        }

        private void AddWelcomeTab(ConnectionManager? connectionManager = null, 
                                  EventHandler<ConnectionInfo>? quickConnectHandler = null)
        {
            var startTab = new TabItem
            {
                Header = "시작",
                IsSelected = true
            };

            // 시작 탭 컨텐츠 생성
            var startContent = new Grid
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250))
            };

            // ScrollViewer 추가하여 스크롤 가능하게 만들기
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 0, 20)
            };


            // 빠른 연결 섹션 추가
            if (connectionManager != null)
            {
                var quickConnectBorder = new Border
                {
                    Background = System.Windows.Media.Brushes.White,
                    CornerRadius = new CornerRadius(0),
                    Padding = new Thickness(20),
                    MaxWidth = 900,
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(222, 226, 230)),
                    BorderThickness = new Thickness(1)
                };

                var savedConnectionsPanel = new StackPanel();

                // 빠른 연결 제목
                var titleAndQuickConnectPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var quickConnectTitle = new TextBlock
                {
                    Text = "빠른 연결",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 20, 0),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(73, 80, 87))
                };
                titleAndQuickConnectPanel.Children.Add(quickConnectTitle);

                // 빠른 접속 입력 영역 (반응형 레이아웃)
                var quickConnectInputPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var ipLabel = new TextBlock
                {
                    Text = "IP 주소:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    FontSize = 14
                };
                quickConnectInputPanel.Children.Add(ipLabel);

                var ipTextBox = new TextBox
                {
                    Width = 200,
                    Height = 30,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    FontSize = 14,
                    Name = "QuickConnectTextBox"
                };
                
                // 플레이스홀더 효과
                ipTextBox.GotFocus += (s, e) =>
                {
                    if (ipTextBox.Text == "예: 192.168.1.100:22")
                    {
                        ipTextBox.Text = "";
                        ipTextBox.Foreground = System.Windows.Media.Brushes.Black;
                    }
                };
                
                ipTextBox.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(ipTextBox.Text))
                    {
                        ipTextBox.Text = "예: 192.168.1.100:22";
                        ipTextBox.Foreground = System.Windows.Media.Brushes.Gray;
                    }
                };
                
                // 초기 플레이스홀더 설정
                ipTextBox.Text = "예: 192.168.1.100:22";
                ipTextBox.Foreground = System.Windows.Media.Brushes.Gray;
                
                // Enter 키 처리
                ipTextBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        ProcessQuickConnect(ipTextBox.Text, quickConnectHandler);
                    }
                };
                
                quickConnectInputPanel.Children.Add(ipTextBox);

                var connectButton = new Button
                {
                    Content = "접속",
                    Width = 80,
                    Height = 30,
                    FontSize = 14,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 123, 255)),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                
                connectButton.Click += (s, e) =>
                {
                    ProcessQuickConnect(ipTextBox.Text, quickConnectHandler);
                };
                
                quickConnectInputPanel.Children.Add(connectButton);
                titleAndQuickConnectPanel.Children.Add(quickConnectInputPanel);
                savedConnectionsPanel.Children.Add(titleAndQuickConnectPanel);

                // 연결 목록 또는 "연결 없음" 메시지
                var connections = connectionManager.GetAllConnections();
                if (connections.Count == 0)
                {
                    var noConnectionsText = new TextBlock
                    {
                        Text = "저장된 연결이 없습니다. 새 연결을 추가해보세요.",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(20),
                        FontStyle = FontStyles.Italic,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125))
                    };
                    savedConnectionsPanel.Children.Add(noConnectionsText);
                }
                else
                {
                    // 연결 목록 생성
                    var connectionsListBox = new ListBox
                    {
                        BorderThickness = new Thickness(0),
                        MaxHeight = 300
                    };
                    ScrollViewer.SetHorizontalScrollBarVisibility(connectionsListBox, ScrollBarVisibility.Disabled);

                    // WrapPanel을 ItemsPanel로 설정
                    var itemsPanelTemplate = new ItemsPanelTemplate();
                    var wrapPanelFactory = new FrameworkElementFactory(typeof(WrapPanel));
                    wrapPanelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
                    itemsPanelTemplate.VisualTree = wrapPanelFactory;
                    connectionsListBox.ItemsPanel = itemsPanelTemplate;

                    connectionsListBox.ItemsSource = connections;

                    // 연결 아이템 템플릿 설정
                    var itemTemplate = CreateConnectionItemTemplate(quickConnectHandler);
                    connectionsListBox.ItemTemplate = itemTemplate;

                    savedConnectionsPanel.Children.Add(connectionsListBox);
                }

                quickConnectBorder.Child = savedConnectionsPanel;
                stackPanel.Children.Add(quickConnectBorder);
            }
            else
            {
                // ConnectionManager가 없을 때 기본 메시지
                var defaultText = new TextBlock
                {
                    Text = "연결 관리자가 없습니다.",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontStyle = FontStyles.Italic,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125))
                };
                stackPanel.Children.Add(defaultText);
            }

            scrollViewer.Content = stackPanel;
            startContent.Children.Add(scrollViewer);
            startTab.Content = startContent;
            TabControl.Items.Add(startTab);
        }

        private DataTemplate CreateConnectionItemTemplate(EventHandler<ConnectionInfo>? quickConnectHandler)
        {
            var template = new DataTemplate();

            // Border 팩토리 생성
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250)));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(5));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));
            borderFactory.SetValue(Border.BorderBrushProperty, 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 236, 239)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.WidthProperty, 180.0);
            borderFactory.SetValue(Border.HeightProperty, 90.0);

            // Grid 팩토리 생성
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            
            // Grid 행 정의
            var rowDef1 = new FrameworkElementFactory(typeof(RowDefinition));
            rowDef1.SetValue(RowDefinition.HeightProperty, new GridLength(1, GridUnitType.Star));
            gridFactory.AppendChild(rowDef1);
            
            var rowDef2 = new FrameworkElementFactory(typeof(RowDefinition));
            rowDef2.SetValue(RowDefinition.HeightProperty, GridLength.Auto);
            gridFactory.AppendChild(rowDef2);

            // 상단 StackPanel (연결 정보)
            var topStackFactory = new FrameworkElementFactory(typeof(StackPanel));
            topStackFactory.SetValue(Grid.RowProperty, 0);

            // 연결 이름
            var nameTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameTextFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            nameTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            nameTextFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
            nameTextFactory.SetValue(TextBlock.ForegroundProperty, 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 37, 41)));
            nameTextFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            topStackFactory.AppendChild(nameTextFactory);

            // 호스트:포트
            var hostPortFactory = new FrameworkElementFactory(typeof(TextBlock));
            hostPortFactory.SetValue(TextBlock.ForegroundProperty, 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125)));
            hostPortFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            hostPortFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            
            var hostPortBinding = new MultiBinding();
            hostPortBinding.StringFormat = "{0}:{1}";
            hostPortBinding.Bindings.Add(new Binding("Hostname"));
            hostPortBinding.Bindings.Add(new Binding("Port"));
            hostPortFactory.SetBinding(TextBlock.TextProperty, hostPortBinding);
            topStackFactory.AppendChild(hostPortFactory);

            // 연결 타입
            var typeTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            typeTextFactory.SetBinding(TextBlock.TextProperty, new Binding("ConnectionType"));
            typeTextFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            typeTextFactory.SetValue(TextBlock.ForegroundProperty, 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(134, 142, 150)));
            typeTextFactory.SetValue(TextBlock.FontStyleProperty, FontStyles.Italic);
            topStackFactory.AppendChild(typeTextFactory);

            // 설명
            var descTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            descTextFactory.SetBinding(TextBlock.TextProperty, new Binding("Description"));
            descTextFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            descTextFactory.SetValue(TextBlock.ForegroundProperty, 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(173, 181, 189)));
            descTextFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            descTextFactory.SetValue(TextBlock.MaxHeightProperty, 12.0);
            topStackFactory.AppendChild(descTextFactory);

            gridFactory.AppendChild(topStackFactory);

            // 연결 버튼
            var buttonFactory = new FrameworkElementFactory(typeof(Button));
            buttonFactory.SetValue(Grid.RowProperty, 1);
            buttonFactory.SetValue(Button.ContentProperty, "연결");
            buttonFactory.SetValue(Button.BackgroundProperty, 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 123, 255)));
            buttonFactory.SetValue(Button.ForegroundProperty, System.Windows.Media.Brushes.White);
            buttonFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            buttonFactory.SetValue(Button.PaddingProperty, new Thickness(8, 3, 8, 3));
            buttonFactory.SetValue(Button.MarginProperty, new Thickness(0, 5, 0, 0));
            buttonFactory.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            buttonFactory.SetValue(Button.FontSizeProperty, 11.0);

            // 버튼 클릭 이벤트 (quickConnectHandler가 있을 때만)
            if (quickConnectHandler != null)
            {
                buttonFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((sender, e) =>
                {
                    if (sender is Button button && button.DataContext is ConnectionInfo connection)
                    {
                        quickConnectHandler.Invoke(this, connection);
                    }
                }));
            }

            gridFactory.AppendChild(buttonFactory);
            borderFactory.AppendChild(gridFactory);
            template.VisualTree = borderFactory;

            return template;
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            Container.BorderBrush = active 
                ? System.Windows.Media.Brushes.DarkGray 
                : System.Windows.Media.Brushes.LightGray;
            Container.BorderThickness = active 
                ? new Thickness(2) 
                : new Thickness(1);
        }

        #region 드래그앤드롭 이벤트 핸들러

        private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 드래그 시작점 저장
            dragStartPoint = e.GetPosition(null);
            
            // 클릭된 TabItem 찾기
            if (e.Source is FrameworkElement element)
            {
                var tabItem = FindParent<TabItem>(element);
                if (tabItem != null && tabItem.Tag is PuttySession)
                {
                    draggingTab = tabItem;
                }
            }
        }

        private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && draggingTab != null)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = dragStartPoint - currentPosition;

                // 드래그 거리가 충분히 멀면 드래그 시작
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // 드래그 데이터 생성
                    var dragData = new DataObject("TabItem", draggingTab);
                    DragDrop.DoDragDrop(TabControl, dragData, DragDropEffects.Move);
                    
                    // 드래그 완료 후 초기화
                    draggingTab = null;
                }
            }
        }

        private void TabControl_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabItem"))
            {
                e.Effects = DragDropEffects.Move;
                
                // 드래그 오버 시각적 피드백
                Container.BorderBrush = System.Windows.Media.Brushes.Green;
                Container.BorderThickness = new Thickness(3);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void TabControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabItem"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void TabControl_DragLeave(object sender, DragEventArgs e)
        {
            // 드래그가 영역을 벗어날 때 시각적 피드백 제거
            SetActive(IsActive); // 원래 상태로 복원
        }

        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // 드래그 오버 시각적 피드백 제거
                SetActive(IsActive); // 원래 상태로 복원
                
                if (e.Data.GetDataPresent("TabItem") && e.Data.GetData("TabItem") is TabItem droppedTab)
                {
                    // 같은 TabControl에 드롭된 경우 무시
                    if (droppedTab.Parent == TabControl)
                    {
                        return;
                    }
                    
                    // 시작 탭인 경우 이동 불가
                    if (droppedTab.Tag is not PuttySession)
                    {
                        return;
                    }
                    
                    // 기존 TabControl에서 제거
                    if (droppedTab.Parent is TabControl sourceTabControl)
                    {
                        sourceTabControl.Items.Remove(droppedTab);
                    }
                    
                    // 새 TabControl에 추가
                    TabControl.Items.Add(droppedTab);
                    TabControl.SelectedItem = droppedTab;
                    
                    DebugLogger.LogInfo($"탭 드래그앤드롭 완료: {((PuttySession)droppedTab.Tag).ConnectionInfo.Name} → {Name}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("탭 드롭 처리 중 오류 발생", ex);
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            
            if (parentObject == null) return null;
            
            if (parentObject is T parent)
                return parent;
            
            return FindParent<T>(parentObject);
        }

        private static void ProcessQuickConnect(string input, EventHandler<ConnectionInfo>? quickConnectHandler)
        {
            try
            {
                // 플레이스홀더 텍스트 확인
                if (string.IsNullOrWhiteSpace(input) || input == "예: 192.168.1.100:22")
                {
                    System.Windows.MessageBox.Show("IP 주소를 입력해주세요.", "빠른 접속", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // IP와 포트 파싱
                string hostname;
                int port = 22; // 기본 포트

                if (input.Contains(':'))
                {
                    var parts = input.Split(':');
                    hostname = parts[0].Trim();
                    
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int parsedPort))
                    {
                        port = parsedPort;
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("올바른 포트 번호를 입력해주세요.", "빠른 접속", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    hostname = input.Trim();
                }

                // IP 주소 유효성 검사 (기본적인 검사)
                if (string.IsNullOrWhiteSpace(hostname))
                {
                    System.Windows.MessageBox.Show("올바른 IP 주소를 입력해주세요.", "빠른 접속", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // ConnectionInfo 생성
                var connectionInfo = new ConnectionInfo
                {
                    Name = $"빠른접속-{hostname}:{port}",
                    Hostname = hostname,
                    Port = port,
                    ConnectionType = ConnectionType.SSH,
                    Username = "", // 사용자가 PuTTY에서 직접 입력
                    Password = "",
                    PrivateKeyPath = ""
                };

                // 빠른 연결 이벤트 발생
                quickConnectHandler?.Invoke(null, connectionInfo);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"빠른 접속 중 오류가 발생했습니다: {ex.Message}", "오류", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion
    }

    public class SplitManager
    {
        private Grid splitContainer;
        private SplitMode currentMode;
        private List<SplitPane> splitPanes;
        private SplitPane? activeSplitPane;
        private ConnectionManager? connectionManager;
        private TabControl? mainTabControl;

        public SplitMode CurrentMode => currentMode;
        public SplitPane? ActiveSplitPane => activeSplitPane;
        public List<SplitPane> SplitPanes => splitPanes;

        public event EventHandler<SplitPane>? ActivePaneChanged;
        public event SelectionChangedEventHandler? TabControlSelectionChanged;
        public event EventHandler<ConnectionInfo>? QuickConnectRequested;

        public SplitManager(Grid container, ConnectionManager? connectionManager = null)
        {
            splitContainer = container;
            currentMode = SplitMode.Single;
            splitPanes = new List<SplitPane>();
            activeSplitPane = null;
            this.connectionManager = connectionManager;
        }

        public void SetMainTabControl(TabControl tabControl)
        {
            mainTabControl = tabControl;
        }

        public void ApplySplit(SplitMode mode)
        {
            // 기존 분할 제거
            ClearSplit();

            currentMode = mode;

            switch (mode)
            {
                case SplitMode.Single:
                    CreateSinglePane();
                    break;
                case SplitMode.Horizontal:
                    CreateHorizontalSplit();
                    break;
                case SplitMode.Vertical:
                    CreateVerticalSplit();
                    break;
                case SplitMode.Quad:
                    CreateQuadSplit();
                    break;
            }

            // 첫 번째 패널을 활성화
            if (splitPanes.Count > 0)
            {
                SetActivePane(splitPanes[0]);
            }
        }

        private void CreateSinglePane()
        {
            // Grid를 1개 영역으로 설정
            splitContainer.RowDefinitions.Clear();
            splitContainer.ColumnDefinitions.Clear();
            
            // 단일 패널 생성
            var singlePane = new SplitPane("메인", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetRow(singlePane.Container, 0);
            Grid.SetColumn(singlePane.Container, 0);
            splitContainer.Children.Add(singlePane.Container);
            splitPanes.Add(singlePane);
            
            // 클릭 이벤트 추가
            singlePane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(singlePane);
        }

        private void CreateHorizontalSplit()
        {
            // Grid를 2행으로 설정
            splitContainer.RowDefinitions.Clear();
            splitContainer.ColumnDefinitions.Clear();
            
            splitContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            splitContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5, GridUnitType.Pixel) }); // Splitter
            splitContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 상단 패널
            var topPane = new SplitPane("상단", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetRow(topPane.Container, 0);
            splitContainer.Children.Add(topPane.Container);
            splitPanes.Add(topPane);

            // Splitter
            var splitter = new GridSplitter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = System.Windows.Media.Brushes.Gray,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ResizeDirection = GridResizeDirection.Rows
            };
            Grid.SetRow(splitter, 1);
            splitContainer.Children.Add(splitter);

            // 하단 패널
            var bottomPane = new SplitPane("하단", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetRow(bottomPane.Container, 2);
            splitContainer.Children.Add(bottomPane.Container);
            splitPanes.Add(bottomPane);

            // 클릭 이벤트 추가
            topPane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(topPane);
            bottomPane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(bottomPane);
        }

        private void CreateVerticalSplit()
        {
            // Grid를 2열로 설정
            splitContainer.RowDefinitions.Clear();
            splitContainer.ColumnDefinitions.Clear();
            
            splitContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) }); // Splitter
            splitContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 좌측 패널
            var leftPane = new SplitPane("좌측", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetColumn(leftPane.Container, 0);
            splitContainer.Children.Add(leftPane.Container);
            splitPanes.Add(leftPane);

            // Splitter
            var splitter = new GridSplitter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = System.Windows.Media.Brushes.Gray,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetColumn(splitter, 1);
            splitContainer.Children.Add(splitter);

            // 우측 패널
            var rightPane = new SplitPane("우측", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetColumn(rightPane.Container, 2);
            splitContainer.Children.Add(rightPane.Container);
            splitPanes.Add(rightPane);

            // 클릭 이벤트 추가
            leftPane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(leftPane);
            rightPane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(rightPane);
        }

        private void CreateQuadSplit()
        {
            // Grid를 2x2로 설정
            splitContainer.RowDefinitions.Clear();
            splitContainer.ColumnDefinitions.Clear();
            
            splitContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            splitContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5, GridUnitType.Pixel) }); // Horizontal Splitter
            splitContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            splitContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) }); // Vertical Splitter
            splitContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 좌상단 패널
            var topLeftPane = new SplitPane("좌상단", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetRow(topLeftPane.Container, 0);
            Grid.SetColumn(topLeftPane.Container, 0);
            splitContainer.Children.Add(topLeftPane.Container);
            splitPanes.Add(topLeftPane);

            // 우상단 패널
            var topRightPane = new SplitPane("우상단", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetRow(topRightPane.Container, 0);
            Grid.SetColumn(topRightPane.Container, 2);
            splitContainer.Children.Add(topRightPane.Container);
            splitPanes.Add(topRightPane);

            // 좌하단 패널
            var bottomLeftPane = new SplitPane("좌하단", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetRow(bottomLeftPane.Container, 2);
            Grid.SetColumn(bottomLeftPane.Container, 0);
            splitContainer.Children.Add(bottomLeftPane.Container);
            splitPanes.Add(bottomLeftPane);

            // 우하단 패널
            var bottomRightPane = new SplitPane("우하단", TabControlSelectionChanged, connectionManager, QuickConnectRequested);
            Grid.SetRow(bottomRightPane.Container, 2);
            Grid.SetColumn(bottomRightPane.Container, 2);
            splitContainer.Children.Add(bottomRightPane.Container);
            splitPanes.Add(bottomRightPane);

            // 수직 Splitter
            var verticalSplitter = new GridSplitter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = System.Windows.Media.Brushes.Gray,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetRow(verticalSplitter, 0);
            Grid.SetColumn(verticalSplitter, 1);
            Grid.SetRowSpan(verticalSplitter, 3);
            splitContainer.Children.Add(verticalSplitter);

            // 수평 Splitter
            var horizontalSplitter = new GridSplitter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = System.Windows.Media.Brushes.Gray,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ResizeDirection = GridResizeDirection.Rows
            };
            Grid.SetRow(horizontalSplitter, 1);
            Grid.SetColumn(horizontalSplitter, 0);
            Grid.SetColumnSpan(horizontalSplitter, 3);
            splitContainer.Children.Add(horizontalSplitter);

            // 클릭 이벤트 추가
            topLeftPane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(topLeftPane);
            topRightPane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(topRightPane);
            bottomLeftPane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(bottomLeftPane);
            bottomRightPane.TabControl.PreviewMouseDown += (s, e) => SetActivePane(bottomRightPane);
        }

        public void SetActivePane(SplitPane pane)
        {
            // 기존 활성 패널 비활성화
            if (activeSplitPane != null)
            {
                activeSplitPane.SetActive(false);
            }

            // 새 패널 활성화
            activeSplitPane = pane;
            activeSplitPane.SetActive(true);

            // 이벤트 발생
            ActivePaneChanged?.Invoke(this, pane);
        }

        public List<TabItem> GetAllPuttyTabs()
        {
            var puttyTabs = new List<TabItem>();
            
            try
            {
                // 분할 모드에 따라 수집 영역 결정
                // 모든 분할 영역들에서 수집 (Single 포함)
                if (splitPanes != null)
                {
                    foreach (var splitPane in splitPanes)
                    {
                        if (splitPane?.TabControl?.Items != null)
                        {
                            foreach (var item in splitPane.TabControl.Items)
                            {
                                if (item is TabItem tab && tab.Tag is PuttySession)
                                {
                                    puttyTabs.Add(tab);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("GetAllPuttyTabs 실행 중 오류 발생", ex);
            }
            
            return puttyTabs;
        }

        public void ClearSplit()
        {
            // 모든 자식 요소 제거
            splitContainer.Children.Clear();
            splitContainer.RowDefinitions.Clear();
            splitContainer.ColumnDefinitions.Clear();

            // 패널 목록 정리
            splitPanes.Clear();
            activeSplitPane = null;
            // currentMode는 여기서 변경하지 않음 (ApplySplit에서 설정)
        }

        public TabControl? GetActiveTabControl()
        {
            return activeSplitPane?.TabControl;
        }

    }
} 