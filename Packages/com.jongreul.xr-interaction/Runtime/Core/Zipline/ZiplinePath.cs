using System;
using System.Collections.Generic;
using System.Numerics;

namespace Jongreul.XrInteraction.Zipline
{
    /// <summary>
    /// 짚라인 케이블 경로. 점을 이은 폴리라인을 호 길이(시작점에서 잰 거리)로 매개화한다.
    /// 거리로 위치·접선을 얻고, 임의의 점을 케이블 위 가장 가까운 거리로 투영한다.
    /// </summary>
    public sealed class ZiplinePath
    {
        readonly Vector3[] _points;
        readonly float[] _cumulative;

        public ZiplinePath(IReadOnlyList<Vector3> points)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            if (points.Count < 2)
                throw new ArgumentException("점이 두 개 이상 필요하다.", nameof(points));

            _points = new Vector3[points.Count];
            _cumulative = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                _points[i] = points[i];
                if (i == 0)
                    continue;

                float segment = Vector3.Distance(points[i - 1], points[i]);
                if (segment <= 1e-6f)
                    throw new ArgumentException($"{i - 1}번과 {i}번 점이 겹친다.", nameof(points));
                _cumulative[i] = _cumulative[i - 1] + segment;
            }
        }

        public float Length => _cumulative[_cumulative.Length - 1];
        public int PointCount => _points.Length;
        public Vector3 Start => _points[0];
        public Vector3 End => _points[_points.Length - 1];
        public Vector3 GetPoint(int index) => _points[index];

        /// <summary>
        /// start에서 end까지 늘어진 케이블. 가운데가 sag만큼 처지는 포물선을 segments개 구간으로 샘플한다.
        /// </summary>
        public static ZiplinePath Sagging(Vector3 start, Vector3 end, float sag, int segments = 24)
        {
            if (segments < 1)
                throw new ArgumentOutOfRangeException(nameof(segments));

            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float s = i / (float)segments;
                Vector3 point = Vector3.Lerp(start, end, s);
                point.Y -= sag * 4f * s * (1f - s);
                points[i] = point;
            }

            return new ZiplinePath(points);
        }

        /// <summary>시작점에서 distance만큼 간 위치. 범위 밖이면 양 끝에 붙는다.</summary>
        public Vector3 Evaluate(float distance)
        {
            int i = Segment(distance, out float t);
            return Vector3.Lerp(_points[i], _points[i + 1], t);
        }

        /// <summary>distance 지점의 진행 방향(단위 벡터).</summary>
        public Vector3 TangentAt(float distance)
        {
            int i = Segment(distance, out _);
            return Vector3.Normalize(_points[i + 1] - _points[i]);
        }

        /// <summary>point에서 가장 가까운 케이블 위 지점의 거리.</summary>
        public float Project(Vector3 point)
        {
            float bestDistance = 0f;
            float bestSquared = float.MaxValue;
            for (int i = 0; i < _points.Length - 1; i++)
            {
                Vector3 a = _points[i];
                Vector3 ab = _points[i + 1] - a;
                float t = Math.Clamp(Vector3.Dot(point - a, ab) / ab.LengthSquared(), 0f, 1f);
                float squared = Vector3.DistanceSquared(point, a + ab * t);
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    bestDistance = _cumulative[i] + t * (_cumulative[i + 1] - _cumulative[i]);
                }
            }

            return bestDistance;
        }

        /// <summary>point와 케이블 사이의 최단 거리.</summary>
        public float DistanceTo(Vector3 point) => Vector3.Distance(point, Evaluate(Project(point)));

        /// <summary>0(시작)~1(끝).</summary>
        public float Progress(float distance) => Math.Clamp(distance / Length, 0f, 1f);

        int Segment(float distance, out float t)
        {
            distance = Math.Clamp(distance, 0f, Length);

            // cumulative[lo] <= distance 인 마지막 구간(이진 탐색)
            int lo = 0;
            int hi = _points.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_cumulative[mid] <= distance)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            t = (distance - _cumulative[lo]) / (_cumulative[lo + 1] - _cumulative[lo]);
            return lo;
        }
    }
}
