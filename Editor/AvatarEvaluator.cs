#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
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
        public const string VERSION = "1.1.0";

        [MenuItem("Thry/Avatar/Evaluator")]
        static void Init()
        {
            AvatarEvaluator window = (AvatarEvaluator)EditorWindow.GetWindow(typeof(AvatarEvaluator));
            window.titleContent = new GUIContent("Avatar Evaluation");
            window.Show();
        }

        [MenuItem("GameObject/Thry/Avatar/Evaluator", true, 0)]
        static bool CanShowFromSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/Thry/Avatar/Evaluator", false, 0)]
        public static void ShowFromSelection()
        {
            AvatarEvaluator window = (AvatarEvaluator)EditorWindow.GetWindow(typeof(AvatarEvaluator));
            window.titleContent = new GUIContent("Avatar Calculator");
            window.avatar = Selection.activeGameObject;
            window.Show();
        }

        bool isGUIInit = false;
        void InitGUI()
        {
            if (avatar != null) Evaluate();
            isGUIInit = true;
        }

        //ui variables
        GameObject avatar;
        bool grabpassesFoldout;
        bool blendshapeMeshesFoldout;
        bool writeDefaultsFoldout;
        bool emptyStatesFoldout;
        Vector2 scrollPosition;

        //eval variables
        long vramSize = 0;
        int grabpassCount = 0;
        int anyStateTransitions = 0;
        Shader[] shadersWithGrabpass;
        (SkinnedMeshRenderer, int)[] skinendMeshesWithBlendshapes;

        //write defaults
        bool _writeDefault;
        string[] _writeDefaultoutliers;

        string[] _emptyStates;

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"<size=20><color=magenta>Thry's Avatar Avatar Evaluator</color></size> v{VERSION}", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter });
            if (GUILayout.Button("Click here & follow me on twitter", EditorStyles.centeredGreyMiniLabel))
                Application.OpenURL("https://twitter.com/thryrallo");
            EditorGUILayout.Space();

            if (!isGUIInit) InitGUI();
            EditorGUI.BeginChangeCheck();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar Gameobject", avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && avatar != null)
            {
                Evaluate();
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
                //Any states
                r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "Any State Transitions: ", EditorStyles.boldLabel);
                r.x += 140;
                GUI.Label(r, "" + anyStateTransitions);
                EditorGUILayout.HelpBox("For each any state transition the conditons are checked every frame. " +
                    "This makes them expensive compared to normal transitions and a large number of them can seriously impact the CPU usage. A healty limit is around 50 transitions.", MessageType.None);
                if (anyStateTransitions > 150)
                    EditorGUILayout.HelpBox("Reduce your any state transitions. At this amount the impact on the CPU is significant.", MessageType.Error);
                else if (anyStateTransitions > 50)
                    EditorGUILayout.HelpBox("Try to replace some of your any state transitions with normal transitions if possible.", MessageType.Warning);

                EditorGUILayout.Space();
                DrawLine(1);

                //Write defaults
                r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "Write Defaults: ", EditorStyles.boldLabel);
                r.x += 140;
                GUI.Label(r, "" + _writeDefault);
                EditorGUILayout.HelpBox("Unity needs all states in your animator to have the same write default value: Either all off or all on. "+
                    "If a state is marked with write default it means that the values animated by this state will be set to their default values when not in this state. " +
                    "This can be useful to make compact toggles, but is very prohhibeting when making more complex systems." +
                    "Click here for more information to animator states.", MessageType.None);
                if (Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    Application.OpenURL("https://docs.unity3d.com/Manual/class-State.html");
                if (_writeDefaultoutliers.Length > 0)
                {
                    EditorGUILayout.HelpBox("Not all of your states have the same write default value.", MessageType.Warning);
                    writeDefaultsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(writeDefaultsFoldout, "Outliers", EditorStyles.foldout);
                    if (writeDefaultsFoldout)
                    {
                        foreach (string s in _writeDefaultoutliers)
                            EditorGUILayout.LabelField(s);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }

                EditorGUILayout.Space();
                DrawLine(1);

                //Empty states
                r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "Empty States: ", EditorStyles.boldLabel);
                r.x += 140;
                GUI.Label(r, "" + _emptyStates.Length);
                if (_emptyStates.Length > 0)
                {
                    EditorGUILayout.HelpBox("Some of your states do not have a motion. This might cause issues. " +
                        "You can place an empty animation clip in them to prevent this.", MessageType.Warning);
                    emptyStatesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(emptyStatesFoldout, "Outliers", EditorStyles.foldout);
                    if (emptyStatesFoldout)
                    {
                        foreach (string s in _emptyStates)
                            EditorGUILayout.LabelField(s);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
            }
            EditorGUILayout.EndScrollView();
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
            IEnumerable<Material> materials = GetMaterials(avatar)[1];
            IEnumerable<Shader> shaders = materials.Where(m => m!= null && m.shader != null).Select(m => m.shader).Distinct();
            shadersWithGrabpass = shaders.Where(s => File.Exists(AssetDatabase.GetAssetPath(s)) &&  Regex.Match(File.ReadAllText(AssetDatabase.GetAssetPath(s)), @"GrabPass\s*{\s*""(\w|_)+""\s+}").Success ).ToArray();
            grabpassCount = shadersWithGrabpass.Count();
#if VRC_SDK_VRCSDK3
            VRCAvatarDescriptor descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            IEnumerable<AnimatorStateMachine> statesMachines = descriptor.baseAnimationLayers.Union(descriptor.specialAnimationLayers).Select(a => a.animatorController).
                Where(a => a != null).SelectMany(a => (a as AnimatorController).layers).Select(l => l.stateMachine).Where(l => l != null);
            anyStateTransitions = statesMachines.SelectMany(l => l.anyStateTransitions).Count();
            IEnumerable<(AnimatorState,string)> states = statesMachines.SelectMany(m => m.states.Select(s => (s.state, m.name+"/"+s.state.name)));

            _emptyStates = states.Where(s => s.Item1.motion == null).Select(s => s.Item2).ToArray();

            IEnumerable<(AnimatorState, string)> wdOn = states.Where(s => s.Item1.writeDefaultValues);
            IEnumerable<(AnimatorState, string)> wdOff = states.Where(s => !s.Item1.writeDefaultValues);
            _writeDefault = wdOn.Count() >= wdOff.Count();
            if (_writeDefault) _writeDefaultoutliers = wdOff.Select(s => s.Item2).ToArray();
            else _writeDefaultoutliers = wdOn.Select(s => s.Item2).ToArray();
#endif

            skinendMeshesWithBlendshapes =  avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0).Select(r => (r, r.sharedMesh.triangles.Length / 3)).OrderByDescending(i => i.Item2).ToArray();
        }

        public static IEnumerable<Material>[] GetMaterials(GameObject avatar)
        {
            IEnumerable<Renderer> allBuiltRenderers = avatar.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.GetComponentsInParent<Transform>(true).All(g => g.tag != "EditorOnly"));

            List<Material> materialsActive = allBuiltRenderers.Where(r => r.gameObject.activeInHierarchy).SelectMany(r => r.sharedMaterials).ToList();
            List<Material> materialsAll = allBuiltRenderers.SelectMany(r => r.sharedMaterials).ToList();
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