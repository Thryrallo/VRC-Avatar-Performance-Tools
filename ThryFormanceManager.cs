using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Thry.AvatarHelpers
{
    public class ThryFormanceManager
    {

        bool experimentalFeaturesFoldout;
        public float thryformance;
        public (Material, float)[] materialPerformance;
        Vector2 materialScroll;
        public AvatarEvaluator ui;

        bool run_thryformance = false;
        bool run_thryformanceRealtime = false;
        bool is_thryformance_init = false;
        bool thryformance_avatar = false;

        public void ThryFormanceGUI(Rect r, GameObject avatar, AvatarEvaluator ui)
        {
            this.ui = ui;
            experimentalFeaturesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(experimentalFeaturesFoldout, "Thryformance", EditorStyles.foldout);
            if (experimentalFeaturesFoldout)
            {
                EditorGUILayout.HelpBox("These lower options are Experimental Features.\n" +
                    "The values they produce are very dependent on your system and Game View Resolution.\n" +
                    "Set your Game View to a high resolution for best results. I recommend 4k.\n" +
                    "Make sure other testing scripts, like Lyuma's Avatar Emulator are disabled, since the overhead taints the results.", MessageType.Warning);

                EditorGUILayout.Space();

                r = GUILayoutUtility.GetRect(new GUIContent(), EditorStyles.boldLabel);
                GUI.Label(r, "Thryformance:", EditorStyles.boldLabel);
                r.x += 100;
                GUI.Label(r, thryformance == 0 ? "Not calculated" : "" + thryformance.ToString("f1"));
                EditorGUILayout.HelpBox("Thryformance is a performance metric that compares frame times of an optimized avatar with yours.\nIt does not take into account VRAM size or animations.\n0 - Worst\n100 - Best", MessageType.None);
                EditorGUI.BeginDisabledGroup(run_thryformance);
                if (GUILayout.Button("Run Tests"))
                {
                    run_thryformance = true;
                    thryformance_avatar = true;
                    EditorApplication.EnterPlaymode();
                }
                if (GUILayout.Button("Do Realtime"))
                {
                    run_thryformance = true;
                    run_thryformanceRealtime = true;
                    thryformance_avatar = true;
                    EditorApplication.EnterPlaymode();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                GUILayout.Label("Material Performance");
                EditorGUILayout.HelpBox("This compares the frame time of each material to that of the Standard Material.\nBigger number is worse.", MessageType.None);
                EditorGUI.BeginDisabledGroup(run_thryformance);
                if (GUILayout.Button("Run Test"))
                {
                    run_thryformance = true;
                    thryformance_avatar = false;
                    EditorApplication.EnterPlaymode();
                }
                EditorGUI.EndDisabledGroup();
                materialScroll = EditorGUILayout.BeginScrollView(materialScroll);
                if (materialPerformance != null)
                {
                    foreach ((Material, float) m in materialPerformance)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(m.Item1, typeof(Material), false);
                        EditorGUILayout.LabelField(m.Item2.ToString("f1"));
                        GUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndScrollView();

                if (run_thryformance)
                {
                    if (EditorApplication.isPlaying && !is_thryformance_init)
                    {
                        GameObject o = new GameObject("Thryformance");
                        Thryformance tf = o.AddComponent<Thryformance>();
                        tf.avatar = avatar;
                        tf.evaluator = this;
                        tf.doRealtime = run_thryformanceRealtime;
                        tf.doAvatar = thryformance_avatar;
                        tf.materials = AvatarEvaluator.GetMaterials(avatar)[1].Select(m => (m, 0f)).ToArray();
                        is_thryformance_init = true;
                    }
                    else if (EditorApplication.isPlayingOrWillChangePlaymode == false)
                    {
                        run_thryformance = false;
                        is_thryformance_init = false;
                        run_thryformanceRealtime = false;
                        thryformance_avatar = false;
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

    }
}