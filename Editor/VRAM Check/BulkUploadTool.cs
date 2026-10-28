#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Thry.AvatarHelpers
{
    // bulk compression and backup/restore. Part of TextureVRAM, see TextureVRAM.cs - Change by - Nommiiee
    // UI rework: tabs, shared plan cards, review list for smart plans, backup history, persisted settings
    public partial class TextureVRAM
    {
        // ------------------------------------------------------------------
        // Backup data
        // ------------------------------------------------------------------

        // One texture's saved settings, stored by GUID so moved/renamed textures can still be restored
        [Serializable]
        class BulkBackupEntry
        {
            public string guid;
            public string path;       // Only for display/logging
            public int maxSize;       // importer.maxTextureSize before the change
            public int pcMaxSize;     // PC platform max size before the change

            // Compression settings, only filled in for edits that change the format
            public bool restoreFormat; // False for size-only edits (and for old backups)
            public bool pcOverridden;
            public int pcFormat;       // TextureImporterFormat stored as int
            public int pcQuality;
        }

        // One backup file = one resize or one group of quick single edits
        [Serializable]
        class BulkBackup
        {
            public string label;
            public string timestamp;
            public List<BulkBackupEntry> entries = new List<BulkBackupEntry>();
        }

        // Exclusion list: textures the bulk tools must never touch, stored by GUID 
        [Serializable]
        class ExclusionData
        {
            public List<string> guids = new List<string>();
        }

        HashSet<string> _excludedGuids = new HashSet<string>();
        bool _exclusionsLoaded;     // Loaded lazily, again after every domain reload

        // Every backup on disk, newest first. Cached so OnGUI never touches the disk
        string[] _backupFiles = new string[0];
        string[] _backupLabels = new string[0];
        bool _backupDirty = true;

        const int MAX_BACKUPS = 25;               // Older backups get deleted
        const double MERGE_WINDOW_SECONDS = 10;   // Quick single edits share one backup
        const string SINGLE_EDIT_LABEL = "Single edit";

        // Backups live in Library/ so Unity doesn't import them and they stay out of version control
        static string BackupDir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "VRAMBackups");

        // Per project, per machine. Move it to ProjectSettings/ if you want to share it through git
        static string ExclusionFile => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "VRAMExclusions.json");

        // Shader names per texture, cached so the list doesn't walk materials every repaint
        Dictionary<Texture, string> _shaderLabels = new Dictionary<Texture, string>();
        Dictionary<Texture, string> _shaderTooltips = new Dictionary<Texture, string>();

        // ------------------------------------------------------------------
        // Bulk tool fields
        // ------------------------------------------------------------------

        // Table styles
        GUIStyle _tableHeaderStyle;
        GUIStyle _tableCellStyle;
        GUIStyle _tableSavingStyle;
        GUIStyle _tableLossStyle;
        GUIStyle _wrapStyle;
        GUIStyle _miniWrapStyle;

        // Bulk resize
        string[] _bulkScaleLabels = new string[] { "¼ size", "½ size", "2× size", "4× size" };
        float[] _bulkScaleValues = new float[] { 0.25f, 0.5f, 2f, 4f };
        long[] _bulkSavings;               // Estimated bytes saved per button (negative = grows)
        bool _bulkPreviewIncludeInactive;  // Detects when the disabled-objects toggle changed
        int _bulkTargetCount;              // How many textures the current scope touches

        // Threshold: bulk tools only touch textures larger than this (0 = all)
        int[] _bulkThresholdValues = new int[] { 0, 512, 1024, 2048, 4096 };
        string[] _bulkThresholdLabels = new string[] { "All sizes", "Larger than 512", "Larger than 1024", "Larger than 2048", "Larger than 4096" };
        int _bulkMinResolution = 0;

        // Auto-fit. The tiers come from the same constants the quality icons use, so they can't drift apart
        int[] _autoFitTargetValues = new int[]
        {
            (int)PC_TEXTURE_MEMORY_EXCELLENT_MiB, (int)PC_TEXTURE_MEMORY_GOOD_MiB,
            (int)PC_TEXTURE_MEMORY_MEDIUM_MiB, (int)PC_TEXTURE_MEMORY_POOR_MiB
        };
        string[] _autoFitTargetLabels = new string[]
        {
            $"Excellent (under {PC_TEXTURE_MEMORY_EXCELLENT_MiB} MB)", $"Good (under {PC_TEXTURE_MEMORY_GOOD_MiB} MB)",
            $"Medium (under {PC_TEXTURE_MEMORY_MEDIUM_MiB} MB)", $"Poor (under {PC_TEXTURE_MEMORY_POOR_MiB} MB)"
        };
        int _autoFitTargetMiB = (int)PC_TEXTURE_MEMORY_GOOD_MiB;
        const int AUTOFIT_MIN_SIZE = 512;              // Never shrink below this (longest side)
        Dictionary<Texture, int> _autoFitPlan;         // Cached plan, rebuilt lazily when null
        long _autoFitProjected;                        // Estimated texture memory after the plan

        // Size cap tool 
        int[] _capValues = new int[] { 256, 512, 1024, 2048, 4096 };
        string[] _capLabels = new string[] { "256", "512", "1024", "2048", "4096" };
        int _capSize = 1024;
        Dictionary<Texture, int> _capPlan; // Cached plan (texture -> cap), rebuilt lazily when null
        long _capSavings;                  // Estimated bytes saved by the plan

        // Optional cap used by auto-fit before it starts halving (0 = no cap) 
        int[] _autoFitCapValues = new int[] { 0, 256, 512, 1024, 2048, 4096 };
        string[] _autoFitCapLabels = new string[] { "No cap", "Cap at 256", "Cap at 512", "Cap at 1024", "Cap at 2048", "Cap at 4096" };
        int _autoFitCapSize = 0;

        // Bulk compression
        Dictionary<Texture, TextureImporterFormat> _compressionPlan; // Fixed-rule plan
        long _compressionSavings;                                    // Estimated bytes saved by the plan

        // Complexity-based smart optimization. The fixed-rule bulk tools stay available unchanged.
        Dictionary<Texture, float> _complexityScores = new Dictionary<Texture, float>();
        Dictionary<Texture, int> _smartResizePlan;
        Dictionary<Texture, TextureImporterFormat> _smartCompressionPlan;
        Dictionary<Texture, long> _smartResizeSaving = new Dictionary<Texture, long>();      // Per texture, so unticking a row updates the totals
        Dictionary<Texture, long> _smartCompressionSaving = new Dictionary<Texture, long>();
        HashSet<Texture> _smartSkipResize = new HashSet<Texture>();                          // Rows the user unticked in the review list
        HashSet<Texture> _smartSkipCompress = new HashSet<Texture>();
        List<TextureInfo> _smartReviewRows = new List<TextureInfo>();
        bool _smartReviewFoldout;
        bool _smartAnalysisReady;
        // Shared quality/aggressiveness preset for smart resize and smart compression.
        // 0 = Low Quality, 1 = Medium Quality, 2 = Balanced Quality, 3 = High Quality, 4 = High Detailed.
        int _smartQualityPreset = 2;
        readonly string[] _smartQualityLabels = new string[]
        {
            "Low Quality", "Medium Quality", "Balanced Quality", "High Quality", "High Detailed"
        };
        readonly string[] _smartQualityShort = new string[] { "Low", "Medium", "Balanced", "High", "Detailed" };

        // ------------------------------------------------------------------
        // Persisted settings (EditorPrefs, so they survive recompiles and restarts)
        // ------------------------------------------------------------------

        const string PREF = "ThryVRAM.";

        void LoadBulkPrefs()
        {
            _tab = Mathf.Clamp(EditorPrefs.GetInt(PREF + "tab", 0), 0, TabLabels.Length - 1);
            _includeInactive = EditorPrefs.GetBool(PREF + "includeInactive", true);
            _aboutFoldout = EditorPrefs.GetBool(PREF + "aboutFoldout", false);
            _avatarCount = Mathf.Max(1, EditorPrefs.GetInt(PREF + "avatarCount", 40));
            _bulkMinResolution = EditorPrefs.GetInt(PREF + "minResolution", 0);
            _autoFitTargetMiB = EditorPrefs.GetInt(PREF + "autoFitTarget", (int)PC_TEXTURE_MEMORY_GOOD_MiB);
            _autoFitCapSize = EditorPrefs.GetInt(PREF + "autoFitCap", 0);
            _capSize = EditorPrefs.GetInt(PREF + "capSize", 1024);
            _smartQualityPreset = Mathf.Clamp(EditorPrefs.GetInt(PREF + "smartPreset", 2), 0, _smartQualityLabels.Length - 1);
        }

        void SaveBulkPrefs()
        {
            EditorPrefs.SetInt(PREF + "tab", _tab);
            EditorPrefs.SetBool(PREF + "includeInactive", _includeInactive);
            EditorPrefs.SetBool(PREF + "aboutFoldout", _aboutFoldout);
            EditorPrefs.SetInt(PREF + "avatarCount", _avatarCount);
            EditorPrefs.SetInt(PREF + "minResolution", _bulkMinResolution);
            EditorPrefs.SetInt(PREF + "autoFitTarget", _autoFitTargetMiB);
            EditorPrefs.SetInt(PREF + "autoFitCap", _autoFitCapSize);
            EditorPrefs.SetInt(PREF + "capSize", _capSize);
            EditorPrefs.SetInt(PREF + "smartPreset", _smartQualityPreset);
        }

        // ------------------------------------------------------------------
        // Hooks called from TextureVRAM.cs
        // ------------------------------------------------------------------

        // Called from InitilizeStyles()
        void InitBulkStyles()
        {
            bool pro = EditorGUIUtility.isProSkin;
            _tableHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            _tableCellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
            _tableSavingStyle = new GUIStyle(_tableCellStyle);
            _tableSavingStyle.normal.textColor = pro ? new Color(0.35f, 0.8f, 0.35f) : new Color(0.05f, 0.5f, 0.1f);
            _tableLossStyle = new GUIStyle(_tableCellStyle);
            _tableLossStyle.normal.textColor = pro ? new Color(0.9f, 0.45f, 0.4f) : new Color(0.7f, 0.15f, 0.1f);
            _wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            _miniWrapStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }

        // Called at the start of Calc(): cached previews and backup info are out of date after any recalculation
        void OnBulkRecalculated()
        {
            _shaderLabels.Clear(); // Materials may have changed 
            _shaderTooltips.Clear();
            _bulkSavings = null;
            ResetSmartPlans();
            _backupDirty = true;
        }

        void ResetSmartPlans()
        {
            _complexityScores.Clear();
            _smartResizePlan = null;
            _smartCompressionPlan = null;
            _smartResizeSaving.Clear();
            _smartCompressionSaving.Clear();
            _smartSkipResize.Clear();
            _smartSkipCompress.Clear();
            _smartReviewRows.Clear();
            _smartAnalysisReady = false;
        }

        // ------------------------------------------------------------------
        // Optimize tab
        // ------------------------------------------------------------------

        void BeginCard(string title, string description = null)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(description)) GUILayout.Label(description, _miniWrapStyle);
            GUILayout.Space(2);
        }

        void EndCard()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }

        static bool ApplyButton(string text, string tooltip, bool enabled, float height = 24f)
        {
            using (new EditorGUI.DisabledScope(!enabled))
                return GUILayout.Button(new GUIContent(text, tooltip), GUILayout.Height(height));
        }

        // "40.2 MB → 31.0 MB (-9.2 MB, 12 texture(s))  [PC rating icon]"
        void DrawPlanSummary(int count, long updated, string emptyText, string suffix = "")
        {
            if (count == 0)
            {
                GUILayout.Label(emptyText, _miniWrapStyle);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} → {AvatarEvaluator.ToMebiByteString(updated)} " +
                                $"(-{AvatarEvaluator.ToShortMebiByteString(Math.Max(0L, _sizeAllTextures - updated))}, {count} texture(s)){suffix}",
                    _wrapStyle, GUILayout.ExpandWidth(true));
                GUILayout.Label("PC", GUILayout.Width(20));
                AvatarEvaluator.DrawQualityIcon(GetTextureQuality(updated, false));
            }
        }

        // Called from OnGUI(): the whole Optimize tab
        void DrawOptimizeTab()
        {
            _scrollMain = EditorGUILayout.BeginScrollView(_scrollMain);

            BeginCard("Scope", "Every tool below only touches textures that match this scope. A backup is saved first, see the Backups tab.");
            EditorGUI.BeginChangeCheck();
            _bulkMinResolution = EditorGUILayout.IntPopup("Only touch textures", _bulkMinResolution, _bulkThresholdLabels, _bulkThresholdValues);
            if (EditorGUI.EndChangeCheck())
                _bulkSavings = null;
            DrawExclusionSummary();

            // Rebuild the cached preview only when needed, not every repaint
            if (_bulkSavings == null || _bulkPreviewIncludeInactive != _includeInactive)
                RefreshBulkPreview();
            GUILayout.Label($"{_bulkTargetCount} of {_texturesList.Count} texture(s) in scope", _miniWrapStyle);
            EndCard();

            DrawResizeCard();
            DrawSmartCard();
            DrawBulkCompression();
            DrawAutoFit();
            DrawSizeCap();

            GUILayout.Label("Bulk tools by Nommiiee", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        void DrawExclusionSummary()
        {
            EnsureExclusionsLoaded();
            int onAvatar = CountExcluded();
            int off = _excludedGuids.Count - onAvatar;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{onAvatar} texture(s) skipped" + (off > 0 ? $" (+{off} not on this avatar)" : ""), _miniWrapStyle, GUILayout.ExpandWidth(true));
                if (GUILayout.Button(new GUIContent("Review", "Show the skipped textures in the Textures tab"), EditorStyles.miniButtonLeft, GUILayout.Width(60)))
                {
                    _tab = 1;
                    _texOnlySkipped = true;
                    GUIUtility.ExitGUI();
                }
                using (new EditorGUI.DisabledScope(_excludedGuids.Count == 0))
                {
                    if (GUILayout.Button("Clear all", EditorStyles.miniButtonRight, GUILayout.Width(60)) &&
                        EditorUtility.DisplayDialog("Clear Exclusions", $"Remove all {_excludedGuids.Count} texture(s) from the exclusion list?", "Clear", "Cancel"))
                    {
                        _excludedGuids.Clear();
                        SaveExclusions();
                        _bulkSavings = null;
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Bulk resize
        // ------------------------------------------------------------------

        void DrawResizeCard()
        {
            if (_bulkSavings == null) return;
            BeginCard("Resize all textures", "Savings are estimates. Growing is only an upper bound, textures can't grow past their source image.");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Resize", EditorStyles.boldLabel, GUILayout.Width(64));
                GUILayout.Label("Updated Usage", _tableHeaderStyle, GUILayout.MinWidth(80));
                GUILayout.Label("Estimated Saving", _tableHeaderStyle, GUILayout.MinWidth(80));
                GUILayout.Label("PC", _tableHeaderStyle, GUILayout.Width(40));
                GUILayout.Space(62);
            }

            for (int i = 0; i < _bulkScaleLabels.Length; i++)
            {
                bool growing = _bulkScaleValues[i] > 1f;
                long saving = _bulkSavings[i];
                long updated = _sizeAllTextures - saving;
                string upTo = growing && saving < 0 ? "up to " : ""; // Growing is only an upper bound

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(_bulkScaleLabels[i], GUILayout.Width(64));
                    GUILayout.Label(upTo + AvatarEvaluator.ToMebiByteString(updated), _tableCellStyle, GUILayout.MinWidth(80));
                    GUILayout.Label(FormatSavings(saving, growing), saving > 0 ? _tableSavingStyle : saving < 0 ? _tableLossStyle : _tableCellStyle, GUILayout.MinWidth(80));
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(40)))
                    {
                        GUILayout.FlexibleSpace(); // Right align under the header
                        AvatarEvaluator.DrawQualityIcon(GetTextureQuality(updated, false));
                    }
                    bool clicked = ApplyButton("Apply", growing ? "Upper bound. Textures can't grow past their source image." : "Applies this resize to all textures in scope",
                        _bulkTargetCount > 0 && saving != 0, 20f);
                    if (clicked)
                    {
                        ChangeAllImportSizes(_bulkScaleValues[i], _bulkScaleLabels[i], saving);
                        GUIUtility.ExitGUI(); // Lists get rebuilt, so stop drawing this frame
                    }
                }
            }
            GUILayout.Label($"Current texture memory: {AvatarEvaluator.ToMebiByteString(_sizeAllTextures)}", _miniWrapStyle);
            EndCard();
        }

        // Scale a resolution, round to nearest power of two, clamp to 32-8192
        static int GetScaledSize(int resolution, float scale) =>
            Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(resolution * scale)), 32, 8192);

        // Single source of truth for which textures the bulk tools touch, so preview and apply agree.
        // Respects the disabled-objects toggle, the size threshold and the exclusion list.
        // Uses the paths/GUIDs cached in Calc(), so it never has to ask the AssetDatabase
        List<TextureInfo> GetBulkTargets()
        {
            EnsureExclusionsLoaded();
            return _texturesList
                .Where(t => (_includeInactive || t.isActive)
                         && t.texture is Texture2D
                         && t.hasImporter
                         && Mathf.Max(t.texture.width, t.texture.height) > _bulkMinResolution
                         && !IsExcludedGuid(t.guid))
                .ToList();
        }

        // Compute the estimated VRAM change for each bulk button and cache it
        void RefreshBulkPreview()
        {
            List<TextureInfo> targets = GetBulkTargets();
            _bulkTargetCount = targets.Count;
            _bulkSavings = new long[_bulkScaleValues.Length];

            for (int i = 0; i < _bulkScaleValues.Length; i++)
            {
                foreach (TextureInfo t in targets)
                {
                    int resolution = Mathf.Max(t.texture.width, t.texture.height);
                    int newSize = GetScaledSize(resolution, _bulkScaleValues[i]);
                    if (newSize == resolution) continue;
                    _bulkSavings[i] += t.size - TextureToBytesUsingBPP(t.texture, t.BPP, (float)newSize / resolution);
                }
            }
            _bulkPreviewIncludeInactive = _includeInactive;

            // The other plans are built from the same targets, so they rebuild lazily too
            _autoFitPlan = null;
            _capPlan = null; // Rebuilt lazily when the threshold, toggle or textures change 
            _compressionPlan = null;
            ResetSmartPlans();
        }

        // ------------------------------------------------------------------
        // Auto-fit UI
        // ------------------------------------------------------------------

        void DrawAutoFit()
        {
            if (_autoFitPlan == null) RefreshAutoFitPlan();

            BeginCard("Auto-fit to target", "Optionally caps everything first, then halves the biggest textures until the estimate is under the target.");

            EditorGUI.BeginChangeCheck();
            _autoFitTargetMiB = EditorGUILayout.IntPopup("Target texture memory", _autoFitTargetMiB, _autoFitTargetLabels, _autoFitTargetValues);
            _autoFitCapSize = EditorGUILayout.IntPopup("Cap everything first", _autoFitCapSize, _autoFitCapLabels, _autoFitCapValues); // Change by - Nommiiee
            if (EditorGUI.EndChangeCheck())
                RefreshAutoFitPlan();

            long targetBytes = _autoFitTargetMiB * 1048576L;
            bool reached = _autoFitProjected <= targetBytes;

            if (_autoFitPlan.Count == 0)
            {
                GUILayout.Label(_sizeAllTextures <= targetBytes
                    ? "Already under the target, nothing to do."
                    : $"Can't reach the target without going below {AUTOFIT_MIN_SIZE}px." + ExcludedNote(), _miniWrapStyle);
            }
            else
            {
                DrawPlanSummary(_autoFitPlan.Count, _autoFitProjected, "",
                    reached ? "" : $". Can't reach the target without going below {AUTOFIT_MIN_SIZE}px." + ExcludedNote());
            }

            if (ApplyButton("Auto-fit", "Applies the cap (if set), then shrinks the biggest textures first until the estimate is under the target", _autoFitPlan.Count > 0))
            {
                ApplyAutoFit();
                GUIUtility.ExitGUI();
            }
            EndCard();
        }

        // ------------------------------------------------------------------
        // Size cap UI
        // ------------------------------------------------------------------

        void DrawSizeCap()
        {
            if (_capPlan == null) RefreshCapPlan();

            BeginCard("Cap max texture size", "Lowers every texture above the chosen size to that size. Never raises anything.");

            EditorGUI.BeginChangeCheck();
            _capSize = EditorGUILayout.IntPopup("Maximum size (px)", _capSize, _capLabels, _capValues);
            if (EditorGUI.EndChangeCheck())
                RefreshCapPlan();

            if (_capPlan.Count == 0)
                GUILayout.Label($"Nothing above {_capSize}px, nothing to do.", _miniWrapStyle);
            else
                DrawPlanSummary(_capPlan.Count, _sizeAllTextures - _capSavings, "");

            if (ApplyButton("Apply cap", "Lowers every texture above the chosen size to that size. Never raises anything", _capPlan.Count > 0))
            {
                ApplySizeCap();
                GUIUtility.ExitGUI();
            }
            EndCard();
        }

        // ------------------------------------------------------------------
        // Bulk compression UI
        // ------------------------------------------------------------------

        void DrawBulkCompression()
        {
            if (_compressionPlan == null) RefreshCompressionPlan();

            BeginCard("Bulk compression", "DXT1 for opaque textures, BC7 for textures with alpha or normal maps.");

            if (_compressionPlan.Count == 0)
                GUILayout.Label("Nothing to compress, all textures in scope are already as small as they can be.", _miniWrapStyle);
            else
                DrawPlanSummary(_compressionPlan.Count, _sizeAllTextures - _compressionSavings, "");

            if (ApplyButton("Compress all", "DXT1 for opaque textures, BC7 for textures with alpha or normal maps", _compressionPlan.Count > 0))
            {
                ApplyBulkCompression();
                GUIUtility.ExitGUI();
            }
            EndCard();
        }

        // ------------------------------------------------------------------
        // Complexity-based smart resize + compression
        // ------------------------------------------------------------------

        static int CountSelected<T>(Dictionary<Texture, T> plan, HashSet<Texture> skipped)
        {
            if (plan == null) return 0;
            int n = 0;
            foreach (KeyValuePair<Texture, T> kv in plan)
                if (!skipped.Contains(kv.Key)) n++;
            return n;
        }

        static long SumSelected<T>(Dictionary<Texture, T> plan, Dictionary<Texture, long> savings, HashSet<Texture> skipped)
        {
            if (plan == null) return 0;
            long sum = 0;
            foreach (KeyValuePair<Texture, T> kv in plan)
            {
                long s;
                if (!skipped.Contains(kv.Key) && savings.TryGetValue(kv.Key, out s)) sum += s;
            }
            return sum;
        }

        void DrawSmartCard()
        {
            BeginCard("Smart optimization",
                "Scores how detailed each texture is (variance, edges, fine detail) and picks a resize and compression per texture. " +
                "Heuristic, no Read/Write needed. Nothing changes until you apply.");

            EditorGUI.BeginChangeCheck();
            _smartQualityPreset = GUILayout.Toolbar(_smartQualityPreset, _smartQualityShort, GUILayout.Height(22));
            if (EditorGUI.EndChangeCheck())
                ResetSmartPlans(); // A changed preset must never leave a preview made with old settings visible
            GUILayout.Label(GetSmartQualityDescription(), _miniWrapStyle);

            if (ApplyButton("Analyze textures", "Builds a preview only; no texture settings are changed", _bulkTargetCount > 0))
            {
                BuildSmartPlans();
                GUIUtility.ExitGUI();
            }

            if (_smartAnalysisReady)
            {
                int resizeCount = CountSelected(_smartResizePlan, _smartSkipResize);
                long resizeSaving = SumSelected(_smartResizePlan, _smartResizeSaving, _smartSkipResize);
                int compressCount = CountSelected(_smartCompressionPlan, _smartSkipCompress);
                long compressSaving = SumSelected(_smartCompressionPlan, _smartCompressionSaving, _smartSkipCompress);

                GUILayout.Space(4);
                GUILayout.Label($"Analyzed {_complexityScores.Count} texture(s). Complexity: 0 = simple, 1 = highly detailed.", _miniWrapStyle);

                GUILayout.Label("Smart resize", EditorStyles.boldLabel);
                DrawPlanSummary(resizeCount, Math.Max(0L, _sizeAllTextures - resizeSaving), "No resolution changes selected.");
                if (ApplyButton("Apply smart resize", "Lowers the max import size of the selected textures", resizeCount > 0, 23))
                {
                    ApplySmartResize();
                    GUIUtility.ExitGUI();
                }

                GUILayout.Label("Smart compression", EditorStyles.boldLabel);
                DrawPlanSummary(compressCount, Math.Max(0L, _sizeAllTextures - compressSaving), "No safe compression changes selected.");
                if (ApplyButton("Apply smart compression", "Changes the PC compression format of the selected textures", compressCount > 0, 23))
                {
                    ApplySmartCompression();
                    GUIUtility.ExitGUI();
                }

                GUILayout.Label("Resize and compression are estimated separately, applying both saves a bit less than the sum.", _miniWrapStyle);

                _smartReviewFoldout = EditorGUILayout.Foldout(_smartReviewFoldout, $"Review changes ({_smartReviewRows.Count} texture(s))", true);
                if (_smartReviewFoldout) DrawSmartReview();
            }
            EndCard();
        }

        // Per texture list of what the smart plans would do, with a checkbox per change
        void DrawSmartReview()
        {
            const int MaxRows = 60;
            float rowH = LINE_H + 2;

            Rect header = GUILayoutUtility.GetRect(0, LINE_H, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(header.x + 18, header.y, 90, LINE_H), "Resize", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(header.x + 128, header.y, 110, LINE_H), "Compression", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(header.x + 258, header.y, 40, LINE_H), "Score", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(header.x + 302, header.y, 100, LINE_H), "Texture", EditorStyles.miniBoldLabel);

            int n = Mathf.Min(_smartReviewRows.Count, MaxRows);
            for (int i = 0; i < n; i++)
            {
                TextureInfo t = _smartReviewRows[i];
                if (t.texture == null) continue;
                Rect r = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
                float x = r.x;

                int newRes;
                if (_smartResizePlan != null && _smartResizePlan.TryGetValue(t.texture, out newRes))
                {
                    bool on = !_smartSkipResize.Contains(t.texture);
                    bool now = GUI.Toggle(new Rect(x, r.y, 16, LINE_H), on, new GUIContent("", "Apply this resize"));
                    if (now != on) { if (now) _smartSkipResize.Remove(t.texture); else _smartSkipResize.Add(t.texture); }
                    GUI.Label(new Rect(x + 18, r.y, 106, LINE_H), $"{Mathf.Max(t.texture.width, t.texture.height)} → {newRes}", EditorStyles.miniLabel);
                }
                x += 128;

                TextureImporterFormat newFormat;
                if (_smartCompressionPlan != null && _smartCompressionPlan.TryGetValue(t.texture, out newFormat))
                {
                    bool on = !_smartSkipCompress.Contains(t.texture);
                    bool now = GUI.Toggle(new Rect(x, r.y, 16, LINE_H), on, new GUIContent("", "Apply this compression change"));
                    if (now != on) { if (now) _smartSkipCompress.Remove(t.texture); else _smartSkipCompress.Add(t.texture); }
                    GUI.Label(new Rect(x + 18, r.y, 110, LINE_H), $"{t.formatString} → {newFormat}", EditorStyles.miniLabel);
                }
                x += 130;

                float score;
                if (_complexityScores.TryGetValue(t.texture, out score))
                    GUI.Label(new Rect(x, r.y, 40, LINE_H), score.ToString("0.00"), EditorStyles.miniLabel);
                x += 44;

                EditorGUI.ObjectField(new Rect(x, r.y, Mathf.Max(60f, r.xMax - x), LINE_H), GUIContent.none, t.texture, typeof(Texture), false);
            }
            if (_smartReviewRows.Count > MaxRows)
                GUILayout.Label($"Showing the {MaxRows} largest of {_smartReviewRows.Count}. All of them are included when you apply.", _miniWrapStyle);
        }

        string GetSmartQualityDescription()
        {
            switch (_smartQualityPreset)
            {
                case 0: return "Low Quality: most aggressive resizing (up to 8× per dimension, never below 128px) and lowest compression-quality setting; prioritizes memory savings.";
                case 1: return "Medium Quality: fairly aggressive resizing (up to 4×, never below 256px) with lower compression effort; keeps basic detail.";
                case 2: return "Balanced Quality: moderate resizing (up to 4×, guided by complexity, never below 256px) and standard compression quality; recommended starting point.";
                case 3: return "High Quality: conservative resizing (up to 2×, never below 512px) and high compression quality; preserves more texture detail.";
                default: return "High Detailed: most detail-preserving preset, only low-complexity textures get a modest resize (never below 512px); highest compression quality. " +
                                "Sizes are relative to the current import size, so analyzing and applying again can shrink further.";
            }
        }

        void GetSmartQualitySettings(out int maxResizeDivisor, out float lowThreshold, out float highThreshold,
            out int compressionQuality, out float bc7DetailThreshold, out int minResolution)
        {
            switch (_smartQualityPreset)
            {
                case 0: // Low Quality: aggressively reduce resolution; BC7 only when required for alpha/normal maps.
                    maxResizeDivisor = 8; lowThreshold = 0.10f; highThreshold = 0.45f; compressionQuality = 0; bc7DetailThreshold = 0.95f; minResolution = 128;
                    break;
                case 1: // Medium Quality
                    maxResizeDivisor = 4; lowThreshold = 0.15f; highThreshold = 0.50f; compressionQuality = 25; bc7DetailThreshold = 0.80f; minResolution = 256;
                    break;
                case 2: // Balanced Quality
                    maxResizeDivisor = 4; lowThreshold = 0.20f; highThreshold = 0.60f; compressionQuality = 50; bc7DetailThreshold = 0.62f; minResolution = 256;
                    break;
                case 3: // High Quality
                    maxResizeDivisor = 2; lowThreshold = 0.25f; highThreshold = 0.65f; compressionQuality = 75; bc7DetailThreshold = 0.50f; minResolution = 512;
                    break;
                default: // High Detailed: only simpler textures get the modest 2x reduction.
                    maxResizeDivisor = 2; lowThreshold = 0.40f; highThreshold = 0.75f; compressionQuality = 100; bc7DetailThreshold = 0.40f; minResolution = 512;
                    break;
            }
        }

        // Picks DXT1 / BC7 for one texture. Returns false when the current format should stay.
        // Fixed: a high-detail opaque texture that is already BC7/DXT5 (8 bpp) used to fall through to DXT1 here.
        // It now keeps its format, BC7 is only chosen to replace a bigger (uncompressed) format.
        bool TryPickSmartCompression(TextureInfo info, float score, float bc7DetailThreshold, out TextureImporterFormat desired, out long saving)
        {
            desired = TextureImporterFormat.Automatic;
            saving = 0;
            if (info.formatString == null || info.formatString.Length == 0 || info.format == 0 || info.BPP <= info.minBPP) return false;

            TextureFormat estimateFormat;
            if (info.hasAlpha || info.isNormalMap)
            {
                desired = TextureImporterFormat.BC7;
                estimateFormat = TextureFormat.BC7;
            }
            else if (score >= bc7DetailThreshold)
            {
                if (info.BPP <= BPP[TextureFormat.BC7]) return false; // Detailed and already BC7-sized, leave it alone
                desired = TextureImporterFormat.BC7;
                estimateFormat = TextureFormat.BC7;
            }
            else
            {
                desired = TextureImporterFormat.DXT1;
                estimateFormat = TextureFormat.DXT1;
            }
            saving = info.size - TextureToBytesUsingBPP(info.texture, BPP[estimateFormat]);
            return saving > 0;
        }

        void BuildSmartPlans()
        {
            ResetSmartPlans();
            _smartResizePlan = new Dictionary<Texture, int>();
            _smartCompressionPlan = new Dictionary<Texture, TextureImporterFormat>();
            List<TextureInfo> targets = GetBulkTargets();
            GetSmartQualitySettings(out int maxResizeDivisor, out float lowThreshold, out float highThreshold,
                out _, out float bc7DetailThreshold, out int minResolution);
            int failed = 0;
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    TextureInfo info = targets[i];
                    EditorUtility.DisplayProgressBar("Analyzing Texture Complexity", info.texture.name,
                        targets.Count == 0 ? 1f : i / (float)targets.Count);
                    Texture2D tex = info.texture as Texture2D;
                    if (!TextureComplexityAnalyzer.TryAnalyze(tex, out float score, out string error))
                    {
                        failed++;
                        Debug.LogWarning($"[VRAM Calculator] Complexity analysis skipped '{info.texture.name}': {error}");
                        continue;
                    }
                    _complexityScores[info.texture] = score;

                    bool planned = false;
                    int currentResolution = Mathf.Max(info.texture.width, info.texture.height);
                    int divisor = TextureComplexityAnalyzer.GetRecommendedDivisor(score, 1, maxResizeDivisor, lowThreshold, highThreshold);
                    int targetResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(32, currentResolution / divisor)), 32, currentResolution);
                    targetResolution = Mathf.Max(targetResolution, Mathf.Min(minResolution, currentResolution)); // Preset floor
                    if (targetResolution < currentResolution)
                    {
                        _smartResizePlan[info.texture] = targetResolution;
                        _smartResizeSaving[info.texture] = info.size - TextureToBytesUsingBPP(info.texture, info.BPP,
                            (float)targetResolution / currentResolution);
                        planned = true;
                    }

                    if (TryPickSmartCompression(info, score, bc7DetailThreshold, out TextureImporterFormat desired, out long saving))
                    {
                        _smartCompressionPlan[info.texture] = desired;
                        _smartCompressionSaving[info.texture] = saving;
                        planned = true;
                    }

                    if (planned) _smartReviewRows.Add(info);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                TextureComplexityAnalyzer.ReleaseResources();
            }
            _smartReviewRows.Sort((a, b) => b.size.CompareTo(a.size));
            _smartAnalysisReady = true;
            _backupDirty = true;
            string failureNote = failed > 0 ? $" {failed} texture(s) could not be analyzed and were left out of the smart plans." : "";
            if (failed > 0) Debug.LogWarning($"[VRAM Calculator] Smart analysis:{failureNote}");
            Debug.Log($"[VRAM Calculator] Smart analysis complete: {_complexityScores.Count}/{targets.Count} analyzed, {_smartResizePlan.Count} resize change(s), {_smartCompressionPlan.Count} compression change(s).{failureNote}");
        }

        void ApplySmartResize()
        {
            if (!_smartAnalysisReady || _smartResizePlan == null) return;
            Dictionary<Texture, int> plan = new Dictionary<Texture, int>();
            foreach (KeyValuePair<Texture, int> kv in _smartResizePlan)
                if (!_smartSkipResize.Contains(kv.Key)) plan[kv.Key] = kv.Value;
            if (plan.Count == 0) return;

            long saving = SumSelected(_smartResizePlan, _smartResizeSaving, _smartSkipResize);
            if (!EditorUtility.DisplayDialog("Confirm Smart Resize",
                $"Complexity analysis recommends changing the max import size of {plan.Count} texture(s).\n\n" +
                $"Preset: {_smartQualityLabels[_smartQualityPreset]}.\n" +
                $"Estimated texture memory: {AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} → {AvatarEvaluator.ToMebiByteString(Math.Max(0L, _sizeAllTextures - saving))}.\n\n" +
                "A backup is saved first, so you can restore the previous settings from the Backups tab.", "Apply", "Cancel")) return;
            if (!SaveAutoFitBackup(plan, "Smart resize") &&
                !EditorUtility.DisplayDialog("Backup Failed", "Could not save the current texture sizes. Apply anyway without a restorable backup?", "Apply anyway", "Cancel")) return;
            int changed = ApplySizePlan(plan, "Smart Resizing Textures");
            Debug.Log($"[VRAM Calculator] Smart resize changed {changed} texture(s).");
            Calc(_avatar);
        }

        void ApplySmartCompression()
        {
            if (!_smartAnalysisReady || _smartCompressionPlan == null) return;
            Dictionary<Texture, TextureImporterFormat> plan = new Dictionary<Texture, TextureImporterFormat>();
            foreach (KeyValuePair<Texture, TextureImporterFormat> kv in _smartCompressionPlan)
                if (!_smartSkipCompress.Contains(kv.Key)) plan[kv.Key] = kv.Value;
            if (plan.Count == 0) return;

            long saving = SumSelected(_smartCompressionPlan, _smartCompressionSaving, _smartSkipCompress);
            if (!EditorUtility.DisplayDialog("Confirm Smart Compression",
                $"Complexity analysis recommends compression changes for {plan.Count} texture(s).\n\n" +
                $"Preset: {_smartQualityLabels[_smartQualityPreset]}.\n" +
                $"Estimated texture memory: {AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} → {AvatarEvaluator.ToMebiByteString(Math.Max(0L, _sizeAllTextures - saving))}.\n\n" +
                "A backup is saved first, so you can restore the previous settings from the Backups tab.", "Apply", "Cancel")) return;
            if (!SaveCompressionBackup(plan, "Smart compression") &&
                !EditorUtility.DisplayDialog("Backup Failed", "Could not save the current compression settings. Apply anyway without a restorable backup?", "Apply anyway", "Cancel")) return;

            GetSmartQualitySettings(out _, out _, out _, out int presetCompressionQuality, out _, out _);
            int changed = 0, step = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (KeyValuePair<Texture, TextureImporterFormat> kv in plan)
                {
                    EditorUtility.DisplayProgressBar("Applying Smart Compression", kv.Key.name, step++ / (float)plan.Count);
                    TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(kv.Key)) as TextureImporter;
                    if (importer == null) continue;
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
                    settings.overridden = true;
                    settings.format = kv.Value;
                    settings.compressionQuality = presetCompressionQuality;
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            Debug.Log($"[VRAM Calculator] Smart compression changed {changed} texture(s).");
            Calc(_avatar);
        }

        // ------------------------------------------------------------------
        // Backups tab
        // ------------------------------------------------------------------

        void DrawBackupsTab()
        {
            if (_backupDirty) RefreshBackupInfo();
            _scrollMain = EditorGUILayout.BeginScrollView(_scrollMain);

            BeginCard("Backups",
                "A backup of the affected textures is saved before every bulk change and single edit. Restoring a backup uses it up (the file is deleted afterwards). " +
                $"The newest {MAX_BACKUPS} are kept.");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Create backup now", "Saves the current size and compression of every texture so you can restore it later"), GUILayout.Height(22)))
                {
                    CreateManualBackup();
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("Open backup folder", "Backups are stored in Library/VRAMBackups"), GUILayout.Height(22)))
                {
                    if (Directory.Exists(BackupDir)) EditorUtility.RevealInFinder(BackupDir);
                }
            }
            EndCard();

            if (_backupFiles.Length == 0)
            {
                GUILayout.Label("No backups yet.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < _backupFiles.Length; i++)
                {
                    using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label(new GUIContent(_backupLabels[i], _backupLabels[i]), GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("Restore", GUILayout.Width(60)))
                        {
                            RestoreBackup(_backupFiles[i]);
                            GUIUtility.ExitGUI();
                        }
                        if (GUILayout.Button("Delete", GUILayout.Width(52)) &&
                            EditorUtility.DisplayDialog("Delete Backup", "Delete this backup without restoring it?", "Delete", "Cancel"))
                        {
                            try { File.Delete(_backupFiles[i]); } catch (Exception e) { Debug.LogWarning($"[VRAM Calculator] Could not delete backup: {e.Message}"); }
                            _backupDirty = true;
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // Put one backup's settings back in one batch. Deleted textures are skipped and counted
        void RestoreBackup(string file)
        {
            if (!File.Exists(file)) { _backupDirty = true; return; }

            BulkBackup backup;
            try { backup = JsonUtility.FromJson<BulkBackup>(File.ReadAllText(file)); }
            catch (Exception e)
            {
                Debug.LogError($"[VRAM Calculator] Could not read backup: {e.Message}");
                return;
            }

            bool isNewest = _backupFiles.Length > 0 && file == _backupFiles[0];
            string olderNote = isNewest ? "" : "\n\nThis is not the newest backup. Newer backups are kept, and textures they changed that are not in this one stay as they are.";

            if (!EditorUtility.DisplayDialog("Restore Backup",
                $"Restore {backup.entries.Count} texture(s) from the '{backup.label}' change made on {backup.timestamp}?\n\n" +
                $"Any changes made since then to these textures will be overwritten. The backup file is deleted afterwards.{olderNote}", "Restore", "Cancel"))
                return;

            int restored = 0, skipped = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < backup.entries.Count; i++)
                {
                    BulkBackupEntry entry = backup.entries[i];
                    EditorUtility.DisplayProgressBar("Restoring Textures", entry.path, i / (float)backup.entries.Count);

                    // Look up by GUID so moved or renamed textures still work
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    TextureImporter importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) { skipped++; continue; }

                    importer.maxTextureSize = entry.maxSize;
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
                    settings.maxTextureSize = entry.pcMaxSize;

                    if (entry.restoreFormat)
                    {
                        settings.overridden = entry.pcOverridden;
                        settings.format = (TextureImporterFormat)entry.pcFormat;
                        settings.compressionQuality = entry.pcQuality;
                    }

                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    restored++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            // The backup is used up
            try { File.Delete(file); } catch { }

            Debug.Log($"[VRAM Calculator] Restored {restored} texture(s), skipped {skipped} (deleted or missing).");
            if (_avatar != null) Calc(_avatar); // Also marks the backup list dirty
            else _backupDirty = true;
        }

        // ------------------------------------------------------------------
        // Exclusion helpers
        // ------------------------------------------------------------------

        // Same as IsExcluded, but takes the GUID cached in TextureInfo so it never touches the AssetDatabase
        bool IsExcludedGuid(string guid)
        {
            EnsureExclusionsLoaded();
            return _excludedGuids.Count > 0 && !string.IsNullOrEmpty(guid) && _excludedGuids.Contains(guid);
        }

        int CountExcluded()
        {
            EnsureExclusionsLoaded();
            if (_excludedGuids.Count == 0) return 0;
            int n = 0;
            foreach (TextureInfo t in _texturesList)
                if (!string.IsNullOrEmpty(t.guid) && _excludedGuids.Contains(t.guid)) n++;
            return n;
        }

        // ------------------------------------------------------------------
        // Unchanged logic (plans, apply, backup files, exclusion storage)
        // ------------------------------------------------------------------

        // Apply a resize to every bulk target at once, with confirm dialog and estimate
        void ChangeAllImportSizes(float scale, string label, long estimatedSavings)
        {
            List<TextureInfo> targets = GetBulkTargets();
            if (targets.Count == 0) return;

            string estimate = estimatedSavings > 0
                ? $"Estimated saving: ~{AvatarEvaluator.ToShortMebiByteString(estimatedSavings)} of VRAM."
                : "No VRAM saving expected.";

            // Tell the user which threshold is active so the texture count makes sense
            string thresholdNote = _bulkMinResolution > 0
                ? $"\nOnly textures larger than {_bulkMinResolution}px are included."
                : "";

            if (!EditorUtility.DisplayDialog("Confirm Bulk Resize",
                $"You are about to change the max import size of {targets.Count} texture(s) to {label} (per side, rounded to the nearest power of two).{thresholdNote}\n\n" +
                $"{estimate}\n\nA backup is saved first, so you can undo it from the Backups tab.", "Yes", "No"))
                return;

            // Back up the current sizes before touching anything. If that fails, let the user decide
            if (!SaveBackup(targets, scale, label) &&
                !EditorUtility.DisplayDialog("Backup Failed",
                    "Could not save a backup of the current texture sizes (see the console).\n\nResize anyway? You won't be able to restore.", "Resize anyway", "Cancel"))
                return;

            int changed = 0;
            try
            {
                AssetDatabase.StartAssetEditing(); // Batch all reimports into one pass
                for (int i = 0; i < targets.Count; i++)
                {
                    Texture tex = targets[i].texture;
                    EditorUtility.DisplayProgressBar("Resizing Textures", tex.name, i / (float)targets.Count);

                    TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
                    if (importer == null) continue;

                    int resolution = Mathf.Max(tex.width, tex.height);
                    int newSize = GetScaledSize(resolution, scale);
                    if (newSize == resolution) continue;

                    importer.maxTextureSize = newSize;
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
                    settings.maxTextureSize = newSize;
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[VRAM Calculator] Resized {changed} texture(s) by {label}.");
            Calc(_avatar); // Recalculate once at the end, not per texture
        }

        // Format the savings text ("-38 MB", "up to +12 MB", "no change")
        static string FormatSavings(long bytes, bool isUpperBound)
        {
            if (bytes == 0) return "no change";
            string size = AvatarEvaluator.ToShortMebiByteString(Math.Abs(bytes));
            if (bytes > 0) return $"-{size}";
            return isUpperBound ? $"up to +{size}" : $"+{size}";
        }

        // Plan: optionally cap everything first, then halve the biggest textures until the target is met or nothing is left 
        Dictionary<Texture, int> BuildAutoFitPlan(long targetBytes, int capPx, out long projected)
        {
            List<TextureInfo> targets = GetBulkTargets();
            int n = targets.Count;
            int[] res = new int[n];
            long[] bytes = new long[n];
            for (int i = 0; i < n; i++)
            {
                res[i] = Mathf.Max(targets[i].texture.width, targets[i].texture.height);
                bytes[i] = targets[i].size;
            }

            Dictionary<Texture, int> plan = new Dictionary<Texture, int>();
            projected = _sizeAllTextures;

            // Phase 1: apply the cap to everything above it, regardless of the target
            if (capPx > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (res[i] <= capPx) continue;
                    Texture tex = targets[i].texture;
                    long newBytes = TextureToBytesUsingBPP(tex, targets[i].BPP, (float)capPx / Mathf.Max(tex.width, tex.height));
                    projected -= bytes[i] - newBytes;
                    bytes[i] = newBytes;
                    res[i] = capPx;
                    plan[tex] = capPx;
                }
            }

            // Phase 2: still above the target? Keep halving the biggest texture that can shrink
            while (projected > targetBytes)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    int next = GetScaledSize(res[i], 0.5f);
                    if (next < AUTOFIT_MIN_SIZE || next >= res[i]) continue;
                    if (best < 0 || bytes[i] > bytes[best]) best = i;
                }
                if (best < 0) break; // Nothing left to shrink, target can't be reached

                Texture tex = targets[best].texture;
                int newRes = GetScaledSize(res[best], 0.5f);
                long newBytes = TextureToBytesUsingBPP(tex, targets[best].BPP, (float)newRes / Mathf.Max(tex.width, tex.height));

                projected -= bytes[best] - newBytes;
                bytes[best] = newBytes;
                res[best] = newRes;
                plan[tex] = newRes;
            }
            return plan;
        }

        // Rebuild the cached plan for the selected target and cap
        void RefreshAutoFitPlan()
        {
            _autoFitPlan = BuildAutoFitPlan(_autoFitTargetMiB * 1048576L, _autoFitCapSize, out _autoFitProjected);
        }

        // Apply the cached plan with a confirm dialog and a backup first
        void ApplyAutoFit()
        {
            if (_autoFitPlan == null || _autoFitPlan.Count == 0) return;

            Dictionary<Texture, int> plan = new Dictionary<Texture, int>(_autoFitPlan); // Copy, Calc() resets the cache
            string capNote = _autoFitCapSize > 0 ? $", cap {_autoFitCapSize}px" : "";
            string label = $"Auto-fit {_autoFitTargetMiB} MB{capNote}";

            if (!EditorUtility.DisplayDialog("Confirm Auto-fit",
                $"This lowers the max import size of {plan.Count} texture(s)" +
                (_autoFitCapSize > 0 ? $" (everything above {_autoFitCapSize}px is capped first, then the biggest are halved as needed)" : ", biggest first") +
                $", bringing texture memory from {AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} to about {AvatarEvaluator.ToMebiByteString(_autoFitProjected)} (target {_autoFitTargetMiB} MB).\n\n" +
                "A backup is saved first, so you can undo it from the Backups tab.", "Yes", "No"))
                return;

            if (!SaveAutoFitBackup(plan, label) &&
                !EditorUtility.DisplayDialog("Backup Failed",
                    "Could not save a backup of the current texture sizes (see the console).\n\nAuto-fit anyway? You won't be able to restore.", "Auto-fit anyway", "Cancel"))
                return;

            int changed = ApplySizePlan(plan, "Auto-fit");
            Debug.Log($"[VRAM Calculator] Auto-fit resized {changed} texture(s) towards {_autoFitTargetMiB} MB{capNote}.");
            Calc(_avatar);
        }

        // Back up every texture in the plan before it changes. Returns false if writing failed
        bool SaveAutoFitBackup(Dictionary<Texture, int> plan, string label)
        {
            BulkBackup backup = new BulkBackup { label = label, timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            foreach (Texture tex in plan.Keys)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                backup.entries.Add(MakeBackupEntry(path, importer, false));
            }
            if (backup.entries.Count == 0) return true;
            return WriteBackupFile(backup);
        }

        // Shared by the cap tool and auto-fit: set the max import size of every texture in the plan. Returns how many changed
        int ApplySizePlan(Dictionary<Texture, int> plan, string progressTitle)
        {
            int changed = 0, step = 0;
            try
            {
                AssetDatabase.StartAssetEditing(); // Batch all reimports into one pass
                foreach (KeyValuePair<Texture, int> kv in plan)
                {
                    EditorUtility.DisplayProgressBar(progressTitle, kv.Key.name, step++ / (float)plan.Count);

                    TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(kv.Key)) as TextureImporter;
                    if (importer == null) continue;

                    importer.maxTextureSize = kv.Value;
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
                    settings.maxTextureSize = kv.Value;
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            return changed;
        }

        // Plan: every target whose longest side is above the cap gets the cap. Never raises anything 
        void RefreshCapPlan()
        {
            _capPlan = new Dictionary<Texture, int>();
            _capSavings = 0;

            foreach (TextureInfo t in GetBulkTargets()) // Toggle and threshold apply
            {
                int res = Mathf.Max(t.texture.width, t.texture.height);
                if (res <= _capSize) continue;

                _capPlan[t.texture] = _capSize;
                _capSavings += t.size - TextureToBytesUsingBPP(t.texture, t.BPP, (float)_capSize / res);
            }
        }

        // Apply the cached cap plan with a confirm dialog and a backup first
        void ApplySizeCap()
        {
            if (_capPlan == null || _capPlan.Count == 0) return;

            Dictionary<Texture, int> plan = new Dictionary<Texture, int>(_capPlan); // Copy, Calc() resets the cache
            long updated = _sizeAllTextures - _capSavings;
            string label = $"Cap {_capSize}px";
            string softNote = _capSize < 512 ? "\n\nHeads up: below 512px, masks and normal maps get visibly soft." : "";

            if (!EditorUtility.DisplayDialog("Confirm Size Cap",
                $"This lowers the max import size of {plan.Count} texture(s) to {_capSize}px.\n\n" +
                $"Estimated texture memory: {AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} → {AvatarEvaluator.ToMebiByteString(updated)}.{softNote}\n\n" +
                "A backup is saved first, so you can undo it from the Backups tab.", "Yes", "No"))
                return;

            if (!SaveAutoFitBackup(plan, label) && // Size-only backup, works for any size plan
                !EditorUtility.DisplayDialog("Backup Failed",
                    "Could not save a backup of the current texture sizes (see the console).\n\nApply the cap anyway? You won't be able to restore.", "Apply anyway", "Cancel"))
                return;

            int changed = ApplySizePlan(plan, "Capping Textures");
            Debug.Log($"[VRAM Calculator] Capped {changed} texture(s) at {_capSize}px.");
            Calc(_avatar);
        }

        // Build the plan with the same rules as the per-row compression buttons, on the same targets as the bulk resize
        void RefreshCompressionPlan()
        {
            _compressionPlan = new Dictionary<Texture, TextureImporterFormat>();
            _compressionSavings = 0;

            foreach (TextureInfo t in GetBulkTargets())
            {
                if (t.formatString.Length < 1 || t.format == 0) continue; // Not compressable
                if (t.BPP <= t.minBPP) continue;                          // Already as small as it can get

                TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t.texture)) as TextureImporter;
                if (importer == null) continue;

                bool useBC7 = t.hasAlpha || importer.textureType == TextureImporterType.NormalMap;
                TextureFormat newFormat = useBC7 ? TextureFormat.BC7 : TextureFormat.DXT1;

                long saving = t.size - TextureToBytesUsingBPP(t.texture, BPP[newFormat]);
                if (saving <= 0) continue;

                _compressionPlan[t.texture] = useBC7 ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT1;
                _compressionSavings += saving;
            }
        }

        // Back up size + PC format of every texture in the plan before it changes. Returns false if writing failed
        bool SaveCompressionBackup(Dictionary<Texture, TextureImporterFormat> plan, string label)
        {
            BulkBackup backup = new BulkBackup { label = label, timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            foreach (Texture tex in plan.Keys)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                backup.entries.Add(MakeBackupEntry(path, importer, true)); // true = also store the compression format
            }
            if (backup.entries.Count == 0) return true;
            return WriteBackupFile(backup);
        }

        // Apply the cached plan with a confirm dialog and a backup first
        void ApplyBulkCompression()
        {
            if (_compressionPlan == null || _compressionPlan.Count == 0) return;

            Dictionary<Texture, TextureImporterFormat> plan = new Dictionary<Texture, TextureImporterFormat>(_compressionPlan); // Copy, Calc() resets the cache
            long updated = _sizeAllTextures - _compressionSavings;

            if (!EditorUtility.DisplayDialog("Confirm Bulk Compression",
                $"You are about to change the PC compression format of {plan.Count} texture(s) (DXT1 for opaque, BC7 for alpha and normal maps).\n\n" +
                $"Estimated texture memory: {AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} → {AvatarEvaluator.ToMebiByteString(updated)}.\n\n" +
                "A backup is saved first, so you can undo it from the Backups tab.", "Yes", "No"))
                return;

            if (!SaveCompressionBackup(plan, "Bulk compression") &&
                !EditorUtility.DisplayDialog("Backup Failed",
                    "Could not save a backup of the current texture settings (see the console).\n\nCompress anyway? You won't be able to restore.", "Compress anyway", "Cancel"))
                return;

            int changed = 0, step = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (KeyValuePair<Texture, TextureImporterFormat> kv in plan)
                {
                    EditorUtility.DisplayProgressBar("Compressing Textures", kv.Key.name, step++ / (float)plan.Count);

                    TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(kv.Key)) as TextureImporter;
                    if (importer == null) continue;

                    // Keep the existing PC settings and only change what we mean to change
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
                    settings.overridden = true;
                    settings.format = kv.Value;
                    settings.compressionQuality = 100;
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[VRAM Calculator] Bulk compression changed {changed} texture(s).");
            Calc(_avatar);
        }

        // Build one backup entry from a texture's current importer settings
        static BulkBackupEntry MakeBackupEntry(string path, TextureImporter importer, bool includeFormat)
        {
            TextureImporterPlatformSettings pc = importer.GetPlatformTextureSettings("PC");
            BulkBackupEntry entry = new BulkBackupEntry
            {
                guid = AssetDatabase.AssetPathToGUID(path),
                path = path,
                maxSize = importer.maxTextureSize,
                pcMaxSize = pc.maxTextureSize
            };

            if (includeFormat)
            {
                entry.restoreFormat = true;
                entry.pcOverridden = pc.overridden;
                entry.pcFormat = (int)pc.format;
                entry.pcQuality = pc.compressionQuality;
            }
            return entry;
        }

        // Write a backup file, prune old ones and mark the cached info dirty. Returns false on failure
        bool WriteBackupFile(BulkBackup backup)
        {
            try
            {
                Directory.CreateDirectory(BackupDir);
                // Milliseconds in the name so two backups in the same second can't overwrite each other
                string file = Path.Combine(BackupDir, $"bulk_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
                File.WriteAllText(file, JsonUtility.ToJson(backup, true));
                PruneBackups();
                _backupDirty = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VRAM Calculator] Could not write backup: {e.Message}");
                return false;
            }
        }

        // Newest backup file, or null if there is none. File names start with a timestamp, so name order = time order
        static string GetNewestBackupFile()
        {
            if (!Directory.Exists(BackupDir)) return null;
            string[] files = Directory.GetFiles(BackupDir, "bulk_*.json");
            if (files.Length == 0) return null;
            Array.Sort(files);
            return files[files.Length - 1];
        }

        // Save the current sizes of every texture that is about to change in a bulk resize. Returns false if writing failed
        bool SaveBackup(List<TextureInfo> targets, float scale, string label)
        {
            BulkBackup backup = new BulkBackup { label = label, timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

            foreach (TextureInfo t in targets)
            {
                string path = AssetDatabase.GetAssetPath(t.texture);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                // Only back up textures that will actually change
                int resolution = Mathf.Max(t.texture.width, t.texture.height);
                if (GetScaledSize(resolution, scale) == resolution) continue;

                backup.entries.Add(MakeBackupEntry(path, importer, false));
            }

            if (backup.entries.Count == 0) return true; // Nothing will change, nothing to back up
            return WriteBackupFile(backup);
        }

        // Manual backup: snapshot size + PC compression of every texture on the avatar, regardless of the toggle and threshold
        void CreateManualBackup()
        {
            BulkBackup backup = new BulkBackup { label = "Manual backup", timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

            foreach (TextureInfo t in _texturesList)
            {
                if (!(t.texture is Texture2D)) continue;
                string path = AssetDatabase.GetAssetPath(t.texture);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue; // e.g. DDS, render textures

                backup.entries.Add(MakeBackupEntry(path, importer, true));
            }

            if (backup.entries.Count == 0)
            {
                Debug.LogWarning("[VRAM Calculator] No textures to back up.");
                return;
            }

            if (WriteBackupFile(backup))
                Debug.Log($"[VRAM Calculator] Manual backup saved ({backup.entries.Count} textures).");
        }

        // Back up one texture before a per-texture edit (size dropdown, 2k button, compression).
        // Called from ChangeImportSize / ChangeCompression in TextureVRAM.cs.
        // Edits within MERGE_WINDOW_SECONDS of the last single edit are merged into that backup, keeping the oldest state
        void SaveSingleBackup(Texture tex, bool includeFormat)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            BulkBackupEntry entry = MakeBackupEntry(path, importer, includeFormat);

            string newest = GetNewestBackupFile();
            if (newest != null && (DateTime.Now - File.GetLastWriteTime(newest)).TotalSeconds < MERGE_WINDOW_SECONDS)
            {
                try
                {
                    BulkBackup existing = JsonUtility.FromJson<BulkBackup>(File.ReadAllText(newest));
                    if (existing.label == SINGLE_EDIT_LABEL) // Never merge into a bulk backup
                    {
                        BulkBackupEntry old = existing.entries.Find(e => e.guid == entry.guid);
                        if (old == null)
                        {
                            existing.entries.Add(entry);
                        }
                        else if (includeFormat && !old.restoreFormat)
                        {
                            // Earlier edit was size-only, now compression changes too. The format hasn't been touched yet, so the current one is still the original
                            old.restoreFormat = true;
                            old.pcOverridden = entry.pcOverridden;
                            old.pcFormat = entry.pcFormat;
                            old.pcQuality = entry.pcQuality;
                        }
                        // else: this texture is already in the backup with its oldest state, keep that

                        existing.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        File.WriteAllText(newest, JsonUtility.ToJson(existing, true));
                        _backupDirty = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VRAM Calculator] Could not merge into last backup, writing a new one: {e.Message}");
                }
            }

            BulkBackup backup = new BulkBackup { label = SINGLE_EDIT_LABEL, timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            backup.entries.Add(entry);
            WriteBackupFile(backup); // A failed single backup is logged, but doesn't block the edit
        }

        // Keep only the newest MAX_BACKUPS files
        static void PruneBackups()
        {
            if (!Directory.Exists(BackupDir)) return;
            string[] files = Directory.GetFiles(BackupDir, "bulk_*.json");
            Array.Sort(files);
            for (int i = 0; i < files.Length - MAX_BACKUPS; i++)
            {
                try { File.Delete(files[i]); } catch { /* not worth failing over */ }
            }
        }

        // List every backup (newest first) and build the dropdown labels 
        void RefreshBackupInfo()
        {
            _backupDirty = false;
            List<string> files = new List<string>();
            List<string> labels = new List<string>();

            if (Directory.Exists(BackupDir))
            {
                string[] all = Directory.GetFiles(BackupDir, "bulk_*.json");
                Array.Sort(all);
                Array.Reverse(all); // File names start with a timestamp, so reversed = newest first

                foreach (string file in all)
                {
                    try
                    {
                        BulkBackup b = JsonUtility.FromJson<BulkBackup>(File.ReadAllText(file));
                        string newest = files.Count == 0 ? " (newest)" : "";
                        // No "/" in labels, Unity would turn it into a submenu
                        labels.Add($"{files.Count + 1}. {b.timestamp} - {b.label} - {b.entries.Count} textures{newest}");
                        files.Add(file);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[VRAM Calculator] Could not read backup {Path.GetFileName(file)}: {e.Message}");
                    }
                }
            }

            _backupFiles = files.ToArray();
            _backupLabels = labels.ToArray();

        }

        void EnsureExclusionsLoaded()
        {
            if (_exclusionsLoaded) return;
            _exclusionsLoaded = true;
            _excludedGuids = new HashSet<string>();

            try
            {
                if (File.Exists(ExclusionFile))
                {
                    ExclusionData data = JsonUtility.FromJson<ExclusionData>(File.ReadAllText(ExclusionFile));
                    if (data != null && data.guids != null)
                        _excludedGuids = new HashSet<string>(data.guids);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRAM Calculator] Could not read exclusion list: {e.Message}");
            }
        }

        void SaveExclusions()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ExclusionFile));
                File.WriteAllText(ExclusionFile, JsonUtility.ToJson(new ExclusionData { guids = _excludedGuids.ToList() }, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRAM Calculator] Could not save exclusion list: {e.Message}");
            }
        }

        // True if the bulk tools must skip this texture. Cheap when the list is empty
        bool IsExcluded(Texture tex)
        {
            EnsureExclusionsLoaded();
            if (_excludedGuids.Count == 0 || tex == null) return false;
            return _excludedGuids.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex)));
        }

        // Add or remove one texture, save, and invalidate every cached preview
        void SetExcluded(Texture tex, bool excluded)
        {
            EnsureExclusionsLoaded();
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex));
            if (string.IsNullOrEmpty(guid)) return; // Not an asset on disk, nothing to key on

            bool changed = excluded ? _excludedGuids.Add(guid) : _excludedGuids.Remove(guid);
            if (!changed) return;

            SaveExclusions();
            _bulkSavings = null; // RefreshBulkPreview also resets the cap, auto-fit and compression plans
        }

        // Shader names of every material that uses this texture. Cached per texture
        void GetShaderInfo(TextureInfo t, out string label, out string tooltip)
        {
            if (_shaderLabels.TryGetValue(t.texture, out label))
            {
                tooltip = _shaderTooltips[t.texture];
                return;
            }

            if (t.materials == null || t.materials.Count == 0)
            {
                label = "(material swap)";
                tooltip = "Not used by a material on the avatar directly";
            }
            else
            {
                List<string> shaders = t.materials.Where(m => m != null && m.shader != null)
                    .Select(m => m.shader.name.Substring(m.shader.name.LastIndexOf('/') + 1)) // Short name, drop the folder path
                    .Distinct().ToList();
                label = shaders.Count == 0 ? "(none)" : shaders.Count == 1 ? shaders[0] : $"{shaders[0]} +{shaders.Count - 1}";
                tooltip = "Materials:\n" + string.Join("\n", t.materials.Where(m => m != null).Select(m => $"{m.name}  ({(m.shader != null ? m.shader.name : "no shader")})"));
            }

            _shaderLabels[t.texture] = label;
            _shaderTooltips[t.texture] = tooltip;
        }

        // Exclude or include many textures with a single save - Change by - Nommiiee
        void SetExcludedMany(List<TextureInfo> textures, bool excluded)
        {
            EnsureExclusionsLoaded();
            bool changed = false;
            foreach (TextureInfo t in textures)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t.texture));
                if (string.IsNullOrEmpty(guid)) continue;
                changed |= excluded ? _excludedGuids.Add(guid) : _excludedGuids.Remove(guid);
            }
            if (!changed) return;
            SaveExclusions();
            _bulkSavings = null;
        }

        // Appended to the "can't reach the target" messages so it's clear why
        string ExcludedNote()
        {
            int n = CountExcluded();
            return n > 0 ? $" {n} texture(s) are excluded." : "";
        }

    }
}
#endif
