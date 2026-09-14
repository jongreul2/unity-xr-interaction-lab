using System.Collections;
using Jongreul.XrInteraction.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Demos.Tests
{
    /// <summary>
    /// 던지기 + 제출 슬롯 GIF 촬영 스크립트. 공 두 개를 던져 넣고, 잡동사니는 튕겨 나오고, 벨을 쳐도 모자라면 제출되지 않으며,
    /// 세 번째 공을 넣고 치면 제출되어 공이 받침대로 돌아가는 장면. 게임은 60 Hz로 돌리고 15 fps로 저장한다.
    /// 일반 테스트 실행에서는 돌지 않는다([Explicit]).
    /// 실행: -runTests -testPlatform PlayMode -testFilter Jongreul.XrInteraction.Demos.Tests.ThrowCapture (-nographics 없이)
    /// </summary>
    [Explicit, Category("Capture")]
    public class ThrowCapture
    {
        const int ThrowSpot = 6;
        const int SwingFrames = 6; // 60 Hz에서 0.1초

        static readonly Vector3 LeftRest = new Vector3(-0.3f, 1.05f, 0.2f);
        static readonly Vector3 RightRest = new Vector3(0.3f, 1.05f, 0.2f);
        static readonly Vector3 ReleaseLocal = new Vector3(0.25f, 1.3f, 0.3f);

        LabDemo _demo;
        Camera _spectator;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_demo != null)
                Object.Destroy(_demo.gameObject);
            if (_spectator != null)
                Object.Destroy(_spectator.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecordThrowSubmit()
        {
            _demo = new GameObject("InteractionLab").AddComponent<LabDemo>();
            _demo.Rig.PreferXRDevice = false;
            _demo.Simulator.KeyboardAndMouse = false;
            _demo.TeleportTo(ThrowSpot);
            _demo.Simulator.SetHead(new Vector3(0f, 1.62f, 0f), 0f, 12f);
            _demo.Simulator.SetHand(HandSide.Left, LeftRest, Quaternion.identity);
            _demo.Simulator.SetHand(HandSide.Right, RightRest, Quaternion.identity);

            // 오른쪽 뒤 위에서: 받침대·던지는 손·포물선·상자·점수판·벨이 한 화면에 들어온다.
            Vector3 rig = _demo.Rig.transform.position;
            _spectator = new GameObject("Spectator").AddComponent<Camera>();
            _spectator.fieldOfView = 50f;
            _spectator.clearFlags = CameraClearFlags.SolidColor;
            _spectator.backgroundColor = LabDemo.Backdrop;
            _spectator.cullingMask &= ~(1 << EquipStation.SelfWearableLayer); // 아바타가 벨을 가린다
            Transform dock = _demo.Rig.transform.Find("BackDock"); // 다른 스테이션(슬롯독)이 리그에 붙인 것
            if (dock != null)
                dock.gameObject.SetActive(false);
            _spectator.transform.position = rig + new Vector3(1.35f, 2.15f, -1.15f);
            _spectator.transform.LookAt(rig + new Vector3(0.05f, 0.75f, 1.35f));
            yield return null;

            _demo.StartCoroutine(Script());
            yield return DemoCapture.Record(new[] { _spectator }, new[] { "throw-submit" }, seconds: 17f, fps: 15f,
                lockTime: true, stepsPerFrame: 4);
        }

        ThrowStation Station => _demo.ThrowSubmit;

        Vector3 Local(Vector3 world) => _demo.Rig.transform.InverseTransformPoint(world);

        IEnumerator Script()
        {
            yield return new WaitForSeconds(0.5f);
            yield return ThrowAt("ball-1", Station.SlotWorldCenter, 0.7f);
            yield return ThrowAt("ball-2", Station.SlotWorldCenter + new Vector3(0.08f, 0f, 0.05f), 0.7f);
            yield return ThrowAt("junk-1", Station.SlotWorldCenter, 0.7f); // 받지 않는 종류 → 튕겨 나옴
            yield return new WaitForSeconds(0.6f);
            yield return PokeBell(); // 두 개뿐 → 제출 안 됨
            yield return ThrowAt("ball-3", Station.SlotWorldCenter + new Vector3(-0.08f, 0f, -0.04f), 0.7f);
            yield return PokeBell(); // 세 개 → 제출, 공은 받침대로
        }

        IEnumerator ThrowAt(string id, Vector3 target, float flightTime)
        {
            ThrowItem item = Station.Find(id);
            yield return Move(HandSide.Right, Local(item.transform.position), 0.45f);
            yield return Grip(HandSide.Right, true);

            // 포물선 역산: release에서 flightTime 뒤 target에 닿는 속도
            Vector3 release = _demo.Rig.transform.TransformPoint(ReleaseLocal);
            Vector3 velocity = (target - release) / flightTime - 0.5f * Physics.gravity * flightTime;
            float dt = Time.captureDeltaTime;
            Vector3 start = release - velocity * (dt * SwingFrames);
            yield return Move(HandSide.Right, Local(start), 0.35f);
            yield return new WaitForSeconds(0.12f); // 멈춘 동안 이전 궤적이 추정 구간에서 빠진다

            for (int i = 1; i <= SwingFrames; i++)
            {
                _demo.Simulator.SetHand(HandSide.Right, Local(start + velocity * (dt * i)), Quaternion.identity);
                if (i == SwingFrames)
                    _demo.Simulator.SetGrip(HandSide.Right, 0f);
                yield return null;
            }

            yield return Move(HandSide.Right, RightRest, 0.4f);
            yield return new WaitForSeconds(flightTime + 0.35f);
        }

        IEnumerator PokeBell()
        {
            Vector3 bell = Local(Station.Bell.position);
            yield return Move(HandSide.Left, bell + new Vector3(0f, 0.15f, -0.05f), 0.4f);
            yield return Move(HandSide.Left, bell, 0.12f);
            yield return Move(HandSide.Left, LeftRest, 0.45f);
            yield return new WaitForSeconds(0.9f);
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
    }
}
