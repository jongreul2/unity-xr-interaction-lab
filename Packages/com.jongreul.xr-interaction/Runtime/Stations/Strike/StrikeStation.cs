using System;
using System.Collections.Generic;
using Jongreul.XrInteraction.Strike;
using UnityEngine;
using UnityEngine.UI;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>손에 쥐면 도구 끝으로 타격을 판정하게 하는 표시. 끝 위치가 곧 타격점이다.</summary>
    public sealed class StrikeTool : MonoBehaviour
    {
        [SerializeField] Transform tip;

        public Transform Tip
        {
            get => tip != null ? tip : transform;
            set => tip = value;
        }
    }

    /// <summary>
    /// 타격 패드. 매 프레임 두 손(또는 쥔 도구 끝)의 위치를 플레이어 리그 기준으로 판정기에 넣고,
    /// 타격면 접촉 영역에 들어오는 순간 한 번 판정한다. 맞으면 게이지가 찬다.
    /// 판정 결과(속도·각도·스트로크)를 표시판에 띄워 실기 튜닝에 쓴다.
    /// </summary>
    public sealed class StrikeStation : MonoBehaviour
    {
        const int GaugeCells = 10;

        [SerializeField] PlayerRig rig;
        [SerializeField] StrikeSettings settings = new StrikeSettings();
        [SerializeField] float surfaceRadius = 0.26f;
        [SerializeField] float contactHeight = 0.03f;
        [SerializeField] float contactDepth = 0.12f;

        readonly StrikeDetector[] _detectors = new StrikeDetector[2];
        readonly bool[] _inside = new bool[2];
        readonly List<string> _log = new List<string>();
        readonly Renderer[] _gauge = new Renderer[GaugeCells];

        Transform _surface;
        Renderer _drumTop;
        Text _resultText;
        Text _logText;
        float _flash;
        Color _flashColor;

        public int Hits { get; private set; }
        public int Contacts { get; private set; }
        public StrikeResult LastResult { get; private set; }
        public StrikeSettings Settings => settings;
        public Transform Surface => _surface;

        public event Action<HandSide, StrikeResult> Evaluated;

        public void Configure(PlayerRig playerRig) => rig = playerRig;

        void Awake()
        {
            Build();
            for (int i = 0; i < _detectors.Length; i++)
                _detectors[i] = new StrikeDetector(settings);
        }

        void Update()
        {
            if (rig == null)
                return;

            double now = Time.timeAsDouble;
            Evaluate(HandSide.Left, now);
            Evaluate(HandSide.Right, now);
            UpdateVisuals();
        }

        public void ResetGauge()
        {
            Hits = 0;
            Contacts = 0;
            _log.Clear();
        }

        void Evaluate(HandSide side, double now)
        {
            Hand hand = rig.GetHand(side);
            if (hand == null)
                return;

            GetStriker(hand, out Vector3 point, out StrikerKind kind);
            int index = (int)side;

            // 리그 기준 위치 — 리그가 움직여도 손을 휘두르지 않았으면 속도가 생기지 않는다.
            Vector3 rigLocal = rig.transform.InverseTransformPoint(point);
            _detectors[index].AddSample(now, rigLocal.ToNumerics());

            bool inside = IsInContactZone(point);
            if (inside && !_inside[index])
            {
                Vector3 normal = rig.transform.InverseTransformDirection(_surface.up);
                StrikeResult result = _detectors[index].Evaluate(now, normal.ToNumerics(), kind);
                Contacts++;
                LastResult = result;
                if (result.Hit)
                    Hits = Mathf.Min(Hits + 1, GaugeCells);

                Flash(result.Hit ? StationKit.Good : StationKit.Bad);
                Log(side, kind, result);
                Evaluated?.Invoke(side, result);
            }

            _inside[index] = inside;
        }

        static void GetStriker(Hand hand, out Vector3 point, out StrikerKind kind)
        {
            if (hand.Held != null && hand.Held.TryGetComponent(out StrikeTool tool))
            {
                point = tool.Tip.position;
                kind = StrikerKind.Tool;
                return;
            }

            point = hand.transform.position;
            kind = StrikerKind.Hand;
        }

        bool IsInContactZone(Vector3 world)
        {
            Vector3 local = _surface.InverseTransformPoint(world);
            float radial = new Vector2(local.x, local.z).magnitude;
            return radial <= surfaceRadius && local.y <= contactHeight && local.y >= -contactDepth;
        }

        #region 표시

        void Build()
        {
            StationKit.Primitive(PrimitiveType.Cylinder, transform, "Stand", new Vector3(0, 0.4f, 0),
                new Vector3(0.12f, 0.4f, 0.12f), StationKit.Surface);
            GameObject drum = StationKit.Primitive(PrimitiveType.Cylinder, transform, "Drum", new Vector3(0, 0.88f, 0),
                new Vector3(0.56f, 0.08f, 0.56f), new Color(0.55f, 0.36f, 0.24f));
            GameObject top = StationKit.Primitive(PrimitiveType.Cylinder, transform, "DrumTop", new Vector3(0, 0.965f, 0),
                new Vector3(0.52f, 0.005f, 0.52f), new Color(0.9f, 0.86f, 0.78f));
            _drumTop = top.GetComponent<Renderer>();

            _surface = new GameObject("Surface").transform;
            _surface.SetParent(transform, false);
            _surface.localPosition = new Vector3(0, 0.97f, 0);

            for (int i = 0; i < GaugeCells; i++)
            {
                float x = (i - (GaugeCells - 1) * 0.5f) * 0.055f;
                GameObject cell = StationKit.Primitive(PrimitiveType.Cube, transform, $"Gauge{i}",
                    new Vector3(x, 1.12f, 0.3f), new Vector3(0.045f, 0.06f, 0.02f), StationKit.Surface);
                _gauge[i] = cell.GetComponent<Renderer>();
            }

            StationKit.Label(transform, "Title", new Vector3(0, 1.62f, 0.32f), new Vector2(0.8f, 0.08f), 56).text =
                "<b>STRIKE PAD</b>";
            _resultText = StationKit.Label(transform, "Result", new Vector3(0, 1.48f, 0.32f), new Vector2(0.9f, 0.12f), 44);
            _logText = StationKit.Label(transform, "Log", new Vector3(0, 1.3f, 0.32f), new Vector2(0.9f, 0.22f), 30,
                TextAnchor.UpperCenter, StationKit.Muted);
            drum.name = "Drum";
        }

        void Flash(Color color)
        {
            _flash = 1f;
            _flashColor = color;
        }

        void UpdateVisuals()
        {
            for (int i = 0; i < GaugeCells; i++)
                _gauge[i].material.color = i < Hits ? StationKit.Warn : StationKit.Surface;

            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 3f);
            _drumTop.material.color = Color.Lerp(new Color(0.9f, 0.86f, 0.78f), _flashColor, _flash);

            StrikeResult r = LastResult;
            _resultText.text = Contacts == 0
                ? $"<color={StationKit.Hex(StationKit.Muted)}>hand ≥ {settings.MinHandSpeed:0.0} m/s · tool ≥ {settings.MinToolSpeed:0.0} m/s · angle ≤ {settings.MaxAngleDegrees:0}° · stroke ≥ {settings.MinStrokeLength:0.00} m</color>"
                : $"<color={StationKit.Hex(r.Hit ? StationKit.Good : StationKit.Bad)}><b>{(r.Hit ? "HIT" : Describe(r.Reason))}</b></color>  " +
                  $"{r.Speed:0.0} m/s · {r.AngleDegrees:0}° · {r.StrokeLength:0.00} m";
            _logText.text = string.Join("\n", _log);
        }

        void Log(HandSide side, StrikerKind kind, StrikeResult result)
        {
            _log.Insert(0, $"{side} {kind}: {result}");
            if (_log.Count > 5)
                _log.RemoveAt(_log.Count - 1);
        }

        static string Describe(StrikeRejectReason reason)
        {
            switch (reason)
            {
                case StrikeRejectReason.WrongAngle: return "SWIPE (angle)";
                case StrikeRejectReason.TooSlow: return "TOO SLOW";
                case StrikeRejectReason.StrokeTooShort: return "WRIST SNAP";
                case StrikeRejectReason.MovingAway: return "PULLING AWAY";
                case StrikeRejectReason.LockedOut: return "BOUNCE (locked)";
                default: return reason.ToString().ToUpperInvariant();
            }
        }

        #endregion
    }
}
