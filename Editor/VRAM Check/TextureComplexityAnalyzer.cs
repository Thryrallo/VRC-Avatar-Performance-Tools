#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Thry.AvatarHelpers
{
    /// <summary>
    /// Based On and Inspired from /// https://github.com/Limitex/avatar-compressor
    /// Lightweight editor-only texture complexity estimator inspired by the metrics used by
    /// Avatar Compressor (luminance variance, edge strength, high-frequency detail, and detail
    /// density). It deliberately analyzes a small GPU-downsampled sample instead of requiring
    /// TextureImporter.isReadable. Scores are heuristic 0..1 values, not byte-for-byte parity
    /// with Avatar Compressor's private analysis backends.
    ///
    /// Changes in this version:
    ///  - The sample is built with a chain of half-size blits (a proper 2x2 box filter per step)
    ///    instead of one huge bilinear blit. A single 4096 -> 96 blit only touches a few texels per
    ///    output pixel, so fine detail turned into noise and the scores were unreliable.
    ///  - Colour of (semi-)transparent pixels no longer counts as detail: luminance is blended
    ///    towards mid grey by alpha, so hidden RGB behind a cutout does not inflate the score.
    ///  - The readback texture and the luminance buffer are reused between calls. Call
    ///    ReleaseResources() when a batch of analyses is finished.
    ///
    /// Known limitation: normal maps are scored on their raw channels (DXT5nm/BC5 layouts are not decoded).
    /// </summary>
    internal static class TextureComplexityAnalyzer
    {
        const int SampleSize = 96;

        static Texture2D s_sample;
        static float[] s_lum;

        /// <summary>Frees the reusable readback texture. Safe to call any time.</summary>
        public static void ReleaseResources()
        {
            if (s_sample != null) UnityEngine.Object.DestroyImmediate(s_sample);
            s_sample = null;
            s_lum = null;
        }

        // Repeatedly halves the texture on the GPU until it is SampleSize x SampleSize.
        // Every step averages exactly 2x2 texels, so no detail is skipped over.
        static RenderTexture DownsampleToSample(Texture2D source)
        {
            Texture src = source;
            RenderTexture prev = null;
            int size = Mathf.Max(source.width, source.height);
            do
            {
                size = Mathf.Max(SampleSize, size / 2);
                RenderTexture next = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                next.filterMode = FilterMode.Bilinear;
                Graphics.Blit(src, next);
                if (prev != null) RenderTexture.ReleaseTemporary(prev);
                prev = next;
                src = next;
            } while (size > SampleSize);
            return prev;
        }

        public static bool TryAnalyze(Texture2D source, out float score, out string error)
        {
            score = 0f;
            error = null;
            if (source == null)
            {
                error = "Texture is missing.";
                return false;
            }

            RenderTexture final = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                final = DownsampleToSample(source);
                RenderTexture.active = final;
                if (s_sample == null)
                {
                    s_sample = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false, true)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                s_sample.ReadPixels(new Rect(0, 0, SampleSize, SampleSize), 0, 0, false);
                s_sample.Apply(false, false);
                Color32[] pixels = s_sample.GetPixels32();
                if (pixels == null || pixels.Length != SampleSize * SampleSize)
                {
                    error = "Could not read the texture sample.";
                    return false;
                }

                int count = pixels.Length;
                double sum = 0.0, sumSq = 0.0;
                double edgeSum = 0.0, highFreqSum = 0.0;
                int edgeCount = 0, detailBlocks = 0, blocks = 0;
                int transparent = 0;
                if (s_lum == null || s_lum.Length != count) s_lum = new float[count];
                float[] lum = s_lum;
                for (int i = 0; i < count; i++)
                {
                    Color32 p = pixels[i];
                    float l = (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
                    // Hidden colour behind transparent pixels is not visible detail.
                    l = Mathf.Lerp(0.5f, l, p.a / 255f);
                    lum[i] = l;
                    sum += l;
                    sumSq += l * l;
                    if (p.a < 16) transparent++;
                }

                double mean = sum / count;
                double variance = Math.Max(0.0, sumSq / count - mean * mean);
                double stdDev = Math.Sqrt(variance);
                for (int y = 1; y < SampleSize - 1; y++)
                {
                    for (int x = 1; x < SampleSize - 1; x++)
                    {
                        int i = y * SampleSize + x;
                        float gx = -lum[i - SampleSize - 1] + lum[i - SampleSize + 1]
                                   - 2f * lum[i - 1] + 2f * lum[i + 1]
                                   - lum[i + SampleSize - 1] + lum[i + SampleSize + 1];
                        float gy = -lum[i - SampleSize - 1] - 2f * lum[i - SampleSize] - lum[i - SampleSize + 1]
                                   + lum[i + SampleSize - 1] + 2f * lum[i + SampleSize] + lum[i + SampleSize + 1];
                        float edge = Mathf.Sqrt(gx * gx + gy * gy) * 0.25f;
                        edgeSum += edge;
                        if (edge > 0.08f) edgeCount++;
                        float localAverage = (lum[i - 1] + lum[i + 1] + lum[i - SampleSize] + lum[i + SampleSize]) * 0.25f;
                        highFreqSum += Mathf.Abs(lum[i] - localAverage);
                    }
                }

                const int block = 8;
                for (int by = 0; by < SampleSize; by += block)
                {
                    for (int bx = 0; bx < SampleSize; bx += block)
                    {
                        double bSum = 0.0, bSq = 0.0;
                        int n = 0;
                        for (int y = by; y < Math.Min(by + block, SampleSize); y++)
                        for (int x = bx; x < Math.Min(bx + block, SampleSize); x++)
                        {
                            float v = lum[y * SampleSize + x];
                            bSum += v; bSq += v * v; n++;
                        }
                        double bMean = bSum / n;
                        double bVar = Math.Max(0.0, bSq / n - bMean * bMean);
                        if (bVar > 0.0025) detailBlocks++;
                        blocks++;
                    }
                }

                double edgeMean = edgeSum / ((SampleSize - 2) * (SampleSize - 2));
                double highFreqMean = highFreqSum / ((SampleSize - 2) * (SampleSize - 2));
                float varianceMetric = Mathf.Clamp01((float)stdDev * 2.4f);
                float edgeMetric = Mathf.Clamp01((float)edgeMean * 3.0f);
                float edgeDensity = edgeCount / (float)((SampleSize - 2) * (SampleSize - 2));
                float highFreqMetric = Mathf.Clamp01((float)highFreqMean * 5.0f);
                float detailDensity = blocks == 0 ? 0f : detailBlocks / (float)blocks;

                // Variance, Sobel edge density/strength, local high-frequency detail, and block detail density.
                score = Mathf.Clamp01(0.25f * varianceMetric + 0.25f * edgeMetric +
                                      0.25f * highFreqMetric + 0.15f * detailDensity + 0.10f * edgeDensity);
                // Sparse alpha textures often contain large empty regions; reduce their apparent detail modestly.
                float transparentRatio = transparent / (float)count;
                if (transparentRatio > 0.60f)
                    score = Mathf.Clamp01(score * 0.90f);
                if (float.IsNaN(score) || float.IsInfinity(score))
                {
                    score = 0f;
                    error = "Complexity analysis returned an invalid score.";
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (final != null) RenderTexture.ReleaseTemporary(final);
            }
        }

        public static int GetRecommendedDivisor(float score, int minDivisor = 1, int maxDivisor = 4,
            float lowThreshold = 0.20f, float highThreshold = 0.60f)
        {
            score = Mathf.Clamp01(score);
            float t;
            if (Mathf.Approximately(highThreshold, lowThreshold)) t = 0.5f;
            else if (score >= highThreshold) t = 0f;
            else if (score <= lowThreshold) t = 1f;
            else t = 1f - (score - lowThreshold) / (highThreshold - lowThreshold);

            float logMin = Mathf.Log(Mathf.Max(1, minDivisor), 2f);
            float logMax = Mathf.Log(Mathf.Max(minDivisor, maxDivisor), 2f);
            int divisor = Mathf.RoundToInt(Mathf.Pow(2f, Mathf.Round(Mathf.Lerp(logMin, logMax, t))));
            int min = Mathf.Max(1, minDivisor);
            int max = Mathf.Max(min, maxDivisor);
            return Mathf.Clamp(divisor, min, max);
        }
    }
}
#endif
