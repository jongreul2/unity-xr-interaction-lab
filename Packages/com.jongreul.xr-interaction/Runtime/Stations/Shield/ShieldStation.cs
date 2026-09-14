using System;
using System.Collections.Generic;
using Jongreul.XrInteraction.Shield;
using UnityEngine;
using UnityEngine.UI;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>스테이션 방패. 놓으면 받침대로 돌아간다.</summary>
    public sealed class StationShield : Grabbable
    {
        internal Transform Home;

        protected override void OnReleased(Hand hand, Vector3 velocity)
        {
            if (Home != null)
                transform.SetPositionAndRotation(Home.position, Home.rotation);
        }
    }

    /// <summary>
    /// 방패 막기 스테이션. 표적 장치가 여러 방향에서 투사체를 쏜다.
    /// 방패를 쥐고 몸 앞에 두면, 방패 앞면과 투사체가 오는 방향 사이 각도가 ±45° 안일 때 막는다.
    /// 막을 때마다 내구도가 줄고 색이 초록 → 노랑 → 빨강으로 바뀌며, 0이면 부서져 아무것도 막지 못한다.
    /// 보라색 투사체는 막기를 무시한다.
    /// </summary>
    public sealed class ShieldStation : MonoBehaviour
    {
        const float LaunchDistance = 2.3f;
        const float ResolveDistance = 0.32f;
        const float ShieldReach = 0.65f;

        static readonly (float Angle, bool Bypass)[] Pattern =
        {
            (0f, false), (25f, false), (-30f, false), (75f, false), (0f, true), (-10f, false),
        };

        struct Projectile
        {
            public Transform Transform;
            public Vector3 Direction;
            public bool Bypass;
        }

        [SerializeField] float fireInterval = 1.7f;
        [SerializeField] float projectileSpeed = 2.2f;
        [SerializeField] int damagePerBlock = 20;

        readonly List<Projectile> _projectiles = new List<Projectile>();

        PlayerRig _rig;
        ShieldBlocker _blocker;
        StationShield _shield;
        Renderer _shieldFace;
        Transform _turret;
        Text _status;
        Text _outcome;
        float _nextFire;
        int _patternIndex;
        float _flash;
        Color _flashColor;

        public bool AutoFire { get; set; } = true;
        public ShieldBlocker Blocker => _blocker;
        public Grabbable Shield => _shield;
        public int Blocked { get; private set; }
        public int Hits { get; private set; }
        public int Bypassed { get; private set; }
        public int InFlight => _projectiles.Count;

        public event Action<BlockOutcome> Resolved;

        public void Configure(PlayerRig rig)
        {
            _rig = rig;
            PokeButton.Create(transform, "Repair", "REPAIR", new Vector3(-0.45f, 0.95f, 0.45f), new Color(0.25f, 0.4f, 0.35f), rig)
                .Pressed += Repair;
        }

        void Awake()
        {
            _blocker = new ShieldBlocker(new BlockJudge(45f), new ShieldDurability(100));
            _blocker.Durability.StageChanged += _ => Recolor();
            Build();
            Recolor();
        }

        void Update()
        {
            if (_rig == null)
                return;

            // 플레이어가 이 스테이션 앞에 있을 때만 쏜다(다른 스테이션에 있을 때 랩을 가로질러 날아오지 않게).
            if (AutoFire && IsPlayerNear && Time.time >= _nextFire)
            {
                (float angle, bool bypass) = Pattern[_patternIndex++ % Pattern.Length];
                FireFrom(angle, bypass);
            }

            MoveProjectiles();

            // 결과 색은 다음 판정 전까지 옅게라도 남긴다(무엇이 일어났는지 계속 읽히게).
            _flash = Mathf.Max(0.45f, _flash - Time.deltaTime * 0.8f);
            _outcome.color = Color.Lerp(StationKit.Muted, _flashColor, _flash);
        }

        /// <summary>플레이어 정면 기준 angle(도)만큼 옆에서 쏜다.</summary>
        public void FireFrom(float angleDegrees, bool bypass)
        {
            _nextFire = Time.time + fireInterval;
            Vector3 target = Target;
            Vector3 origin = target + Quaternion.Euler(0f, angleDegrees, 0f) * transform.forward * LaunchDistance + Vector3.up * 0.15f;
            Vector3 direction = (target - origin).normalized;

            _turret.position = origin;
            _turret.rotation = Quaternion.LookRotation(direction, Vector3.up);

            GameObject ball = StationKit.Primitive(PrimitiveType.Sphere, transform, "Projectile", Vector3.zero,
                Vector3.one * 0.1f, bypass ? new Color(0.7f, 0.35f, 0.95f) : new Color(0.98f, 0.55f, 0.2f));
            ball.transform.position = origin;
            _projectiles.Add(new Projectile { Transform = ball.transform, Direction = direction, Bypass = bypass });
        }

        public void Repair() => _blocker.Durability.Repair();

        Vector3 Target => _rig.Head.position + Vector3.down * 0.35f;

        bool IsPlayerNear
        {
            get
            {
                Vector3 offset = _rig.Head.position - transform.position;
                offset.y = 0f;
                return offset.magnitude < 1.5f;
            }
        }

        void MoveProjectiles()
        {
            Vector3 target = Target;
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _projectiles[i];
                projectile.Transform.position += projectile.Direction * (projectileSpeed * Time.deltaTime);
                if (Vector3.Distance(projectile.Transform.position, target) > ResolveDistance)
                    continue;

                Resolve(projectile, target);
                Destroy(projectile.Transform.gameObject);
                _projectiles.RemoveAt(i);
            }
        }

        void Resolve(Projectile projectile, Vector3 target)
        {
            BlockOutcome outcome;
            bool shieldInFront = _shield.IsHeld && Vector3.Distance(_shield.transform.position, target) <= ShieldReach;
            if (!shieldInFront)
                outcome = projectile.Bypass ? BlockOutcome.Bypassed : BlockOutcome.Missed;
            else
                outcome = _blocker.ReceiveAttack(_shield.transform.forward.ToNumerics(), projectile.Direction.ToNumerics(),
                    damagePerBlock, projectile.Bypass);

            switch (outcome)
            {
                case BlockOutcome.Blocked:
                    Blocked++;
                    Show("BLOCKED", StationKit.Good);
                    break;
                case BlockOutcome.Bypassed:
                    Bypassed++;
                    Hits++;
                    Show("UNBLOCKABLE", new Color(0.75f, 0.45f, 1f));
                    break;
                case BlockOutcome.ShieldBroken:
                    Hits++;
                    Show("SHIELD BROKEN", StationKit.Bad);
                    break;
                default:
                    Hits++;
                    Show("HIT", StationKit.Bad);
                    break;
            }

            UpdateStatus();
            Resolved?.Invoke(outcome);
        }

        void Show(string text, Color color)
        {
            _outcome.text = text;
            _flashColor = color;
            _flash = 1f;
        }

        void Recolor()
        {
            if (_shieldFace == null)
                return;

            switch (_blocker.Durability.Stage)
            {
                case DurabilityStage.Healthy: _shieldFace.material.color = StationKit.Good; break;
                case DurabilityStage.Worn: _shieldFace.material.color = StationKit.Warn; break;
                case DurabilityStage.Critical: _shieldFace.material.color = StationKit.Bad; break;
                default: _shieldFace.material.color = new Color(0.2f, 0.2f, 0.22f); break;
            }

            UpdateStatus();
        }

        void UpdateStatus()
        {
            if (_status == null)
                return;
            ShieldDurability d = _blocker.Durability;
            _status.text = $"durability {d.Current}/{d.Max} ({d.Stage})   blocked {Blocked}   hit {Hits}";
        }

        void Build()
        {
            StationKit.Primitive(PrimitiveType.Cube, transform, "Stand", new Vector3(0.3f, 0.5f, 0.45f), new Vector3(0.1f, 1f, 0.1f), StationKit.Surface);

            var home = new GameObject("ShieldHome").transform;
            home.SetParent(transform, false);
            home.localPosition = new Vector3(0.3f, 1.15f, 0.42f);

            var shieldGo = new GameObject("Shield");
            shieldGo.transform.SetParent(transform, false);
            shieldGo.transform.SetPositionAndRotation(home.position, home.rotation);
            GameObject disc = StationKit.Primitive(PrimitiveType.Cylinder, shieldGo.transform, "Face", Vector3.zero,
                new Vector3(0.42f, 0.015f, 0.42f), StationKit.Good);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 원판 면이 +Z(앞)를 향하게
            StationKit.Primitive(PrimitiveType.Cylinder, shieldGo.transform, "Boss", new Vector3(0f, 0f, 0.02f),
                new Vector3(0.1f, 0.01f, 0.1f), new Color(0.85f, 0.85f, 0.9f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _shieldFace = disc.GetComponent<Renderer>();

            var collider = shieldGo.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.42f, 0.42f, 0.05f);
            var body = shieldGo.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            _shield = shieldGo.AddComponent<StationShield>();
            _shield.Home = home;

            _turret = StationKit.Primitive(PrimitiveType.Cube, transform, "Turret", new Vector3(0f, 1.4f, LaunchDistance),
                new Vector3(0.18f, 0.18f, 0.3f), new Color(0.4f, 0.42f, 0.5f)).transform;

            StationKit.Label(transform, "Title", new Vector3(0f, 2.05f, 1.2f), new Vector2(1f, 0.1f), 64).text = "<b>SHIELD</b>";
            _outcome = StationKit.Label(transform, "Outcome", new Vector3(0f, 1.9f, 1.2f), new Vector2(1f, 0.1f), 56);
            _status = StationKit.Label(transform, "Status", new Vector3(0f, 1.78f, 1.2f), new Vector2(1.4f, 0.06f), 34,
                TextAnchor.MiddleCenter, StationKit.Muted);
        }
    }
}
