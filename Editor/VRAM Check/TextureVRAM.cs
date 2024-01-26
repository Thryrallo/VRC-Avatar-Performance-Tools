#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Drawing;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

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

        [MenuItem("GameObject/Thry/Avatar/VRAM", true, 0)]
        static bool CanShowFromSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/Thry/Avatar/VRAM", false, 0)]
        public static void ShowFromSelection()
        {
            TextureVRAM window = (TextureVRAM)EditorWindow.GetWindow(typeof(TextureVRAM));
            window.titleContent = new GUIContent("VRAM Calculator");
            window._avatar = Selection.activeGameObject;
            window.Show();
        }

        public static void Init(GameObject avatar)
        {
            TextureVRAM window = (TextureVRAM)EditorWindow.GetWindow(typeof(TextureVRAM));
            window.titleContent = new GUIContent("VRAM Calculator");
            window._avatar = avatar;
            window.Calc(avatar);
            window.Show();
        }

        //https://docs.unity3d.com/Manual/class-TextureImporterOverride.html

        static Dictionary<TextureFormat, float> BPP = new Dictionary<TextureFormat, float>()
        {
        //
        // Summary:
        //     Alpha-only texture format, 8 bit integer.
            { TextureFormat.Alpha8 , 9 },
        //
        // Summary:
        //     A 16 bits/pixel texture format. Texture stores color with an alpha channel.
            { TextureFormat.ARGB4444 , 16 },
        //
        // Summary:
        //     Color texture format, 8-bits per channel.
            { TextureFormat.RGB24 , 24 },
        //
        // Summary:
        //     Color with alpha texture format, 8-bits per channel.
            { TextureFormat.RGBA32 , 32 },
        //
        // Summary:
        //     Color with alpha texture format, 8-bits per channel.
            { TextureFormat.ARGB32 , 32 },
        //
        // Summary:
        //     A 16 bit color texture format.
            { TextureFormat.RGB565 , 16 },
        //
        // Summary:
        //     Single channel (R) texture format, 16 bit integer.
            { TextureFormat.R16 , 16 },
        //
        // Summary:
        //     Compressed color texture format.
            { TextureFormat.DXT1 , 4 },
        //
        // Summary:
        //     Compressed color with alpha channel texture format.
            { TextureFormat.DXT5 , 8 },
        //
        // Summary:
        //     Color and alpha texture format, 4 bit per channel.
            { TextureFormat.RGBA4444 , 16 },
        //
        // Summary:
        //     Color with alpha texture format, 8-bits per channel.
            { TextureFormat.BGRA32 , 32 },
        //
        // Summary:
        //     Scalar (R) texture format, 16 bit floating point.
            { TextureFormat.RHalf , 16 },
        //
        // Summary:
        //     Two color (RG) texture format, 16 bit floating point per channel.
            { TextureFormat.RGHalf , 32 },
        //
        // Summary:
        //     RGB color and alpha texture format, 16 bit floating point per channel.
            { TextureFormat.RGBAHalf , 64 },
        //
        // Summary:
        //     Scalar (R) texture format, 32 bit floating point.
            { TextureFormat.RFloat , 32 },
        //
        // Summary:
        //     Two color (RG) texture format, 32 bit floating point per channel.
            { TextureFormat.RGFloat , 64 },
        //
        // Summary:
        //     RGB color and alpha texture format, 32-bit floats per channel.
            { TextureFormat.RGBAFloat , 128 },
        //
        // Summary:
        //     A format that uses the YUV color space and is often used for video encoding or
        //     playback.
            { TextureFormat.YUY2 , 16 },
        //
        // Summary:
        //     RGB HDR format, with 9 bit mantissa per channel and a 5 bit shared exponent.
            { TextureFormat.RGB9e5Float , 32 },
        //
        // Summary:
        //     HDR compressed color texture format.
            { TextureFormat.BC6H , 8 },
        //
        // Summary:
        //     High quality compressed color texture format.
            { TextureFormat.BC7 , 8 },
        //
        // Summary:
        //     Compressed one channel (R) texture format.
            { TextureFormat.BC4 , 4 },
        //
        // Summary:
        //     Compressed two-channel (RG) texture format.
            { TextureFormat.BC5 , 8 },
        //
        // Summary:
        //     Compressed color texture format with Crunch compression for smaller storage sizes.
            { TextureFormat.DXT1Crunched , 4 },
        //
        // Summary:
        //     Compressed color with alpha channel texture format with Crunch compression for
        //     smaller storage sizes.
            { TextureFormat.DXT5Crunched , 8 },
        //
        // Summary:
        //     PowerVR (iOS) 2 bits/pixel compressed color texture format.
            { TextureFormat.PVRTC_RGB2 , 6 },
        //
        // Summary:
        //     PowerVR (iOS) 2 bits/pixel compressed with alpha channel texture format.
            { TextureFormat.PVRTC_RGBA2 , 8 },
        //
        // Summary:
        //     PowerVR (iOS) 4 bits/pixel compressed color texture format.
            { TextureFormat.PVRTC_RGB4 , 12 },
        //
        // Summary:
        //     PowerVR (iOS) 4 bits/pixel compressed with alpha channel texture format.
            { TextureFormat.PVRTC_RGBA4 , 16 },
        //
        // Summary:
        //     ETC (GLES2.0) 4 bits/pixel compressed RGB texture format.
            { TextureFormat.ETC_RGB4 , 12 },
        //
        // Summary:
        //     ETC2 EAC (GL ES 3.0) 4 bitspixel compressed unsigned single-channel texture format.
            { TextureFormat.EAC_R , 4 },
        //
        // Summary:
        //     ETC2 EAC (GL ES 3.0) 4 bitspixel compressed signed single-channel texture format.
            { TextureFormat.EAC_R_SIGNED , 4 },
        //
        // Summary:
        //     ETC2 EAC (GL ES 3.0) 8 bitspixel compressed unsigned dual-channel (RG) texture
        //     format.
            { TextureFormat.EAC_RG , 8 },
        //
        // Summary:
        //     ETC2 EAC (GL ES 3.0) 8 bitspixel compressed signed dual-channel (RG) texture
        //     format.
            { TextureFormat.EAC_RG_SIGNED , 8 },
        //
        // Summary:
        //     ETC2 (GL ES 3.0) 4 bits/pixel compressed RGB texture format.
            { TextureFormat.ETC2_RGB , 12 },
        //
        // Summary:
        //     ETC2 (GL ES 3.0) 4 bits/pixel RGB+1-bit alpha texture format.
            { TextureFormat.ETC2_RGBA1 , 12 },
        //
        // Summary:
        //     ETC2 (GL ES 3.0) 8 bits/pixel compressed RGBA texture format.
            { TextureFormat.ETC2_RGBA8 , 32 },
        //
        // Summary:
        //     ASTC (4x4 pixel block in 128 bits) compressed RGB texture format.
            { TextureFormat.ASTC_4x4 , 8 },
        //
        // Summary:
        //     ASTC (5x5 pixel block in 128 bits) compressed RGB texture format.
            { TextureFormat.ASTC_5x5 , 5.12f },
        //
        // Summary:
        //     ASTC (6x6 pixel block in 128 bits) compressed RGB texture format.
            { TextureFormat.ASTC_6x6 , 3.55f },
        //
        // Summary:
        //     ASTC (8x8 pixel block in 128 bits) compressed RGB texture format.
            { TextureFormat.ASTC_8x8 , 2 },
        //
        // Summary:
        //     ASTC (10x10 pixel block in 128 bits) compressed RGB texture format.
            { TextureFormat.ASTC_10x10 , 1.28f },
        //
        // Summary:
        //     ASTC (12x12 pixel block in 128 bits) compressed RGB texture format.
            { TextureFormat.ASTC_12x12 , 1 },
        //
        // Summary:
        //     Two color (RG) texture format, 8-bits per channel.
            { TextureFormat.RG16 , 16 },
        //
        // Summary:
        //     Single channel (R) texture format, 8 bit integer.
            { TextureFormat.R8 , 8 },
        //
        // Summary:
        //     Compressed color texture format with Crunch compression for smaller storage sizes.
            { TextureFormat.ETC_RGB4Crunched , 12 },
        //
        // Summary:
        //     Compressed color with alpha channel texture format using Crunch compression for
        //     smaller storage sizes.
            { TextureFormat.ETC2_RGBA8Crunched , 32 },
        //
        // Summary:
        //     ASTC (4x4 pixel block in 128 bits) compressed RGB(A) HDR texture format.
            { TextureFormat.ASTC_HDR_4x4 , 8 },
        //
        // Summary:
        //     ASTC (5x5 pixel block in 128 bits) compressed RGB(A) HDR texture format.
            { TextureFormat.ASTC_HDR_5x5 , 5.12f },
        //
        // Summary:
        //     ASTC (6x6 pixel block in 128 bits) compressed RGB(A) HDR texture format.
            { TextureFormat.ASTC_HDR_6x6 , 3.55f },
        //
        // Summary:
        //     ASTC (8x8 pixel block in 128 bits) compressed RGB(A) texture format.
            { TextureFormat.ASTC_HDR_8x8 , 2 },
        //
        // Summary:
        //     ASTC (10x10 pixel block in 128 bits) compressed RGB(A) HDR texture format.
            { TextureFormat.ASTC_HDR_10x10 , 1.28f },
        //
        // Summary:
        //     ASTC (12x12 pixel block in 128 bits) compressed RGB(A) HDR texture format.
            { TextureFormat.ASTC_HDR_12x12 , 1 },
        //
        // Summary:
        //     Two channel (RG) texture format, 16 bit integer per channel.
            { TextureFormat.RG32 , 32 },
        //
        // Summary:
        //     Three channel (RGB) texture format, 16 bit integer per channel.
            { TextureFormat.RGB48 , 48 },
        //
        // Summary:
        //     Four channel (RGBA) texture format, 16 bit integer per channel.
            { TextureFormat.RGBA64 , 64 }
        };

        static Dictionary<RenderTextureFormat, float> RT_BPP = new Dictionary<RenderTextureFormat, float>()
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

        static Dictionary<VertexAttributeFormat, int> VertexAttributeByteSize = new Dictionary<VertexAttributeFormat, int>()
        {
            { VertexAttributeFormat.UNorm8, 1},
            { VertexAttributeFormat.SNorm8, 1},
            { VertexAttributeFormat.UInt8, 1},
            { VertexAttributeFormat.SInt8, 1},

            { VertexAttributeFormat.UNorm16, 2},
            { VertexAttributeFormat.SNorm16, 2},
            { VertexAttributeFormat.UInt16, 2},
            { VertexAttributeFormat.SInt16, 2},
            { VertexAttributeFormat.Float16, 2},

            { VertexAttributeFormat.Float32, 4},
            { VertexAttributeFormat.UInt32, 4},
            { VertexAttributeFormat.SInt32, 4},
        };

        const long PC_TEXTURE_MEMORY_EXCELLENT_MiB = 40;
        const long PC_TEXTURE_MEMORY_GOOD_MiB = 75;
        const long PC_TEXTURE_MEMORY_MEDIUM_MiB = 110;
        const long PC_TEXTURE_MEMORY_POOR_MiB = 150;

        const long PC_MESH_MEMORY_EXCELLENT_MiB = 20;
        const long PC_MESH_MEMORY_GOOD_MiB = 35;
        const long PC_MESH_MEMORY_MEDIUM_MiB = 55;
        const long PC_MESH_MEMORY_POOR_MiB = 80;

        const long QUEST_TEXTURE_MEMORY_EXCELLENT_MiB = 10;
        const long QUEST_TEXTURE_MEMORY_GOOD_MiB = 18;
        const long QUEST_TEXTURE_MEMORY_MEDIUM_MiB = 25;
        const long QUEST_TEXTURE_MEMORY_POOR_MiB = 40;

        const long QUEST_MESH_MEMORY_EXCELLENT_MiB = 5;
        const long QUEST_MESH_MEMORY_GOOD_MiB = 10;
        const long QUEST_MESH_MEMORY_MEDIUM_MiB = 15;
        const long QUEST_MESH_MEMORY_POOR_MiB = 25;

        struct TextureInfo
        {
            public Texture texture;
            public string print;
            public long size;
            public bool isActive;
            public float BPP;
            public int minBPP;
            public string formatString;
            public TextureFormat format;
            public bool hasAlpha;

            public List<Material> materials;
            public bool materialDropDown;
        }

        struct MeshInfo
        {
            public Mesh mesh;
            public string print;
            public long size;
            public bool isActive;
        }

        GameObject _avatar;
        long _sizeActive;
        long _sizeAll;
        long _sizeAllTextures;
        long _sizeAllMeshes;
        AvatarEvaluator.Quality _pcTextureQuality;
        AvatarEvaluator.Quality _questTextureQuality;
        AvatarEvaluator.Quality _pcMeshQuality;
        AvatarEvaluator.Quality _questMeshQuality;
        bool _includeInactive = true;
        List<TextureInfo> _texturesList;
        List<MeshInfo> _meshesList;
        Vector2 _scrollPosMajor;
        Vector2 _scrollPosMesh;
        Vector2 _scrollposTex;

        GUIStyle styleButtonTextFloatLeft;
        GUIContent matActiveIcon;
        GUIContent matInactiveIcon;
        GUIContent meshInactiveIcon;
        GUIContent meshActiveIcon;
        GUIContent refreshIcon;
        Texture infoIcon;

        bool _texturesFoldout;
        bool _meshesFoldout;

        int[] _textureSizeOptions = new int[] { 0, 128, 256, 512, 1024, 2048, 4096, 8192 };
        TextureImporterFormat[] _compressionFormatOptions = new TextureImporterFormat[]{ TextureImporterFormat.Automatic, TextureImporterFormat.Automatic, TextureImporterFormat.BC7, TextureImporterFormat.DXT1, TextureImporterFormat.DXT5 };


        private void OnEnable() {
            matActiveIcon = EditorGUIUtility.IconContent("d_Material Icon");
            matInactiveIcon = EditorGUIUtility.IconContent("d_Material On Icon");
            infoIcon = EditorGUIUtility.Load("console.infoicon") as Texture;
            meshInactiveIcon = EditorGUIUtility.IconContent("d_Mesh Icon");
            meshActiveIcon = EditorGUIUtility.IconContent("d_MeshCollider Icon");
            refreshIcon = EditorGUIUtility.IconContent("RotateTool On", "Recalculate");
        }

        private void InitilizeStyles()
        {
            styleButtonTextFloatLeft = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
        }

        private void OnGUI()
        {
            if(styleButtonTextFloatLeft == null)
            {
                InitilizeStyles();
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"<size=20><color=magenta>Thry's Avatar VRAM Calculator</color></size> v{AvatarEvaluator.VERSION}", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter });
            if (GUILayout.Button("Click here & follow me on twitter", EditorStyles.centeredGreyMiniLabel))
                Application.OpenURL("https://twitter.com/thryrallo");
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("VRAM is not download size", MessageType.Warning);
            EditorGUILayout.HelpBox("Video memory size can affect your fps greatly. Your graphics card only has a " +
                "certain amount of video memory and if that is used up it has to start moving assets between its memory and your system memory, which is really slow.", MessageType.Warning);
            //EditorGUILayout.HelpBox("Video memory usage adds up quickly\nExample: 150MB / per avatar * 40 Avatars + 2GB World = 8GB\n=> Uses up all VRAM on an RTX 3070", MessageType.Info);
            EditorGUILayout.HelpBox("Video memory usage adds up quickly\n" +
                "Taking into account a world VRAM usage of 2GB - If your model uses 150MB of VRAM and there were 40 of you, all 8 GB of VRAM would be used up on an RTX 3070.",
                MessageType.Info);

            GUILine();

            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                //GUILayout.Label("Avatar", GUILayout.Width(40));
                GUI.enabled = _avatar != null;
                if(GUILayout.Button(refreshIcon, GUILayout.Width(30), GUILayout.Height(30))) {
                    meshSizeCache.Clear();
                    Calc(_avatar);
                }
                GUI.enabled = true;

                _avatar = (GameObject)EditorGUILayout.ObjectField(GUIContent.none, _avatar, typeof(GameObject), true, GUILayout.Height(30));
                if (EditorGUI.EndChangeCheck() && _avatar != null) {
                    Calc(_avatar);
                }

            }

            if (_avatar != null)
            {
                _scrollPosMajor = EditorGUILayout.BeginScrollView(_scrollPosMajor);

                GUILine();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
                // TODO
                // if (_sizeAll > VRAM_EXCESSIVE_THRESHOLD) EditorGUILayout.HelpBox("Your avatar uses a lot of video memory. Please reduce the texture sizes or change the compression to prevent bottlenecking yourself and others.", MessageType.Error);
                // else if (_sizeAll > VRAM_WARNING_THRESHOLD) EditorGUILayout.HelpBox("Your avatar is still ok. Try not to add too many more big textures.", MessageType.Warning);
                // else EditorGUILayout.HelpBox("Your avatar is in a good place regarding video memeory size.", MessageType.None);

                EditorGUILayout.BeginVertical();
                    EditorGUILayout.BeginHorizontal(GUI.skin.box);
                        EditorGUILayout.LabelField("Texture Memory: ", AvatarEvaluator.ToMebiByteString(_sizeAllTextures), GUILayout.Width(250));
                        EditorGUILayout.LabelField("PC", GUILayout.Width(20));
                        AvatarEvaluator.DrawQualityIcon(_pcTextureQuality);
                        EditorGUILayout.LabelField("Quest", GUILayout.Width(40));
                        AvatarEvaluator.DrawQualityIcon(_questTextureQuality);         
                        GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal(GUI.skin.box);
                        EditorGUILayout.LabelField("Mesh Memory: ", AvatarEvaluator.ToMebiByteString(_sizeAllMeshes), GUILayout.Width(250));
                        EditorGUILayout.LabelField("PC", GUILayout.Width(20));
                        AvatarEvaluator.DrawQualityIcon(_pcMeshQuality);
                        EditorGUILayout.LabelField("Quest", GUILayout.Width(40));
                        AvatarEvaluator.DrawQualityIcon(_questMeshQuality);
                        GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                    EditorGUILayout.LabelField("Combined (all): ", AvatarEvaluator.ToMebiByteString(_sizeAll));
                    EditorGUILayout.LabelField("Combined (only active): ", AvatarEvaluator.ToMebiByteString(_sizeActive));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox("Inactive Objects are not unloaded. They are moved to system memory first if you run out of VRAM, " +
                "so they are not as bad as active textures, but you should still try to keep their VRAM low.", MessageType.None);

                EditorGUILayout.LabelField("If there were 40 of your avatar you would take up <b>" + AvatarEvaluator.ToMebiByteString(_sizeAll * 40) + "</b> of Video Memory.", new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });
                EditorGUILayout.LabelField("If there were 80 of your avatar you would take up <b>" + AvatarEvaluator.ToMebiByteString(_sizeAll * 80) + "</b> of Video Memory.", new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });
                //EditorGUILayout.LabelField("Size of 40 avatars:", );

                if (_texturesList == null) Calc(_avatar);
                if (_texturesList != null)
                {
                    GUILine();
                    EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
                    _includeInactive = EditorGUILayout.ToggleLeft("Show assets of disabled Objects", _includeInactive);

                    //EditorGUI.indentLevel += 2;

                    _texturesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_texturesFoldout, $"Textures  ({AvatarEvaluator.ToMebiByteString(_sizeAllTextures)})");
                    if (_texturesFoldout)
                    {
                        EditorGUILayout.HelpBox("The determining factor of texture VRAM size is the resolution of the texture and its compression format.\n" +
                            "Both can be changed in the importer settings of each texture.\n\n" +
                            "Resolution: View your avatar from ~1 meter away. Reduce the texture size until you see a noticeable drop in quality.\n" +
                            "Compression: BC7 or DXT5 for textures with alpha. DXT1 for images without alpha.\n\n" +
                            "BC7 and DXT5 have the same VRAM size. BC7 is higher quality, DXT5 is smaller in download size.", MessageType.Info);
                        EditorGUILayout.Space(5);

                        _scrollposTex = EditorGUILayout.BeginScrollView(_scrollposTex, GUILayout.Height(Math.Min(500, _texturesList.Count * 30)));
                        for (int texIdx = 0; texIdx < _texturesList.Count; texIdx++)
                        {
                            TextureInfo texInfo = _texturesList[texIdx];
                            if (_includeInactive || texInfo.isActive)
                            {
                                // get info messages
                                List<string> infoMessages = new List<string>();
                                if(texInfo.formatString.Length < 1) {
                                    infoMessages.Add("This texture is not compressable");
                                }
                                if(texInfo.materials.Count < 1) {
                                    infoMessages.Add("This texture is used in a material swap");
                                }

                                // idk how to make a fake indent (because im using guilayout.button which ignores indents) so i just used a horizontal scope with a space :)
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.Space(25);
                                EditorGUILayout.BeginVertical("box");
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    using (new EditorGUI.DisabledGroupScope(texInfo.materials.Count < 1))
                                    {
                                        GUIContent content = texInfo.materialDropDown ? matActiveIcon : matInactiveIcon;
                                        if (GUILayout.Button(content, GUILayout.Width(25), GUILayout.Height(20)))
                                        {
                                            texInfo.materialDropDown = !texInfo.materialDropDown;
                                            _texturesList[texIdx] = texInfo;
                                        }
                                    }

                                    // set labelwidth for guilayout.label
                                    EditorGUIUtility.labelWidth = 50;
                                    GUILayout.Label(texInfo.print, GUILayout.Width(70), GUILayout.Height(20));

                                    if (infoMessages.Count > 0) {
                                        string messages = string.Join("\n", infoMessages);
                                        GUILayout.Label(new GUIContent(infoIcon, messages), GUILayout.Width(20), GUILayout.Height(20));
                                    } else {
                                        GUILayout.Space(25);
                                    }

                                    EditorGUILayout.ObjectField(texInfo.texture, typeof(object), false, GUILayout.MinWidth(200), GUILayout.Height(20));
                                    
                                    int resolution = Mathf.Max(texInfo.texture.width, texInfo.texture.height);
                                    _textureSizeOptions[0] = resolution;
                                    int newResolution = EditorGUILayout.IntPopup(resolution, _textureSizeOptions.Select(x => x.ToString()).ToArray(), _textureSizeOptions, GUILayout.Width(55), GUILayout.Height(20));
                                    if(newResolution != resolution)
                                        ChangeImportSize(texInfo, newResolution);

                                    TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texInfo.texture)) as TextureImporter;
                                    bool isTextureWithChangableFormat = texInfo.formatString.Length > 0 && texInfo.texture is Texture2D && importer != null;
                                    // importer == null happens for e.g. DDS textures
                                    if(texInfo.formatString.Length > 0)
                                    {
                                        if(texInfo.format != 0 && isTextureWithChangableFormat)
                                        {
                                            _compressionFormatOptions[0] = ((TextureImporterFormat)texInfo.format);
                                            int newFormat = EditorGUILayout.Popup(0, _compressionFormatOptions.Select(x => x.ToString()).ToArray(), GUILayout.Width(75), GUILayout.Height(20));
                                            if(newFormat != 0)
                                                ChangeCompression(texInfo, _compressionFormatOptions[newFormat]);
                                        } else
                                        {
                                            if(GUILayout.Button(new GUIContent(texInfo.formatString), EditorStyles.label, GUILayout.Width(75), GUILayout.Height(20)))
                                                Application.OpenURL("https://docs.unity.cn/2019.4/Documentation/Manual/class-TextureImporterOverride.html");
                                        }
                                    }else
                                    {
                                        GUILayout.Space(75);
                                    }
                                    
                                    if (isTextureWithChangableFormat && texInfo.BPP > texInfo.minBPP)
                                    {
                                        TextureImporterFormat newImporterFormat = texInfo.hasAlpha || importer.textureType == TextureImporterType.NormalMap ?
                                            TextureImporterFormat.BC7 : TextureImporterFormat.DXT1;
                                        TextureFormat newFormat = newImporterFormat == TextureImporterFormat.BC7 ? TextureFormat.BC7 : TextureFormat.DXT1;
                                        string savedSize = AvatarEvaluator.ToShortMebiByteString(texInfo.size - TextureToBytesUsingBPP(texInfo.texture, BPP[newFormat]));
                                        if (GUILayout.Button($"{newFormat} → -{savedSize}", styleButtonTextFloatLeft, GUILayout.Width(120), GUILayout.Height(20))
                                            && EditorUtility.DisplayDialog("Confirm Compression Format Change!", $"You are about to change the compression format of texture '{texInfo.texture.name}' from {texInfo.format} => {newFormat}\n\n" +
                                            $"If you wish to return this texture's compression to {texInfo.formatString}, you will have to do so manually as this action is not undo-able.\n\nAre you sure?", "Yes", "No"))
                                        {
                                            ChangeCompression(texInfo, newImporterFormat);
                                        }
                                    }
                                    else
                                    {
                                        EditorGUILayout.GetControlRect(GUILayout.Width(120), GUILayout.Height(20));
                                    }

                                    if(resolution > 2048 && importer != null)
                                    {
                                        string savedSize = AvatarEvaluator.ToShortMebiByteString(texInfo.size - TextureToBytesUsingBPP(texInfo.texture, texInfo.BPP, 2048f / resolution));
                                        if (GUILayout.Button($"2k → -{savedSize}", styleButtonTextFloatLeft, GUILayout.Width(120), GUILayout.Height(20)))
                                        {
                                            ChangeImportSize(texInfo, 2048);
                                        }
                                    }
                                    else
                                    {
                                        EditorGUILayout.GetControlRect(GUILayout.Width(120), GUILayout.Height(20));
                                    }

                                    GUILayout.FlexibleSpace();
                                }

                                if (texInfo.materialDropDown)
                                {
                                    GUILayout.Label($"Used in {texInfo.materials.Count()} material(s) on '{_avatar.name}'", EditorStyles.boldLabel);
                                    EditorGUI.indentLevel++;
                                    foreach (Material mat in texInfo.materials){
                                        EditorGUILayout.ObjectField(mat, typeof(Material), false, GUILayout.Width(395), GUILayout.Height(20));
                                    }
                                    EditorGUI.indentLevel--;
                                    GUILayout.Space(5);
                                }

                                EditorGUILayout.EndVertical();
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                        EditorGUILayout.EndScrollView();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();

                    _meshesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_meshesFoldout, $"Meshes  ({AvatarEvaluator.ToMebiByteString(_sizeAllMeshes)})");
                    if (_meshesFoldout)
                    {
                        _scrollPosMesh = EditorGUILayout.BeginScrollView(_scrollPosMesh, GUILayout.Height(Math.Min(500, _meshesList.Count * 30)));
                        for (int mIdx = 0; mIdx < _meshesList.Count; mIdx++) {
                            MeshInfo meshInfo = _meshesList[mIdx];
                            if (_includeInactive || meshInfo.isActive)
                            {
                                EditorGUILayout.BeginHorizontal();
                                GUILayout.Space(25);
                                using (new EditorGUILayout.HorizontalScope("box")) {
                                    EditorGUILayout.LabelField(new GUIContent(meshInfo.print, meshInfo.print), GUILayout.Width(100), GUILayout.Height(20));
                                    EditorGUILayout.ObjectField(meshInfo.mesh, typeof(Mesh), false, GUILayout.Width(400), GUILayout.Height(20));
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                        EditorGUILayout.EndScrollView();
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    //EditorGUI.indentLevel -= 2;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        void ChangeCompression(TextureInfo texInfo, TextureImporterFormat format)
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texInfo.texture)) as TextureImporter;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings()
            {
                name = "PC",
                overridden = (int)format != -1,
                format = format,
                maxTextureSize = importer.maxTextureSize,
                compressionQuality = 100
            });
            importer.SaveAndReimport();
            Calc(_avatar);
        }

        void ChangeImportSize(TextureInfo texInfo, int size)
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texInfo.texture)) as TextureImporter;
            importer.maxTextureSize = size;
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
            settings.maxTextureSize = size;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            Calc(_avatar);
        }

        static void GUILine(int i_height = 1)
        {
            GUILayout.Space(10);
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            if (rect != null){
                rect.width = EditorGUIUtility.currentViewWidth;
                GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            }
            GUILayout.Space(10);
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
                    if (!m.HasProperty(id)) continue;
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
                size += CalculateTextureSize(t.Key, new TextureInfo()).size;
            IEnumerable<Mesh> allMeshes = avatar.GetComponentsInChildren<Renderer>(true).Select(r => r is SkinnedMeshRenderer ? (r as SkinnedMeshRenderer).sharedMesh : r is MeshRenderer ? r.GetComponent<MeshFilter>().sharedMesh : null);
            foreach (Mesh m in allMeshes)
            {
                if (m == null) continue;
                size += CalculateMeshSize(m);
            }
            return size;
        }

        List<Material> GetMaterialsUsingTexture(Texture texture, List<Material> materialsToSearch) {
            List<Material> materials = new List<Material>();

            foreach(Material mat in materialsToSearch) {
                foreach (string propName in mat.GetTexturePropertyNames()) {
                    Texture matTex = mat.GetTexture(propName);
                    if (matTex != null && matTex == texture) {
                        materials.Add(mat);
                        break;
                    }
                }
            }

            return materials;
        }

        public long Calc(GameObject avatar)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Getting VRAM Data", "Getting Materials", 0.5f);
                // get all materials in avatar
                List<Material> tempMaterials = avatar.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials)
                    .Where(mat => mat != null)
                    .Distinct()
                    .ToList();

                EditorUtility.DisplayProgressBar("Getting VRAM Data", "Getting Textures", 0.5f);
                Dictionary<Texture, bool> textures = GetTextures(avatar);
                _texturesList = new List<TextureInfo>();
                _sizeAll = 0;
                _sizeActive = 0;
                _sizeAllTextures = 0;
                _sizeAllMeshes = 0;

                int numTextures = textures.Keys.Count;
                int texIdx = 1;
                foreach (KeyValuePair<Texture, bool> t in textures)
                {
                    EditorUtility.DisplayProgressBar("Getting VRAM Data", $"Calculating texture size for {t.Key.name}", texIdx / (float)numTextures);
                    TextureInfo texInfo = new TextureInfo();
                    texInfo = CalculateTextureSize(t.Key, texInfo);
                    texInfo.texture = t.Key;
                    texInfo.print = AvatarEvaluator.ToMebiByteString(texInfo.size);
                    texInfo.isActive = t.Value;

                    // get materials
                    texInfo.materials = GetMaterialsUsingTexture(texInfo.texture, tempMaterials);
                    texInfo.materialDropDown = false;

                    _texturesList.Add(texInfo);

                    if (t.Value) _sizeActive += texInfo.size;
                    _sizeAllTextures += texInfo.size;

                    texIdx++;
                }
                _texturesList.Sort((t1, t2) => t2.size.CompareTo(t1.size));

                EditorUtility.DisplayProgressBar("Getting VRAM Data", "Getting Meshes", 0.5f);
                //Meshes
                Dictionary<Mesh, bool> meshes = new Dictionary<Mesh, bool>();
                IEnumerable<Mesh> allMeshes = avatar.GetComponentsInChildren<Renderer>(true).Select(r => r is SkinnedMeshRenderer ? (r as SkinnedMeshRenderer).sharedMesh : r is MeshRenderer ? r.GetComponent<MeshFilter>().sharedMesh : null);
                IEnumerable<Mesh> activeMeshes = avatar.GetComponentsInChildren<Renderer>().Select(r => r is SkinnedMeshRenderer ? (r as SkinnedMeshRenderer).sharedMesh : r is MeshRenderer ? r.GetComponent<MeshFilter>().sharedMesh : null);
                foreach (Mesh m in allMeshes)
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

                int numMeshes = meshes.Keys.Count;
                int meshIdx = 1;
                _meshesList = new List<MeshInfo>();
                foreach (KeyValuePair<Mesh, bool> m in meshes)
                {
                    EditorUtility.DisplayProgressBar("Getting VRAM Data", $"Calculating mesh size for '{m.Key.name}'", meshIdx / (float)numMeshes);
                    long bytes = CalculateMeshSize(m.Key);
                    if (m.Value) _sizeActive += bytes;
                    _sizeAllMeshes += bytes;

                    MeshInfo meshInfo = new MeshInfo();
                    meshInfo.mesh = m.Key;
                    meshInfo.size = bytes;
                    meshInfo.print = AvatarEvaluator.ToMebiByteString(bytes);
                    meshInfo.isActive = m.Value;
                    _meshesList.Add(meshInfo);
                    meshIdx++;
                }
                EditorUtility.DisplayProgressBar("Getting VRAM Data", "Finishing Up", 0.5f);
                _meshesList.Sort((m1, m2) => m2.size.CompareTo(m1.size));
                _sizeAll = _sizeAllTextures + _sizeAllMeshes;

                // Assign quality
                _pcTextureQuality = GetTextureQuality(_sizeAllTextures, false);
                _pcMeshQuality = GetMeshQuality(_sizeAllMeshes, false);
                _questTextureQuality = GetTextureQuality(_sizeAllTextures, true);
                _questMeshQuality = GetMeshQuality(_sizeAllMeshes, true);
            } finally {
                EditorUtility.ClearProgressBar();
            }

            return _sizeAll;
        }

        public static AvatarEvaluator.Quality GetTextureQuality(long size, bool quest)
        {
            if (quest)
                return GetQuality(size, QUEST_TEXTURE_MEMORY_EXCELLENT_MiB, QUEST_TEXTURE_MEMORY_GOOD_MiB, QUEST_TEXTURE_MEMORY_MEDIUM_MiB, QUEST_TEXTURE_MEMORY_POOR_MiB);
            else
                return GetQuality(size, PC_TEXTURE_MEMORY_EXCELLENT_MiB, PC_TEXTURE_MEMORY_GOOD_MiB, PC_TEXTURE_MEMORY_MEDIUM_MiB, PC_TEXTURE_MEMORY_POOR_MiB);
        }

        public static AvatarEvaluator.Quality GetMeshQuality(long size, bool quest)
        {
            if (quest)
                return GetQuality(size, QUEST_MESH_MEMORY_EXCELLENT_MiB, QUEST_MESH_MEMORY_GOOD_MiB, QUEST_MESH_MEMORY_MEDIUM_MiB, QUEST_MESH_MEMORY_POOR_MiB);
            else
                return GetQuality(size, PC_MESH_MEMORY_EXCELLENT_MiB, PC_MESH_MEMORY_GOOD_MiB, PC_MESH_MEMORY_MEDIUM_MiB, PC_MESH_MEMORY_POOR_MiB);
        }

        static AvatarEvaluator.Quality GetQuality(long size, long excellent, long good, long medium, long poor)
        {
            if (size < excellent * 1048576)
                return AvatarEvaluator.Quality.Excellent;
            else if (size < good * 1048576)
                return AvatarEvaluator.Quality.Good;
            else if (size < medium * 1048576)
                return AvatarEvaluator.Quality.Medium;
            else if (size < poor * 1048576)
                return AvatarEvaluator.Quality.Poor;
            else
                return AvatarEvaluator.Quality.VeryPoor;
        }

        static Dictionary<Mesh, long> meshSizeCache = new Dictionary<Mesh, long>();

        static long CalculateMeshSize(Mesh mesh)
        {
            if (meshSizeCache.ContainsKey(mesh))
                return meshSizeCache[mesh];
            
            long bytes = 0;

            var vertexAttributes = mesh.GetVertexAttributes();
            long vertexAttributeVRAMSize = 0;
            foreach (var vertexAttribute in vertexAttributes)
            {
                int skinnedMeshPositionNormalTangentMultiplier = 1;
                // skinned meshes have 2x the amount of position, normal and tangent data since they store both the un-skinned and skinned data in VRAM
                if (mesh.HasVertexAttribute(VertexAttribute.BlendIndices) && mesh.HasVertexAttribute(VertexAttribute.BlendWeight) &&
                    (vertexAttribute.attribute == VertexAttribute.Position || vertexAttribute.attribute == VertexAttribute.Normal || vertexAttribute.attribute == VertexAttribute.Tangent))
                    skinnedMeshPositionNormalTangentMultiplier = 2;
                vertexAttributeVRAMSize += VertexAttributeByteSize[vertexAttribute.format] * vertexAttribute.dimension * skinnedMeshPositionNormalTangentMultiplier;
            }
            var deltaPositions = new Vector3[mesh.vertexCount];
            var deltaNormals = new Vector3[mesh.vertexCount];
            var deltaTangents = new Vector3[mesh.vertexCount];
            long blendShapeVRAMSize = 0;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                var blendShapeName = mesh.GetBlendShapeName(i);
                var blendShapeFrameCount = mesh.GetBlendShapeFrameCount(i);
                for (int j = 0; j < blendShapeFrameCount; j++)
                {
                    mesh.GetBlendShapeFrameVertices(i, j, deltaPositions, deltaNormals, deltaTangents);
                    for (int k = 0; k < deltaPositions.Length; k++)
                    {
                        if (deltaPositions[k] != Vector3.zero || deltaNormals[k] != Vector3.zero || deltaTangents[k] != Vector3.zero)
                        {
                            // every affected vertex has 1 uint for the index, 3 floats for the position, 3 floats for the normal and 3 floats for the tangent
                            // this is true even if all normals or tangents in all blend shapes are zero
                            blendShapeVRAMSize += 40;
                        }
                    }
                }
            }
            bytes = vertexAttributeVRAMSize * mesh.vertexCount + blendShapeVRAMSize;
            meshSizeCache[mesh] = bytes;
            return bytes;
        }

        static TextureInfo CalculateTextureSize(Texture t, TextureInfo info)
        {
            if ( t is Texture2D)
            {
                TextureFormat format = ((Texture2D)t).format;
                if(!BPP.TryGetValue(format, out info.BPP))
                    info.BPP = 16;
                info.formatString = format.ToString();
                info.format = format;
                info.size = TextureToBytesUsingBPP(t, info.BPP);

                string path = AssetDatabase.GetAssetPath(t);
                if(t != null && string.IsNullOrWhiteSpace(path) == false)
                {
                    AssetImporter assetImporter = AssetImporter.GetAtPath(path);
                    if (assetImporter is TextureImporter)
                    {
                        TextureImporter textureImporter = (TextureImporter)assetImporter;
                        info.hasAlpha = textureImporter.DoesSourceTextureHaveAlpha();
                        info.minBPP = (info.hasAlpha || textureImporter.textureType == TextureImporterType.NormalMap) ? 8 : 4;
                    }
                }
                
            }else if(t is Texture2DArray)
            {
                Texture2DArray t2dArray = t as Texture2DArray;
                if(!BPP.TryGetValue(t2dArray.format, out info.BPP))
                    info.BPP = 16;
                info.formatString = t2dArray.format.ToString();
                info.format = t2dArray.format;
                info.size = TextureToBytesUsingBPP(t, info.BPP) * t2dArray.depth;
            } else if( t is Cubemap)
            {
                Cubemap cm = t as Cubemap;
                if(!BPP.TryGetValue(cm.format, out info.BPP))
                    info.BPP = 16;
                info.formatString = cm.format.ToString();
                info.format = cm.format;
                info.size = TextureToBytesUsingBPP(t, info.BPP);
                if (cm.dimension == TextureDimension.Tex3D)
                {
                    info.size *= 6;
                }
            }
            else if (t is RenderTexture)
            {
                RenderTexture rt = t as RenderTexture;
                if(!RT_BPP.TryGetValue(rt.format, out info.BPP))
                    info.BPP = 16;
                info.BPP = info.BPP + rt.depth;
                info.formatString = rt.format.ToString();
                info.hasAlpha = rt.format == RenderTextureFormat.ARGB32 || rt.format == RenderTextureFormat.ARGBHalf || rt.format == RenderTextureFormat.ARGBFloat;
                info.size = TextureToBytesUsingBPP(t, info.BPP);
            }
            else
            {
                info.size = Profiler.GetRuntimeMemorySizeLong(t);
            }

            return info;
        }

        static long TextureToBytesUsingBPP(Texture t, float bpp, float resolutionScale = 1)
        {
            int width = (int)(t.width * resolutionScale);
            int height = (int)(t.height * resolutionScale);
            long bytes = 0;
            if (t is Texture2D || t is Texture2DArray || t is Cubemap)
            {
                for (int index = 0; index < t.mipmapCount; ++index)
                    bytes += (long) Mathf.RoundToInt((float) ((width * height) >> 2 * index) * bpp / 8);
            }
            else if (t is RenderTexture)
            {
                RenderTexture rt = t as RenderTexture;
                double mipmaps = 1;
                for (int i = 0; i < rt.mipmapCount; i++) mipmaps += Math.Pow(0.25, i + 1);
                bytes = (long)((RT_BPP[rt.format] + rt.depth) * width * height * (rt.useMipMap ? mipmaps : 1) / 8);
            }
            else
            {
                bytes = Profiler.GetRuntimeMemorySizeLong(t);
            }
            return bytes;
        }
    }
}
#endif
