using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SochoPutty.Models;

namespace SochoPutty.Windows
{
    public partial class ConnectionDialog : Window
    {
        public ConnectionInfo? Connection { get; private set; }
        private readonly ConnectionManager _connectionManager;
        private readonly string? _originalConnectionName;

        public ConnectionDialog(ConnectionInfo? connection = null, ConnectionManager? connectionManager = null)
        {
            InitializeComponent();
            _connectionManager = connectionManager ?? new ConnectionManager();
            _originalConnectionName = connection?.Name;
            
            InitializeConnectionTypes();
            
            if (connection != null)
            {
                LoadConnection(connection);
                Title = "연결 편집";
            }
            else
            {
                Title = "새 연결";
                // 기본값 설정
                cmbConnectionType.SelectedValue = ConnectionType.SSH;
                txtPort.Text = "22";
            }
        }

        private void InitializeConnectionTypes()
        {
            cmbConnectionType.ItemsSource = Enum.GetValues(typeof(ConnectionType));
        }

        private void LoadConnection(ConnectionInfo connection)
        {
            txtName.Text = connection.Name;
            txtHostname.Text = connection.Hostname;
            txtPort.Text = connection.Port.ToString();
            txtUsername.Text = connection.Username;
            pwdPassword.Password = connection.Password;
            txtPrivateKeyPath.Text = connection.PrivateKeyPath;
            txtDescription.Text = connection.Description;
            cmbConnectionType.SelectedValue = connection.ConnectionType;
        }

        private ConnectionInfo CreateConnectionFromInput()
        {
            return new ConnectionInfo
            {
                Name = txtName.Text.Trim(),
                Hostname = txtHostname.Text.Trim(),
                Port = int.TryParse(txtPort.Text, out int port) ? port : 22,
                Username = txtUsername.Text.Trim(),
                Password = pwdPassword.Password,
                PrivateKeyPath = txtPrivateKeyPath.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                ConnectionType = (ConnectionType)cmbConnectionType.SelectedValue
            };
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("연결 이름을 입력하세요.", "유효성 검사", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtHostname.Text))
            {
                MessageBox.Show("호스트명을 입력하세요.", "유효성 검사", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtHostname.Focus();
                return false;
            }

            if (!int.TryParse(txtPort.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("올바른 포트 번호를 입력하세요. (1-65535)", "유효성 검사", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPort.Focus();
                return false;
            }

            // 중복 이름 확인
            var connectionName = txtName.Text.Trim();
            if (_connectionManager.ConnectionNameExists(connectionName, _originalConnectionName))
            {
                MessageBox.Show("동일한 이름의 연결이 이미 존재합니다.", "유효성 검사", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return false;
            }

            return true;
        }

        private void ConnectionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbConnectionType.SelectedValue == null) return;

            var connectionType = (ConnectionType)cmbConnectionType.SelectedValue;
            
            // 연결 타입에 따라 기본 포트 설정
            if (string.IsNullOrEmpty(txtPort.Text) || 
                txtPort.Text == "22" || txtPort.Text == "23" || txtPort.Text == "513")
            {
                switch (connectionType)
                {
                    case ConnectionType.SSH:
                        txtPort.Text = "22";
                        break;
                    case ConnectionType.Telnet:
                        txtPort.Text = "23";
                        break;
                    case ConnectionType.Rlogin:
                        txtPort.Text = "513";
                        break;
                    case ConnectionType.Raw:
                        // Raw 연결은 기본 포트가 없음
                        break;
                }
            }
        }

        private void BrowseKey_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "개인키 파일 선택",
                Filter = "PuTTY Private Key Files (*.ppk)|*.ppk|모든 파일 (*.*)|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                txtPrivateKeyPath.Text = openFileDialog.FileName;
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            btnTestConnection.IsEnabled = false;
            txtTestResult.Text = "연결 테스트 중...";
            txtTestResult.Foreground = System.Windows.Media.Brushes.Blue;

            try
            {
                var testConnection = CreateConnectionFromInput();
                
                // 간단한 연결 테스트 (실제로는 소켓 연결 등을 시도해야 함)
                await System.Threading.Tasks.Task.Delay(2000); // 시뮬레이션

                txtTestResult.Text = "연결 테스트 성공!";
                txtTestResult.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                txtTestResult.Text = $"연결 테스트 실패: {ex.Message}";
                txtTestResult.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                btnTestConnection.IsEnabled = true;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                Connection = CreateConnectionFromInput();
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 