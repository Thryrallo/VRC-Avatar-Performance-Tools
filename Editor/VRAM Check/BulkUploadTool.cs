#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Thry.AvatarHelpers
{
    // bulk compression and backup/restore. Part of TextureVRAM, see TextureVRAM.cs - Feature by - Nommiiee
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

        // Cached info about the newest backup, so OnGUI never touches the disk
        string _latestBackupFile;
        string _backupSummary = "Restore last backup (none)";
        bool _backupDirty = true;

        const int MAX_BACKUPS = 25;               // Older backups get deleted
        const double MERGE_WINDOW_SECONDS = 10;   // Quick single edits share one backup
        const string SINGLE_EDIT_LABEL = "Single edit";

        // ------------------------------------------------------------------
        // Bulk tool fields
        // ------------------------------------------------------------------

        bool _bulkToolsFoldout; // Open/closed state of the Bulk Tools section

        // Table styles
        GUIStyle _tableHeaderStyle;
        GUIStyle _tableCellStyle;
        GUIStyle _tableSavingStyle;
        GUIStyle _tableLossStyle;

        // Bulk resize
        string[] _bulkScaleLabels = new string[] { "¼ size", "½ size", "2× size", "4× size" };
        float[] _bulkScaleValues = new float[] { 0.25f, 0.5f, 2f, 4f };
        long[] _bulkSavings;               // Estimated bytes saved per button (negative = grows)
        bool _bulkPreviewIncludeInactive;  // Detects when the disabled-objects toggle changed

        // Threshold: bulk tools only touch textures larger than this (0 = all)
        int[] _bulkThresholdValues = new int[] { 0, 512, 1024, 2048, 4096 };
        string[] _bulkThresholdLabels = new string[] { "All sizes", "Larger than 512", "Larger than 1024", "Larger than 2048", "Larger than 4096" };
        int _bulkMinResolution = 0;

        // Auto-fit
        int[] _autoFitTargetValues = new int[] { 40, 75, 110, 150 }; // MiB, matches the PC quality tiers
        string[] _autoFitTargetLabels = new string[] { "Excellent (under 40 MB)", "Good (under 75 MB)", "Medium (under 110 MB)", "Poor (under 150 MB)" };
        int _autoFitTargetMiB = 75;
        const int AUTOFIT_MIN_SIZE = 512;              // Never shrink below this (longest side)
        Dictionary<Texture, int> _autoFitPlan;         // Cached plan, rebuilt lazily when null
        long _autoFitProjected;                        // Estimated texture memory after the plan

        // Bulk compression
        Dictionary<Texture, TextureImporterFormat> _compressionPlan; // Cached plan, rebuilt lazily when null
        long _compressionSavings;                                    // Estimated bytes saved by the plan

        // ------------------------------------------------------------------
        // Hooks called from TextureVRAM.cs
        // ------------------------------------------------------------------

        // Called from InitilizeStyles()
        void InitBulkStyles()
        {
            _tableHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            _tableCellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
            _tableSavingStyle = new GUIStyle(_tableCellStyle);
            _tableSavingStyle.normal.textColor = new UnityEngine.Color(0.35f, 0.8f, 0.35f);
            _tableLossStyle = new GUIStyle(_tableCellStyle);
            _tableLossStyle.normal.textColor = new UnityEngine.Color(0.9f, 0.45f, 0.4f);
        }

        // Called at the start of Calc(): cached previews and backup info are out of date after any recalculation
        void OnBulkRecalculated()
        {
            _bulkSavings = null;
            _backupDirty = true;
        }

        // Called from OnGUI(): the whole Bulk Tools section, above Textures
        void DrawBulkTools()
        {
            _bulkToolsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_bulkToolsFoldout, "Bulk Tools (resize, backup, auto-fit, compression) - Feature By Nommiiee");
            if (_bulkToolsFoldout)
            {
                // Threshold dropdown, changing it invalidates the cached preview
                EditorGUI.BeginChangeCheck();
                _bulkMinResolution = EditorGUILayout.IntPopup("Only touch textures", _bulkMinResolution, _bulkThresholdLabels, _bulkThresholdValues);
                if (EditorGUI.EndChangeCheck())
                    _bulkSavings = null;

                // Rebuild the cached preview only when needed, not every repaint
                if (_bulkSavings == null || _bulkPreviewIncludeInactive != _includeInactive)
                    RefreshBulkPreview();

                // Bulk resize button row
                EditorGUILayout.LabelField("Resize all textures at once (savings are estimates)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < _bulkScaleLabels.Length; i++)
                    {
                        bool growing = _bulkScaleValues[i] > 1f;
                        string tooltip = growing
                            ? "Upper bound. Textures can't grow past their source image."
                            : "Applies this resize to all matching textures";

                        if (GUILayout.Button(new GUIContent(_bulkScaleLabels[i], tooltip), GUILayout.Height(25)))
                        {
                            ChangeAllImportSizes(_bulkScaleValues[i], _bulkScaleLabels[i], _bulkSavings[i]);
                            GUIUtility.ExitGUI(); // Lists get rebuilt, so stop drawing this frame
                        }
                    }
                }

                // Manual backup button, right before the restore button
                if (GUILayout.Button(new GUIContent("Create backup now", "Saves the current size and compression of every texture so you can restore it later"), GUILayout.Height(22)))
                    CreateManualBackup();

                // Restore button, disabled when there is no backup
                if (_backupDirty) RefreshBackupInfo();
                using (new EditorGUI.DisabledGroupScope(_latestBackupFile == null))
                {
                    if (GUILayout.Button(new GUIContent(_backupSummary, "Puts the texture settings back to how they were before the last change"), GUILayout.Height(22)))
                    {
                        RestoreLastBackup();
                        GUIUtility.ExitGUI();
                    }
                }

                DrawBulkPreviewTable();
                DrawBulkCompression();
                DrawAutoFit();
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndFoldoutHeaderGroup(); // Must always be called, even when the section is closed
        }

        // ------------------------------------------------------------------
        // Bulk resize
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
                $"{estimate}\n\nA backup is saved first, so you can undo it with 'Restore last backup'.", "Yes", "No"))
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

        // Scale a resolution, round to nearest power of two, clamp to 32-8192
        static int GetScaledSize(int resolution, float scale) =>
            Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(resolution * scale)), 32, 8192);

        // Single source of truth for which textures the bulk tools touch, so preview and apply agree.
        // Respects the disabled-objects toggle and the size threshold
        List<TextureInfo> GetBulkTargets() => _texturesList
            .Where(t => (_includeInactive || t.isActive)
                    && t.texture is Texture2D
                    && Mathf.Max(t.texture.width, t.texture.height) > _bulkMinResolution
                    && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t.texture)) is TextureImporter)
            .ToList();

        // Compute the estimated VRAM change for each bulk button and cache it
        void RefreshBulkPreview()
        {
            List<TextureInfo> targets = GetBulkTargets();
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
            _compressionPlan = null;
        }

        // Format the savings text ("-38 MB", "up to +12 MB", "no change")
        static string FormatSavings(long bytes, bool isUpperBound)
        {
            if (bytes == 0) return "no change";
            string size = AvatarEvaluator.ToShortMebiByteString(Math.Abs(bytes));
            if (bytes > 0) return $"-{size}";
            return isUpperBound ? $"up to +{size}" : $"+{size}";
        }

        // Current Usage / Updated Usage / Estimated Saving / PC Rating for every bulk option
        void DrawBulkPreviewTable()
        {
            const float labelWidth = 80;
            const float colMinWidth = 90;
            const float ratingWidth = 70;

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Resize", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    GUILayout.Label("Current Usage", _tableHeaderStyle, GUILayout.MinWidth(colMinWidth));
                    GUILayout.Label("Updated Usage", _tableHeaderStyle, GUILayout.MinWidth(colMinWidth));
                    GUILayout.Label("Estimated Saving", _tableHeaderStyle, GUILayout.MinWidth(colMinWidth));
                    GUILayout.Label("PC Rating", _tableHeaderStyle, GUILayout.Width(ratingWidth));
                }

                for (int i = 0; i < _bulkScaleLabels.Length; i++)
                {
                    bool growing = _bulkScaleValues[i] > 1f;
                    long saving = _bulkSavings[i];
                    long updated = _sizeAllTextures - saving;
                    string upTo = growing && saving < 0 ? "up to " : ""; // Growing is only an upper bound

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(_bulkScaleLabels[i], GUILayout.Width(labelWidth));
                        GUILayout.Label(AvatarEvaluator.ToMebiByteString(_sizeAllTextures), _tableCellStyle, GUILayout.MinWidth(colMinWidth));
                        GUILayout.Label(upTo + AvatarEvaluator.ToMebiByteString(updated), _tableCellStyle, GUILayout.MinWidth(colMinWidth));
                        GUILayout.Label(FormatSavings(saving, growing), saving > 0 ? _tableSavingStyle : saving < 0 ? _tableLossStyle : _tableCellStyle, GUILayout.MinWidth(colMinWidth));

                        using (new EditorGUILayout.HorizontalScope(GUILayout.Width(ratingWidth)))
                        {
                            GUILayout.FlexibleSpace(); // Right align under the header
                            AvatarEvaluator.DrawQualityIcon(GetTextureQuality(updated, false));
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Auto-fit
        // ------------------------------------------------------------------

        // Greedy plan: repeatedly halve the biggest texture that can still shrink until the target is met or nothing is left
        Dictionary<Texture, int> BuildAutoFitPlan(long targetBytes, out long projected)
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

            while (projected > targetBytes)
            {
                // Pick the biggest texture that can still be halved without going under the floor
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

        // Rebuild the cached plan for the selected target
        void RefreshAutoFitPlan()
        {
            _autoFitPlan = BuildAutoFitPlan(_autoFitTargetMiB * 1048576L, out _autoFitProjected);
        }

        // Auto-fit UI: target dropdown, plan summary and the apply button
        void DrawAutoFit()
        {
            if (_autoFitPlan == null) RefreshAutoFitPlan();

            EditorGUILayout.LabelField("Auto-fit to target", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _autoFitTargetMiB = EditorGUILayout.IntPopup("Target texture memory", _autoFitTargetMiB, _autoFitTargetLabels, _autoFitTargetValues);
            if (EditorGUI.EndChangeCheck())
                RefreshAutoFitPlan();

            long targetBytes = _autoFitTargetMiB * 1048576L;
            bool reached = _autoFitProjected <= targetBytes;

            using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
            {
                if (_sizeAllTextures <= targetBytes)
                {
                    GUILayout.Label("Already under the target, nothing to do.");
                }
                else
                {
                    string result = $"{AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} → {AvatarEvaluator.ToMebiByteString(_autoFitProjected)} ({_autoFitPlan.Count} texture(s) resized)";
                    GUILayout.Label(reached ? result : result + $". Can't reach the target without going below {AUTOFIT_MIN_SIZE}px.");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("PC", GUILayout.Width(20));
                    AvatarEvaluator.DrawQualityIcon(GetTextureQuality(_autoFitProjected, false));
                }
            }

            using (new EditorGUI.DisabledGroupScope(_autoFitPlan.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("Auto-fit", "Shrinks the biggest textures first until the estimate is under the target"), GUILayout.Height(25)))
                {
                    ApplyAutoFit();
                    GUIUtility.ExitGUI();
                }
            }
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

        // Apply the cached plan with a confirm dialog and a backup first
        void ApplyAutoFit()
        {
            if (_autoFitPlan == null || _autoFitPlan.Count == 0) return;

            Dictionary<Texture, int> plan = new Dictionary<Texture, int>(_autoFitPlan); // Copy, Calc() resets the cache
            string label = $"Auto-fit {_autoFitTargetMiB} MB";

            if (!EditorUtility.DisplayDialog("Confirm Auto-fit",
                $"This lowers the max import size of {plan.Count} texture(s), biggest first, to bring texture memory from " +
                $"{AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} to about {AvatarEvaluator.ToMebiByteString(_autoFitProjected)} (target {_autoFitTargetMiB} MB).\n\n" +
                "A backup is saved first, so you can undo it with 'Restore last backup'.", "Yes", "No"))
                return;

            if (!SaveAutoFitBackup(plan, label) &&
                !EditorUtility.DisplayDialog("Backup Failed",
                    "Could not save a backup of the current texture sizes (see the console).\n\nAuto-fit anyway? You won't be able to restore.", "Auto-fit anyway", "Cancel"))
                return;

            int changed = 0, step = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (KeyValuePair<Texture, int> kv in plan)
                {
                    EditorUtility.DisplayProgressBar("Auto-fit", kv.Key.name, step++ / (float)plan.Count);

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

            Debug.Log($"[VRAM Calculator] Auto-fit resized {changed} texture(s) towards {_autoFitTargetMiB} MB.");
            Calc(_avatar);
        }

        // ------------------------------------------------------------------
        // Bulk compression
        // ------------------------------------------------------------------

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

        // Bulk compression UI: before -> after summary, PC rating icon and the apply button
        void DrawBulkCompression()
        {
            if (_compressionPlan == null) RefreshCompressionPlan();

            EditorGUILayout.LabelField("Bulk compression", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
            {
                if (_compressionPlan.Count == 0)
                {
                    GUILayout.Label("Nothing to compress, all matching textures are already as small as they can be.");
                }
                else
                {
                    long updated = _sizeAllTextures - _compressionSavings;
                    GUILayout.Label($"{AvatarEvaluator.ToMebiByteString(_sizeAllTextures)} → {AvatarEvaluator.ToMebiByteString(updated)} " +
                                    $"(-{AvatarEvaluator.ToShortMebiByteString(_compressionSavings)}, {_compressionPlan.Count} texture(s))");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("PC", GUILayout.Width(20));
                    AvatarEvaluator.DrawQualityIcon(GetTextureQuality(updated, false));
                }
            }

            using (new EditorGUI.DisabledGroupScope(_compressionPlan.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("Compress all", "DXT1 for opaque textures, BC7 for textures with alpha or normal maps"), GUILayout.Height(25)))
                {
                    ApplyBulkCompression();
                    GUIUtility.ExitGUI();
                }
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
                "A backup is saved first, so you can undo it with 'Restore last backup'.", "Yes", "No"))
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

        // ------------------------------------------------------------------
        // Backup and restore
        // ------------------------------------------------------------------

        // Backups live in Library/ so Unity doesn't import them and they stay out of version control
        static string BackupDir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "VRAMBackups");

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

        // Find the newest backup and cache a label for the restore button
        void RefreshBackupInfo()
        {
            _backupDirty = false;
            _latestBackupFile = null;
            _backupSummary = "Restore last backup (none)";

            string newest = GetNewestBackupFile();
            if (newest == null) return;

            try
            {
                BulkBackup backup = JsonUtility.FromJson<BulkBackup>(File.ReadAllText(newest));
                _latestBackupFile = newest;
                _backupSummary = $"Restore last backup ({backup.entries.Count} textures, {backup.label}, {backup.timestamp})";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRAM Calculator] Could not read backup: {e.Message}");
            }
        }

        // Put the saved settings back in one batch. Textures that were deleted are skipped and counted
        void RestoreLastBackup()
        {
            if (_latestBackupFile == null || !File.Exists(_latestBackupFile)) return;

            BulkBackup backup;
            try { backup = JsonUtility.FromJson<BulkBackup>(File.ReadAllText(_latestBackupFile)); }
            catch (Exception e)
            {
                Debug.LogError($"[VRAM Calculator] Could not read backup: {e.Message}");
                return;
            }

            if (!EditorUtility.DisplayDialog("Restore Backup",
                $"Restore {backup.entries.Count} texture(s) from the '{backup.label}' change made on {backup.timestamp}?\n\n" +
                "Any changes made since then to these textures will be overwritten.", "Restore", "Cancel"))
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
                    if (importer == null) { skipped++; continue; } // Deleted or no longer a texture

                    importer.maxTextureSize = entry.maxSize;
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("PC");
                    settings.maxTextureSize = entry.pcMaxSize;

                    // Only compression edits saved the format, size-only entries leave it alone
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

            // The backup is used up, so the next "last backup" is the one before it
            try { File.Delete(_latestBackupFile); } catch { }

            Debug.Log($"[VRAM Calculator] Restored {restored} texture(s), skipped {skipped} (deleted or missing).");
            Calc(_avatar); // Also marks the backup info dirty
        }
    }
}
#endif