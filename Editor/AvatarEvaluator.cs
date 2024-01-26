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
        public const string VERSION = "1.3.6";

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
            window._avatar = Selection.activeGameObject;
            window.Show();
        }

        public const string GUID_EXCELLENT_ICON = "644caf5607820c7418cf0d248b12f33b";
        public const string GUID_GOOD_ICON = "4109d4977ddfb6548b458318e220ac70";
        public const string GUID_MEDIUM_ICON = "9296abd40c7c1934cb668aae07b41c69";
        public const string GUID_POOR_ICON = "e561d0406779ab948b7f155498d101ee";
        public const string GUID_VERY_POOR_ICON = "2886eb1248200a94d9eaec82336fbbad";

        public enum Quality { Excellent, Good, Medium, Poor, VeryPoor }

        static Texture2D ICON_EXCELLENT => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(GUID_EXCELLENT_ICON));
        static Texture2D ICON_GOOD => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(GUID_GOOD_ICON));
        static Texture2D ICON_MEDIUM => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(GUID_MEDIUM_ICON));
        static Texture2D ICON_POOR => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(GUID_POOR_ICON));
        static Texture2D ICON_VERY_POOR => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(GUID_VERY_POOR_ICON)); 

        const int GRABPASS_LIMIT_EXCELLENT = 0;
        const int GRABPASS_LIMIT_MEDIUM = 1;

        const int ANYSTATE_LIMIT_EXCELLENT = 50;
        const int ANYSTATE_LIMIT_GOOD = 80;
        const int ANYSTATE_LIMIT_MEDIUM = 100;
        const int ANYSTATE_LIMIT_POOR = 150;

        const int BLENDSHAPE_DATA_LIMIT_EXCELLENT = 8000;
        const int BLENDSHAPE_DATA_LIMIT_GOOD = 16000;
        const int BLENDSHAPE_DATA_LIMIT_MEDIUM = 32000;
        const int BLENDSHAPE_DATA_LIMIT_POOR = 50000;

        const int LAYER_LIMIT_EXCELLENT = 12;
        const int LAYER_LIMIT_GOOD = 20;
        const int LAYER_LIMIT_MEDIUM = 30;
        const int LAYER_LIMIT_POOR = 45;

        GUIContent refreshIcon;

        //ui variables
        GameObject _avatar;
        bool _writeDefaultsFoldout;
        bool _emptyStatesFoldout;
        Vector2 _scrollPosition;

        //eval variables
        long _vramSize = 0;
        Quality _vramQuality = Quality.Excellent;

        int _grabpassCount = 0;
        Quality _grabpassQuality = Quality.Excellent;
        bool _grabpassFoldout = false;

        (SkinnedMeshRenderer renderer, int verticies, int blendshapeCount)[] _skinendMeshesWithBlendshapes;
        long _totalBlendshapeVerticies = 0;
        Quality _blendshapeQuality = Quality.Excellent;
        bool _blendshapeFoldout;

        int _anyStateTransitions = 0;
        Quality _anyStateTransitionsQuality = Quality.Excellent;
        bool _anyStateFoldout = false;

        int _layerCount = 0;
        Quality _layerCountQuality = Quality.Excellent;
        bool _layerCountFoldout = false;

        Shader[] _shadersWithGrabpass;

        //write defaults
        bool _writeDefault;
        string[] _writeDefaultoutliers;

        string[] _emptyStates;

        private void OnEnable() {
            refreshIcon = EditorGUIUtility.IconContent("RotateTool On", "Recalculate");
            if (_avatar != null) Evaluate();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"<size=20><color=magenta>Thry's Avatar Evaluator</color></size> v{VERSION}", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter });
            if (GUILayout.Button("Click here & follow me on twitter", EditorStyles.centeredGreyMiniLabel))
                Application.OpenURL("https://twitter.com/thryrallo");
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                //GUILayout.Label("Avatar", GUILayout.Width(40));
                GUI.enabled = _avatar != null;
                if(GUILayout.Button(refreshIcon, GUILayout.Width(30), GUILayout.Height(30))) {
                    Evaluate();
                }
                GUI.enabled = true;

                _avatar = (GameObject)EditorGUILayout.ObjectField(GUIContent.none, _avatar, typeof(GameObject), true, GUILayout.Height(30));
                if (EditorGUI.EndChangeCheck() && _avatar != null) {
                    Evaluate();
                }

            }

            if (_avatar == null)
            {
#if VRC_SDK_VRCSDK3 && !UDON
                IEnumerable<VRCAvatarDescriptor> avatars = new List<VRCAvatarDescriptor>();
#if UNITY_2020_1_OR_NEWER
                for(int i=0;i<SceneManager.loadedSceneCount;i++)
#else
                for(int i=0;i<EditorSceneManager.sceneCount;i++)
#endif
                    avatars = avatars.Concat( EditorSceneManager.GetSceneAt(i).GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<VRCAvatarDescriptor>()).Where( d => d.gameObject.activeInHierarchy) );
                if(avatars.Count() > 0)
                {
                    _avatar = avatars.First().gameObject;
                    Evaluate();
                }
#endif
            }

            if (_avatar != null)
            {
                if (_shadersWithGrabpass == null) Evaluate();
                if (_skinendMeshesWithBlendshapes == null) Evaluate();
                EditorGUILayout.Space();
                DrawLine(1);
                //VRAM
                if(DrawSection(_vramQuality, "VRAM", ToMebiByteString(_vramSize), false))
                    TextureVRAM.Init(_avatar);

                Rect r;

                //Grabpasses
                _grabpassFoldout = DrawSection(_grabpassQuality, "Grabpasses", _grabpassCount.ToString(), _grabpassFoldout);
                if(_grabpassFoldout)
                {
                    DrawGrabpassFoldout();
                }
                //Blendshapes
                _blendshapeFoldout = DrawSection(_blendshapeQuality, "Blendshapes", _totalBlendshapeVerticies.ToString(), _blendshapeFoldout);
                if(_blendshapeFoldout)
                {
                    DrawBlendshapeFoldout();
                }

                // Any states
                _anyStateFoldout = DrawSection(_anyStateTransitionsQuality, "Any State Transitions", _anyStateTransitions.ToString(), _anyStateFoldout);
                if(_anyStateFoldout)
                {
                    using(new DetailsFoldout("For each any state transition the conditons are checked every frame. " +
                        "This makes them expensive compared to normal transitions and a large number of them can seriously impact the CPU usage. A healty limit is around 50 transitions."))
                        {

                        }
                }

                // Layer count
                _layerCountFoldout = DrawSection(_layerCountQuality, "Layer Count", _layerCount.ToString(), _layerCountFoldout);
                if(_layerCountFoldout)
                {
                    using(new DetailsFoldout("The more layers you have the more expensive the animator is to run. " +
                        "Animators run on the CPU, so in a CPU-limited game like VRC the smaller the layer count, the better."))
                        {

                        }
                }

                EditorGUILayout.Space();
                DrawLine(1);

                //Write defaults
                r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "Write Defaults: ", EditorStyles.boldLabel);
                r.x += 140;
                GUI.Label(r, "" + _writeDefault);
                EditorGUILayout.HelpBox("Unity needs all the states in your animator to have the same write default value: Either all off or all on. "+
                    "If a state is marked with write defaults it means that the values animated by this state will be set to their default values when not in this state. " +
                    "This can be useful to make compact toggles, but is very prohibiting when making more complex systems." +
                    "Click here for more information on animator states.", MessageType.None);
                if (Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    Application.OpenURL("https://docs.unity3d.com/Manual/class-State.html");
                if (_writeDefaultoutliers.Length > 0)
                {
                    EditorGUILayout.HelpBox("Not all of your states have the same write default value.", MessageType.Warning);
                    _writeDefaultsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_writeDefaultsFoldout, "Outliers", EditorStyles.foldout);
                    if (_writeDefaultsFoldout)
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
                    _emptyStatesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_emptyStatesFoldout, "Outliers", EditorStyles.foldout);
                    if (_emptyStatesFoldout)
                    {
                        foreach (string s in _emptyStates)
                            EditorGUILayout.LabelField(s);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        bool DrawSection(Quality quality, string header, string value, bool foldout)
        {
            EditorGUILayout.BeginHorizontal();
                DrawQualityIcon(quality);
                EditorGUILayout.LabelField($"{header}:", EditorStyles.boldLabel, GUILayout.Width(150));
                EditorGUILayout.LabelField(value, GUILayout.Width(200));
                GUILayout.FlexibleSpace();
                if(GUILayout.Button(foldout ? "Hide Details" : "Show Details", GUILayout.Width(150)))
                {
                    foldout = !foldout;
                }
            EditorGUILayout.EndHorizontal();
            return foldout;
        }

        class DetailsFoldout : GUI.Scope
        {
            public DetailsFoldout(string info)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.BeginVertical();
                if (string.IsNullOrWhiteSpace(info) == false)
                    EditorGUILayout.HelpBox(info, MessageType.Info);
                EditorGUILayout.Space();
            }

            protected override void CloseScope()
            {
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        class GUILayoutIndent : GUI.Scope
        {
            public GUILayoutIndent(int indent)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(indent * 15);
                GUILayout.BeginVertical();
            }

            protected override void CloseScope()
            {
                GUILayout.EndHorizontal();
            }
        }

        void DrawGrabpassFoldout()
        {
            using(new DetailsFoldout("Grabpasses are very expensive. They save your whole screen at a certain point in the rendering process to use it as a texture in the shader."))
            {
                if (_grabpassCount > 0)
                {
                    foreach (Shader s in _shadersWithGrabpass)
                        EditorGUILayout.ObjectField(s, typeof(Shader), false);
                }
            }
        }

        void DrawBlendshapeFoldout()
        {
            using(new DetailsFoldout("The performance impact of blendshapes grows linearly with polygon count. The general consensus is that above 32,000 triangles splitting your mesh will improve performance." +
                    " Click here for more information on blendshapes from the VRChat Documentation."))
            {
                if(Event.current.type == EventType.MouseDown && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    Application.OpenURL("https://docs.vrchat.com/docs/avatar-optimizing-tips#-except-when-youre-using-shapekeys");

                    EditorGUILayout.BeginHorizontal(GUI.skin.box);
                            EditorGUILayout.LabelField("Blendshape Triangles: ", _totalBlendshapeVerticies.ToString());    
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal(GUI.skin.box);
                            EditorGUILayout.LabelField("#Meshes: ", _skinendMeshesWithBlendshapes.Length.ToString());    
                    EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                if (_skinendMeshesWithBlendshapes.Count() > 0 && _skinendMeshesWithBlendshapes.First().Item2 > 32000)
                    EditorGUILayout.HelpBox($"Consider splitting \"{_skinendMeshesWithBlendshapes.First().Item1.name}\" into multiple meshes where only one has blendshapes. " +
                        $"This will reduce the amount polygons actively affected by blendshapes.", MessageType.Error);

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Skinned Mesh Renderer");
                EditorGUILayout.LabelField("Affected Triangles");
                EditorGUILayout.EndHorizontal();
                foreach ((SkinnedMeshRenderer, int, int) mesh in _skinendMeshesWithBlendshapes)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(mesh.Item1, typeof(SkinnedMeshRenderer), true);
                    EditorGUILayout.LabelField($"=> {mesh.Item2} triangles.");
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        public static void DrawQualityIcon(Quality type)
        {
            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 16, GUILayout.Width(16), GUILayout.Height(16)), 
                AvatarEvaluator.GetQualityIcon(type));
        }

        public static Texture2D GetQualityIcon(Quality type)
        {
            Texture2D icon = ICON_VERY_POOR;
            switch (type)
            {
                case Quality.VeryPoor:
                    icon = ICON_VERY_POOR;
                    break;
                case Quality.Poor:
                    icon = ICON_POOR;
                    break;
                case Quality.Medium:
                    icon = ICON_MEDIUM;
                    break;
                case Quality.Good:
                    icon = ICON_GOOD;
                    break;
                case Quality.Excellent:
                    icon = ICON_EXCELLENT;
                    break;
            }
            return icon;
        }

        static Quality GetQuality(long value, long excellent, long good, long medium, long poor)
        {
            if (value < excellent)
                return AvatarEvaluator.Quality.Excellent;
            else if (value < good)
                return AvatarEvaluator.Quality.Good;
            else if (value < medium)
                return AvatarEvaluator.Quality.Medium;
            else if (value < poor)
                return AvatarEvaluator.Quality.Poor;
            else
                return AvatarEvaluator.Quality.VeryPoor;
        }
        
        void DrawLine(int i_height)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            rect.height = i_height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

        void Evaluate()
        {
            _vramSize = TextureVRAM.QuickCalc(_avatar);
            _vramQuality = TextureVRAM.GetTextureQuality(_vramSize, false);
            IEnumerable<Material> materials = GetMaterials(_avatar)[1];
            IEnumerable<Shader> shaders = materials.Where(m => m!= null && m.shader != null).Select(m => m.shader).Distinct();
            _shadersWithGrabpass = shaders.Where(s => File.Exists(AssetDatabase.GetAssetPath(s)) &&  Regex.Match(File.ReadAllText(AssetDatabase.GetAssetPath(s)), @"GrabPass\s*{\s*""(\w|_)+""\s+}").Success ).ToArray();
            _grabpassCount = _shadersWithGrabpass.Count();
            _grabpassQuality = _grabpassCount > GRABPASS_LIMIT_MEDIUM ? Quality.VeryPoor : _grabpassCount > GRABPASS_LIMIT_EXCELLENT ? Quality.Medium : Quality.Excellent;
#if VRC_SDK_VRCSDK3 && !UDON
            VRCAvatarDescriptor descriptor = _avatar.GetComponent<VRCAvatarDescriptor>();
            IEnumerable<AnimatorControllerLayer> layers = descriptor.baseAnimationLayers.Union(descriptor.specialAnimationLayers).Select(a => a.animatorController).
                Where(a => a != null).SelectMany(a => (a as AnimatorController).layers).Where(l => l != null);
            IEnumerable<AnimatorStateMachine> statesMachines = layers.Select(l => l.stateMachine).Where(s => s != null);
            _anyStateTransitions = statesMachines.SelectMany(l => l.anyStateTransitions).Count();
            _anyStateTransitionsQuality = GetQuality(_anyStateTransitions, ANYSTATE_LIMIT_EXCELLENT, ANYSTATE_LIMIT_GOOD, ANYSTATE_LIMIT_MEDIUM, ANYSTATE_LIMIT_POOR);
            IEnumerable<(AnimatorState,string)> states = statesMachines.SelectMany(m => m.states.Select(s => (s.state, m.name+"/"+s.state.name)));

            _emptyStates = states.Where(s => s.Item1.motion == null).Select(s => s.Item2).ToArray();

            IEnumerable<(AnimatorState, string)> wdOn = states.Where(s => s.Item1.writeDefaultValues);
            IEnumerable<(AnimatorState, string)> wdOff = states.Where(s => !s.Item1.writeDefaultValues);
            _writeDefault = wdOn.Count() >= wdOff.Count();
            if (_writeDefault) _writeDefaultoutliers = wdOff.Select(s => s.Item2).ToArray();
            else _writeDefaultoutliers = wdOn.Select(s => s.Item2).ToArray();

            _layerCount = layers.Count();
            _layerCountQuality = GetQuality(_layerCount, LAYER_LIMIT_EXCELLENT, LAYER_LIMIT_GOOD, LAYER_LIMIT_MEDIUM, LAYER_LIMIT_POOR);
#endif

            _skinendMeshesWithBlendshapes =  _avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0).Select(r => (r, r.sharedMesh.triangles.Length / 3, r.sharedMesh.blendShapeCount)).OrderByDescending(i => i.Item2).ToArray();
            _totalBlendshapeVerticies = _skinendMeshesWithBlendshapes.Sum(i => i.verticies);
            _blendshapeQuality = GetQuality(_totalBlendshapeVerticies, BLENDSHAPE_DATA_LIMIT_EXCELLENT, BLENDSHAPE_DATA_LIMIT_GOOD, BLENDSHAPE_DATA_LIMIT_MEDIUM, BLENDSHAPE_DATA_LIMIT_POOR);
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

        public static string ToShortMebiByteString(long l)
        {
            if (l < Math.Pow(2, 10)) return l + " B";
            if (l < Math.Pow(2, 20)) return (l / Math.Pow(2, 10)).ToString("n0") + " KiB";
            if (l < Math.Pow(2, 30)) return (l / Math.Pow(2, 20)).ToString("n1") + " MiB";
            else return (l / Math.Pow(2, 30)).ToString("n1") + " GiB";
        }
    }
}
#endif
