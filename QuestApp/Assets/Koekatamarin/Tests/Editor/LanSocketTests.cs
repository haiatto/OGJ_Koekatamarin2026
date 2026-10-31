using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Koekatamarin.Tests
{
    public sealed class LanSocketTests
    {
        [Test]
        public void DiscoveryAndDuplicateDeliveryWorkOverRealUdp()
        {
            var go = new GameObject("LAN integration test");
            var peer = go.AddComponent<LanPeer>();
            using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            client.Client.ReceiveTimeout = 2000;
            try
            {
                peer.Connect(8);
                Assert.That(peer.Connected, Is.True, peer.Status);
                var packet = new LanPacket { room = 8, peer = Guid.NewGuid().ToString("N"), kind = "hello" };
                Send(client, packet);
                Pump(peer);
                Assert.That(Read(client).kind, Is.EqualTo("hello"));
                Assert.That(peer.PeerCount, Is.EqualTo(1));
                int delivered = 0;
                peer.Received += received => { Assert.That(received.text, Is.EqualTo("こんにちは")); delivered++; };
                packet.kind = "shot"; packet.id = Guid.NewGuid().ToString("N"); packet.text = "こんにちは";
                Send(client, packet); Pump(peer);
                Assert.That(Read(client).id, Is.EqualTo(packet.id));
                Send(client, packet); Pump(peer);
                Assert.That(Read(client).kind, Is.EqualTo("ack"));
                Assert.That(delivered, Is.EqualTo(1), "A retried event must not spawn twice");
                packet.room = 9; packet.id = Guid.NewGuid().ToString("N");
                Send(client, packet); Pump(peer);
                Assert.That(delivered, Is.EqualTo(1), "Other rooms are isolated");
            }
            finally { peer.Disconnect(); UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void DelayedBurstPreservesDistinctShotsAndTransportsSharedPose()
        {
            var go = new GameObject("LAN spatial integration test");
            var peer = go.AddComponent<LanPeer>();
            using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            client.Client.ReceiveTimeout = 2000;
            try
            {
                peer.SharedSpaceReady = true;
                peer.Connect(7);
                var packet = new LanPacket { room = 7, peer = Guid.NewGuid().ToString("N"), kind = "hello", space = 1 };
                Send(client, packet); Pump(peer);
                Assert.That(Read(client).space, Is.EqualTo(1));
                Assert.That(peer.AlignedPeerCount, Is.EqualTo(1));
                int delivered = 0;
                peer.Received += received =>
                {
                    Assert.That(received.space, Is.EqualTo(1));
                    Assert.That(received.position, Is.EqualTo(new Vector3(1, .8f, -2)));
                    Assert.That(received.yaw, Is.EqualTo(-35));
                    delivered++;
                };
                packet.kind = "shot"; packet.text = "同じ場所"; packet.position = new Vector3(1, .8f, -2); packet.yaw = -35;
                // Both events arrive before a single receiver frame, as after Wi-Fi scheduling delays.
                packet.id = Guid.NewGuid().ToString("N"); Send(client, packet);
                packet.id = Guid.NewGuid().ToString("N"); Send(client, packet);
                Pump(peer);
                Assert.That(Read(client).kind, Is.EqualTo("ack"));
                Assert.That(Read(client).kind, Is.EqualTo("ack"));
                Assert.That(delivered, Is.EqualTo(2), "A delayed burst must not be acknowledged then silently dropped");
                Send(client, packet); Pump(peer);
                Assert.That(Read(client).kind, Is.EqualTo("ack"));
                Assert.That(delivered, Is.EqualTo(2));

                Assert.That(peer.SendShot("折り返し", 2, 1, new Vector3(-2, 1, .5f), 45), Is.True);
                var outbound = Read(client);
                Assert.That(outbound.kind, Is.EqualTo("shot"));
                Assert.That(outbound.position, Is.EqualTo(new Vector3(-2, 1, .5f)));
                Assert.That(outbound.yaw, Is.EqualTo(45));
                Assert.That(outbound.text, Is.EqualTo("折り返し"));
            }
            finally { peer.Disconnect(); UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ManualAddressConnectsSymmetricPeersWithoutBroadcasts()
        {
            var aObject = new GameObject("Manual peer A");
            var bObject = new GameObject("Manual peer B");
            var a = aObject.AddComponent<LanPeer>();
            var b = bObject.AddComponent<LanPeer>();
            a.BroadcastDiscovery = false; b.BroadcastDiscovery = false;
            try
            {
                a.Connect(4, 0); b.Connect(4, 0);
                Assert.That(a.Connected && b.Connected, Is.True);
                Assert.That(a.AddManualAddress("127.0.0.1", b.LocalPort), Is.True);
                Pump(b); Pump(a); Pump(b);
                Assert.That(a.PeerCount, Is.EqualTo(1));
                Assert.That(b.PeerCount, Is.EqualTo(1));
                string receivedA = null, receivedB = null;
                a.Received += packet => receivedA = packet.text;
                b.Received += packet => receivedB = packet.text;
                Assert.That(a.SendShot("手動で接続", 1), Is.True);
                Assert.That(b.SendShot("相互に送信", 2), Is.True);
                Pump(a); Pump(b); Pump(a);
                Assert.That(receivedA, Is.EqualTo("相互に送信"));
                Assert.That(receivedB, Is.EqualTo("手動で接続"));
                a.Disconnect();
                Assert.That(a.AddManualAddress("127.0.0.1", b.LocalPort), Is.False);
            }
            finally
            {
                a.Disconnect(); b.Disconnect();
                UnityEngine.Object.DestroyImmediate(aObject); UnityEngine.Object.DestroyImmediate(bObject);
            }
        }

        [Test]
        public void DiscoveredPeerGetsUnicastKeepaliveWithoutBroadcastOrManualTarget()
        {
            var go = new GameObject("Unicast keepalive");
            var peer = go.AddComponent<LanPeer>();
            peer.BroadcastDiscovery = false;
            using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            client.Client.ReceiveTimeout = 2000;
            try
            {
                peer.Connect(6);
                Send(client, new LanPacket { kind = "hello", room = 6, peer = Guid.NewGuid().ToString("N") });
                Pump(peer);
                Assert.That(Read(client).kind, Is.EqualTo("hello"));
                // Advance the hello scheduler without a wall-clock sleep in EditMode.
                typeof(LanPeer).GetField("nextHello", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(peer, -1f);
                Pump(peer);
                Assert.That(Read(client).kind, Is.EqualTo("hello"), "The receiving side needs unicast keepalives too");
            }
            finally { peer.Disconnect(); UnityEngine.Object.DestroyImmediate(go); }
        }

        static void Send(UdpClient client, LanPacket packet)
        {
            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
            client.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, LanPeer.Port));
        }
        static LanPacket Read(UdpClient client)
        {
            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            return JsonUtility.FromJson<LanPacket>(Encoding.UTF8.GetString(client.Receive(ref endpoint)));
        }
        static void Pump(LanPeer peer)
        {
            // Drive the runtime receive loop in an EditMode test; no scene or headset needed.
            var update = typeof(LanPeer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 10; i++) { update.Invoke(peer, null); System.Threading.Thread.Sleep(5); }
        }
    }
}
