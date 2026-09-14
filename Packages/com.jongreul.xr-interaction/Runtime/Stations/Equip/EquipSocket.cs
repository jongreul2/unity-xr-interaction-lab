using Jongreul.XrInteraction.Equip;
using UnityEngine;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>
    /// 신체 소켓(머리·얼굴·등). 트랜스폼의 forward가 소켓 바깥쪽 법선이다.
    /// 근접 판정은 Core의 <see cref="SocketSensor"/>(거리 + 법선 각도)가 한다.
    /// </summary>
    public sealed class EquipSocket : MonoBehaviour
    {
        const int HaloDots = 12;

        [SerializeField] string slot;
        [SerializeField] float radius = 0.2f;
        [SerializeField] float maxAngleDegrees = 80f;

        GameObject _halo;
        Renderer[] _haloDots;

        public string Slot => slot;

        public static EquipSocket Create(Transform parent, string slot, Vector3 localPosition, Quaternion localRotation,
            float radius = 0.2f, float maxAngleDegrees = 80f)
        {
            var go = new GameObject($"Socket_{slot}");
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            var socket = go.AddComponent<EquipSocket>();
            socket.slot = slot;
            socket.radius = radius;
            socket.maxAngleDegrees = maxAngleDegrees;
            return socket;
        }

        public SocketCandidate ToCandidate() =>
            new SocketCandidate(slot, transform.position.ToNumerics(), transform.forward.ToNumerics(), radius,
                maxAngleDegrees);

        /// <summary>소켓 둘레에 점 고리를 띄워 "여기에 놓으면 장착"을 알린다.</summary>
        public void SetHighlight(bool visible, Color color)
        {
            if (visible && _halo == null)
                BuildHalo();
            if (_halo == null)
                return;

            _halo.SetActive(visible);
            if (!visible)
                return;
            foreach (Renderer dot in _haloDots)
                dot.material.color = color;
        }

        void BuildHalo()
        {
            _halo = new GameObject("Halo");
            _halo.transform.SetParent(transform, false);
            _haloDots = new Renderer[HaloDots];
            for (int i = 0; i < HaloDots; i++)
            {
                float angle = i * Mathf.PI * 2f / HaloDots;
                var position = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.12f;
                _haloDots[i] = StationKit.Primitive(PrimitiveType.Sphere, _halo.transform, $"Dot{i}", position,
                    Vector3.one * 0.018f, StationKit.Good).GetComponent<Renderer>();
            }
        }
    }
}
