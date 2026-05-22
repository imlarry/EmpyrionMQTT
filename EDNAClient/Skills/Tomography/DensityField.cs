using System;
using System.Collections;

namespace EDNAClient.Skills.Tomography
{
    // Bounded dense scalar field with a populated-mask for sparse iteration.
    // Indexed as field[x + y*W + z*W*H]. The grid is allocated tightly to the
    // structure's bounding box plus a `halo` margin so smoothing can bleed
    // without clipping at the boundary.
    internal sealed class DensityField
    {
        public readonly int W, H, D;
        public readonly int Halo;
        public readonly float[] Field;
        public readonly BitArray Populated;

        public DensityField(int w, int h, int d, int halo)
        {
            W = w; H = h; D = d; Halo = halo;
            Field = new float[w * h * d];
            Populated = new BitArray(w * h * d);
        }

        public int Index(int x, int y, int z) => x + y * W + z * W * H;

        public void Splat(int x, int y, int z, float density)
        {
            int i = Index(x, y, z);
            Field[i] = density;
            Populated[i] = true;
        }

        // Sample at integer voxel coords with edge-clamping.
        public float SampleClamped(int x, int y, int z)
        {
            if (x < 0) x = 0; else if (x >= W) x = W - 1;
            if (y < 0) y = 0; else if (y >= H) y = H - 1;
            if (z < 0) z = 0; else if (z >= D) z = D - 1;
            return Field[Index(x, y, z)];
        }

        // Trilinear sample at fractional coords. Used for vertex normal gradient.
        public float SampleTrilinear(float fx, float fy, float fz)
        {
            int x0 = (int)Math.Floor(fx); int x1 = x0 + 1;
            int y0 = (int)Math.Floor(fy); int y1 = y0 + 1;
            int z0 = (int)Math.Floor(fz); int z1 = z0 + 1;
            float tx = fx - x0, ty = fy - y0, tz = fz - z0;

            float c000 = SampleClamped(x0, y0, z0);
            float c100 = SampleClamped(x1, y0, z0);
            float c010 = SampleClamped(x0, y1, z0);
            float c110 = SampleClamped(x1, y1, z0);
            float c001 = SampleClamped(x0, y0, z1);
            float c101 = SampleClamped(x1, y0, z1);
            float c011 = SampleClamped(x0, y1, z1);
            float c111 = SampleClamped(x1, y1, z1);

            float c00 = c000 * (1 - tx) + c100 * tx;
            float c10 = c010 * (1 - tx) + c110 * tx;
            float c01 = c001 * (1 - tx) + c101 * tx;
            float c11 = c011 * (1 - tx) + c111 * tx;
            float c0  = c00  * (1 - ty) + c10  * ty;
            float c1  = c01  * (1 - ty) + c11  * ty;
            return c0 * (1 - tz) + c1 * tz;
        }

        // Separable 3D Gaussian. Three single-axis passes via a temp buffer.
        // kernel must have odd length; element 0 is the center weight (etc).
        public void GaussianSmooth(float[] kernel)
        {
            int half = kernel.Length / 2;
            var tmp = new float[Field.Length];

            // X pass: Field -> tmp
            for (int z = 0; z < D; z++)
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float acc = 0f;
                        for (int k = -half; k <= half; k++)
                        {
                            int xi = x + k;
                            if (xi < 0) xi = 0; else if (xi >= W) xi = W - 1;
                            acc += Field[xi + y * W + z * W * H] * kernel[k + half];
                        }
                        tmp[x + y * W + z * W * H] = acc;
                    }

            // Y pass: tmp -> Field
            for (int z = 0; z < D; z++)
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                    {
                        float acc = 0f;
                        for (int k = -half; k <= half; k++)
                        {
                            int yi = y + k;
                            if (yi < 0) yi = 0; else if (yi >= H) yi = H - 1;
                            acc += tmp[x + yi * W + z * W * H] * kernel[k + half];
                        }
                        Field[x + y * W + z * W * H] = acc;
                    }

            // Z pass: Field -> tmp
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    for (int z = 0; z < D; z++)
                    {
                        float acc = 0f;
                        for (int k = -half; k <= half; k++)
                        {
                            int zi = z + k;
                            if (zi < 0) zi = 0; else if (zi >= D) zi = D - 1;
                            acc += Field[x + y * W + zi * W * H] * kernel[k + half];
                        }
                        tmp[x + y * W + z * W * H] = acc;
                    }

            Array.Copy(tmp, Field, Field.Length);
        }

        // Returns a 1D Gaussian kernel of half-width `radius` for the given sigma.
        public static float[] MakeGaussian(double sigma, int radius)
        {
            int len = radius * 2 + 1;
            var k = new float[len];
            double sum = 0;
            double twoSig2 = 2 * sigma * sigma;
            for (int i = -radius; i <= radius; i++)
            {
                double v = Math.Exp(-(i * i) / twoSig2);
                k[i + radius] = (float)v;
                sum += v;
            }
            for (int i = 0; i < len; i++) k[i] = (float)(k[i] / sum);
            return k;
        }
    }
}
