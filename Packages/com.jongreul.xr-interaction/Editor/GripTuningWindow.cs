using Jongreul.XrInteraction.Grip;
using UnityEditor;
using UnityEngine;

namespace Jongreul.XrInteraction.Editor
{
    /// <summary>
    /// Play 중 손에 쥔 도구의 그립 오프셋을 슬라이더로 맞추면 즉시 반영하고, 버튼 하나로 프로필에 저장한다.
    /// 코드를 모르는 아티스트·기획도 헤드셋을 쓴 채(또는 시뮬레이터로) 쥔 느낌을 보며 조정할 수 있게 하는 도구.
    /// </summary>
    public sealed class GripTuningWindow : EditorWindow
    {
        Grabbable _target;
        Vector3 _position;
        Vector3 _euler;
        bool _loaded;

        [MenuItem("Tools/XR Interaction Lab/Grip Tuning")]
        static void Open() => GetWindow<GripTuningWindow>("Grip Tuning");

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Play 중 손에 쥔 도구의 오프셋(손 기준)을 조절하면 바로 반영됩니다. Save로 프로필에 저장합니다.",
                MessageType.Info);

            _target = (Grabbable)EditorGUILayout.ObjectField("Target", _target, typeof(Grabbable), true);
            if (GUILayout.Button("Use held item"))
                PickHeld();

            if (_target == null)
                return;

            if (!Application.isPlaying || !_target.IsHeld)
            {
                EditorGUILayout.HelpBox("Play 모드에서 손에 쥐고 있을 때만 조절할 수 있습니다.", MessageType.Warning);
                _loaded = false;
                return;
            }

            if (!_loaded)
                LoadFromCurrent();

            EditorGUILayout.LabelField("Hand", _target.Holder.Side.ToString());

            EditorGUI.BeginChangeCheck();
            _position = EditorGUILayout.Vector3Field("Position (m)", _position);
            _euler.x = EditorGUILayout.Slider("Pitch (X°)", _euler.x, -180f, 180f);
            _euler.y = EditorGUILayout.Slider("Yaw (Y°)", _euler.y, -180f, 180f);
            _euler.z = EditorGUILayout.Slider("Roll (Z°)", _euler.z, -180f, 180f);
            if (EditorGUI.EndChangeCheck())
                _target.SetOffsetOverride(CurrentValue());

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload"))
                    LoadFromCurrent();

                if (GUILayout.Button("Clear override"))
                {
                    _target.ClearOffsetOverride();
                    _loaded = false;
                }
            }

            GripOffsetProfile profile = _target.GripProfile;
            using (new EditorGUI.DisabledScope(profile == null))
            {
                if (GUILayout.Button(profile == null ? "Save (프로필 없음)" : $"Save to {profile.name} ({_target.Holder.Side})"))
                    Save(profile);
            }
        }

        void PickHeld()
        {
            foreach (Grabbable grabbable in FindObjectsByType<Grabbable>(FindObjectsSortMode.None))
            {
                if (grabbable.IsHeld)
                {
                    _target = grabbable;
                    _loaded = false;
                    return;
                }
            }
        }

        void LoadFromCurrent()
        {
            GripOffset current = _target.CurrentOffset;
            _position = current.Position.ToUnity();
            _euler = Normalize(current.Rotation.ToUnity().eulerAngles);
            _loaded = true;
        }

        GripOffset CurrentValue() =>
            new GripOffset(_position.ToNumerics(), Quaternion.Euler(_euler).ToNumerics());

        void Save(GripOffsetProfile profile)
        {
            Undo.RecordObject(profile, "Save grip offset");
            profile.Set(_target.Holder.Side, CurrentValue());
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent($"{profile.name} 저장"));
        }

        static Vector3 Normalize(Vector3 euler) =>
            new Vector3(Mathf.DeltaAngle(0f, euler.x), Mathf.DeltaAngle(0f, euler.y), Mathf.DeltaAngle(0f, euler.z));
    }
}
