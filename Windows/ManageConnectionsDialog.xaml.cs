using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using Newtonsoft.Json;
using SochoPutty.Models;

namespace SochoPutty.Windows
{
    public partial class ManageConnectionsDialog : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        public struct Margins
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        private readonly ConnectionManager _connectionManager;
        private List<ConnectionInfo> _connections = null!;
        public ConnectionInfo? SelectedConnectionToConnect { get; private set; }

        public ManageConnectionsDialog(ConnectionManager connectionManager)
        {
            InitializeComponent();
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            LoadConnections();
        }

        private void LoadConnections()
        {
            _connections = _connectionManager.GetAllConnections();
            dgConnections.ItemsSource = _connections;
            UpdateConnectionCount();
        }

        private void UpdateConnectionCount()
        {
            txtConnectionCount.Text = _connections.Count.ToString();
        }

        private void UpdateButtonStates()
        {
            var isSelected = dgConnections.SelectedItem != null;
            btnEdit.IsEnabled = isSelected;
            btnDuplicate.IsEnabled = isSelected;
            btnDelete.IsEnabled = isSelected;
            btnConnect.IsEnabled = isSelected;
            
            // 순서 변경 버튼 상태
            var selectedIndex = dgConnections.SelectedIndex;
            btnMoveUp.IsEnabled = isSelected && selectedIndex > 0;
            btnMoveDown.IsEnabled = isSelected && selectedIndex < _connections.Count - 1;
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgConnections.SelectedItem is ConnectionInfo connection)
            {
                EditConnection(connection);
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConnectionDialog(null, _connectionManager);
            if (dialog.ShowDialog() == true && dialog.Connection != null)
            {
                try
                {
                    _connectionManager.AddConnection(dialog.Connection);
                    LoadConnections();
                    
                    // 새로 추가된 연결을 선택
                    var addedConnection = _connections.FirstOrDefault(c => c.Name == dialog.Connection.Name);
                    if (addedConnection != null)
                    {
                        dgConnections.SelectedItem = addedConnection;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"연결 추가 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is ConnectionInfo connection)
            {
                EditConnection(connection);
            }
        }

        private void EditConnection(ConnectionInfo connection)
        {
            var originalConnectionName = connection.Name; // 원래 연결 이름 저장
            var dialog = new ConnectionDialog(connection.Clone(), _connectionManager);
            if (dialog.ShowDialog() == true && dialog.Connection != null)
            {
                try
                {
                    _connectionManager.UpdateConnection(dialog.Connection, originalConnectionName);
                    LoadConnections();
                    
                    // 편집된 연결을 선택
                    var updatedConnection = _connections.FirstOrDefault(c => c.Name == dialog.Connection.Name);
                    if (updatedConnection != null)
                    {
                        dgConnections.SelectedItem = updatedConnection;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"연결 업데이트 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is ConnectionInfo connection)
            {
                var duplicatedConnection = connection.Clone();
                duplicatedConnection.Name = GetUniqueConnectionName(connection.Name);
                
                var dialog = new ConnectionDialog(duplicatedConnection, _connectionManager);
                if (dialog.ShowDialog() == true && dialog.Connection != null)
                {
                    try
                    {
                        _connectionManager.AddConnection(dialog.Connection);
                        LoadConnections();
                        
                        // 복제된 연결을 선택
                        var addedConnection = _connections.FirstOrDefault(c => c.Name == dialog.Connection.Name);
                        if (addedConnection != null)
                        {
                            dgConnections.SelectedItem = addedConnection;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"연결 복제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private string GetUniqueConnectionName(string baseName)
        {
            var candidateName = $"{baseName} - 복사본";
            var counter = 1;
            
            while (_connectionManager.ConnectionNameExists(candidateName))
            {
                candidateName = $"{baseName} - 복사본 ({counter})";
                counter++;
            }
            
            return candidateName;
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is ConnectionInfo connection)
            {
                var result = MessageBox.Show(
                    $"'{connection.Name}' 연결을 삭제하시겠습니까?", 
                    "연결 삭제", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _connectionManager.RemoveConnection(connection.Name);
                        LoadConnections();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"연결 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is ConnectionInfo connection)
            {
                SelectedConnectionToConnect = connection;
                DialogResult = true;
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "연결 설정 가져오기",
                Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(openFileDialog.FileName);
                    var importedConnections = JsonConvert.DeserializeObject<List<ConnectionInfo>>(json);
                    
                    if (importedConnections != null && importedConnections.Count > 0)
                    {
                        var addedCount = 0;
                        var skippedCount = 0;
                        
                        foreach (var connection in importedConnections)
                        {
                            try
                            {
                                if (_connectionManager.ConnectionNameExists(connection.Name))
                                {
                                    connection.Name = GetUniqueConnectionName(connection.Name);
                                }
                                
                                _connectionManager.AddConnection(connection);
                                addedCount++;
                            }
                            catch
                            {
                                skippedCount++;
                            }
                        }
                        
                        LoadConnections();
                        MessageBox.Show($"{addedCount}개의 연결을 가져왔습니다.\n{skippedCount}개는 건너뛰었습니다.", 
                                      "가져오기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("유효한 연결 정보를 찾을 수 없습니다.", "가져오기 실패", 
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일 가져오기 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_connections.Count == 0)
            {
                MessageBox.Show("내보낼 연결이 없습니다.", "정보", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Title = "연결 설정 내보내기",
                Filter = "JSON 파일 (*.json)|*.json",
                DefaultExt = "json",
                FileName = "putty_connections.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(_connections, Formatting.Indented);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    MessageBox.Show($"{_connections.Count}개의 연결을 내보냈습니다.", "내보내기 완료", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일 내보내기 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is ConnectionInfo connection)
            {
                try
                {
                    var selectedIndex = dgConnections.SelectedIndex;
                    if (_connectionManager.MoveConnectionUp(connection.Name))
                    {
                        LoadConnections();
                        // 이동된 연결을 다시 선택
                        if (selectedIndex > 0)
                        {
                            dgConnections.SelectedIndex = selectedIndex - 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"연결 순서 변경 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is ConnectionInfo connection)
            {
                try
                {
                    var selectedIndex = dgConnections.SelectedIndex;
                    if (_connectionManager.MoveConnectionDown(connection.Name))
                    {
                        LoadConnections();
                        // 이동된 연결을 다시 선택
                        if (selectedIndex < _connections.Count - 1)
                        {
                            dgConnections.SelectedIndex = selectedIndex + 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"연결 순서 변경 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // 커스텀 타이틀바 윈도우 컨트롤 버튼 이벤트 핸들러
        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close_Click(sender, e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            // Enable shadow
            int val = 2;
            DwmSetWindowAttribute(hwnd, 2, ref val, sizeof(int)); // DWMWA_NCRENDERING_POLICY = 2

            Margins margins = new Margins
            {
                cxLeftWidth = 1,
                cxRightWidth = 1,
                cyTopHeight = 1,
                cyBottomHeight = 1
            };

            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
    }
} 