#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
#if VRC_SDK_VRCSDK3 && !UDON
using VRC.SDK3.Avatars.Components;
#endif

namespace Thry.AvatarHelpers
{
    public class TextureVRAM : EditorWindow
    {
        [MenuItem("Thry/Avatar/VRAM")]
        public static void Init()
        {
            TextureVRAM window = (TextureVRAM)EditorWindow.GetWindow(typeof(TextureVRAM));
            window.titleContent = new GUIContent("VRAM Calculator");
            window.Show();
        }

        public static void Init(GameObject avatar)
        {
            TextureVRAM window = (TextureVRAM)EditorWindow.GetWindow(typeof(TextureVRAM));
            window.titleContent = new GUIContent("VRAM Calculator");
            window.avatar = avatar;
            window.Calc(avatar);
            window.Show();
        }

        //https://docs.unity3d.com/Manual/class-TextureImporterOverride.html
        static Dictionary<TextureImporterFormat, int> BPP = new Dictionary<TextureImporterFormat, int>()
    {
        { TextureImporterFormat.BC7 , 8 },
        { TextureImporterFormat.DXT5 , 8 },
        { TextureImporterFormat.DXT5Crunched , 8 },
        { TextureImporterFormat.RGBA64 , 64 },
        { TextureImporterFormat.RGBA32 , 32 },
        { TextureImporterFormat.RGBA16 , 16 },
        { TextureImporterFormat.DXT1 , 4 },
        { TextureImporterFormat.DXT1Crunched , 4 },
        { TextureImporterFormat.RGB48 , 64 },
        { TextureImporterFormat.RGB24 , 32 },
        { TextureImporterFormat.RGB16 , 16 },
        { TextureImporterFormat.BC5 , 8 },
        { TextureImporterFormat.RG32 , 32 },
        { TextureImporterFormat.BC4 , 4 },
        { TextureImporterFormat.R8 , 8 },
        { TextureImporterFormat.R16 , 16 },
        { TextureImporterFormat.Alpha8 , 8 },
        { TextureImporterFormat.RGBAHalf , 64 },
        { TextureImporterFormat.BC6H , 8 },
        { TextureImporterFormat.RGB9E5 , 32 },
        { TextureImporterFormat.ETC2_RGBA8Crunched , 8 },
        { TextureImporterFormat.ETC2_RGB4 , 4 },
        { TextureImporterFormat.ETC2_RGBA8 , 8 },
        { TextureImporterFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA , 4 },
        { TextureImporterFormat.PVRTC_RGB2 , 2 },
        { TextureImporterFormat.PVRTC_RGB4 , 4 }
    };

        static Dictionary<RenderTextureFormat, int> RT_BPP = new Dictionary<RenderTextureFormat, int>()
        {
            //
            // Summary:
            //     Color render texture format, 8 bits per channel.
            { RenderTextureFormat.ARGB32 , 32 },
            //
            // Summary:
            //     A depth render texture format.
            { RenderTextureFormat.Depth , 0 },
            //
            // Summary:
            //     Color render texture format, 16 bit floating point per channel.
            { RenderTextureFormat.ARGBHalf , 64 },
            //
            // Summary:
            //     A native shadowmap render texture format.
            { RenderTextureFormat.Shadowmap , 8 }, //guessed bpp
            //
            // Summary:
            //     Color render texture format.
            { RenderTextureFormat.RGB565 , 32 }, //guessed bpp
            //
            // Summary:
            //     Color render texture format, 4 bit per channel.
            { RenderTextureFormat.ARGB4444 , 16 }, 
            //
            // Summary:
            //     Color render texture format, 1 bit for Alpha channel, 5 bits for Red, Green and
            //     Blue channels.
            { RenderTextureFormat.ARGB1555 , 16 },
            //
            // Summary:
            //     Default color render texture format: will be chosen accordingly to Frame Buffer
            //     format and Platform.
            { RenderTextureFormat.Default , 32 }, //fuck
            //
            // Summary:
            //     Color render texture format. 10 bits for colors, 2 bits for alpha.
            { RenderTextureFormat.ARGB2101010 , 32 },
            //
            // Summary:
            //     Default HDR color render texture format: will be chosen accordingly to Frame
            //     Buffer format and Platform.
            { RenderTextureFormat.DefaultHDR , 128 }, //fuck
            //
            // Summary:
            //     Four color render texture format, 16 bits per channel, fixed point, unsigned
            //     normalized.
            { RenderTextureFormat.ARGB64 , 64 },
            //
            // Summary:
            //     Color render texture format, 32 bit floating point per channel.
            { RenderTextureFormat.ARGBFloat , 128 },
            //
            // Summary:
            //     Two color (RG) render texture format, 32 bit floating point per channel.
            { RenderTextureFormat.RGFloat , 64 },
            //
            // Summary:
            //     Two color (RG) render texture format, 16 bit floating point per channel.
            { RenderTextureFormat.RGHalf , 32 },
            //
            // Summary:
            //     Scalar (R) render texture format, 32 bit floating point.
            { RenderTextureFormat.RFloat , 32 },
            //
            // Summary:
            //     Scalar (R) render texture format, 16 bit floating point.
            { RenderTextureFormat.RHalf , 16 },
            //
            // Summary:
            //     Single channel (R) render texture format, 8 bit integer.
            { RenderTextureFormat.R8 , 8 },
            //
            // Summary:
            //     Four channel (ARGB) render texture format, 32 bit signed integer per channel.
            { RenderTextureFormat.ARGBInt , 128 },
            //
            // Summary:
            //     Two channel (RG) render texture format, 32 bit signed integer per channel.
            { RenderTextureFormat.RGInt , 64 },
            //
            // Summary:
            //     Scalar (R) render texture format, 32 bit signed integer.
            { RenderTextureFormat.RInt , 32 },
            //
            // Summary:
            //     Color render texture format, 8 bits per channel.
            { RenderTextureFormat.BGRA32 , 32 },
            //
            // Summary:
            //     Color render texture format. R and G channels are 11 bit floating point, B channel is 10 bit floating point.
            { RenderTextureFormat.RGB111110Float , 32 },
            //
            // Summary:
            //     Two color (RG) render texture format, 16 bits per channel, fixed point, unsigned normalized
            { RenderTextureFormat.RG32 , 32 },
            //
            // Summary:
            //     Four channel (RGBA) render texture format, 16 bit unsigned integer per channel.
            { RenderTextureFormat.RGBAUShort , 64 },
            //
            // Summary:
            //     Two channel (RG) render texture format, 8 bits per channel.
            { RenderTextureFormat.RG16 , 16 },
            //
            // Summary:
            //     Color render texture format, 10 bit per channel, extended range.
            { RenderTextureFormat.BGRA10101010_XR , 40 },
            //
            // Summary:
            //     Color render texture format, 10 bit per channel, extended range.
            { RenderTextureFormat.BGR101010_XR , 30 },
            //
            // Summary:
            //     Single channel (R) render texture format, 16 bit integer.
            { RenderTextureFormat.R16 , 16 }
        };

        GameObject avatar;
        long sizeActive;
        long sizeAll;
        bool includeInactive = true;
        List<(Texture, string, long, bool)> texutesList;
        List<(Mesh, string, long, bool)> meshesList;
        Vector2 scrollpos;

        bool texturesFoldout;
        bool meshesFoldout;
        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<size=20>Thry's Avatar VRAM Calculator</size> v0.2", new GUIStyle(EditorStyles.label) { richText = true, alignment= TextAnchor.MiddleCenter });

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("VRAM is not Download size", MessageType.Warning);
            EditorGUILayout.HelpBox("The video memory size can affect your fps greatly. Your graphics card only has a " +
                "certain amount of video memory and if that is used up it has to start moving assets between your system memory and the video memory, which is really slow.", MessageType.Warning);
            EditorGUILayout.HelpBox("Video memeory usage adds up quickly\nExample: 200MB / per avatar * 40 Avatars + 2GB World = 10GB\n=> Uses up all VRAM on an RTX 3080", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.Space();


            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar Gameobject", avatar, typeof(GameObject), true);

            if (EditorGUI.EndChangeCheck() && avatar != null)
            {
                Calc(avatar);
            }

            if (avatar != null)
            {
                if (GUILayout.Button("Recalculate")) Calc(avatar);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
                if (sizeAll > 200000000) EditorGUILayout.HelpBox("Your avatar uses a lot of video memory. Please reduce the texture sizes or change the compression to prevent bottlenecking yourself and others.", MessageType.Error);
                else if (sizeAll > 100000000) EditorGUILayout.HelpBox("Your avatar is still ok. Try not to add too many more big textures.", MessageType.Warning);
                else EditorGUILayout.HelpBox("Your avatar is in a good place regarding video memeory size.", MessageType.None);
                EditorGUILayout.LabelField("Size (all): ", AvatarEvaluator.ToMebiByteString(sizeAll));
                EditorGUILayout.LabelField("Size (only active): ", AvatarEvaluator.ToMebiByteString(sizeActive));

                EditorGUILayout.HelpBox("Inactive Objects are not unloaded. They are moved to system memory first if you run out of VRAM, " +
                "so they are not as bad as active textures, but you should still try to keep their VRAM low.", MessageType.None);

                EditorGUILayout.LabelField("If there were 40 of your avatar you would take up <b>" + AvatarEvaluator.ToMebiByteString(sizeAll * 40) + "</b> of Video Memory.", new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });
                EditorGUILayout.LabelField("If there were 80 of your avatar you would take up <b>" + AvatarEvaluator.ToMebiByteString(sizeAll * 80) + "</b> of Video Memory.", new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });
                //EditorGUILayout.LabelField("Size of 40 avatars:", );

                if (texutesList == null) Calc(avatar);
                if (texutesList != null)
                {
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
                    includeInactive = EditorGUILayout.ToggleLeft("Show assets of disabled Objects", includeInactive);
                    scrollpos = EditorGUILayout.BeginScrollView(scrollpos);

                    EditorGUI.indentLevel += 2;
                    texturesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(texturesFoldout, "Textures");
                    if (texturesFoldout)
                    {
                        foreach ((Texture, string, long, bool) keyValue in texutesList)
                        {
                            if (includeInactive || keyValue.Item4)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.ObjectField(keyValue.Item1, typeof(Texture), false);
                                EditorGUILayout.LabelField(keyValue.Item2);
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                    }
                    
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    meshesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(meshesFoldout, "Meshes");
                    if (meshesFoldout)
                    {
                        foreach ((Mesh, string, long, bool) keyValue in meshesList)
                        {
                            if (includeInactive || keyValue.Item4)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.ObjectField(keyValue.Item1, typeof(Mesh), false);
                                EditorGUILayout.LabelField(keyValue.Item2);
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    EditorGUI.indentLevel -= 2;
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        public static void GUI_Small_VRAM_Evaluation(long size, GameObject avatar)
        {
            if (size > 200000000) EditorGUILayout.HelpBox("Your avatar uses a lot of video memory. Please reduce the texture sizes or change the compression to prevent bottlenecking yourself and others.", MessageType.Error);
            else if (size > 100000000) EditorGUILayout.HelpBox("Your avatar is still ok. Try not to add too many more big textures.", MessageType.Warning);
        }

        

        static Dictionary<Texture, bool> GetTextures(GameObject avatar)
        {
            IEnumerable<Material>[] materials = AvatarEvaluator.GetMaterials(avatar);
            Dictionary<Texture, bool> textures = new Dictionary<Texture, bool>();
            foreach (Material m in materials[1])
            {
                if (m == null) continue;
                int[] textureIds = m.GetTexturePropertyNameIDs();
                bool isActive = materials[0].Contains(m);
                foreach (int id in textureIds)
                {
                    Texture t = m.GetTexture(id);
                    if (t == null) continue;
                    if (textures.ContainsKey(t))
                    {
                        if (textures[t] == false && isActive) textures[t] = true;
                    }
                    else
                    {
                        textures.Add(t, isActive);
                    }
                }
            }
            return textures;
        }

        public static long QuickCalc(GameObject avatar)
        {
            Dictionary<Texture, bool> textures = GetTextures(avatar);
            long size = 0;
            foreach (KeyValuePair<Texture, bool> t in textures)
                size += CalcSize(t.Key, t.Value).Item1;
            IEnumerable<Mesh> allMeshes = avatar.GetComponentsInChildren<Renderer>(true).Select(r => r is SkinnedMeshRenderer ? (r as SkinnedMeshRenderer).sharedMesh : r is MeshRenderer ? r.GetComponent<MeshFilter>().sharedMesh : null);
            foreach (Mesh m in allMeshes)
            {
                if (m == null) continue;
                size += Profiler.GetRuntimeMemorySizeLong(m);
            }
            return size;
        }

        public long Calc(GameObject avatar)
        {
            Dictionary<Texture, bool> textures = GetTextures(avatar);
            texutesList = new List<(Texture, string, long, bool)>();
            sizeAll = 0;
            sizeActive = 0;
            foreach (KeyValuePair<Texture, bool> t in textures)
            {
                (long, string) textureInfo = CalcSize(t.Key, t.Value);
                texutesList.Add( (t.Key, AvatarEvaluator.ToMebiByteString(textureInfo.Item1) + textureInfo.Item2, textureInfo.Item1, t.Value) );

                if (t.Value) sizeActive += textureInfo.Item1;
                sizeAll += textureInfo.Item1;
            }
            texutesList.Sort((t1, t2) => t2.Item3.CompareTo(t1.Item3));

            //Meshes
            Dictionary<Mesh, bool> meshes = new Dictionary<Mesh, bool>(); 
            IEnumerable<Mesh> allMeshes = avatar.GetComponentsInChildren<Renderer>(true).Select(r => r is SkinnedMeshRenderer?(r as SkinnedMeshRenderer).sharedMesh: r is MeshRenderer?r.GetComponent<MeshFilter>().sharedMesh:null);
            IEnumerable<Mesh> activeMeshes = avatar.GetComponentsInChildren<Renderer>().Select(r => r is SkinnedMeshRenderer?(r as SkinnedMeshRenderer).sharedMesh: r is MeshRenderer?r.GetComponent<MeshFilter>().sharedMesh:null);
            foreach(Mesh m in allMeshes)
            {
                if (m == null) continue;
                bool isActive = activeMeshes.Contains(m);
                if (meshes.ContainsKey(m))
                {
                    if (meshes[m] == false && isActive) meshes[m] = true;
                }
                else
                {
                    meshes.Add(m, isActive);
                }
            }
            meshesList = new List<(Mesh, string, long, bool)>();
            foreach(KeyValuePair<Mesh,bool> m in meshes)
            {
                long bytes = Profiler.GetRuntimeMemorySizeLong(m.Key);
                if (m.Value) sizeActive += bytes;
                sizeAll += bytes;

                meshesList.Add((m.Key, AvatarEvaluator.ToMebiByteString(bytes), bytes, m.Value));
            }
            meshesList.Sort((m1, m2) => m2.Item3.CompareTo(m1.Item3));

            return sizeAll;
        }

        static (long,string) CalcSize(Texture t, bool addToList)
        {
            string add = "";
            long bytesCount = 0;

            string path = AssetDatabase.GetAssetPath(t);
            if (t != null && path != null && t is RenderTexture == false && t.dimension == UnityEngine.Rendering.TextureDimension.Tex2D)
            {
                AssetImporter assetImporter = AssetImporter.GetAtPath(path);
                if (assetImporter is TextureImporter)
                {
                    TextureImporter textureImporter = (TextureImporter)assetImporter;
                    TextureImporterFormat textureFormat = textureImporter.GetPlatformTextureSettings("PC").format;
#pragma warning disable CS0618
                    if (textureFormat == TextureImporterFormat.AutomaticCompressed) textureFormat = textureImporter.GetAutomaticFormat("PC");
#pragma warning restore CS0618

                    if (BPP.ContainsKey(textureFormat))
                    {
                        add = "    (" + textureFormat + ")";
                        double mipmaps = 1;
                        for (int i = 0; i < t.mipmapCount; i++) mipmaps += Math.Pow(0.25, i + 1);
                        bytesCount = (long)(BPP[textureFormat] * t.width * t.height * (textureImporter.mipmapEnabled ? mipmaps : 1) / 8);
                        //Debug.Log(bytesCount);
                    }
                    else
                    {
                        Debug.LogWarning("[Thry][VRAM] Does not have BPP for " + textureFormat);
                    }
                }
                else
                {
                    bytesCount = Profiler.GetRuntimeMemorySizeLong(t);
                }
            }
            else if (t is RenderTexture)
            {
                RenderTexture rt = t as RenderTexture;
                double mipmaps = 1;
                for (int i = 0; i < rt.mipmapCount; i++) mipmaps += Math.Pow(0.25, i + 1);
                bytesCount = (long)((RT_BPP[rt.format] + rt.depth) * rt.width * rt.height * (rt.useMipMap ? mipmaps : 1) / 8);
            }
            else
            {
                bytesCount = Profiler.GetRuntimeMemorySizeLong(t);
            }

            return (bytesCount,add);
        }
    }
}
#endif