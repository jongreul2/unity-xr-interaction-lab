using UnityEngine;
using UnityEngine.UI;

namespace Jongreul.XrInteraction.Stations
{
    /// <summary>스테이션을 코드로 조립하는 도우미(기본 도형, 월드 공간 라벨). 외부 에셋 없이 동작한다.</summary>
    public static class StationKit
    {
        public static readonly Color Ink = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color Muted = new Color(0.6f, 0.64f, 0.7f);
        public static readonly Color Good = new Color(0.25f, 0.78f, 0.5f);
        public static readonly Color Bad = new Color(0.93f, 0.38f, 0.38f);
        public static readonly Color Warn = new Color(0.97f, 0.74f, 0.26f);
        public static readonly Color Accent = new Color(0.38f, 0.55f, 0.98f);
        public static readonly Color Surface = new Color(0.23f, 0.25f, 0.3f);

        static Font _font;

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>기본 도형 하나. collider=false면 충돌체를 떼어 손 그랩·물리에 걸리지 않게 한다.</summary>
        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Color color, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            SetColor(go, color);
            if (!collider)
                Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
        }

        /// <summary>월드 공간 텍스트. 1 m = 1000 px 기준으로 크기를 잡는다.</summary>
        public static Text Label(Transform parent, string name, Vector3 localPosition, Vector2 sizeMeters, int fontSize,
            TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = sizeMeters * 1000f;
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = localPosition;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color ?? Ink;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
    }
}
