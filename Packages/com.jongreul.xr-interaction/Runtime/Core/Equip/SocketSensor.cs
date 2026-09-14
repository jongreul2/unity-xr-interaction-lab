using System;
using System.Collections.Generic;
using System.Numerics;

namespace Jongreul.XrInteraction.Equip
{
    /// <summary>신체 소켓 하나(머리·얼굴·등 등). 법선은 소켓 바깥쪽 방향.</summary>
    public readonly struct SocketCandidate
    {
        public readonly string Slot;
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly float Radius;
        public readonly float MaxAngleDegrees;

        public SocketCandidate(string slot, Vector3 position, Vector3 normal, float radius, float maxAngleDegrees)
        {
            Slot = slot;
            Position = position;
            Normal = normal;
            Radius = radius;
            MaxAngleDegrees = maxAngleDegrees;
        }
    }

    /// <summary>
    /// 소켓 근접 판정. 거리만 보면 등 뒤에서 머리 소켓에 붙는 식의 오판이 생긴다.
    /// 그래서 "소켓에서 아이템으로 가는 방향"이 소켓 법선과 이루는 각도도 함께 본다.
    /// </summary>
    public static class SocketSensor
    {
        /// <summary>0(경계)~1(정중앙) 점수. 범위 밖이거나 각도가 벗어나면 -1.</summary>
        public static float Score(in SocketCandidate socket, Vector3 itemPosition)
        {
            Vector3 offset = itemPosition - socket.Position;
            float distance = offset.Length();
            if (distance > socket.Radius)
                return -1f;

            // 소켓 위에 정확히 겹치면 방향을 정의할 수 없으므로 통과로 본다.
            if (distance > 1e-5f && socket.Normal.LengthSquared() > 1e-10f)
            {
                float cos = Vector3.Dot(offset / distance, Vector3.Normalize(socket.Normal));
                float angle = MathF.Acos(Math.Clamp(cos, -1f, 1f)) * (180f / MathF.PI);
                if (angle > socket.MaxAngleDegrees)
                    return -1f;
            }

            return 1f - distance / socket.Radius;
        }

        /// <summary>아이템 슬롯과 같은 소켓 중 점수가 가장 높은 것의 인덱스. 없으면 -1.</summary>
        public static int FindBest(IReadOnlyList<SocketCandidate> sockets, string slot, Vector3 itemPosition)
        {
            int best = -1;
            float bestScore = -1f;
            for (int i = 0; i < sockets.Count; i++)
            {
                if (!string.Equals(sockets[i].Slot, slot, StringComparison.Ordinal))
                    continue;

                float score = Score(sockets[i], itemPosition);
                if (score >= 0f && score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            return best;
        }
    }
}
