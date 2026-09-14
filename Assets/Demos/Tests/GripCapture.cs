using System.Collections;
using Jongreul.XrInteraction.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Demos.Tests
{
    /// <summary>
    /// 그립 정렬 GIF 촬영 스크립트. 옆으로 누운 채 쥐어지는 새 도구를 슬라이더로 맞추고 저장한 뒤,
    /// 왼손으로 쥐면 거울상으로 쥐어지는 장면. 게임 시간을 촬영 프레임에 고정한다.
    /// 일반 테스트 실행에서는 돌지 않는다([Explicit]).
    /// 실행: -runTests -testPlatform PlayMode -testFilter Jongreul.XrInteraction.Demos.Tests.GripCapture (-nographics 없이)
    /// </summary>
    [Explicit, Category("Capture")]
    public class GripCapture
    {
        const int GripSpot = 5;

        static readonly Vector3 LeftRest = new Vector3(-0.3f, 1.05f, 0.2f);
        static readonly Vector3 RightRest = new Vector3(0.3f, 1.05f, 0.2f);
        static readonly Vector3 RightShow = new Vector3(0.2f, 1.3f, 0.34f);
        static readonly Vector3 LeftShow = new Vector3(-0.2f, 1.3f, 0.34f);

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
        public IEnumerator RecordGripAlign()
        {
            _demo = new GameObject("InteractionLab").AddComponent<LabDemo>();
            _demo.Rig.PreferXRDevice = false;
            _demo.Simulator.KeyboardAndMouse = false;
            _demo.TeleportTo(GripSpot);
            _demo.Simulator.SetHead(new Vector3(0f, 1.62f, 0f), -20f, 18f);
            _demo.Simulator.SetHand(HandSide.Left, LeftRest, Quaternion.identity);
            _demo.Simulator.SetHand(HandSide.Right, RightRest, Quaternion.identity);

            // 어깨 너머에서: 오른손의 도구와 왼쪽 패널이 함께 보인다. 아바타(내 몸 레이어)는 가리므로 뺀다.
            Vector3 rig = _demo.Rig.transform.position;
            _spectator = new GameObject("Spectator").AddComponent<Camera>();
            _spectator.fieldOfView = 55f;
            _spectator.clearFlags = CameraClearFlags.SolidColor;
            _spectator.backgroundColor = LabDemo.Backdrop;
            _spectator.cullingMask &= ~(1 << EquipStation.SelfWearableLayer);
            _spectator.transform.position = rig + new Vector3(0.2f, 1.75f, -0.45f);
            _spectator.transform.LookAt(rig + new Vector3(-0.18f, 1.36f, 0.42f));
            yield return null;

            _demo.StartCoroutine(Script());
            yield return DemoCapture.Record(new[] { _spectator }, new[] { "grip-align" }, seconds: 18f, fps: 15f,
                lockTime: true);
        }

        GripAlignStation Station => _demo.Grip;

        Vector3 Local(Vector3 world) => _demo.Rig.transform.InverseTransformPoint(world);

        IEnumerator Script()
        {
            GripTool paddle = Station.FindTool("Paddle");
            yield return new WaitForSeconds(0.5f);

            // 새 도구를 쥐면 옆으로 누운 채 손에서 비껴 있다
            yield return Move(HandSide.Right, Local(paddle.transform.position), 0.6f);
            yield return Grip(HandSide.Right, true);
            yield return Move(HandSide.Right, RightShow, 0.6f);
            yield return new WaitForSeconds(0.5f);

            // 왼손으로 슬라이더를 끌면 손 안의 도구가 바로 따라 돈다
            yield return Drag(GripAxis.Roll, 0f, 1.0f);
            yield return Drag(GripAxis.X, 0f, 0.6f);
            yield return Drag(GripAxis.Pitch, 40f, 0.8f);
            yield return Drag(GripAxis.Yaw, -20f, 0.6f); // 좌우 비대칭 — 왼손에서 거울상이 눈에 보이게
            yield return new WaitForSeconds(0.4f);

            // SAVE를 누르면 이 도구의 오른손 프로필에 저장
            yield return Poke(Station.SaveButton.transform);
            yield return Move(HandSide.Left, LeftRest, 0.5f);
            yield return new WaitForSeconds(0.6f);

            // 놓으면 제자리로, 왼손으로 쥐면 오른손 값의 거울상으로 쥐어진다
            yield return Grip(HandSide.Right, false);
            yield return Move(HandSide.Right, RightRest, 0.4f);
            yield return Move(HandSide.Left, Local(paddle.transform.position), 0.6f);
            yield return Grip(HandSide.Left, true);
            yield return Move(HandSide.Left, LeftShow, 0.6f);
        }

        IEnumerator Drag(GripAxis axis, float value, float seconds)
        {
            WorldSlider slider = Station.GetSlider(axis);
            yield return Move(HandSide.Left, Local(slider.Knob.transform.position), 0.5f);
            yield return Grip(HandSide.Left, true);
            yield return Move(HandSide.Left, Local(slider.WorldPointFor(value)), seconds);
            yield return Grip(HandSide.Left, false);
        }

        IEnumerator Poke(Transform button)
        {
            Vector3 front = Local(button.position - button.forward * 0.12f);
            yield return Move(HandSide.Left, front, 0.5f);
            yield return Move(HandSide.Left, Local(button.position), 0.2f);
            yield return Move(HandSide.Left, front, 0.25f);
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
