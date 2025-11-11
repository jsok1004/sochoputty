using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SochoPutty.Models
{
    public class SimpleP2PChatManager : IDisposable
    {
        private const int DiscoveryPort = 45900;
        private const int MessagePort = 45901;
        private const string DiscoverySignature = "SOCHO_CHAT_HELLO_V1";
        private const string MessageSignature = "SOCHO_CHAT_MESSAGE_V1";
        private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(45);

        private readonly ConcurrentDictionary<string, ChatPeer> _peers = new ConcurrentDictionary<string, ChatPeer>();
        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private CancellationTokenSource? _cts;
        private UdpClient? _discoveryClient;
        private TcpListener? _messageListener;
        private Task? _discoveryListenerTask;
        private Task? _broadcastTask;
        private Task? _messageListenerTask;
        private Timer? _peerSweepTimer;
        private string _localIpAddress = string.Empty;

        public event EventHandler<ChatPeer>? PeerUpdated;
        public event EventHandler<ChatMessageEventArgs>? MessageReceived;
        public event EventHandler<ChatMessageEventArgs>? MessageSent;
        public event EventHandler<string>? StatusChanged;

        public string LocalIpAddress => _localIpAddress;

        public IReadOnlyCollection<ChatPeer> Peers => _peers.Values.ToList();

        public async Task StartAsync()
        {
            if (_cts != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _localIpAddress = GetLocalIPv4Address() ?? throw new InvalidOperationException("로컬 IPv4 주소를 확인할 수 없습니다.");

            _discoveryClient = new UdpClient();
            _discoveryClient.EnableBroadcast = true;
            _discoveryClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _discoveryClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

            _messageListener = new TcpListener(IPAddress.Any, MessagePort);
            _messageListener.Start();

            _discoveryListenerTask = Task.Run(() => ReceiveDiscoveryLoopAsync(_cts.Token));
            _broadcastTask = Task.Run(() => BroadcastPresenceLoopAsync(_cts.Token));
            _messageListenerTask = Task.Run(() => AcceptMessagesLoopAsync(_cts.Token));
            _peerSweepTimer = new Timer(_ => SweepOfflinePeers(), null, PeerTimeout, PeerTimeout);

            StatusChanged?.Invoke(this, $"채팅 매니저 시작: 로컬 IP {_localIpAddress}");
            await BroadcastPresenceAsync();
        }

        public async Task StopAsync()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();

            _peerSweepTimer?.Dispose();
            _peerSweepTimer = null;

            if (_discoveryClient != null)
            {
                _discoveryClient.Close();
                _discoveryClient.Dispose();
                _discoveryClient = null;
            }

            if (_messageListener != null)
            {
                _messageListener.Stop();
                _messageListener = null;
            }

            await Task.WhenAll(
                _discoveryListenerTask ?? Task.CompletedTask,
                _broadcastTask ?? Task.CompletedTask,
                _messageListenerTask ?? Task.CompletedTask);

            _discoveryListenerTask = null;
            _broadcastTask = null;
            _messageListenerTask = null;

            _cts.Dispose();
            _cts = null;

            StatusChanged?.Invoke(this, "채팅 매니저가 중지되었습니다.");
        }

        public async Task ManualDiscoveryAsync()
        {
            await BroadcastPresenceAsync();
        }

        public async Task SendMessageAsync(string targetIpAddress, string content)
        {
            if (string.IsNullOrWhiteSpace(targetIpAddress))
            {
                throw new ArgumentException("대상 IP 주소가 지정되지 않았습니다.", nameof(targetIpAddress));
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var message = new SimpleP2PChatMessage
            {
                SenderIpAddress = _localIpAddress,
                RecipientIpAddress = targetIpAddress,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(targetIpAddress, MessagePort);
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));
                if (completed != connectTask)
                {
                    throw new TimeoutException("대상 피어에 연결할 수 없습니다.");
                }

                var stream = client.GetStream();
                var payload = SerializePacket(message);
                var buffer = Encoding.UTF8.GetBytes(payload);
                await stream.WriteAsync(buffer);
                await stream.FlushAsync();

                MessageSent?.Invoke(this, new ChatMessageEventArgs(message, targetIpAddress));
                UpdatePeerOnline(targetIpAddress, true);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"메시지 전송 실패 ({targetIpAddress}): {ex.Message}");
                UpdatePeerOnline(targetIpAddress, false);
                throw;
            }
        }

        public void UpdatePeerFromHistory(ChatPeer peer)
        {
            peer.IsKnown = true;
            peer.IsOnline = false;
            peer.LastOnlineTime = DateTime.MinValue;
            _peers.AddOrUpdate(peer.IpAddress, peer, (_, existing) =>
            {
                existing.IsKnown = true;
                return existing;
            });
            PeerUpdated?.Invoke(this, peer);
        }

        private async Task ReceiveDiscoveryLoopAsync(CancellationToken token)
        {
            if (_discoveryClient == null)
            {
                return;
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _discoveryClient.ReceiveAsync(token);
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    if (!message.StartsWith(DiscoverySignature, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var remoteIp = result.RemoteEndPoint.Address.ToString();
                    if (remoteIp == _localIpAddress)
                    {
                        continue;
                    }

                    UpdatePeerOnline(remoteIp, true);

                    // 디스커버리 응답 전송 (유니캐스트)
                    await SendDiscoveryResponseAsync(remoteIp);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"디스커버리 수신 오류: {ex.Message}");
                }
            }
        }

        private async Task BroadcastPresenceLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await BroadcastPresenceAsync();
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"브로드캐스트 실패: {ex.Message}");
                }

                try
                {
                    await Task.Delay(BroadcastInterval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task AcceptMessagesLoopAsync(CancellationToken token)
        {
            if (_messageListener == null)
            {
                return;
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _messageListener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => HandleIncomingMessageAsync(client, token), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"메시지 수신 대기 오류: {ex.Message}");
                }
            }
        }

        private async Task HandleIncomingMessageAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var payload = await reader.ReadToEndAsync(token);
                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        return;
                    }

                    var envelope = JsonSerializer.Deserialize<ChatPacket>(payload, _serializerOptions);
                    if (envelope == null)
                    {
                        return;
                    }

                    if (!string.Equals(envelope.Signature, MessageSignature, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (envelope.Message == null || string.IsNullOrWhiteSpace(envelope.Message.SenderIpAddress))
                    {
                        return;
                    }

                    UpdatePeerOnline(envelope.Message.SenderIpAddress, true);
                    envelope.Message.RecipientIpAddress = _localIpAddress;
                    var peerIp = envelope.Message.SenderIpAddress;
                    MessageReceived?.Invoke(this, new ChatMessageEventArgs(envelope.Message, peerIp));
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"수신 처리 오류: {ex.Message}");
                }
            }
        }

        private async Task BroadcastPresenceAsync()
        {
            if (_discoveryClient == null)
            {
                return;
            }

            var broadcastAddress = GetBroadcastAddress();
            var data = Encoding.UTF8.GetBytes(DiscoverySignature);
            await _discoveryClient.SendAsync(data, data.Length, broadcastAddress);
        }

        private ValueTask SendDiscoveryResponseAsync(string targetIp)
        {
            if (_discoveryClient == null)
            {
                return ValueTask.CompletedTask;
            }

            try
            {
                var data = Encoding.UTF8.GetBytes(DiscoverySignature);
                var endPoint = new IPEndPoint(IPAddress.Parse(targetIp), DiscoveryPort);
                return new ValueTask(_discoveryClient.SendAsync(data, data.Length, endPoint));
            }
            catch
            {
                return ValueTask.CompletedTask;
            }
        }

        private void UpdatePeerOnline(string ipAddress, bool isOnline)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return;
            }

            var updatedPeer = _peers.AddOrUpdate(
                ipAddress,
                _ => new ChatPeer
                {
                    IpAddress = ipAddress,
                    IsOnline = isOnline,
                    LastOnlineTime = DateTime.UtcNow,
                    IsKnown = true
                },
                (_, existing) =>
                {
                    existing.IsOnline = isOnline;
                    existing.LastOnlineTime = DateTime.UtcNow;
                    existing.IsKnown = true;
                    return existing;
                });

            PeerUpdated?.Invoke(this, updatedPeer);
        }

        private void SweepOfflinePeers()
        {
            foreach (var peer in _peers.Values)
            {
                if (!peer.IsOnline)
                {
                    continue;
                }

                if (DateTime.UtcNow - peer.LastOnlineTime > PeerTimeout)
                {
                    peer.IsOnline = false;
                    PeerUpdated?.Invoke(this, peer);
                }
            }
        }

        private string SerializePacket(SimpleP2PChatMessage message)
        {
            var packet = new ChatPacket
            {
                Signature = MessageSignature,
                Message = message
            };

            return JsonSerializer.Serialize(packet, _serializerOptions);
        }

        private static string? GetLocalIPv4Address()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // 무시
            }

            return null;
        }

        private IPEndPoint GetBroadcastAddress()
        {
            try
            {
                var segments = _localIpAddress.Split('.');
                if (segments.Length == 4)
                {
                    var broadcast = $"{segments[0]}.{segments[1]}.{segments[2]}.255";
                    return new IPEndPoint(IPAddress.Parse(broadcast), DiscoveryPort);
                }
            }
            catch
            {
                // 무시하고 기본 브로드캐스트 사용
            }

            return new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
        }

        public void Dispose()
        {
            _ = StopAsync();
        }

        public class ChatMessageEventArgs : EventArgs
        {
            public ChatMessageEventArgs(SimpleP2PChatMessage message, string peerIpAddress)
            {
                Message = message;
                PeerIpAddress = peerIpAddress;
            }

            public SimpleP2PChatMessage Message { get; }

            public string PeerIpAddress { get; }
        }

        private class ChatPacket
        {
            public string Signature { get; set; } = string.Empty;

            public SimpleP2PChatMessage? Message { get; set; }
        }
    }
}

