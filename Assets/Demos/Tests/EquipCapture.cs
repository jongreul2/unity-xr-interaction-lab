using System.Collections;
using Jongreul.XrInteraction.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Demos.Tests
{
    /// <summary>
    /// 장착 옷장 GIF 촬영 스크립트(관찰자 시점). 일반 테스트 실행에서는 돌지 않는다([Explicit]).
    /// 모자 장착 → 다른 모자는 교체 확인 → 한 번 더 놓아 교체 → 얼굴 탭 → 안경 장착. 마네킹이 따라 입는다.
    /// </summary>
    [Explicit, Category("Capture")]
    public class EquipCapture
    {
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
        public IEnumerator RecordWardrobe()
        {
            _demo = new GameObject("InteractionLab").AddComponent<LabDemo>();
            _demo.Rig.PreferXRDevice = false;
            _demo.Simulator.KeyboardAndMouse = false;
            _demo.TeleportTo(1);
            _demo.Simulator.SetHead(new Vector3(0f, 1.62f, 0f), 0f, 8f);
            _demo.Simulator.SetHand(HandSide.Left, new Vector3(-0.3f, 1.0f, 0.15f), Quaternion.identity);
            _demo.Simulator.SetHand(HandSide.Right, new Vector3(0.3f, 1.0f, 0.15f), Quaternion.identity);

            var spectatorGo = new GameObject("Spectator");
            _spectator = spectatorGo.AddComponent<Camera>();
            _spectator.fieldOfView = 50f;
            _spectator.clearFlags = CameraClearFlags.SolidColor;
            _spectator.backgroundColor = LabDemo.Backdrop;
            // 플레이어 어깨 위 뒤쪽에서 진열대와 마네킹을 함께 내려다본다(모자는 위에서 잘 보인다).
            Vector3 focus = new Vector3(LabDemo.EquipSpotX + 0.6f, 1.35f, 0.65f);
            _spectator.transform.position = new Vector3(LabDemo.EquipSpotX - 0.35f, 2.35f, -1.35f);
            _spectator.transform.LookAt(focus);
            yield return null;

            _demo.StartCoroutine(Script());
            yield return DemoCapture.Record(_spectator, "equip", seconds: 17f, fps: 15f);
        }

        Vector3 Local(Vector3 world) => _demo.Rig.transform.InverseTransformPoint(world);

        Vector3 AboveHead => _demo.Rig.Head.localPosition + new Vector3(0f, 0.2f, 0.02f);

        Vector3 InFrontOfFace => _demo.Rig.Head.localPosition + new Vector3(0f, -0.01f, 0.16f);

        Vector3 Rest => new Vector3(0.3f, 1.0f, 0.15f);

        IEnumerator Script()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Wear("cap", AboveHead);
            yield return Wear("crown", AboveHead);   // 교체 확인 요청
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Wear("crown", AboveHead);   // 교체
            yield return Poke(_demo.Equip.transform.Find("Shelf/Tab_face"));
            yield return Wear("glasses", InFrontOfFace);
            yield return Move(Rest, 0.4f);
        }

        IEnumerator Wear(string itemId, Vector3 dropLocal)
        {
            EquipPreviewItem preview = null;
            foreach (EquipPreviewItem candidate in _demo.Equip.Previews)
            {
                if (candidate.Item.Id == itemId)
                    preview = candidate;
            }

            Assert.That(preview, Is.Not.Null, itemId);
            yield return Move(Local(preview.transform.position), 0.5f);
            _demo.Simulator.SetGrip(HandSide.Right, 1f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Move(dropLocal, 0.7f);
            yield return new WaitForSecondsRealtime(0.35f);
            _demo.Simulator.SetGrip(HandSide.Right, 0f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Move(Rest, 0.4f);
        }

        IEnumerator Poke(Transform button)
        {
            Assert.That(button, Is.Not.Null);
            Vector3 front = Local(button.position + button.forward * -0.08f);
            yield return Move(front, 0.4f);
            yield return Move(Local(button.position), 0.2f);
            yield return Move(front, 0.2f);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        IEnumerator Move(Vector3 toLocal, float seconds)
        {
            Vector3 from = _demo.Simulator.GetHandPosition(HandSide.Right);
            float start = Time.realtimeSinceStartup;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / seconds);
                float eased = t * t * (3f - 2f * t);
                _demo.Simulator.SetHand(HandSide.Right, Vector3.Lerp(from, toLocal, eased), Quaternion.identity);
                yield return null;
            }
        }
    }
}
