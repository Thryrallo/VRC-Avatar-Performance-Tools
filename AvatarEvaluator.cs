#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
#if VRC_SDK_VRCSDK3 && !UDON
using VRC.SDK3.Avatars.Components;
#endif

namespace Thry.AvatarHelpers {
    public class AvatarEvaluator : EditorWindow
    {
        [MenuItem("Thry/Avatar/Evaluator")]
        static void Init()
        {
            AvatarEvaluator window = (AvatarEvaluator)EditorWindow.GetWindow(typeof(AvatarEvaluator));
            window.titleContent = new GUIContent("Avatar Evaluation");
            window.Show();
        }

        bool isGUIInit = false;
        void InitGUI()
        {
            if (avatar != null) Evaluate();
            thryformanceManager = new ThryFormanceManager();
            isGUIInit = true;
        }

        //ui variables
        GameObject avatar;
        bool grabpassesFoldout;
        bool blendshapeMeshesFoldout;

        //eval variables
        ThryFormanceManager thryformanceManager;
        long vramSize = 0;
        int grabpassCount = 0;
        Shader[] shadersWithGrabpass;
        (SkinnedMeshRenderer, int)[] skinendMeshesWithBlendshapes;
        private void OnGUI()
        {
            if (!isGUIInit) InitGUI();
            EditorGUI.BeginChangeCheck();
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar Gameobject", avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && avatar != null)
            {
                Evaluate();
                thryformanceManager = new ThryFormanceManager();
            }

            if (avatar == null)
            {
#if VRC_SDK_VRCSDK3 && !UDON
                IEnumerable<VRCAvatarDescriptor> avatars = new List<VRCAvatarDescriptor>();
                for(int i=0;i<EditorSceneManager.loadedSceneCount;i++)
                    avatars = avatars.Concat( EditorSceneManager.GetSceneAt(i).GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<VRCAvatarDescriptor>()).Where( d => d.gameObject.activeInHierarchy) );
                if(avatars.Count() > 0)
                {
                    avatar = avatars.First().gameObject;
                    Evaluate();
                    thryformanceManager = new ThryFormanceManager();
                }
#endif
            }

            if (avatar != null)
            {
                if (GUILayout.Button("Recalculate")) Evaluate();
                if (shadersWithGrabpass == null) Evaluate();
                if (skinendMeshesWithBlendshapes == null) Evaluate();
                EditorGUILayout.Space();
                DrawLine(1);
                //VRAM
                Rect r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "VRAM:", EditorStyles.boldLabel);
                r.x += 60;
                GUI.Label(r, ToMebiByteString(vramSize));
                r.x += r.width - 150 - 60;
                r.width = 150;
                if (GUI.Button(r, "More details")) TextureVRAM.Init(avatar);

                TextureVRAM.GUI_Small_VRAM_Evaluation(vramSize, avatar);

                EditorGUILayout.Space();
                DrawLine(1);
                //Grabpasses
                r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "Grabpasses: ", EditorStyles.boldLabel);
                r.x += 100;
                GUI.Label(r, "" + grabpassCount);

                EditorGUILayout.HelpBox("Grabpasses are very expensive. They save your whole screen at a certain point in the rendering process to use it as a texture in the shader.", MessageType.None);
                if (grabpassCount > 1)
                    EditorGUILayout.HelpBox("Reduce your Grabpasses. Any more than 1 is excessive.", MessageType.Error);
                else if(grabpassCount > 0)
                    EditorGUILayout.HelpBox("If the effect from using the Grabpass is minimal you should consider removing it.", MessageType.Warning);
                if (grabpassCount > 0)
                {
                    grabpassesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(grabpassesFoldout, "Shaders with Grabpasses", EditorStyles.foldout);
                    if (grabpassesFoldout)
                        foreach (Shader s in shadersWithGrabpass)
                            EditorGUILayout.ObjectField(s, typeof(Shader), false);
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
                //Blendshapes

                EditorGUILayout.Space();
                DrawLine(1);
                GUILayout.Label("Blendshapes", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("The performance impact of Blendshapes grows linear with polygon count. A general consense is that above 32.000 polygones splitting your mesh will improve performance." +
                    " Click here for more information to blendshapes from the VRChat Documentation.", MessageType.None);
                if(Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    Application.OpenURL("https://docs.vrchat.com/docs/avatar-optimizing-tips#-except-when-youre-using-shapekeys");
                if (skinendMeshesWithBlendshapes.Count() > 0 && skinendMeshesWithBlendshapes.First().Item2 > 32000)
                    EditorGUILayout.HelpBox($"Consider splitting \"{skinendMeshesWithBlendshapes.First().Item1.name}\" into multiple meshes where only one has blendshapes. " +
                        $"This will reduce the amount polygons actively affected by blendshapes.", MessageType.Error);
                blendshapeMeshesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(blendshapeMeshesFoldout, "Skinned Meshes With Blendshapes", EditorStyles.foldout);
                if (blendshapeMeshesFoldout)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Skinned Mesh Renderer");
                    EditorGUILayout.LabelField("Affected Triangles");
                    EditorGUILayout.EndHorizontal();
                    foreach ((SkinnedMeshRenderer, int) mesh in skinendMeshesWithBlendshapes)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(mesh.Item1, typeof(SkinnedMeshRenderer), true);
                        EditorGUILayout.LabelField($"=> {mesh.Item2} triangles.");
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                
                EditorGUILayout.Space();
                DrawLine(1);

                GUILayout.Label("Very Experimental and Speculative Features", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("These are some benchmarking tools i toyed with. They try to benchmark avatars/materials in comparision to a reference." +
                    "I don't recommend you go by these metric for anything, but it might be interesting for some of you to play with, which is why i leave them in.", MessageType.None);
                if (thryformanceManager == null) thryformanceManager = new ThryFormanceManager();
                thryformanceManager.ThryFormanceGUI(r, avatar, this);
            }
        }
        
        void DrawLine(int i_height)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            rect.height = i_height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

        void Evaluate()
        {
            vramSize = TextureVRAM.QuickCalc(avatar);
            IEnumerable<Shader> shaders = AvatarEvaluator.GetMaterials(avatar)[1].Where(m => m!= null && m.shader != null).Select(m => m.shader).Distinct();
            shadersWithGrabpass = shaders.Where(s => File.Exists(AssetDatabase.GetAssetPath(s)) &&  Regex.Match(File.ReadAllText(AssetDatabase.GetAssetPath(s)), @"GrabPass\s*{\s*""(\w|_)+""\s+}").Success ).ToArray();
            grabpassCount = shadersWithGrabpass.Count();

            skinendMeshesWithBlendshapes =  avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh.blendShapeCount > 0).Select(r => (r, r.sharedMesh.triangles.Length / 3)).OrderByDescending(i => i.Item2).ToArray();
        }

        public static IEnumerable<Material>[] GetMaterials(GameObject avatar)
        {
            List<Material> materialsActive = avatar.GetComponentsInChildren<Renderer>(false).SelectMany(r => r.sharedMaterials).ToList();
            List<Material> materialsAll = avatar.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToList();
#if VRC_SDK_VRCSDK3 && !UDON
            //animation materials
            VRCAvatarDescriptor descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null)
            {
                IEnumerable<AnimationClip> clips = descriptor.baseAnimationLayers.Select(l => l.animatorController).Where(a => a != null).SelectMany(a => a.animationClips).Distinct();
                foreach (AnimationClip clip in clips)
                {
                    IEnumerable<Material> clipMaterials = AnimationUtility.GetObjectReferenceCurveBindings(clip).Where(b => b.isPPtrCurve && b.type.IsSubclassOf(typeof(Renderer)) && b.propertyName.StartsWith("m_Materials"))
                        .SelectMany(b => AnimationUtility.GetObjectReferenceCurve(clip, b)).Select(r => r.value as Material);
                    materialsAll.AddRange(clipMaterials);
                }
            }

#endif
            return new IEnumerable<Material>[] { materialsActive.Distinct(), materialsAll.Distinct() };
        }

        public static string ToByteString(long l)
        {
            if (l < 1000) return l + " B";
            if (l < 1000000) return (l / 1000f).ToString("n2") + " KB";
            if (l < 1000000000) return (l / 1000000f).ToString("n2") + " MB";
            else return (l / 1000000000f).ToString("n2") + " GB";
        }

        public static string ToMebiByteString(long l)
        {
            if (l < Math.Pow(2, 10)) return l + " B";
            if (l < Math.Pow(2, 20)) return (l / Math.Pow(2, 10)).ToString("n2") + " KiB";
            if (l < Math.Pow(2, 30)) return (l / Math.Pow(2, 20)).ToString("n2") + " MiB";
            else return (l / Math.Pow(2, 30)).ToString("n2") + " GiB";
        }
    }
}
#endif