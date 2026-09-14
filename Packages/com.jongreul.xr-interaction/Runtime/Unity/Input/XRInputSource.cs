using UnityEngine;
using UnityEngine.XR;

namespace Jongreul.XrInteraction
{
    /// <summary>한 프레임의 손 입력. 위치·회전은 트래킹 원점(플레이어 리그) 기준.</summary>
    public readonly struct HandInputFrame
    {
        public readonly bool Tracked;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Grip;
        public readonly float Trigger;
        public readonly bool Primary;

        public HandInputFrame(bool tracked, Vector3 position, Quaternion rotation, float grip, float trigger, bool primary)
        {
            Tracked = tracked;
            Position = position;
            Rotation = rotation;
            Grip = grip;
            Trigger = trigger;
            Primary = primary;
        }
    }

    /// <summary>머리·손 입력 공급원. 실기기와 데스크톱 시뮬레이터가 같은 모양으로 들어온다.</summary>
    public interface IXRInputSource
    {
        bool TryGetHead(out Vector3 position, out Quaternion rotation);
        bool TryGetHand(HandSide side, out HandInputFrame frame);
    }

    /// <summary>
    /// Unity 내장 XR 입력(<see cref="InputDevices"/>). OpenXR로 Quest 컨트롤러를 읽는다. 툴킷을 거치지 않는다.
    /// </summary>
    public sealed class XRDeviceInputSource : IXRInputSource
    {
        public bool IsAvailable =>
            InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid ||
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;

        public bool TryGetHead(out Vector3 position, out Quaternion rotation)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            position = default;
            rotation = Quaternion.identity;
            return device.isValid &&
                   device.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
                   device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
        }

        public bool TryGetHand(HandSide side, out HandInputFrame frame)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(side == HandSide.Left ? XRNode.LeftHand : XRNode.RightHand);
            if (!device.isValid)
            {
                frame = default;
                return false;
            }

            device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked);
            device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                rotation = Quaternion.identity;
            device.TryGetFeatureValue(CommonUsages.grip, out float grip);
            device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
            device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary);

            frame = new HandInputFrame(tracked, position, rotation, grip, trigger, primary);
            return true;
        }
    }
}
