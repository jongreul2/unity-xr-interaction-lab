using Jongreul.XrInteraction.Stations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jongreul.XrInteraction.Demos.Editor
{
    /// <summary>
    /// 데모 씬을 코드로 다시 만든다. 씬에는 부트스트랩(<see cref="LabDemo"/>) 하나만 두고,
    /// 그립 정렬 도구 프로필 에셋을 연결한다(에셋이어야 Play 중 SAVE가 디스크에 남는다).
    /// 배치 실행: -executeMethod Jongreul.XrInteraction.Demos.Editor.DemoSceneBuilder.BuildAll
    /// </summary>
    public static class DemoSceneBuilder
    {
        public const string LabScenePath = "Assets/Demos/InteractionLab.unity";
        public const string GripProfileFolder = "Assets/Demos/GripProfiles";

        [MenuItem("Tools/XR Interaction Lab/Rebuild Demo Scene")]
        public static void BuildAll()
        {
            // 새 씬(Single)을 열면 참조 없는 에셋이 메모리에서 내려가 방금 만든 프로필 참조가 죽는다 → 씬을 먼저 연다.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GripOffsetProfile[] profiles = EnsureGripProfiles();
            var lab = new GameObject("InteractionLab", typeof(LabDemo)).GetComponent<LabDemo>();
            var serialized = new SerializedObject(lab);
            SerializedProperty list = serialized.FindProperty("gripProfiles");
            list.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, LabScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LabScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[DemoSceneBuilder] {LabScenePath}");
        }

        /// <summary>도구별 프로필 에셋. 없으면 기본값으로 만들고, 있으면 그대로 둔다(저장해 둔 튜닝을 지우지 않는다).</summary>
        static GripOffsetProfile[] EnsureGripProfiles()
        {
            if (!AssetDatabase.IsValidFolder(GripProfileFolder))
                AssetDatabase.CreateFolder("Assets/Demos", "GripProfiles");

            var profiles = new GripOffsetProfile[GripAlignStation.DefaultTools.Length];
            for (int i = 0; i < profiles.Length; i++)
            {
                string path = $"{GripProfileFolder}/{GripAlignStation.DefaultTools[i].Name}.asset";
                profiles[i] = AssetDatabase.LoadAssetAtPath<GripOffsetProfile>(path);
                if (profiles[i] != null)
                    continue;

                profiles[i] = GripAlignStation.CreateDefaultProfile(i);
                AssetDatabase.CreateAsset(profiles[i], path);
            }

            AssetDatabase.SaveAssets();
            return profiles;
        }
    }
}
