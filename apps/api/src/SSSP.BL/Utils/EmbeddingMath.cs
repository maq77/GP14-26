using System;
using System.Collections.Generic;

namespace SSSP.BL.Utils
{
    public static class EmbeddingMath
    {
        public static byte[] ToByteArray(IReadOnlyList<float> embedding)
        {
            if (embedding == null || embedding.Count == 0)
                return Array.Empty<byte>();

            var bytes = new byte[embedding.Count * sizeof(float)];
            Buffer.BlockCopy(embedding.ToArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static float[] ByteArrayToFloatArray(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return Array.Empty<float>();

            var floats = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }

        public static double ComputeCosineSimilarity(
            IReadOnlyList<float> a,
            IReadOnlyList<float> b)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0)
                return 0.0;

            if (a.Count != b.Count)
                return 0.0;

            double dot = 0;
            double normA = 0;
            double normB = 0;

            for (var i = 0; i < a.Count; i++)
            {
                var x = a[i];
                var y = b[i];

                dot += x * y;
                normA += x * x;
                normB += y * y;
            }

            if (normA == 0 || normB == 0)
                return 0.0;

            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}
