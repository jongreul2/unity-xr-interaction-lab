using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Demos.Tests
{
    /// <summary>
    /// 짚라인 GIF 촬영 스크립트. 1인칭(본 화면)과 옆에서 따라가는 관찰자 시점(작은 창)을 같은 프레임에 저장한다.
    /// 게임 시간을 촬영 프레임에 고정하므로 스크립트도 게임 시간으로 기다린다.
    /// 일반 테스트 실행에서는 돌지 않는다([Explicit]).
    /// 실행: -runTests -testPlatform PlayMode -testFilter Jongreul.XrInteraction.Demos.Tests.ZiplineCapture (-nographics 없이)
    /// </summary>
    [Explicit, Category("Capture")]
    public class ZiplineCapture
    {
        const int ZiplineSpot = 4;
        const float Fps = 15f;

        static readonly Vector3 LeftRest = new Vector3(-0.3f, 1.1f, 0.25f);
        static readonly Vector3 RightRest = new Vector3(0.3f, 1.1f, 0.25f);

        LabDemo _demo;
        Camera _chase;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_demo != null)
                Object.Destroy(_demo.gameObject);
            if (_chase != null)
                Object.Destroy(_chase.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecordZipline()
        {
            _demo = new GameObject("InteractionLab").AddComponent<LabDemo>();
            _demo.Rig.PreferXRDevice = false;
            _demo.Simulator.KeyboardAndMouse = false;
            _demo.TeleportTo(ZiplineSpot);
            _demo.Simulator.SetHead(new Vector3(0f, 1.62f, 0f), 0f, -2f);
            _demo.Simulator.SetHand(HandSide.Left, LeftRest, Quaternion.identity);
            _demo.Simulator.SetHand(HandSide.Right, RightRest, Quaternion.identity);

            _chase = new GameObject("ChaseCamera").AddComponent<Camera>();
            _chase.fieldOfView = 55f;
            _chase.clearFlags = CameraClearFlags.SolidColor;
            _chase.backgroundColor = LabDemo.Backdrop;
            _chase.gameObject.AddComponent<ChaseRig>().Target = _demo.Rig.transform;
            yield return null;

            _demo.StartCoroutine(Script());
            yield return DemoCapture.Record(new[] { _demo.HeadCamera, _chase }, new[] { "zipline", "zipline-side" },
                seconds: 7f, fps: Fps, lockTime: true);
        }

        Vector3 HandleLocal => _demo.Rig.transform.InverseTransformPoint(_demo.Zipline.Handle.transform.position);

        IEnumerator Script()
        {
            yield return new WaitForSeconds(0.6f);

            // 머리 위 손잡이를 오른손으로 잡으면 출발
            yield return Move(HandSide.Right, HandleLocal + new Vector3(0.07f, 0f, 0f), 0.8f);
            yield return Grip(HandSide.Right, true);
            yield return new WaitForSeconds(1.1f);

            // 타는 도중 왼손으로 바꿔 잡는다(리그가 손잡이에 고정돼 있어 로컬 위치는 그대로다)
            yield return Move(HandSide.Left, HandleLocal + new Vector3(-0.07f, 0f, 0f), 0.45f);
            yield return Grip(HandSide.Left, true);
            _demo.Simulator.SetGrip(HandSide.Right, 0f);
            yield return Move(HandSide.Right, RightRest, 0.5f);

            // 종점에서 자동으로 놓고 떨어진다
            while (_demo.Zipline.Ride.IsRiding || _demo.Zipline.IsFalling)
                yield return null;
            _demo.Simulator.SetGrip(HandSide.Left, 0f);
            _demo.Simulator.SetHead(new Vector3(0f, 1.62f, 0f), 0f, 12f);
            yield return Move(HandSide.Left, LeftRest, 0.4f);
        }

        IEnumerator Move(HandSide side, Vector3 toLocal, float seconds)
        {
            Vector3 from = _demo.Simulator.GetHandPosition(side);
            float start = Time.time;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Clamp01((Time.time - start) / seconds);
                float eased = t * t * (3f - 2f * t);
                _demo.Simulator.SetHand(side, Vector3.Lerp(from, toLocal, eased), Quaternion.identity);
                yield return null;
            }
        }

        IEnumerator Grip(HandSide side, bool down)
        {
            _demo.Simulator.SetGrip(side, down ? 1f : 0f);
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>리그 오른쪽 옆에서 따라가는 카메라. 스테이션이 리그를 옮긴(Update) 뒤, 촬영 전에 자리를 잡는다.</summary>
        [DefaultExecutionOrder(100)]
        sealed class ChaseRig : MonoBehaviour
        {
            public Transform Target;

            void Update()
            {
                if (Target == null)
                    return;
                Vector3 rig = Target.position;
                transform.position = rig + new Vector3(2.9f, 1.9f, -0.9f);
                transform.LookAt(rig + new Vector3(0f, 1.25f, 0.7f));
            }
        }
    }
}
