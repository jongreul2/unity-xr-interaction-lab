using System.Collections;
using Jongreul.XrInteraction.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrInteraction.Demos.Tests
{
    /// <summary>방패·카트리지 GIF 촬영 스크립트. 일반 테스트 실행에서는 돌지 않는다([Explicit]).</summary>
    [Explicit, Category("Capture")]
    public class ShieldCartridgeCapture
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

        LabDemo CreateDemo(int station)
        {
            _demo = new GameObject("InteractionLab").AddComponent<LabDemo>();
            _demo.Rig.PreferXRDevice = false;
            _demo.Simulator.KeyboardAndMouse = false;
            _demo.TeleportTo(station);
            _demo.Simulator.SetHand(HandSide.Left, new Vector3(-0.3f, 1.0f, 0.15f), Quaternion.identity);
            _demo.Simulator.SetHand(HandSide.Right, new Vector3(0.3f, 1.0f, 0.15f), Quaternion.identity);
            return _demo;
        }

        Vector3 Local(Vector3 world) => _demo.Rig.transform.InverseTransformPoint(world);

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

        IEnumerator Grip(bool down)
        {
            _demo.Simulator.SetGrip(HandSide.Right, down ? 1f : 0f);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        [UnityTest]
        public IEnumerator RecordShield()
        {
            CreateDemo(2);
            _demo.Shield.AutoFire = false;
            _demo.Simulator.SetHead(new Vector3(0f, 1.62f, 0f), 0f, 4f);

            // 오른쪽 앞 위에서 내려다본다: 방패를 든 아바타와 여러 방향에서 오는 투사체가 함께 보인다.
            _spectator = new GameObject("Spectator").AddComponent<Camera>();
            _spectator.fieldOfView = 48f;
            _spectator.clearFlags = CameraClearFlags.SolidColor;
            _spectator.backgroundColor = LabDemo.Backdrop;
            _spectator.transform.position = new Vector3(LabDemo.ShieldSpotX + 1.35f, 2.05f, -0.6f);
            _spectator.transform.LookAt(new Vector3(LabDemo.ShieldSpotX + 0.05f, 1.35f, 0.9f));
            yield return null;

            _demo.StartCoroutine(ShieldScript());
            yield return DemoCapture.Record(_spectator, "shield", seconds: 15f, fps: 15f);
        }

        IEnumerator ShieldScript()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Move(Local(_demo.Shield.Shield.transform.position), 0.6f);
            yield return Grip(true);
            yield return Move(new Vector3(0.05f, 1.28f, 0.34f), 0.6f);

            (float angle, bool bypass)[] shots = { (0f, false), (20f, false), (-28f, false), (75f, false), (0f, true), (-10f, false), (5f, false) };
            foreach ((float angle, bool bypass) in shots)
            {
                _demo.Shield.FireFrom(angle, bypass);
                yield return new WaitForSecondsRealtime(1.6f);
            }
        }

        [UnityTest]
        public IEnumerator RecordCartridge()
        {
            CreateDemo(3);
            _demo.Simulator.SetHead(new Vector3(0f, 1.62f, 0f), 0f, 10f);

            _spectator = new GameObject("Spectator").AddComponent<Camera>();
            _spectator.fieldOfView = 50f;
            _spectator.clearFlags = CameraClearFlags.SolidColor;
            _spectator.backgroundColor = LabDemo.Backdrop;
            _spectator.transform.position = new Vector3(LabDemo.CartridgeSpotX - 0.95f, 1.95f, -1.45f);
            _spectator.transform.LookAt(new Vector3(LabDemo.CartridgeSpotX + 0.1f, 1.15f, 0.1f));
            yield return null;

            _demo.StartCoroutine(CartridgeScript());
            yield return DemoCapture.Record(_spectator, "cartridge", seconds: 16f, fps: 15f);
        }

        IEnumerator CartridgeScript()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PlugIn("hook", 0);
            yield return PlugIn("boost", 1);
            yield return Poke(_demo.Cartridge.transform.Find("Booth/Use0"));
            yield return Poke(_demo.Cartridge.transform.Find("Booth/Use1"));
            yield return new WaitForSecondsRealtime(0.6f);

            // 쿨타임 중인 hook을 뽑아 3번 슬롯에 꽂아도 쿨타임은 그대로다
            yield return Move(Local(_demo.Cartridge.GetDockSlot(0).position), 0.6f);
            yield return Grip(true);
            yield return Move(Local(_demo.Cartridge.GetDockSlot(2).position), 0.5f);
            yield return Grip(false);
            yield return Move(new Vector3(0.3f, 1.0f, 0.15f), 0.6f);
        }

        IEnumerator PlugIn(string id, int slot)
        {
            CartridgeItem item = null;
            foreach (CartridgeItem candidate in _demo.Cartridge.Items)
                if (candidate.Id == id)
                    item = candidate;
            Assert.That(item, Is.Not.Null, id);

            yield return Move(Local(item.transform.position), 0.6f);
            yield return Grip(true);
            yield return Move(new Vector3(0.35f, 1.3f, -0.05f), 0.4f);
            yield return Move(Local(_demo.Cartridge.GetDockSlot(slot).position), 0.6f);
            yield return Grip(false);
            yield return Move(new Vector3(0.3f, 1.0f, 0.15f), 0.6f);
        }

        IEnumerator Poke(Transform button)
        {
            Assert.That(button, Is.Not.Null);
            Vector3 front = Local(button.position + Vector3.back * 0.1f);
            yield return Move(front, 0.4f);
            yield return Move(Local(button.position), 0.2f);
            yield return Move(front, 0.2f);
        }
    }
}
