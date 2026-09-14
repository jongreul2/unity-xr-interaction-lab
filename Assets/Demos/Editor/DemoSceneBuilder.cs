using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jongreul.XrInteraction.Demos.Editor
{
    /// <summary>
    /// 데모 씬을 코드로 다시 만든다. 씬에는 부트스트랩(<see cref="LabDemo"/>) 하나만 둔다.
    /// 배치 실행: -executeMethod Jongreul.XrInteraction.Demos.Editor.DemoSceneBuilder.BuildAll
    /// </summary>
    public static class DemoSceneBuilder
    {
        public const string LabScenePath = "Assets/Demos/InteractionLab.unity";

        [MenuItem("Tools/XR Interaction Lab/Rebuild Demo Scene")]
        public static void BuildAll()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("InteractionLab", typeof(LabDemo));
            EditorSceneManager.SaveScene(scene, LabScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LabScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[DemoSceneBuilder] {LabScenePath}");
        }
    }
}
