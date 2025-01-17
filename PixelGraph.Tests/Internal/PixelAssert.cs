﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PixelGraph.Tests.Internal
{
    internal static class PixelAssert
    {
        public static void Equals(byte expectedGray, Image<Rgba32> image, int x = 0, int y = 0)
        {
            RedEquals(expectedGray, image);
            GreenEquals(expectedGray, image);
            BlueEquals(expectedGray, image);
        }

        public static void Equals(byte expectedRed, byte expectedGreen, byte expectedBlue, Image<Rgba32> image, int x = 0, int y = 0)
        {
            RedEquals(expectedRed, image, x, y);
            GreenEquals(expectedGreen, image, x, y);
            BlueEquals(expectedBlue, image, x, y);
        }

        public static void Equals(byte expectedRed, byte expectedGreen, byte expectedBlue, byte expectedAlpha, Image<Rgba32> image, int x = 0, int y = 0)
        {
            RedEquals(expectedRed, image, x, y);
            GreenEquals(expectedGreen, image, x, y);
            BlueEquals(expectedBlue, image, x, y);
            AlphaEquals(expectedAlpha, image, x, y);
        }

        public static void RedEquals(byte expectedValue, Image<Rgba32> image, int x = 0, int y = 0) =>
            Assert.Equal(expectedValue, image[x, y].R);

        public static void GreenEquals(byte expectedValue, Image<Rgba32> image, int x = 0, int y = 0) =>
            Assert.Equal(expectedValue, image[x, y].G);

        public static void BlueEquals(byte expectedValue, Image<Rgba32> image, int x = 0, int y = 0) =>
            Assert.Equal(expectedValue, image[x, y].B);

        public static void AlphaEquals(byte expectedValue, Image<Rgba32> image, int x = 0, int y = 0) =>
            Assert.Equal(expectedValue, image[x, y].A);
    }
}
