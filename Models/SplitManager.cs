using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SochoPutty.Models
{
    public enum SplitMode
    {
        None,           // 분할 없음
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
                TabStripPlacement = Dock.Top
            };

            // 이벤트 핸들러 연결
            if (selectionChangedHandler != null)
            {
                TabControl.SelectionChanged += selectionChangedHandler;
            }

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

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 제목 추가
            var titleText = new TextBlock
            {
                Text = $"{Name} 영역",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 58, 64))
            };
            stackPanel.Children.Add(titleText);

            // 빠른 연결 섹션 추가
            if (connectionManager != null)
            {
                var quickConnectBorder = new Border
                {
                    Background = System.Windows.Media.Brushes.White,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20),
                    MaxWidth = 900,
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(222, 226, 230)),
                    BorderThickness = new Thickness(1)
                };

                var quickConnectPanel = new StackPanel();

                // 빠른 연결 제목
                var quickConnectTitle = new TextBlock
                {
                    Text = "빠른 연결",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 15),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(73, 80, 87))
                };
                quickConnectPanel.Children.Add(quickConnectTitle);

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
                    quickConnectPanel.Children.Add(noConnectionsText);
                }
                else
                {
                    // 연결 목록 생성
                    var connectionsListBox = new ListBox
                    {
                        BorderThickness = new Thickness(0),
                        MaxHeight = 400
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

                    quickConnectPanel.Children.Add(connectionsListBox);
                }

                quickConnectBorder.Child = quickConnectPanel;
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

            startContent.Children.Add(stackPanel);
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
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
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
                ? System.Windows.Media.Brushes.Blue 
                : System.Windows.Media.Brushes.LightGray;
            Container.BorderThickness = active 
                ? new Thickness(2) 
                : new Thickness(1);
        }
    }

    public class SplitManager
    {
        private Grid splitContainer;
        private SplitMode currentMode;
        private List<SplitPane> splitPanes;
        private SplitPane? activeSplitPane;
        private ConnectionManager? connectionManager;

        public SplitMode CurrentMode => currentMode;
        public SplitPane? ActiveSplitPane => activeSplitPane;
        public List<SplitPane> SplitPanes => splitPanes;

        public event EventHandler<SplitPane>? ActivePaneChanged;
        public event SelectionChangedEventHandler? TabControlSelectionChanged;
        public event EventHandler<ConnectionInfo>? QuickConnectRequested;

        public SplitManager(Grid container, ConnectionManager? connectionManager = null)
        {
            splitContainer = container;
            currentMode = SplitMode.None;
            splitPanes = new List<SplitPane>();
            activeSplitPane = null;
            this.connectionManager = connectionManager;
        }

        public void ApplySplit(SplitMode mode)
        {
            // 기존 분할 제거
            ClearSplit();

            currentMode = mode;

            switch (mode)
            {
                case SplitMode.None:
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
            
            foreach (var splitPane in splitPanes)
            {
                foreach (var item in splitPane.TabControl.Items)
                {
                    if (item is TabItem tab && tab.Tag != null && tab.Header.ToString() != "시작")
                    {
                        puttyTabs.Add(tab);
                    }
                }
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
            currentMode = SplitMode.None;
        }

        public TabControl? GetActiveTabControl()
        {
            return activeSplitPane?.TabControl;
        }
    }
} 