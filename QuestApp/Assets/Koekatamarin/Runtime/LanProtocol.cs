using System;
using System.Collections.Generic;
using UnityEngine;

namespace Koekatamarin
{
    [Serializable]
    public sealed class LanPacket
    {
        public string app = "koekatamarin";
        public int version = 1;
        public int room;
        public string peer;
        public string kind;
        public string id;
        public string text;
        public int style;
    }

    // Plain text is data, never TMP rich text. Each sender owns its utterance IDs.
    public static class LanProtocol
    {
        public const int MaxBytes = 2048;
        public const int MaxTextLength = 80;

        public static bool Valid(LanPacket p, int room, string self)
        {
            if (p == null || p.app != "koekatamarin" || p.version != 1 || p.room != room ||
                p.peer == self || !Guid.TryParseExact(p.peer, "N", out _)) return false;
            if (p.kind == "hello") return true;
            if (p.kind != "shot" && p.kind != "ack") return false;
            if (!Guid.TryParseExact(p.id, "N", out _)) return false;
            if (p.kind == "ack") return true;
            if (string.IsNullOrWhiteSpace(p.text) || p.text.Length > MaxTextLength || p.style < 0 || p.style > 5)
                return false;
            foreach (char c in p.text) if (char.IsControl(c)) return false;
            // Reject malformed UTF-16 before passing text into the renderer.
            for (int i = 0; i < p.text.Length; i++)
            {
                if (char.IsHighSurrogate(p.text[i]))
                {
                    if (++i >= p.text.Length || !char.IsLowSurrogate(p.text[i])) return false;
                }
                else if (char.IsLowSurrogate(p.text[i])) return false;
            }
            return true;
        }

        public static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var text = value.Trim().Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
            if (text.Length > MaxTextLength) text = text.Substring(0, MaxTextLength);
            if (text.Length > 0 && char.IsHighSurrogate(text[text.Length - 1])) text = text.Substring(0, text.Length - 1);
            return text;
        }
    }

    // Bounded replay window; duplicate datagrams still receive an ACK but never spawn twice.
    public sealed class RecentEvents
    {
        readonly HashSet<string> seen = new HashSet<string>();
        readonly Queue<string> order = new Queue<string>();
        readonly int capacity;
        public RecentEvents(int capacity = 1024) { this.capacity = Math.Max(1, capacity); }
        public bool Accept(string peer, string id)
        {
            var key = peer + ":" + id;
            if (!seen.Add(key)) return false;
            order.Enqueue(key);
            while (order.Count > capacity) seen.Remove(order.Dequeue());
            return true;
        }
    }
}
