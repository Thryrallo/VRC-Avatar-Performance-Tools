#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;




namespace Thry.AvatarHelpers
{

    public partial class TextureVRAM : EditorWindow
    
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

            // Filled once in Calc() so drawing never has to talk to the AssetDatabase
            public string path;
            public string guid;
            public bool hasImporter;            // Texture2D with a TextureImporter, i.e. editable
            public bool isNormalMap;
            public bool suggestCompress;        // A cheaper format (DXT1 / BC7) exists
            public TextureImporterFormat suggestFormat;
            public long suggestCompressSaving;
            public bool suggest2k;              // Larger than 2048, a 2k button is offered
            public long suggest2kSaving;
        }

        struct MeshInfo
        {
            public Mesh mesh;
            public string print;
            public long size;
            public bool isActive;
        }

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        [SerializeField] GameObject _avatar;   // Serialized so the window survives script recompiles
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

        int _tab;
        static readonly string[] TabLabels = { "Overview", "Textures", "Meshes", "Optimize", "Backups" };
        Vector2 _scrollMain;
        Vector2 _scrollTex;
        Vector2 _scrollMesh;

        string _texSearch = "";
        bool _texOnlySkipped;
        bool _texHelpFoldout;
        bool _aboutFoldout;
        int _avatarCount = 40;
        bool _recalcQueued;
        readonly List<int> _shownTex = new List<int>();
        readonly List<int> _shownMesh = new List<int>();

        GUIStyle styleButtonTextFloatLeft;
        GUIStyle _titleStyle;
        GUIStyle _richStyle;
        GUIStyle _miniRichStyle;
        GUIContent matActiveIcon;      // Shown while the material list is open
        GUIContent matInactiveIcon;    // Shown while it is closed
        GUIContent refreshIcon;
        Texture infoIcon;

        // Row layout (rows are drawn with plain Rects so they adapt to the window width and can be virtualized)
        const float LINE_H = 18f;
        const float PAD = 4f;
        const float MESH_ROW_H = 28f;
        const string MSG_NOT_COMPRESSABLE = "This texture is not compressable";
        const string MSG_MATERIAL_SWAP = "This texture is used in a material swap";
        const string MSG_BOTH = MSG_NOT_COMPRESSABLE + "\n" + MSG_MATERIAL_SWAP;

        static readonly int[] SizeOptions = { 128, 256, 512, 1024, 2048, 4096, 8192 };
        static readonly TextureImporterFormat[] FormatOptions =
            { TextureImporterFormat.Automatic, TextureImporterFormat.BC7, TextureImporterFormat.DXT1, TextureImporterFormat.DXT5 };

        // ------------------------------------------------------------------
        // Window lifecycle
        // ------------------------------------------------------------------

        private void OnEnable()
        {
            minSize = new Vector2(480, 360);
            matActiveIcon = EditorGUIUtility.IconContent("Material On Icon");
            matInactiveIcon = EditorGUIUtility.IconContent("Material Icon");
            infoIcon = EditorGUIUtility.Load("console.infoicon") as Texture;
            refreshIcon = EditorGUIUtility.IconContent("Refresh", "Recalculate");
            if (refreshIcon == null || refreshIcon.image == null) refreshIcon = new GUIContent("↻", "Recalculate");
            LoadBulkPrefs();
            if (_avatar != null && _texturesList == null) RequestRecalc();
        }

        private void OnDisable()
        {
            SaveBulkPrefs();
        }

        private void InitilizeStyles()
        {
            styleButtonTextFloatLeft = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
            _titleStyle = new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter };
            _richStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            _miniRichStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
            InitBulkStyles();
        }

        // Calc() is heavy and shows a progress bar, so never run it in the middle of a layout pass.
        // Ask for it here and it runs right after the current GUI event.
        void RequestRecalc()
        {
            if (_recalcQueued) return;
            _recalcQueued = true;
            EditorApplication.delayCall += () =>
            {
                _recalcQueued = false;
                if (this == null || _avatar == null) return;
                Calc(_avatar);
                Repaint();
            };
        }

        // ------------------------------------------------------------------
        // Main GUI
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (styleButtonTextFloatLeft == null) InitilizeStyles();

            DrawHeader();
            DrawAvatarField();

            if (_avatar == null)
            {
                EditorGUILayout.HelpBox("Drag your avatar (or any object with renderers) into the field above.", MessageType.None);
                return;
            }
            if (_texturesList == null || _meshesList == null)
            {
                RequestRecalc();
                GUILayout.Label("Calculating...", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EditorGUI.BeginChangeCheck();
            _includeInactive = EditorGUILayout.ToggleLeft(
                new GUIContent("Include disabled objects", "Affects the lists below AND the bulk tools on the Optimize tab"), _includeInactive);
            if (EditorGUI.EndChangeCheck()) GUI.FocusControl(null);

            _tab = GUILayout.Toolbar(_tab, TabLabels, GUILayout.Height(24));
            GUILayout.Space(4);

            switch (_tab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawTexturesTab(); break;
                case 2: DrawMeshesTab(); break;
                case 3: DrawOptimizeTab(); break;
                default: DrawBackupsTab(); break;
            }
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"<size=18><color=magenta>Thry's Avatar VRAM Calculator</color></size> v{AvatarEvaluator.VERSION}", _titleStyle, GUILayout.Height(26));
            if (GUILayout.Button("Click here & follow me on twitter", EditorStyles.centeredGreyMiniLabel))
                Application.OpenURL("https://twitter.com/thryrallo");

            _aboutFoldout = EditorGUILayout.Foldout(_aboutFoldout, "What is VRAM and why does it matter?", true);
            if (_aboutFoldout)
            {
                EditorGUILayout.HelpBox("VRAM is not download size.\n\n" +
                    "Video memory size can affect your fps greatly. Your graphics card only has a certain amount of video memory and if that is used up " +
                    "it has to start moving assets between its memory and your system memory, which is really slow.\n\n" +
                    "Video memory usage adds up quickly. Taking into account a world VRAM usage of 2GB - if your model uses 150MB of VRAM and there were 40 of you, " +
                    "all 8 GB of VRAM would be used up on an RTX 3070.", MessageType.Info);
            }
            GUILine();
        }

        void DrawAvatarField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_avatar == null))
                {
                    if (GUILayout.Button(refreshIcon, GUILayout.Width(30), GUILayout.Height(24)))
                    {
                        meshSizeCache.Clear();
                        RequestRecalc();
                    }
                }

                // The change check only wraps the object field, a button click must not count as a change
                EditorGUI.BeginChangeCheck();
                _avatar = (GameObject)EditorGUILayout.ObjectField(GUIContent.none, _avatar, typeof(GameObject), true, GUILayout.Height(24));
                if (EditorGUI.EndChangeCheck())
                {
                    _texturesList = null;
                    _meshesList = null;
                    if (_avatar != null) RequestRecalc();
                }
            }
        }

        static void GUILine(int i_height = 1)
        {
            GUILayout.Space(6);
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.35f));
            GUILayout.Space(6);
        }

        // ------------------------------------------------------------------
        // Overview tab
        // ------------------------------------------------------------------

        void DrawOverviewTab()
        {
            _scrollMain = EditorGUILayout.BeginScrollView(_scrollMain);

            DrawMemoryRow("Texture memory", _sizeAllTextures, _pcTextureQuality, _questTextureQuality);
            DrawMemoryRow("Mesh memory", _sizeAllMeshes, _pcMeshQuality, _questMeshQuality);
            DrawShareBar();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
            {
                EditorGUILayout.LabelField("Combined (all): ", AvatarEvaluator.ToMebiByteString(_sizeAll));
                EditorGUILayout.LabelField("Combined (only active): ", AvatarEvaluator.ToMebiByteString(_sizeActive));
            }
            EditorGUILayout.LabelField("Inactive objects are not unloaded. They are moved to system memory first if you run out of VRAM, " +
                "so they are not as bad as active ones, but you should still keep their VRAM low.", _miniWrapStyle);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("If there were", GUILayout.ExpandWidth(false));
                _avatarCount = Mathf.Max(1, EditorGUILayout.IntField(_avatarCount, GUILayout.Width(45)));
                GUILayout.Label($"of your avatar they would take up <b>{AvatarEvaluator.ToMebiByteString(_sizeAll * _avatarCount)}</b> of video memory.", _richStyle);
            }

            GUILine();
            EditorGUILayout.LabelField("Largest textures", EditorStyles.boldLabel);
            int shown = 0;
            for (int i = 0; i < _texturesList.Count && shown < 5; i++)
            {
                TextureInfo t = _texturesList[i];
                if (!_includeInactive && !t.isActive) continue;
                shown++;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(t.print, GUILayout.Width(80));
                    EditorGUILayout.ObjectField(t.texture, typeof(Texture), false);
                }
            }
            if (shown == 0) EditorGUILayout.LabelField("No textures found.", EditorStyles.miniLabel);
            else if (GUILayout.Button("View all textures", GUILayout.Width(140))) _tab = 1;

            EditorGUILayout.EndScrollView();
        }

        void DrawMemoryRow(string label, long size, AvatarEvaluator.Quality pc, AvatarEvaluator.Quality quest)
        {
            using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label(label, GUILayout.Width(110));
                GUILayout.Label(AvatarEvaluator.ToMebiByteString(size), EditorStyles.boldLabel, GUILayout.MinWidth(70));
                GUILayout.FlexibleSpace();
                GUILayout.Label("PC", GUILayout.Width(22));
                AvatarEvaluator.DrawQualityIcon(pc);
                GUILayout.Label("Quest", GUILayout.Width(40));
                AvatarEvaluator.DrawQualityIcon(quest);
            }
        }

        // Textures vs meshes as one stacked bar
        void DrawShareBar()
        {
            long total = Math.Max(1L, _sizeAllTextures + _sizeAllMeshes);
            Rect r = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
            r.xMin += 2; r.xMax -= 2;
            Color texColor = new Color(0.35f, 0.65f, 0.95f);
            Color meshColor = new Color(0.95f, 0.65f, 0.3f);
            float texWidth = r.width * _sizeAllTextures / total;
            EditorGUI.DrawRect(new Rect(r.x, r.y, texWidth, r.height), texColor);
            EditorGUI.DrawRect(new Rect(r.x + texWidth, r.y, r.width - texWidth, r.height), meshColor);
            int texPct = Mathf.RoundToInt(100f * _sizeAllTextures / total);
            GUILayout.Label($"<color=#59A6F2>■</color> Textures {texPct}%    <color=#F2A64D>■</color> Meshes {100 - texPct}%", _miniRichStyle);
        }

        // ------------------------------------------------------------------
        // Textures tab (virtualized, rows adapt to the window width)
        // ------------------------------------------------------------------

        static float TexRowHeight(TextureInfo t)
        {
            float h = PAD * 2 + LINE_H;
            if (t.suggestCompress || t.suggest2k) h += LINE_H + 2;
            if (t.materialDropDown && t.materials != null) h += 2 + LINE_H * (1 + t.materials.Count);
            return h + 2;
        }

        void DrawTexturesTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _texSearch = EditorGUILayout.TextField(new GUIContent("Search", "Matches texture name or shader name"), _texSearch);
                if (GUILayout.Button("X", GUILayout.Width(22))) { _texSearch = ""; GUI.FocusControl(null); }
                _texOnlySkipped = GUILayout.Toggle(_texOnlySkipped, new GUIContent("Only skipped", "Show only textures the bulk tools ignore"), GUI.skin.button, GUILayout.Width(90));
            }

            // Build the visible set
            _shownTex.Clear();
            string search = (_texSearch ?? "").Trim();
            for (int i = 0; i < _texturesList.Count; i++)
            {
                TextureInfo t = _texturesList[i];
                if (!_includeInactive && !t.isActive) continue;
                if (_texOnlySkipped && !IsExcludedGuid(t.guid)) continue;
                if (search.Length > 0)
                {
                    bool match = t.texture != null && t.texture.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match)
                    {
                        GetShaderInfo(t, out string shaderLabel, out string tip); // Tooltip lists the full shader names
                        match = tip.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    if (!match) continue;
                }
                _shownTex.Add(i);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{_shownTex.Count} shown", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_shownTex.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Skip all shown", "Bulk tools will ignore these textures"), EditorStyles.miniButtonLeft, GUILayout.Width(100)))
                    {
                        SetExcludedMany(ShownTextures(), true);
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("Include all shown", EditorStyles.miniButtonRight, GUILayout.Width(110)))
                    {
                        SetExcludedMany(ShownTextures(), false);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            _texHelpFoldout = EditorGUILayout.Foldout(_texHelpFoldout, "How to reduce texture memory", true);
            if (_texHelpFoldout)
            {
                EditorGUILayout.HelpBox("The determining factor of texture VRAM size is the resolution of the texture and its compression format.\n" +
                    "Both can be changed in the importer settings of each texture (or right here).\n\n" +
                    "Resolution: View your avatar from ~1 meter away. Reduce the texture size until you see a noticeable drop in quality.\n" +
                    "Compression: BC7 or DXT5 for textures with alpha. DXT1 for images without alpha.\n\n" +
                    "BC7 and DXT5 have the same VRAM size. BC7 is higher quality, DXT5 is smaller in download size.", MessageType.Info);
            }

            // Only rows near the viewport are drawn, everything else is one blank space
            _scrollTex = EditorGUILayout.BeginScrollView(_scrollTex);
            float y = 0f, skipped = 0f;
            float top = _scrollTex.y - 80f, bottom = _scrollTex.y + position.height + 80f;
            for (int n = 0; n < _shownTex.Count; n++)
            {
                int idx = _shownTex[n];
                float h = TexRowHeight(_texturesList[idx]);
                if (y + h < top || y > bottom)
                {
                    skipped += h;
                    y += h;
                    continue;
                }
                if (skipped > 0f) { GUILayout.Space(skipped); skipped = 0f; }
                Rect r = GUILayoutUtility.GetRect(0, h, GUILayout.ExpandWidth(true));
                DrawTextureRow(r, idx);
                y += h;
            }
            if (skipped > 0f) GUILayout.Space(skipped);
            EditorGUILayout.EndScrollView();
        }

        List<TextureInfo> ShownTextures()
        {
            List<TextureInfo> list = new List<TextureInfo>(_shownTex.Count);
            foreach (int idx in _shownTex) list.Add(_texturesList[idx]);
            return list;
        }

        void DrawTextureRow(Rect r, int idx)
        {
            TextureInfo t = _texturesList[idx];
            if (t.texture == null) return;
            bool hasAsset = !string.IsNullOrEmpty(t.guid);
            bool excluded = hasAsset && IsExcludedGuid(t.guid);
            GetShaderInfo(t, out string shaderLabel, out string shaderTooltip);

            Rect box = new Rect(r.x + 2, r.y + 1, r.width - 6, r.height - 2);
            GUI.Box(box, GUIContent.none);
            float lx = box.x + PAD;
            float rx = box.xMax - PAD;
            float ly = box.y + PAD;

            // Left: material toggle, VRAM size, info icon
            Rect matR = new Rect(lx, ly, 24, LINE_H);
            using (new EditorGUI.DisabledScope(t.materials == null || t.materials.Count < 1))
            {
                if (GUI.Button(matR, t.materialDropDown ? matActiveIcon : matInactiveIcon))
                {
                    t.materialDropDown = !t.materialDropDown;
                    _texturesList[idx] = t;
                }
            }
            lx = matR.xMax + 4;

            Rect sizeR = new Rect(lx, ly, 64, LINE_H);
            GUI.Label(sizeR, new GUIContent(t.print, shaderTooltip));
            lx = sizeR.xMax;

            bool notCompressable = t.formatString == null || t.formatString.Length < 1;
            bool materialSwap = t.materials == null || t.materials.Count < 1;
            Rect infoR = new Rect(lx + 2, ly, 18, LINE_H);
            if ((notCompressable || materialSwap) && infoIcon != null)
                GUI.Label(infoR, new GUIContent(infoIcon, notCompressable && materialSwap ? MSG_BOTH : notCompressable ? MSG_NOT_COMPRESSABLE : MSG_MATERIAL_SWAP));
            lx = infoR.xMax + 4;

            // Right (from the edge inwards): skip toggle, format, resolution
            Rect skipR = new Rect(rx - 40, ly, 40, LINE_H);
            rx = skipR.x - 4;
            using (new EditorGUI.DisabledScope(!hasAsset))
            {
                bool now = GUI.Toggle(skipR, excluded, new GUIContent("Skip", "Pressed = the bulk tools (resize, cap, auto-fit, compression) won't touch this texture"), EditorStyles.miniButton);
                if (now != excluded) SetExcluded(t.texture, now);
            }

            if (!notCompressable)
            {
                Rect fmtR = new Rect(rx - 76, ly, 76, LINE_H);
                rx = fmtR.x - 4;
                if (t.hasImporter && t.format != 0)
                {
                    if (EditorGUI.DropdownButton(fmtR, new GUIContent(t.formatString, "Change the PC compression format"), FocusType.Passive))
                        ShowFormatMenu(t);
                }
                else if (GUI.Button(fmtR, new GUIContent(t.formatString, "Format can't be changed here. Click to open the Unity docs."), EditorStyles.label))
                {
                    Application.OpenURL("https://docs.unity.cn/2019.4/Documentation/Manual/class-TextureImporterOverride.html");
                }
            }

            int resolution = Mathf.Max(t.texture.width, t.texture.height);
            Rect resR = new Rect(rx - 62, ly, 62, LINE_H);
            rx = resR.x - 4;
            if (t.hasImporter)
            {
                if (EditorGUI.DropdownButton(resR, new GUIContent(resolution.ToString(), "Change the max import size"), FocusType.Passive))
                    ShowResolutionMenu(t, resolution);
            }
            else
            {
                GUI.Label(resR, resolution.ToString(), EditorStyles.centeredGreyMiniLabel);
            }

            // Middle: the texture itself takes whatever space is left
            Rect objR = new Rect(lx, ly, Mathf.Max(60f, rx - lx), LINE_H);
            EditorGUI.ObjectField(objR, GUIContent.none, t.texture, typeof(Texture), false);

            float nextY = ly + LINE_H + 2;

            // Second line: one-click suggestions (only when there is something to suggest)
            if (t.suggestCompress || t.suggest2k)
            {
                float sx = box.x + PAD + 28;
                if (t.suggestCompress)
                {
                    Rect b = new Rect(sx, nextY, 150, LINE_H);
                    string saved = AvatarEvaluator.ToShortMebiByteString(t.suggestCompressSaving);
                    if (GUI.Button(b, new GUIContent($"{t.suggestFormat} → -{saved}", "Switch to the smallest suitable format (DXT1 for opaque, BC7 for alpha / normal maps)"), EditorStyles.miniButton)
                        && EditorUtility.DisplayDialog("Confirm Compression Format Change!",
                            $"You are about to change the compression format of texture '{t.texture.name}' from {t.formatString} => {t.suggestFormat}\n\n" +
                            $"If you wish to return this texture's compression to {t.formatString}, use the Backups tab or change it manually.\n\nAre you sure?", "Yes", "No"))
                    {
                        ChangeCompression(t, t.suggestFormat);
                        GUIUtility.ExitGUI();
                    }
                    sx = b.xMax + 4;
                }
                if (t.suggest2k)
                {
                    Rect b = new Rect(sx, nextY, 120, LINE_H);
                    if (GUI.Button(b, new GUIContent($"2k → -{AvatarEvaluator.ToShortMebiByteString(t.suggest2kSaving)}", "Limit the max import size to 2048"), EditorStyles.miniButton))
                    {
                        ChangeImportSize(t, 2048);
                        GUIUtility.ExitGUI();
                    }
                }
                nextY += LINE_H + 2;
            }

            // Expanded material list
            if (t.materialDropDown && t.materials != null)
            {
                float mx = box.x + PAD + 28;
                GUI.Label(new Rect(mx, nextY, box.xMax - mx - PAD, LINE_H), $"Used in {t.materials.Count} material(s) on '{_avatar.name}'", EditorStyles.boldLabel);
                nextY += LINE_H;
                foreach (Material mat in t.materials)
                {
                    EditorGUI.ObjectField(new Rect(mx, nextY, box.xMax - mx - PAD, LINE_H), GUIContent.none, mat, typeof(Material), false);
                    nextY += LINE_H;
                }
            }
        }

        void ShowResolutionMenu(TextureInfo t, int current)
        {
            GenericMenu menu = new GenericMenu();
            foreach (int s in SizeOptions)
            {
                int size = s;
                menu.AddItem(new GUIContent(size.ToString()), size == current, () => ChangeImportSize(t, size));
            }
            menu.ShowAsContext();
        }

        void ShowFormatMenu(TextureInfo t)
        {
            GenericMenu menu = new GenericMenu();
            foreach (TextureImporterFormat f in FormatOptions)
            {
                TextureImporterFormat format = f;
                menu.AddItem(new GUIContent(format.ToString()), format.ToString() == t.formatString, () => ChangeCompression(t, format));
            }
            menu.ShowAsContext();
        }

        // ------------------------------------------------------------------
        // Meshes tab
        // ------------------------------------------------------------------

        void DrawMeshesTab()
        {
            _shownMesh.Clear();
            for (int i = 0; i < _meshesList.Count; i++)
                if (_includeInactive || _meshesList[i].isActive) _shownMesh.Add(i);

            GUILayout.Label($"{_shownMesh.Count} mesh(es), {AvatarEvaluator.ToMebiByteString(_sizeAllMeshes)} total", EditorStyles.miniLabel);

            _scrollMesh = EditorGUILayout.BeginScrollView(_scrollMesh);
            float skipped = 0f;
            float top = _scrollMesh.y - 80f, bottom = _scrollMesh.y + position.height + 80f;
            for (int n = 0; n < _shownMesh.Count; n++)
            {
                float y = n * MESH_ROW_H;
                if (y + MESH_ROW_H < top || y > bottom)
                {
                    skipped += MESH_ROW_H;
                    continue;
                }
                if (skipped > 0f) { GUILayout.Space(skipped); skipped = 0f; }
                Rect r = GUILayoutUtility.GetRect(0, MESH_ROW_H, GUILayout.ExpandWidth(true));
                MeshInfo m = _meshesList[_shownMesh[n]];
                GUI.Box(new Rect(r.x + 2, r.y + 1, r.width - 6, r.height - 2), GUIContent.none);
                GUI.Label(new Rect(r.x + 8, r.y + 5, 80, LINE_H), new GUIContent(m.print, m.print));
                EditorGUI.ObjectField(new Rect(r.x + 92, r.y + 5, Mathf.Max(60f, r.width - 102), LINE_H), GUIContent.none, m.mesh, typeof(Mesh), false);
            }
            if (skipped > 0f) GUILayout.Space(skipped);
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        // Per texture edits
        // ------------------------------------------------------------------

        void ChangeCompression(TextureInfo texInfo, TextureImporterFormat format)
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texInfo.texture)) as TextureImporter;
            if (importer == null) return;
            SaveSingleBackup(texInfo.texture, true);

            // Start from the current PC settings so nothing else gets reset
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
            settings.name = "PC";
            settings.overridden = format != TextureImporterFormat.Automatic;
            settings.format = format;
            settings.compressionQuality = 100;
            if (!settings.overridden) settings.maxTextureSize = importer.maxTextureSize;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            Calc(_avatar);
        }

        void ChangeImportSize(TextureInfo texInfo, int size)
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texInfo.texture)) as TextureImporter;
            if (importer == null) return;
            SaveSingleBackup(texInfo.texture, false);

            importer.maxTextureSize = size;
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
            settings.maxTextureSize = size;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            Calc(_avatar);
        }

        // ------------------------------------------------------------------
        // Calculation
        // ------------------------------------------------------------------

        static Mesh GetRendererMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer) return ((SkinnedMeshRenderer)r).sharedMesh;
            if (r is MeshRenderer)
            {
                MeshFilter filter = r.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }
            return null;
        }

        static Dictionary<Texture, bool> GetTextures(GameObject avatar)
        {
            IEnumerable<Material>[] materials = AvatarEvaluator.GetMaterials(avatar);
            HashSet<Material> activeMaterials = new HashSet<Material>(materials[0].Where(m => m != null));
            Dictionary<Texture, bool> textures = new Dictionary<Texture, bool>();
            foreach (Material m in materials[1])
            {
                if (m == null) continue;
                int[] textureIds = m.GetTexturePropertyNameIDs();
                bool isActive = activeMaterials.Contains(m);
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
            IEnumerable<Mesh> allMeshes = avatar.GetComponentsInChildren<Renderer>(true).Select(r => GetRendererMesh(r));
            foreach (Mesh m in allMeshes)
            {
                if (m == null) continue;
                size += CalculateMeshSize(m);
            }
            return size;
        }

        // texture -> materials of the avatar's renderers that use it, built in a single pass
        static Dictionary<Texture, List<Material>> BuildTextureMaterialMap(List<Material> materials)
        {
            Dictionary<Texture, List<Material>> map = new Dictionary<Texture, List<Material>>();
            HashSet<Texture> seen = new HashSet<Texture>();
            foreach (Material mat in materials)
            {
                seen.Clear();
                foreach (int id in mat.GetTexturePropertyNameIDs())
                {
                    if (!mat.HasProperty(id)) continue;
                    Texture tex = mat.GetTexture(id);
                    if (tex == null || !seen.Add(tex)) continue;
                    List<Material> list;
                    if (!map.TryGetValue(tex, out list)) map[tex] = list = new List<Material>();
                    list.Add(mat);
                }
            }
            return map;
        }

        // Cache everything the UI needs about asset paths / importers / suggestions, once per Calc()
        void FillAssetInfo(ref TextureInfo t)
        {
            t.path = AssetDatabase.GetAssetPath(t.texture);
            t.guid = string.IsNullOrEmpty(t.path) ? "" : AssetDatabase.AssetPathToGUID(t.path);
            TextureImporter importer = t.texture is Texture2D && !string.IsNullOrEmpty(t.path) ? AssetImporter.GetAtPath(t.path) as TextureImporter : null;
            t.hasImporter = importer != null;
            if (importer == null) return;

            t.isNormalMap = importer.textureType == TextureImporterType.NormalMap;

            if (t.formatString != null && t.formatString.Length > 0 && t.format != 0 && t.BPP > t.minBPP)
            {
                bool useBC7 = t.hasAlpha || t.isNormalMap;
                TextureFormat newFormat = useBC7 ? TextureFormat.BC7 : TextureFormat.DXT1;
                long saving = t.size - TextureToBytesUsingBPP(t.texture, BPP[newFormat]);
                if (saving > 0)
                {
                    t.suggestCompress = true;
                    t.suggestFormat = useBC7 ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT1;
                    t.suggestCompressSaving = saving;
                }
            }

            int resolution = Mathf.Max(t.texture.width, t.texture.height);
            if (resolution > 2048)
            {
                t.suggest2k = true;
                t.suggest2kSaving = t.size - TextureToBytesUsingBPP(t.texture, t.BPP, 2048f / resolution);
            }
        }

        public long Calc(GameObject avatar)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Getting VRAM Data", "Getting Materials", 0.1f);
                // get all materials in avatar
                List<Material> tempMaterials = avatar.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials)
                    .Where(mat => mat != null)
                    .Distinct()
                    .ToList();

                EditorUtility.DisplayProgressBar("Getting VRAM Data", "Getting Textures", 0.2f);
                Dictionary<Texture, bool> textures = GetTextures(avatar);
                Dictionary<Texture, List<Material>> textureMaterials = BuildTextureMaterialMap(tempMaterials);

                OnBulkRecalculated();

                // Build into locals so a failure half way can't leave the window with half a result
                List<TextureInfo> texList = new List<TextureInfo>();
                long sizeActive = 0, sizeAllTextures = 0, sizeAllMeshes = 0;

                int numTextures = textures.Keys.Count;
                int texIdx = 1;
                foreach (KeyValuePair<Texture, bool> t in textures)
                {
                    EditorUtility.DisplayProgressBar("Getting VRAM Data", $"Calculating texture size for {t.Key.name}", 0.2f + 0.5f * texIdx / (float)numTextures);
                    TextureInfo texInfo = new TextureInfo();
                    texInfo = CalculateTextureSize(t.Key, texInfo);
                    texInfo.texture = t.Key;
                    texInfo.print = AvatarEvaluator.ToMebiByteString(texInfo.size);
                    texInfo.isActive = t.Value;

                    List<Material> mats;
                    texInfo.materials = textureMaterials.TryGetValue(t.Key, out mats) ? mats : new List<Material>();
                    texInfo.materialDropDown = false;
                    FillAssetInfo(ref texInfo);

                    texList.Add(texInfo);

                    if (t.Value) sizeActive += texInfo.size;
                    sizeAllTextures += texInfo.size;
                    texIdx++;
                }
                texList.Sort((t1, t2) => t2.size.CompareTo(t1.size));

                EditorUtility.DisplayProgressBar("Getting VRAM Data", "Getting Meshes", 0.75f);
                // Meshes
                Dictionary<Mesh, bool> meshes = new Dictionary<Mesh, bool>();
                HashSet<Mesh> activeMeshes = new HashSet<Mesh>(avatar.GetComponentsInChildren<Renderer>().Select(r => GetRendererMesh(r)).Where(m => m != null));
                foreach (Mesh m in avatar.GetComponentsInChildren<Renderer>(true).Select(r => GetRendererMesh(r)))
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
                List<MeshInfo> meshList = new List<MeshInfo>();
                foreach (KeyValuePair<Mesh, bool> m in meshes)
                {
                    EditorUtility.DisplayProgressBar("Getting VRAM Data", $"Calculating mesh size for '{m.Key.name}'", 0.75f + 0.2f * meshIdx / (float)numMeshes);
                    long bytes = CalculateMeshSize(m.Key);
                    if (m.Value) sizeActive += bytes;
                    sizeAllMeshes += bytes;

                    MeshInfo meshInfo = new MeshInfo();
                    meshInfo.mesh = m.Key;
                    meshInfo.size = bytes;
                    meshInfo.print = AvatarEvaluator.ToMebiByteString(bytes);
                    meshInfo.isActive = m.Value;
                    meshList.Add(meshInfo);
                    meshIdx++;
                }
                meshList.Sort((m1, m2) => m2.size.CompareTo(m1.size));

                _texturesList = texList;
                _meshesList = meshList;
                _sizeActive = sizeActive;
                _sizeAllTextures = sizeAllTextures;
                _sizeAllMeshes = sizeAllMeshes;
                _sizeAll = sizeAllTextures + sizeAllMeshes;

                // Assign quality
                _pcTextureQuality = GetTextureQuality(_sizeAllTextures, false);
                _pcMeshQuality = GetMeshQuality(_sizeAllMeshes, false);
                _questTextureQuality = GetTextureQuality(_sizeAllTextures, true);
                _questMeshQuality = GetMeshQuality(_sizeAllMeshes, true);
            }
            finally
            {
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
                if (cm.dimension == TextureDimension.Cube)
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
