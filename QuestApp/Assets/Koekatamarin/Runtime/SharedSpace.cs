using UnityEngine;

namespace Koekatamarin
{
    // A and B are the same two physical marks for every headset. No cloud or saved spatial map.
    public sealed class SharedSpace
    {
        public enum Step { None, Origin, Direction, Ready }
        public Step Current { get; private set; }
        public Vector3 Origin { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public bool Ready => Current == Step.Ready;
        public bool Capturing => Current == Step.Origin || Current == Step.Direction;
        public void Begin() { Clear(); Current = Step.Origin; }
        public void Clear() { Current = Step.None; Origin = Vector3.zero; Rotation = Quaternion.identity; }

        public bool Capture(Vector3 point, out string message)
        {
            if (!Finite(point)) { message = "位置を取得できません。もう一度指定してください"; return false; }
            if (Current == Step.Origin)
            {
                Origin = point;
                Current = Step.Direction;
                message = "同じ目印Bへ移動し右グリップ（Aから0.5〜5m）";
                return true;
            }
            if (Current == Step.Direction)
            {
                Vector3 forward = point - Origin; forward.y = 0;
                if (forward.magnitude < .5f || forward.magnitude > 5f)
                { message = "BはAから水平に0.5〜5m離してください"; return false; }
                Rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                Current = Step.Ready;
                message = "位置合わせ完了 / 全員が同じA・Bを指定してください";
                return true;
            }
            message = "先に「位置合わせ」を押してください";
            return false;
        }

        public Pose ToShared(Pose world) => new Pose(Quaternion.Inverse(Rotation) * (world.position - Origin),
            Quaternion.Inverse(Rotation) * world.rotation);
        public Pose ToWorld(Vector3 position, float yaw) => new Pose(Origin + Rotation * position,
            Rotation * Quaternion.Euler(0, yaw, 0));
        public static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
