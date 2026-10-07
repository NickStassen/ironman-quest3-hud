using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronManHud.EditorTools
{
    /// <summary>Menu: IronManHUD > Create Demo Scene. Builds Assets/Scenes/IronManHUD.unity and adds it to Build Settings.</summary>
    public static class IronManHudSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/IronManHUD.unity";
        private const string CameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string DefaultModelPath = "Assets/Resources/yolo.sentis";

        [MenuItem("IronManHUD/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefabPath);
            if (rigPrefab == null)
            {
                EditorUtility.DisplayDialog("IronManHUD",
                    "Could not find " + CameraRigPrefabPath + ".\nIs the Meta XR Core SDK installed? You can instead add the 'Camera Rig' and 'Passthrough' Building Blocks by hand (see docs/SETUP.md).",
                    "OK");
                return;
            }
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            rig.name = "OVRCameraRig";

            var manager = rig.GetComponent<OVRManager>();
            if (manager == null)
            {
                manager = rig.AddComponent<OVRManager>();
            }
            manager.isInsightPassthroughEnabled = true;
            EditorUtility.SetDirty(manager);

            if (rig.GetComponentInChildren<OVRPassthroughLayer>() == null)
            {
                rig.AddComponent<OVRPassthroughLayer>(); // defaults to Underlay
            }

            foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                EditorUtility.SetDirty(cam);
            }

            var appGo = new GameObject("IronManHUD");
            SceneManager.MoveGameObjectToScene(appGo, scene);
            var app = appGo.AddComponent<HudApp>();
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(DefaultModelPath);
            if (model != null)
            {
                app.Model = model;
            }
            EditorUtility.SetDirty(app);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path != ScenePath)
                {
                    scenes.Add(s);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[IronManHud] Created " + ScenePath + (model != null ? " (model assigned)" : " (no model found at " + DefaultModelPath + "; HUD will run without detection)"));
            EditorUtility.DisplayDialog("IronManHUD",
                "Scene created at " + ScenePath + " and added to Build Settings.\n\nNext: Meta > Tools > Project Setup Tool > Fix All, then File > Build And Run.",
                "OK");
        }
    }
}
