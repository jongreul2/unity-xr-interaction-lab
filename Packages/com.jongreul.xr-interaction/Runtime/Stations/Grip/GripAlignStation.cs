using System;
using System.Collections.Generic;
using Jongreul.XrInteraction.Grip;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jongreul.XrInteraction.Stations
{
    public enum GripAxis
    {
        X,
        Y,
        Z,
        Pitch,
        Yaw,
        Roll,
    }

    /// <summary>그립 정렬 스테이션의 도구. 놓으면 받침대 제자리로 돌아간다.</summary>
    public sealed class GripTool : Grabbable
    {
        internal GripAlignStation Station;

        public string DisplayName { get; internal set; }
        public Transform Home { get; internal set; }

        protected override void OnGrabbed(Hand hand) => Station?.OnToolGrabbed(this);

        protected override void OnReleased(Hand hand, Vector3 velocity) => Station?.OnToolReleased(this);
    }

    /// <summary>슬라이더 손잡이. 잡혀도 손을 따라가지 않고 트랙 위로만 움직인다.</summary>
    public sealed class SliderKnob : Grabbable
    {
        internal WorldSlider Slider;
        float _grabOffset;

        protected override void OnGrabbed(Hand hand)
        {
            // 잡은 지점과 손잡이 중심의 차이를 유지해 잡는 순간 값이 튀지 않게 한다.
            _grabOffset = Slider.LocalX(transform.position) - Slider.LocalX(hand.transform.position);
        }

        protected override void LateUpdate()
        {
            if (IsHeld)
                Slider.DragTo(Holder.transform.position, _grabOffset);
        }
    }

    /// <summary>손으로 끄는 3D 슬라이더. 트랙은 로컬 X축, 수학은 Core <see cref="TrackSlider"/>.</summary>
    public sealed class WorldSlider : MonoBehaviour
    {
        public const float Length = 0.3f;

        float _normalized;
        float _min;
        float _max;
        float _step;
        string _format;
        Transform _knob;
        Text _valueText;

        public string Label { get; private set; }
        public SliderKnob Knob { get; private set; }
        public float Value => TrackSlider.Snap(TrackSlider.ToValue(_normalized, _min, _max), _step);

        public event Action<WorldSlider> Changed;

        public static WorldSlider Create(Transform parent, string label, Vector3 localPosition, float min, float max,
            float step, string format)
        {
            var go = new GameObject($"Slider_{label}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var slider = go.AddComponent<WorldSlider>();
            slider.Label = label;
            slider._min = min;
            slider._max = max;
            slider._step = step;
            slider._format = format;

            StationKit.Primitive(PrimitiveType.Cube, go.transform, "Track", Vector3.zero,
                new Vector3(Length, 0.008f, 0.008f), new Color(0.4f, 0.43f, 0.5f));
            StationKit.Primitive(PrimitiveType.Cube, go.transform, "Zero",
                new Vector3(TrackSlider.ToLocalX(TrackSlider.FromValue(0f, min, max), Length), 0f, 0f),
                new Vector3(0.004f, 0.03f, 0.01f), StationKit.Muted);
            StationKit.Label(go.transform, "Name", new Vector3(-Length * 0.5f - 0.075f, 0f, -0.01f),
                new Vector2(0.12f, 0.04f), 26, TextAnchor.MiddleRight).text = label;
            slider._valueText = StationKit.Label(go.transform, "Value", new Vector3(Length * 0.5f + 0.075f, 0f, -0.01f),
                new Vector2(0.12f, 0.04f), 26, TextAnchor.MiddleLeft, StationKit.Warn);

            GameObject knob = StationKit.Primitive(PrimitiveType.Sphere, go.transform, "Knob", Vector3.zero,
                Vector3.one * 0.035f, StationKit.Accent, collider: true);
            var body = knob.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            slider.Knob = knob.AddComponent<SliderKnob>();
            slider.Knob.Slider = slider;
            slider._knob = knob.transform;

            slider.SetValue(0f);
            return slider;
        }

        public float LocalX(Vector3 world) => transform.InverseTransformPoint(world).x;

        /// <summary>트랙 위 value 지점의 월드 위치(촬영·테스트용).</summary>
        public Vector3 WorldPointFor(float value) =>
            transform.TransformPoint(new Vector3(TrackSlider.ToLocalX(TrackSlider.FromValue(value, _min, _max), Length), 0f, 0f));

        /// <summary>코드에서 값 넣기. notify=false면 Changed를 부르지 않는다(도구를 잡을 때 현재 값 불러오기).</summary>
        public void SetValue(float value, bool notify = false)
        {
            _normalized = TrackSlider.FromValue(value, _min, _max);
            Place();
            if (notify)
                Changed?.Invoke(this);
        }

        /// <summary>손 위치를 트랙에 투영해 값을 바꾼다.</summary>
        internal void DragTo(Vector3 handWorld, float grabOffset)
        {
            float normalized = TrackSlider.ToNormalized(LocalX(handWorld) + grabOffset, Length);
            bool changed = !Mathf.Approximately(normalized, _normalized);
            _normalized = normalized;
            Place();
            if (changed)
                Changed?.Invoke(this);
        }

        void Place()
        {
            _knob.localPosition = new Vector3(TrackSlider.ToLocalX(_normalized, Length), 0f, 0f);
            _valueText.text = Value.ToString(_format);
        }
    }

    /// <summary>
    /// 그립 정렬 스테이션. 도구를 한 손에 쥐고 다른 손으로 패널 슬라이더를 끌면 손 기준 오프셋(위치·회전)이 바로 바뀌고,
    /// SAVE로 그 도구의 프로필에 저장한다. 오른손 값만 저장하면 왼손은 거울상으로 쥔다.
    /// 헤드셋을 쓴 채 코드 없이 쥔 느낌을 맞추는 VR 안의 튜닝 도구 — 에디터 창(Grip Tuning)과 같은 프로필을 쓴다.
    /// </summary>
    public sealed class GripAlignStation : MonoBehaviour
    {
        public const float PositionRange = 0.1f;

        /// <summary>기본 도구와 오른손 오프셋(위치 m, 회전 °). 마지막은 아직 맞추지 않은 새 도구 — 옆으로 누운 채 쥐어진다.</summary>
        public static readonly (string Name, Vector3 Position, Vector3 Euler)[] DefaultTools =
        {
            ("Hammer", new Vector3(0f, 0f, 0.03f), new Vector3(45f, -8f, 0f)),
            ("Torch", new Vector3(0f, -0.02f, 0.02f), new Vector3(90f, 0f, 0f)),
            ("Paddle", new Vector3(0.05f, 0f, 0f), new Vector3(0f, 0f, 70f)),
        };

        static readonly string[] AxisNames = { "X", "Y", "Z", "PITCH", "YAW", "ROLL" };

        readonly List<GripTool> _tools = new List<GripTool>();
        readonly List<GripOffsetProfile> _ownedProfiles = new List<GripOffsetProfile>();
        readonly WorldSlider[] _sliders = new WorldSlider[AxisNames.Length];

        PlayerRig _rig;
        Transform _panel;
        Text _readout;
        GripTool _active;
        bool _dirty;
        string _notice = string.Empty;

        public IReadOnlyList<GripTool> Tools => _tools;
        public GripTool ActiveTool => _active;
        public bool HasUnsavedChanges => _dirty;
        public PokeButton SaveButton { get; private set; }
        public PokeButton RevertButton { get; private set; }
        public WorldSlider GetSlider(GripAxis axis) => _sliders[(int)axis];

        public event Action<GripTool, HandSide> Saved;

        void Awake()
        {
            BuildTable();
            BuildPanel();
        }

        /// <param name="profiles">도구 순서대로의 프로필 에셋. 없으면 기본값으로 만든 런타임 프로필을 쓴다.</param>
        public void Configure(PlayerRig rig, IReadOnlyList<GripOffsetProfile> profiles = null)
        {
            _rig = rig;
            if (profiles != null)
            {
                for (int i = 0; i < _tools.Count && i < profiles.Count; i++)
                {
                    if (profiles[i] != null)
                        _tools[i].GripProfile = profiles[i];
                }
            }

            BuildButtons();
        }

        public GripTool FindTool(string toolName)
        {
            foreach (GripTool tool in _tools)
            {
                if (tool.DisplayName == toolName)
                    return tool;
            }

            return null;
        }

        /// <summary>기본값 프로필 하나(런타임 인스턴스). 씬 빌더는 이것으로 에셋을 만든다.</summary>
        public static GripOffsetProfile CreateDefaultProfile(int index)
        {
            (string toolName, Vector3 position, Vector3 euler) = DefaultTools[index];
            var profile = ScriptableObject.CreateInstance<GripOffsetProfile>();
            profile.name = toolName;
            profile.Set(HandSide.Right, new GripOffset(position.ToNumerics(), Quaternion.Euler(euler).ToNumerics()));
            return profile;
        }

        /// <summary>슬라이더 여섯 개가 나타내는 오프셋.</summary>
        public GripOffset CurrentValue()
        {
            var position = new Vector3(Value(GripAxis.X), Value(GripAxis.Y), Value(GripAxis.Z));
            var euler = new Vector3(Value(GripAxis.Pitch), Value(GripAxis.Yaw), Value(GripAxis.Roll));
            return new GripOffset(position.ToNumerics(), Quaternion.Euler(euler).ToNumerics());
        }

        /// <summary>쥔 손 쪽 프로필에 저장한다. 쥔 도구가 없으면 false.</summary>
        public bool Save()
        {
            if (_active == null || !_active.IsHeld || _active.GripProfile == null)
                return false;

            HandSide side = _active.Holder.Side;
            GripOffsetProfile profile = _active.GripProfile;
            profile.Set(side, CurrentValue());
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.Contains(profile))
            {
                UnityEditor.EditorUtility.SetDirty(profile);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif
            _active.ClearOffsetOverride();
            _dirty = false;
            _notice = $"saved to {profile.name} ({side.ToString().ToLowerInvariant()} hand)";
            Saved?.Invoke(_active, side);
            return true;
        }

        /// <summary>조절한 값을 버리고 프로필 값으로 돌아간다.</summary>
        public void Revert()
        {
            if (_active == null || !_active.IsHeld)
                return;

            _active.ClearOffsetOverride();
            LoadSliders(_active.CurrentOffset);
            _dirty = false;
            _notice = "reverted to profile";
        }

        internal void OnToolGrabbed(GripTool tool)
        {
            _active = tool;
            _dirty = false;
            _notice = string.Empty;
            LoadSliders(tool.CurrentOffset);
        }

        internal void OnToolReleased(GripTool tool)
        {
            if (tool == _active)
            {
                if (_dirty)
                {
                    tool.ClearOffsetOverride();
                    _notice = "released without saving: discarded";
                }

                _active = null;
                _dirty = false;
            }

            tool.transform.SetPositionAndRotation(tool.Home.position, tool.Home.rotation);
        }

        void OnSliderChanged(WorldSlider slider)
        {
            if (_active == null || !_active.IsHeld)
                return;

            _active.SetOffsetOverride(CurrentValue());
            _dirty = true;
            _notice = string.Empty;
        }

        float Value(GripAxis axis) => _sliders[(int)axis].Value;

        void LoadSliders(GripOffset offset)
        {
            Vector3 position = offset.Position.ToUnity();
            Vector3 euler = offset.Rotation.ToUnity().eulerAngles;
            float[] values =
            {
                position.x, position.y, position.z,
                Mathf.DeltaAngle(0f, euler.x), Mathf.DeltaAngle(0f, euler.y), Mathf.DeltaAngle(0f, euler.z),
            };
            for (int i = 0; i < _sliders.Length; i++)
                _sliders[i].SetValue(values[i]);
        }

        void Update()
        {
            if (_active != null && _active.IsHeld)
            {
                string state = _dirty
                    ? $"<color={StationKit.Hex(StationKit.Warn)}>modified</color>"
                    : $"<color={StationKit.Hex(StationKit.Good)}>matches profile</color>";
                bool mirrored = _active.Holder.Side == HandSide.Left && _active.GripProfile != null &&
                                _active.GripProfile.MirrorForLeft;
                string footer = mirrored && !_dirty ? "left hand = mirror of the right-hand value" : _notice;
                _readout.text =
                    $"<b>{_active.DisplayName.ToUpperInvariant()}</b> in {_active.Holder.Side.ToString().ToLowerInvariant()} hand · {state}\n" +
                    $"pos ({Value(GripAxis.X):0.000}, {Value(GripAxis.Y):0.000}, {Value(GripAxis.Z):0.000}) m   " +
                    $"rot ({Value(GripAxis.Pitch):0}, {Value(GripAxis.Yaw):0}, {Value(GripAxis.Roll):0})\n{footer}";
            }
            else
            {
                _readout.text = string.IsNullOrEmpty(_notice)
                    ? "hold a tool in one hand,\ndrag the sliders with the other"
                    : _notice;
            }
        }

        void OnDestroy()
        {
            foreach (GripOffsetProfile profile in _ownedProfiles)
            {
                if (profile != null)
                    Destroy(profile);
            }
        }

        #region 조립

        void BuildTable()
        {
            StationKit.Primitive(PrimitiveType.Cube, transform, "Table", new Vector3(0.1f, 0.45f, 0.5f),
                new Vector3(0.8f, 0.9f, 0.35f), new Color(0.2f, 0.21f, 0.25f));

            for (int i = 0; i < DefaultTools.Length; i++)
            {
                var home = new GameObject($"Home_{DefaultTools[i].Name}").transform;
                home.SetParent(transform, false);
                home.localPosition = new Vector3(0.1f + (i - 1) * 0.22f, 0.99f, 0.5f);
                _tools.Add(BuildTool(i, home));
            }
        }

        GripTool BuildTool(int index, Transform home)
        {
            string toolName = DefaultTools[index].Name;
            var go = new GameObject($"Tool_{toolName}");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(home.position, home.rotation);

            // 손잡이(쥐는 점)가 원점, 긴 축이 +Y.
            var wood = new Color(0.55f, 0.38f, 0.24f);
            var steel = new Color(0.62f, 0.65f, 0.7f);
            switch (index)
            {
                case 0:
                    StationKit.Primitive(PrimitiveType.Cylinder, go.transform, "Handle", new Vector3(0f, 0.06f, 0f),
                        new Vector3(0.028f, 0.14f, 0.028f), wood);
                    StationKit.Primitive(PrimitiveType.Cube, go.transform, "Head", new Vector3(0f, 0.2f, 0f),
                        new Vector3(0.13f, 0.05f, 0.05f), steel);
                    break;
                case 1:
                    StationKit.Primitive(PrimitiveType.Cylinder, go.transform, "Body", new Vector3(0f, 0.05f, 0f),
                        new Vector3(0.04f, 0.11f, 0.04f), new Color(0.25f, 0.27f, 0.32f));
                    StationKit.Primitive(PrimitiveType.Cylinder, go.transform, "Lens", new Vector3(0f, 0.165f, 0f),
                        new Vector3(0.05f, 0.008f, 0.05f), new Color(1f, 0.93f, 0.6f));
                    break;
                default:
                    StationKit.Primitive(PrimitiveType.Cylinder, go.transform, "Handle", new Vector3(0f, 0.03f, 0f),
                        new Vector3(0.025f, 0.08f, 0.025f), wood);
                    StationKit.Primitive(PrimitiveType.Cube, go.transform, "Blade", new Vector3(0f, 0.2f, 0f),
                        new Vector3(0.12f, 0.18f, 0.015f), StationKit.Accent);
                    break;
            }

            var collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.02f, 0f);
            collider.size = new Vector3(0.06f, 0.16f, 0.06f);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            GripOffsetProfile profile = CreateDefaultProfile(index);
            _ownedProfiles.Add(profile);

            var tool = go.AddComponent<GripTool>();
            tool.DisplayName = toolName;
            tool.Home = home;
            tool.Station = this;
            tool.GripProfile = profile;
            return tool;
        }

        void BuildPanel()
        {
            _panel = new GameObject("TuningPanel").transform;
            _panel.SetParent(transform, false);
            _panel.localPosition = new Vector3(-0.6f, 0f, 0.42f);
            _panel.localRotation = Quaternion.Euler(0f, -55f, 0f); // 플레이어 쪽을 본다

            StationKit.Primitive(PrimitiveType.Cube, _panel, "Board", new Vector3(0f, 1.28f, 0.03f),
                new Vector3(0.64f, 0.84f, 0.01f), new Color(0.13f, 0.14f, 0.17f));
            StationKit.Primitive(PrimitiveType.Cylinder, _panel, "Stand", new Vector3(0f, 0.43f, 0.05f),
                new Vector3(0.05f, 0.43f, 0.05f), StationKit.Surface);
            StationKit.Label(_panel, "Title", new Vector3(0f, 1.79f, 0f), new Vector2(0.7f, 0.07f), 50).text =
                "<b>GRIP ALIGN</b>";
            _readout = StationKit.Label(_panel, "Readout", new Vector3(0f, 1.62f, -0.01f), new Vector2(0.62f, 0.13f), 22,
                TextAnchor.MiddleCenter, StationKit.Muted);

            float[] rows = { 1.5f, 1.415f, 1.33f, 1.22f, 1.135f, 1.05f };
            for (int i = 0; i < _sliders.Length; i++)
            {
                bool rotation = i >= (int)GripAxis.Pitch;
                _sliders[i] = WorldSlider.Create(_panel, AxisNames[i], new Vector3(0.02f, rows[i], 0f),
                    rotation ? -180f : -PositionRange, rotation ? 180f : PositionRange, rotation ? 1f : 0.002f,
                    rotation ? "0" : "0.000");
                _sliders[i].Changed += OnSliderChanged;
            }
        }

        void BuildButtons()
        {
            SaveButton = PokeButton.Create(_panel, "Save", "SAVE", new Vector3(-0.1f, 0.94f, -0.02f),
                new Color(0.2f, 0.45f, 0.3f), _rig);
            RevertButton = PokeButton.Create(_panel, "Revert", "REVERT", new Vector3(0.1f, 0.94f, -0.02f),
                StationKit.Surface, _rig);
            SaveButton.Pressed += () => Save();
            RevertButton.Pressed += Revert;
        }

        #endregion
    }
}
