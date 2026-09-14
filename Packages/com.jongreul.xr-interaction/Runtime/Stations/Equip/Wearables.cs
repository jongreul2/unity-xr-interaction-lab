using System.Collections.Generic;
using Jongreul.XrInteraction.Equip;
using UnityEngine;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>데모용 착용물 정의와 기본 도형으로 만든 모양. 실제 게임에서는 프리팹·어드레서블이 들어갈 자리.</summary>
    public static class Wearables
    {
        public const string Head = "head";
        public const string Face = "face";
        public const string Back = "back";

        public static readonly string[] Slots = { Head, Face, Back };

        public static readonly IReadOnlyList<EquipItem> Catalog = new[]
        {
            new EquipItem("cap", Head, "hats"),
            new EquipItem("crown", Head, "hats"),
            new EquipItem("beanie", Head, "hats"),
            new EquipItem("glasses", Face, "face"),
            new EquipItem("visor", Face, "face"),
            new EquipItem("cape", Back, "back"),
            new EquipItem("wings", Back, "back"),
        };

        public static readonly string[] Categories = { "hats", "face", "back" };

        /// <summary>소켓 원점·방향 기준 모양. 소켓 forward가 바깥쪽이다.</summary>
        public static GameObject Create(string itemId, Transform parent, float scale = 1f)
        {
            var root = new GameObject($"Wearable_{itemId}");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            Transform t = root.transform;

            switch (itemId)
            {
                case "cap":
                    StationKit.Primitive(PrimitiveType.Cylinder, t, "Crown", new Vector3(0, 0, 0.03f), new Vector3(0.2f, 0.035f, 0.2f), new Color(0.85f, 0.25f, 0.25f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    StationKit.Primitive(PrimitiveType.Cube, t, "Brim", new Vector3(0, -0.08f, 0.0f), new Vector3(0.16f, 0.1f, 0.01f), new Color(0.7f, 0.2f, 0.2f));
                    break;
                case "crown":
                    StationKit.Primitive(PrimitiveType.Cylinder, t, "Band", new Vector3(0, 0, 0.04f), new Vector3(0.18f, 0.045f, 0.18f), new Color(0.98f, 0.8f, 0.25f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2f / 5f;
                        StationKit.Primitive(PrimitiveType.Sphere, t, $"Jewel{i}", new Vector3(Mathf.Cos(a) * 0.085f, Mathf.Sin(a) * 0.085f, 0.1f), Vector3.one * 0.03f, new Color(0.3f, 0.75f, 0.95f));
                    }
                    break;
                case "beanie":
                    StationKit.Primitive(PrimitiveType.Sphere, t, "Dome", new Vector3(0, 0, 0.02f), new Vector3(0.2f, 0.2f, 0.12f), new Color(0.3f, 0.55f, 0.45f));
                    StationKit.Primitive(PrimitiveType.Sphere, t, "Pom", new Vector3(0, 0, 0.1f), Vector3.one * 0.05f, new Color(0.95f, 0.95f, 0.9f));
                    break;
                case "glasses":
                    StationKit.Primitive(PrimitiveType.Cylinder, t, "LeftLens", new Vector3(-0.045f, 0, 0), new Vector3(0.06f, 0.005f, 0.06f), new Color(0.12f, 0.12f, 0.14f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    StationKit.Primitive(PrimitiveType.Cylinder, t, "RightLens", new Vector3(0.045f, 0, 0), new Vector3(0.06f, 0.005f, 0.06f), new Color(0.12f, 0.12f, 0.14f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    StationKit.Primitive(PrimitiveType.Cube, t, "Bridge", Vector3.zero, new Vector3(0.04f, 0.008f, 0.008f), new Color(0.12f, 0.12f, 0.14f));
                    break;
                case "visor":
                    StationKit.Primitive(PrimitiveType.Cube, t, "Shade", Vector3.zero, new Vector3(0.18f, 0.06f, 0.015f), new Color(0.2f, 0.45f, 0.95f));
                    break;
                case "cape":
                    StationKit.Primitive(PrimitiveType.Cube, t, "Cloth", new Vector3(0, -0.22f, 0.02f), new Vector3(0.36f, 0.5f, 0.015f), new Color(0.55f, 0.2f, 0.6f));
                    break;
                case "wings":
                    StationKit.Primitive(PrimitiveType.Cube, t, "LeftWing", new Vector3(-0.16f, 0.02f, 0.04f), new Vector3(0.28f, 0.14f, 0.012f), new Color(0.95f, 0.95f, 1f)).transform.localRotation = Quaternion.Euler(0, 0, 18);
                    StationKit.Primitive(PrimitiveType.Cube, t, "RightWing", new Vector3(0.16f, 0.02f, 0.04f), new Vector3(0.28f, 0.14f, 0.012f), new Color(0.95f, 0.95f, 1f)).transform.localRotation = Quaternion.Euler(0, 0, -18);
                    break;
                default:
                    StationKit.Primitive(PrimitiveType.Cube, t, "Unknown", Vector3.zero, Vector3.one * 0.08f, Color.magenta);
                    break;
            }

            return root;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
