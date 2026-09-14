using System;
using Jongreul.XrInteraction.Grip;
using NUnit.Framework;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace Jongreul.XrInteraction.Tests.Grip
{
    public class GripOffsetTests
    {
        static readonly Vector3 Up = new Vector3(0, 1, 0);

        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 1e-4f;

        static bool Near(Quaternion a, Quaternion b) => MathF.Abs(Quaternion.Dot(a, b)) > 0.99999f;

        [Test]
        public void Identity_PutsItemAtHand()
        {
            var hand = new Vector3(1, 2, 3);
            Quaternion rotation = Quaternion.CreateFromAxisAngle(Up, 1f);

            GripOffset.Identity.Apply(hand, rotation, out Vector3 itemPosition, out Quaternion itemRotation);

            Assert.That(Near(itemPosition, hand), Is.True);
            Assert.That(Near(itemRotation, rotation), Is.True);
        }

        [Test]
        public void OffsetPosition_RotatesWithHand()
        {
            var offset = new GripOffset(new Vector3(0, 0, 0.1f), Quaternion.Identity);
            Quaternion yaw90 = Quaternion.CreateFromAxisAngle(Up, MathF.PI / 2);

            offset.Apply(Vector3.Zero, yaw90, out Vector3 itemPosition, out _);

            // 손이 Y축으로 90° 돌면 앞(+Z) 오프셋은 +X로 간다
            Assert.That(Near(itemPosition, new Vector3(0.1f, 0, 0)), Is.True, itemPosition.ToString());
        }

        [Test]
        public void FromPoses_RoundTrips()
        {
            var hand = new Vector3(0.3f, 1.2f, 0.4f);
            Quaternion handRotation = Quaternion.CreateFromYawPitchRoll(0.4f, -0.3f, 0.2f);
            var item = new Vector3(0.35f, 1.1f, 0.55f);
            Quaternion itemRotation = Quaternion.CreateFromYawPitchRoll(1.1f, 0.2f, -0.5f);

            GripOffset offset = GripOffset.FromPoses(hand, handRotation, item, itemRotation);
            offset.Apply(hand, handRotation, out Vector3 position, out Quaternion rotation);

            Assert.That(Near(position, item), Is.True);
            Assert.That(Near(rotation, itemRotation), Is.True);
        }

        [Test]
        public void MirrorTwice_IsOriginal_AndMirrorFlipsX()
        {
            var offset = new GripOffset(new Vector3(0.02f, -0.01f, 0.08f), Quaternion.CreateFromYawPitchRoll(0.5f, 0.1f, 0.3f));

            GripOffset mirrored = offset.MirrorX();

            Assert.That(mirrored.Position.X, Is.EqualTo(-0.02f).Within(1e-6f));
            Assert.That(Near(mirrored.MirrorX().Position, offset.Position), Is.True);
            Assert.That(Near(mirrored.MirrorX().Rotation, offset.Rotation), Is.True);
        }

        [Test]
        public void MirroredOffset_OnMirroredHand_GivesMirroredItem()
        {
            // 오른손 자세와 그 거울상(왼손)에 각각 원본·거울 오프셋을 적용하면 도구 위치도 거울상이어야 한다
            var offset = new GripOffset(new Vector3(0.03f, 0.01f, 0.1f), Quaternion.CreateFromYawPitchRoll(0.3f, 0.2f, 0.1f));
            var rightHand = new Vector3(0.25f, 1.2f, 0.3f);
            Quaternion rightRotation = Quaternion.CreateFromYawPitchRoll(-0.4f, 0.1f, 0.2f);
            var leftHand = new Vector3(-rightHand.X, rightHand.Y, rightHand.Z);
            var leftRotation = new Quaternion(rightRotation.X, -rightRotation.Y, -rightRotation.Z, rightRotation.W);

            offset.Apply(rightHand, rightRotation, out Vector3 rightItem, out _);
            offset.MirrorX().Apply(leftHand, leftRotation, out Vector3 leftItem, out _);

            Assert.That(Near(leftItem, new Vector3(-rightItem.X, rightItem.Y, rightItem.Z)), Is.True,
                $"{leftItem} vs {rightItem}");
        }
    }
}
