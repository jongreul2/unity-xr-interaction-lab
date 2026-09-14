using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Demos.Tests
{
    /// <summary>
    /// 타격 패드 GIF 촬영 스크립트. 일반 테스트 실행에서는 돌지 않는다([Explicit]).
    /// 실행: -runTests -testPlatform PlayMode -testFilter Jongreul.XrInteraction.Demos.Tests.StrikeCapture (-nographics 없이)
    /// </summary>
    [Explicit, Category("Capture")]
    public class StrikeCapture
    {
        LabDemo _demo;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_demo != null)
                Object.Destroy(_demo.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecordStrikePad()
        {
            _demo = new GameObject("InteractionLab").AddComponent<LabDemo>();
            _demo.Rig.PreferXRDevice = false;
            _demo.Simulator.KeyboardAndMouse = false;
            _demo.Simulator.SetHead(new Vector3(0f, 1.62f, -0.15f), 0f, 24f);
            _demo.Simulator.SetHand(HandSide.Left, new Vector3(-0.32f, 1.05f, 0.25f), Quaternion.identity);
            yield return null;

            _demo.StartCoroutine(Script());
            yield return DemoCapture.Record(_demo.HeadCamera, "strike", seconds: 16f, fps: 15f);
        }

        IEnumerator Script()
        {
            Vector3 top = _demo.Strike.Surface.position; // 리그가 원점이라 로컬 = 월드
            Vector3 above = top + new Vector3(0.04f, 0.38f, 0f);
            Hand(above);
            yield return new WaitForSecondsRealtime(0.8f);

            yield return Strike(above, top, 0.13f);                                   // 명중
            yield return Strike(above, top, 0.13f);                                   // 명중
            yield return Swipe(top + new Vector3(-0.45f, 0.012f, 0f), top + new Vector3(0.12f, 0f, 0f)); // 옆으로 스침
            yield return Move(top + new Vector3(0.12f, 0f, 0f), above, 0.35f);
            yield return Strike(above, top, 0.6f);                                    // 느리게 누름
            yield return Strike(top + new Vector3(0.04f, 0.07f, 0f), top, 0.03f);     // 손목 스냅
            yield return Move(top, above, 0.35f);
            yield return Strike(above, top, 0.12f);                                   // 명중
            yield return Strike(above, top, 0.12f);                                   // 명중
            yield return Strike(above, top, 0.12f);                                   // 명중
        }

        void Hand(Vector3 position) => _demo.Simulator.SetHand(HandSide.Right, position, Quaternion.identity);

        IEnumerator Strike(Vector3 from, Vector3 top, float downSeconds)
        {
            yield return Move(from, from, 0.15f);
            yield return Move(from, top - new Vector3(0f, 0.02f, 0f), downSeconds);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Move(top - new Vector3(0f, 0.02f, 0f), from, 0.3f);
            yield return new WaitForSecondsRealtime(0.15f);
        }

        IEnumerator Swipe(Vector3 from, Vector3 to)
        {
            yield return Move(from, from, 0.3f);
            yield return Move(from, to, 0.14f);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        IEnumerator Move(Vector3 from, Vector3 to, float seconds)
        {
            float start = Time.realtimeSinceStartup;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / seconds);
                Hand(Vector3.Lerp(from, to, t));
                yield return null;
            }
        }
    }
}
