using System;
using UnityEngine;
using UnityEngine.UI;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>
    /// 손으로 누르는 3D 버튼. 손 끝이 버튼 상자 안에 들어오는 순간 한 번 눌리고, 빠져나가야 다시 눌린다.
    /// 레이 포인터 없이 손을 직접 뻗어 누르는 방식.
    /// </summary>
    public sealed class PokeButton : MonoBehaviour
    {
        [SerializeField] Vector3 size = new Vector3(0.14f, 0.07f, 0.06f);

        readonly bool[] _inside = new bool[2];
        PlayerRig _rig;
        Renderer _cap;
        Text _label;
        Color _color;
        float _pressFlash;

        public event Action Pressed;

        public string Label
        {
            get => _label != null ? _label.text : string.Empty;
            set
            {
                if (_label != null)
                    _label.text = value;
            }
        }

        public static PokeButton Create(Transform parent, string name, string label, Vector3 localPosition, Color color,
            PlayerRig rig)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var button = go.AddComponent<PokeButton>();
            button._rig = rig;
            button._color = color;
            button._cap = StationKit.Primitive(PrimitiveType.Cube, go.transform, "Cap", Vector3.zero, button.size, color)
                .GetComponent<Renderer>();
            button._label = StationKit.Label(go.transform, "Label", new Vector3(0, 0, -button.size.z * 0.5f - 0.002f),
                new Vector2(button.size.x, button.size.y), 26);
            button._label.text = label;
            return button;
        }

        public void SetColor(Color color) => _color = color;

        /// <summary>코드·테스트에서 누르기.</summary>
        public void Press()
        {
            _pressFlash = 1f;
            Pressed?.Invoke();
        }

        void Update()
        {
            if (_rig != null)
            {
                Check(_rig.LeftHand, 0);
                Check(_rig.RightHand, 1);
            }

            _pressFlash = Mathf.Max(0f, _pressFlash - Time.deltaTime * 4f);
            if (_cap != null)
                _cap.material.color = Color.Lerp(_color, Color.white, _pressFlash * 0.6f);
        }

        void Check(Hand hand, int index)
        {
            if (hand == null)
                return;

            Vector3 local = transform.InverseTransformPoint(hand.transform.position);
            Vector3 half = size * 0.5f;
            bool inside = Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
            if (inside && !_inside[index])
                Press();
            _inside[index] = inside;
        }
    }
}
