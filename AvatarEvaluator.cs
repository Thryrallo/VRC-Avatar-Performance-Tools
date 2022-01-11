#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
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
            isGUIInit = true;
        }

        long vramSize = 0;
        GameObject avatar;
        int grabpassCount = 0;
        Shader[] shadersWithGrabpass;
        bool grabpassesFoldout;
        private void OnGUI()
        {
            if (!isGUIInit) InitGUI();
            EditorGUI.BeginChangeCheck();
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar Gameobject", avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && avatar != null)
            {
                Evaluate();
            }

            if (avatar != null)
            {
                if (GUILayout.Button("Recalculate")) Evaluate();
                if (shadersWithGrabpass == null) Evaluate();
                EditorGUILayout.Space();

                Rect r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "VRAM:", EditorStyles.boldLabel);
                r.x += 60;
                GUI.Label(r, ToMebiByteString(vramSize));
                r.x += r.width - 150 - 60;
                r.width = 150;
                if (GUI.Button(r, "More details")) TextureVRAM.Init(avatar);

                TextureVRAM.GUI_Small_VRAM_Evaluation(vramSize, avatar);

                EditorGUILayout.Space();

                r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "Grabpasses: ", EditorStyles.boldLabel);
                r.x += 100;
                GUI.Label(r, "" + grabpassCount);

                EditorGUILayout.HelpBox("Grabpasses are very expensive. They save your whole screen at a certain point in the rendering process to use it as a texture in the shader.", MessageType.None);
                if (grabpassCount > 1)
                    EditorGUILayout.HelpBox("Reduce your Grabpasses. Any more than 1 is excessive.", MessageType.Error);
                else if(grabpassCount > 0)
                    EditorGUILayout.HelpBox("If the effect from using the Grabpass is minimal you should consider removing it.", MessageType.Error);
                if (grabpassCount > 0)
                {
                    grabpassesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(grabpassesFoldout, "Shaders with Grabpasses");
                    if (grabpassesFoldout)
                        foreach (Shader s in shadersWithGrabpass)
                            EditorGUILayout.ObjectField(s, typeof(Shader), false);
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
            }
        }

        void Evaluate()
        {
            vramSize = TextureVRAM.QuickCalc(avatar);
            IEnumerable<Shader> shaders = AvatarEvaluator.GetMaterials(avatar)[1].Where(m => m!= null && m.shader != null).Select(m => m.shader).Distinct();
            shadersWithGrabpass = shaders.Where(s => AssetDatabase.GetAssetPath(s) != null &&  Regex.Match(File.ReadAllText(AssetDatabase.GetAssetPath(s)), @"GrabPass\s*{\s*""(\w|_)+""\s+}").Success ).ToArray();
            grabpassCount = shadersWithGrabpass.Count();
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