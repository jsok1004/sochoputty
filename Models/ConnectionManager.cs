using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SochoPutty.Models
{
    public class ConnectionManager
    {
        private const string ConnectionsFileName = "connections.json";
        private readonly string _dataDirectory;
        private readonly string _connectionsFilePath;
        private List<ConnectionInfo> _connections;

        public ConnectionManager()
        {
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SochoPutty");
            _connectionsFilePath = Path.Combine(_dataDirectory, ConnectionsFileName);
            _connections = new List<ConnectionInfo>();
            
            EnsureDataDirectoryExists();
            LoadConnections();
        }

        private void EnsureDataDirectoryExists()
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        public List<ConnectionInfo> GetAllConnections()
        {
            return _connections.ToList();
        }

        public void AddConnection(ConnectionInfo connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (string.IsNullOrWhiteSpace(connection.Name))
                throw new ArgumentException("연결 이름은 필수입니다.", nameof(connection));

            if (_connections.Any(c => c.Name.Equals(connection.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("동일한 이름의 연결이 이미 존재합니다.", nameof(connection));

            _connections.Add(connection);
            SaveConnections();
        }

        public void UpdateConnection(ConnectionInfo connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            var existingConnection = _connections.FirstOrDefault(c => c.Name.Equals(connection.Name, StringComparison.OrdinalIgnoreCase));
            if (existingConnection == null)
                throw new ArgumentException("업데이트할 연결을 찾을 수 없습니다.", nameof(connection));

            var index = _connections.IndexOf(existingConnection);
            _connections[index] = connection;
            SaveConnections();
        }

        public void UpdateConnection(ConnectionInfo connection, string originalConnectionName)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            
            if (string.IsNullOrWhiteSpace(originalConnectionName))
                throw new ArgumentException("원래 연결 이름은 필수입니다.", nameof(originalConnectionName));

            var existingConnection = _connections.FirstOrDefault(c => c.Name.Equals(originalConnectionName, StringComparison.OrdinalIgnoreCase));
            if (existingConnection == null)
                throw new ArgumentException("업데이트할 연결을 찾을 수 없습니다.", nameof(originalConnectionName));

            var index = _connections.IndexOf(existingConnection);
            _connections[index] = connection;
            SaveConnections();
        }

        public void RemoveConnection(string connectionName)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentException("연결 이름은 필수입니다.", nameof(connectionName));

            var connection = _connections.FirstOrDefault(c => c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase));
            if (connection != null)
            {
                _connections.Remove(connection);
                SaveConnections();
            }
        }

        public ConnectionInfo? GetConnection(string connectionName)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                return null;

            return _connections.FirstOrDefault(c => c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase));
        }

        public void LoadConnections()
        {
            try
            {
                if (File.Exists(_connectionsFilePath))
                {
                    var json = File.ReadAllText(_connectionsFilePath);
                    var connections = JsonConvert.DeserializeObject<List<ConnectionInfo>>(json);
                    _connections = connections ?? new List<ConnectionInfo>();
                }
                else
                {
                    // 기본 연결 정보 생성
                    CreateDefaultConnections();
                }
            }
            catch (Exception)
            {
                _connections = new List<ConnectionInfo>();
                // 로그 또는 사용자에게 알림 (여기서는 기본 연결 생성)
                CreateDefaultConnections();
            }
        }

        private void CreateDefaultConnections()
        {
            _connections.Add(new ConnectionInfo
            {
                Name = "예제 서버",
                Hostname = "example.com",
                Port = 22,
                Username = "user",
                ConnectionType = ConnectionType.SSH,
                Description = "예제 SSH 연결"
            });

            SaveConnections();
        }

        public void SaveConnections()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_connections, Formatting.Indented);
                File.WriteAllText(_connectionsFilePath, json);
            }
            catch (Exception ex)
            {
                // 로그 또는 사용자에게 알림
                throw new InvalidOperationException("연결 정보를 저장하는 중 오류가 발생했습니다.", ex);
            }
        }

        public bool ConnectionNameExists(string name, string? excludeName = null)
        {
            return _connections.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && 
                                    (excludeName == null || !c.Name.Equals(excludeName, StringComparison.OrdinalIgnoreCase)));
        }

        public bool MoveConnectionUp(string connectionName)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                return false;

            var connection = _connections.FirstOrDefault(c => c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase));
            if (connection == null)
                return false;

            var currentIndex = _connections.IndexOf(connection);
            if (currentIndex <= 0)
                return false; // 이미 첫 번째이거나 찾을 수 없음

            _connections.RemoveAt(currentIndex);
            _connections.Insert(currentIndex - 1, connection);
            SaveConnections();
            return true;
        }

        public bool MoveConnectionDown(string connectionName)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                return false;

            var connection = _connections.FirstOrDefault(c => c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase));
            if (connection == null)
                return false;

            var currentIndex = _connections.IndexOf(connection);
            if (currentIndex < 0 || currentIndex >= _connections.Count - 1)
                return false; // 이미 마지막이거나 찾을 수 없음

            _connections.RemoveAt(currentIndex);
            _connections.Insert(currentIndex + 1, connection);
            SaveConnections();
            return true;
        }
    }
} 