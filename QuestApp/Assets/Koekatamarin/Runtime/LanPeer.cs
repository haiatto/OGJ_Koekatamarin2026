using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Koekatamarin
{
    // Symmetric LAN peers: no host, cloud, NAT traversal, login or telemetry.
    // The small event stream is acknowledged and retried independently for each peer.
    public sealed class LanPeer : MonoBehaviour
    {
        public const int Port = 47726;
        const int MaxPeers = 7;
        const float PeerTimeout = 6f;
        sealed class Peer
        {
            public IPEndPoint endpoint;
            public float lastSeen;
            public bool aligned;
            public float tokens = 4;
            public float refillAt;
        }
        sealed class Pending
        {
            public string peer;
            public string id;
            public byte[] bytes;
            public float next;
            public int attempts;
        }
        readonly Dictionary<string, Peer> peers = new Dictionary<string, Peer>();
        readonly List<Pending> pending = new List<Pending>();
        readonly List<string> expired = new List<string>();
        RecentEvents recent = new RecentEvents();
        UdpClient socket;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject wifiLock;
#endif
        string self;
        int room;
        float nextHello;
        float nextSend;
        public bool Connected => socket != null;
        public int PeerCount => peers.Count;
        public bool SharedSpaceReady { get; set; }
        public int AlignedPeerCount
        {
            get { int count = 0; foreach (var peer in peers.Values) if (peer.aligned) count++; return count; }
        }
        public int MissedDeliveries { get; private set; }
        public string Status { get; private set; } = "LAN未接続";
        public event Action<LanPacket> Received;

        public void Connect(int roomNumber)
        {
            Disconnect();
            if (roomNumber < 1 || roomNumber > 9) return;
            room = roomNumber;
            self = Guid.NewGuid().ToString("N");
            recent = new RecentEvents();
            MissedDeliveries = 0;
            nextHello = 0;
            nextSend = 0;
            try
            {
                socket = new UdpClient(AddressFamily.InterNetwork);
                socket.Client.ReceiveBufferSize = 65536;
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                socket.EnableBroadcast = true;
                socket.Client.Blocking = false;
                AcquireWifiLock();
                Status = "部屋 " + room + " をLAN内で探索中";
            }
            catch (SocketException e) { Disconnect(); Status = "LAN開始失敗: " + e.SocketErrorCode; }
        }

        public void Disconnect()
        {
            socket?.Close();
            socket = null;
            ReleaseWifiLock();
            peers.Clear();
            pending.Clear();
            Status = "LAN未接続";
        }

        public bool SendShot(string text, int style, int space = 0, Vector3 position = default, float yaw = 0)
        {
            if (socket == null || Time.unscaledTime < nextSend || peers.Count == 0) return false;
            var packet = Packet("shot");
            packet.id = Guid.NewGuid().ToString("N");
            packet.text = LanProtocol.Clean(text);
            packet.style = style;
            packet.space = space; packet.position = position; packet.yaw = yaw;
            if (!LanProtocol.Valid(packet, room, "")) return false;
            nextSend = Time.unscaledTime + .2f;
            byte[] bytes = Encode(packet);
            foreach (var peer in peers)
            {
                if (pending.Count >= 256) { MissedDeliveries++; continue; }
                Send(bytes, peer.Value.endpoint);
                pending.Add(new Pending { peer = peer.Key, id = packet.id, bytes = bytes,
                    attempts = 1, next = Time.unscaledTime + .25f });
            }
            return true;
        }

        LanPacket Packet(string kind) => new LanPacket { room = room, peer = self, kind = kind,
            space = kind == "hello" && SharedSpaceReady ? 1 : 0 };
        static byte[] Encode(LanPacket p) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(p));
        void Send(byte[] data, IPEndPoint endpoint)
        {
            try { socket?.Send(data, data.Length, endpoint); }
            catch (SocketException e) { Status = "LAN送信: " + e.SocketErrorCode; }
        }

        void Update()
        {
            if (socket == null) return;
            float now = Time.unscaledTime;
            if (now >= nextHello)
            {
                nextHello = now + 1f;
                Send(Encode(Packet("hello")), new IPEndPoint(IPAddress.Broadcast, Port));
            }
            // Limit per-frame work even if a sender floods the socket.
            for (int i = 0; i < 64; i++)
            {
                byte[] bytes;
                var endpoint = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    if (socket.Available == 0) break;
                    bytes = socket.Receive(ref endpoint);
                }
                catch (SocketException) { break; }
                if (bytes.Length == 0 || bytes.Length > LanProtocol.MaxBytes) continue;
                LanPacket packet;
                try { packet = JsonUtility.FromJson<LanPacket>(Encoding.UTF8.GetString(bytes)); }
                catch (ArgumentException) { continue; }
                if (!LanProtocol.Valid(packet, room, self)) continue;
                if (packet.kind == "hello")
                {
                    if (!peers.TryGetValue(packet.peer, out var found))
                    {
                        if (peers.Count >= MaxPeers) continue;
                        found = new Peer { endpoint = endpoint, refillAt = now };
                        peers.Add(packet.peer, found);
                        Send(Encode(Packet("hello")), endpoint);
                    }
                    // A peer cannot be moved to another address by an event packet.
                    if (found.endpoint.Equals(endpoint)) { found.lastSeen = now; found.aligned = packet.space == 1; }
                    continue;
                }
                if (!peers.TryGetValue(packet.peer, out var sender) || !sender.endpoint.Equals(endpoint)) continue;
                if (packet.kind == "ack")
                {
                    pending.RemoveAll(p => p.peer == packet.peer && p.id == packet.id);
                    continue;
                }
                var ack = Packet("ack");
                ack.id = packet.id;
                if (recent.Contains(packet.peer, packet.id)) { Send(Encode(ack), endpoint); continue; }
                sender.tokens = Mathf.Min(4, sender.tokens + Mathf.Max(0, now - sender.refillAt) * 5);
                sender.refillAt = now;
                // Accept a small delayed burst. Over-budget new events are NOT acknowledged so retries can succeed.
                if (sender.tokens < 1) continue;
                sender.tokens--;
                recent.Accept(packet.peer, packet.id);
                Send(Encode(ack), endpoint);
                Received?.Invoke(packet);
            }
            expired.Clear();
            foreach (var peer in peers) if (now - peer.Value.lastSeen > PeerTimeout) expired.Add(peer.Key);
            foreach (var key in expired) peers.Remove(key);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (now < p.next) continue;
                if (p.attempts >= 6 || !peers.TryGetValue(p.peer, out var recipient))
                { pending.RemoveAt(i); MissedDeliveries++; continue; }
                Send(p.bytes, recipient.endpoint);
                p.attempts++;
                p.next = now + .25f;
            }
        }

        void OnApplicationPause(bool pause) { if (pause) Disconnect(); }
        void OnDisable() { Disconnect(); }

        void AcquireWifiLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi");
                wifiLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "koekatamarin-lan");
                wifiLock.Call("setReferenceCounted", false);
                wifiLock.Call("acquire");
            }
            catch (AndroidJavaException e) { Debug.LogWarning("Wi-Fi discovery lock: " + e.Message); }
#endif
        }
        void ReleaseWifiLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (wifiLock == null) return;
            try { if (wifiLock.Call<bool>("isHeld")) wifiLock.Call("release"); }
            catch (AndroidJavaException e) { Debug.LogWarning("Wi-Fi discovery lock release: " + e.Message); }
            wifiLock.Dispose();
            wifiLock = null;
#endif
        }
    }
}
