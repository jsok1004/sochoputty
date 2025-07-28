using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

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

        public SplitPane(string name, SelectionChangedEventHandler? selectionChangedHandler = null)
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

            // 기본 환영 탭 추가
            AddWelcomeTab();

            Container.Child = TabControl;
        }

        private void AddWelcomeTab()
        {
            var welcomeTab = new TabItem
            {
                Header = "환영",
                IsSelected = true
            };

            var welcomeContent = new Grid
            {
                Background = System.Windows.Media.Brushes.LightBlue
            };

            var welcomeText = new TextBlock
            {
                Text = $"{Name} 영역",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16,
                FontWeight = FontWeights.Bold
            };

            welcomeContent.Children.Add(welcomeText);
            welcomeTab.Content = welcomeContent;
            TabControl.Items.Add(welcomeTab);
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

        public SplitMode CurrentMode => currentMode;
        public SplitPane? ActiveSplitPane => activeSplitPane;
        public List<SplitPane> SplitPanes => splitPanes;

        public event EventHandler<SplitPane>? ActivePaneChanged;
        public event SelectionChangedEventHandler? TabControlSelectionChanged;

        public SplitManager(Grid container)
        {
            splitContainer = container;
            currentMode = SplitMode.None;
            splitPanes = new List<SplitPane>();
            activeSplitPane = null;
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
            var topPane = new SplitPane("상단", TabControlSelectionChanged);
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
            var bottomPane = new SplitPane("하단", TabControlSelectionChanged);
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
            var leftPane = new SplitPane("좌측", TabControlSelectionChanged);
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
            var rightPane = new SplitPane("우측", TabControlSelectionChanged);
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
            var topLeftPane = new SplitPane("좌상단", TabControlSelectionChanged);
            Grid.SetRow(topLeftPane.Container, 0);
            Grid.SetColumn(topLeftPane.Container, 0);
            splitContainer.Children.Add(topLeftPane.Container);
            splitPanes.Add(topLeftPane);

            // 우상단 패널
            var topRightPane = new SplitPane("우상단", TabControlSelectionChanged);
            Grid.SetRow(topRightPane.Container, 0);
            Grid.SetColumn(topRightPane.Container, 2);
            splitContainer.Children.Add(topRightPane.Container);
            splitPanes.Add(topRightPane);

            // 좌하단 패널
            var bottomLeftPane = new SplitPane("좌하단", TabControlSelectionChanged);
            Grid.SetRow(bottomLeftPane.Container, 2);
            Grid.SetColumn(bottomLeftPane.Container, 0);
            splitContainer.Children.Add(bottomLeftPane.Container);
            splitPanes.Add(bottomLeftPane);

            // 우하단 패널
            var bottomRightPane = new SplitPane("우하단", TabControlSelectionChanged);
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