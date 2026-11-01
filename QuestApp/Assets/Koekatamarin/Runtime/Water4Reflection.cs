using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Koekatamarin
{
    // Port of Water4 PlanarReflection: reflected view, oblique near plane and
    // half-resolution render targets. XR uses one target per actual eye.
    public sealed class Water4Reflection : MonoBehaviour
    {
        public MeshRenderer water;
        Camera helper;
        RenderTexture left, right;
        static bool rendering;
        public int RenderCount { get; private set; }
        public RenderTexture LeftTexture => left;
        public RenderTexture RightTexture => right;
        void OnEnable() { RenderPipelineManager.beginCameraRendering += Render; }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            Release(ref left); Release(ref right);
            if (helper != null) { DisposeObject(helper.gameObject); helper = null; }
        }
        static void DisposeObject(Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        static void Release(ref RenderTexture target)
        { if (target != null) { target.Release(); DisposeObject(target); target = null; } }
        static void Ensure(ref RenderTexture target, int width, int height, string label)
        {
            if (target != null && target.width == width && target.height == height) return;
            Release(ref target);
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = label, hideFlags = HideFlags.HideAndDontSave, useMipMap = false, antiAliasing = 1 };
            target.Create();
        }
        void Render(ScriptableRenderContext context, Camera source)
        {
            if (rendering || water == null || !water.enabled || source.cameraType == CameraType.Reflection ||
                source.cameraType == CameraType.Preview || !isActiveAndEnabled) return;
            if (helper == null)
            {
                helper = new GameObject("Water4 planar reflection", typeof(Camera)).GetComponent<Camera>();
                helper.gameObject.hideFlags = HideFlags.HideAndDontSave;
                helper.enabled = false;
                var data = helper.GetUniversalAdditionalCameraData();
                data.allowXRRendering = false;
                data.renderPostProcessing = false;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.renderShadows = false;
            }
            bool stereo = source.stereoEnabled;
            int width = Mathf.Max(16, (stereo ? XRSettings.eyeTextureWidth : source.pixelWidth) / 2);
            int height = Mathf.Max(16, (stereo ? XRSettings.eyeTextureHeight : source.pixelHeight) / 2);
            Ensure(ref left, width, height, "Water4 reflection left");
            if (stereo) Ensure(ref right, width, height, "Water4 reflection right");
            bool previousCulling = GL.invertCulling;
            bool visible = water.enabled;
            rendering = true;
            try
            {
                water.enabled = false;
                helper.CopyFrom(source);
                helper.enabled = false;
                helper.cameraType = CameraType.Reflection;
                helper.stereoTargetEye = StereoTargetEyeMask.None;
                helper.useOcclusionCulling = false;
                helper.allowMSAA = false;
                helper.clearFlags = CameraClearFlags.SolidColor;
                helper.backgroundColor = Color.white; // Original PlanarReflection clearColor.
                var reflection = ReflectionMatrix(transform.position, transform.up, .07f);
                GL.invertCulling = !previousCulling;
                int count = stereo ? 2 : 1;
                for (int eye = 0; eye < count; eye++)
                {
                    var which = eye == 0 ? Camera.StereoscopicEye.Left : Camera.StereoscopicEye.Right;
                    Matrix4x4 view = stereo ? source.GetStereoViewMatrix(which) : source.worldToCameraMatrix;
                    Matrix4x4 projection = stereo ? source.GetStereoProjectionMatrix(which) : source.projectionMatrix;
                    helper.worldToCameraMatrix = view * reflection;
                    Matrix4x4 inverse = helper.worldToCameraMatrix.inverse;
                    helper.transform.SetPositionAndRotation(inverse.GetColumn(3), Quaternion.LookRotation(-inverse.GetColumn(2), inverse.GetColumn(1)));
                    helper.projectionMatrix = projection;
                    Vector3 point = helper.worldToCameraMatrix.MultiplyPoint(transform.position + transform.up * .07f);
                    Vector3 normal = helper.worldToCameraMatrix.MultiplyVector(transform.up).normalized;
                    helper.projectionMatrix = helper.CalculateObliqueMatrix(new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal)));
                    helper.targetTexture = eye == 0 ? left : right;
                    // Render within the caller's context, before its render graph starts.
                    // A full render request here would re-enter/reset the active XR layout.
#pragma warning disable CS0618
                    UniversalRenderPipeline.RenderSingleCamera(context, helper);
#pragma warning restore CS0618
                    RenderCount++;
                }
                water.sharedMaterial.SetTexture("_ReflectionLeft", left);
                water.sharedMaterial.SetTexture("_ReflectionRight", stereo ? right : left);
            }
            finally { water.enabled = visible; GL.invertCulling = previousCulling; rendering = false; }
        }
        public static Matrix4x4 ReflectionMatrix(Vector3 position, Vector3 normal, float offset)
        {
            normal.Normalize();
            float d = -Vector3.Dot(normal, position) - offset;
            var result = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++) result[row, column] -= 2 * normal[row] * normal[column];
                result[row, 3] = -2 * d * normal[row];
            }
            return result;
        }
    }
}
