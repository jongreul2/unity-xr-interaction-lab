using System.Collections;
using System.IO;
using UnityEngine;

namespace Jongreul.XrInteraction.Demos.Tests
{
    /// <summary>
    /// 데모 화면을 PNG 프레임으로 저장한다(README GIF용). 카메라를 렌더 텍스처로 돌려
    /// 배치 모드(-nographics 없이)에서도 화면 없이 캡처된다.
    /// </summary>
    public static class DemoCapture
    {
        public const int Width = 1280;
        public const int Height = 720;

        public static IEnumerator Record(Camera camera, string name, float seconds, float fps) =>
            Record(new[] { camera }, new[] { name }, seconds, fps);

        /// <summary>
        /// 여러 카메라를 같은 순간에 찍어 각자 폴더에 저장한다(1인칭 + 관찰자 겹쳐 보기용).
        /// lockTime이면 게임 시간을 프레임당 1/fps로 고정한다 — 저장이 실시간을 못 따라가도 GIF 속도가 실제 속도와 같다
        /// (이때 촬영 스크립트는 실시간이 아니라 게임 시간으로 기다려야 한다).
        /// </summary>
        public static IEnumerator Record(Camera[] cameras, string[] names, float seconds, float fps, bool lockTime = false)
        {
            int previousCaptureFramerate = Time.captureFramerate;
            if (lockTime)
                Time.captureFramerate = Mathf.RoundToInt(fps);

            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "artifacts", "frames");
            var directories = new string[cameras.Length];
            var targets = new RenderTexture[cameras.Length];
            var previousTargets = new RenderTexture[cameras.Length];
            for (int i = 0; i < cameras.Length; i++)
            {
                directories[i] = Path.Combine(root, names[i]);
                if (Directory.Exists(directories[i]))
                    Directory.Delete(directories[i], true);
                Directory.CreateDirectory(directories[i]);

                targets[i] = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                previousTargets[i] = cameras[i].targetTexture;
                cameras[i].targetTexture = targets[i];
            }

            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            float interval = 1f / fps;
            int count = Mathf.CeilToInt(seconds * fps);
            float next = Time.realtimeSinceStartup;
            for (int frame = 0; frame < count; frame++)
            {
                if (lockTime)
                {
                    if (frame > 0)
                        yield return null; // 한 프레임 = 게임 시간 1/fps
                }
                else
                {
                    while (Time.realtimeSinceStartup < next)
                        yield return null;
                    next += interval;
                }

                for (int i = 0; i < cameras.Length; i++)
                {
                    cameras[i].Render();
                    RenderTexture previousActive = RenderTexture.active;
                    RenderTexture.active = targets[i];
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    pixels.Apply(false);
                    RenderTexture.active = previousActive;
                    File.WriteAllBytes(Path.Combine(directories[i], $"{frame:D4}.png"), pixels.EncodeToPNG());
                }
            }

            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].targetTexture = previousTargets[i];
                Object.Destroy(targets[i]);
                Debug.Log($"[DemoCapture] {count} frames -> {directories[i]}");
            }

            Object.Destroy(pixels);
            if (lockTime)
                Time.captureFramerate = previousCaptureFramerate;
        }
    }
}
