﻿using PixelGraph.Common.ResourcePack;
using PixelGraph.Common.Textures;

namespace PixelGraph.Common.TextureFormats.Java
{
    public class LabPbr11Format : ITextureFormatFactory
    {
        public const string Description = "The first LabPbr standard.";


        public ResourcePackEncoding Create()
        {
            return new() {
                Opacity = new ResourcePackOpacityChannelProperties {
                    Texture = TextureTags.Color,
                    Color = ColorChannel.Alpha,
                    MinValue = 0m,
                    MaxValue = 255m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                    DefaultValue = 255,
                },

                ColorRed = new ResourcePackColorRedChannelProperties {
                    Texture = TextureTags.Color,
                    Color = ColorChannel.Red,
                    MinValue = 0m,
                    MaxValue = 255m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                ColorGreen = new ResourcePackColorGreenChannelProperties {
                    Texture = TextureTags.Color,
                    Color = ColorChannel.Green,
                    MinValue = 0m,
                    MaxValue = 255m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                ColorBlue = new ResourcePackColorBlueChannelProperties {
                    Texture = TextureTags.Color,
                    Color = ColorChannel.Blue,
                    MinValue = 0m,
                    MaxValue = 255m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                NormalX = new ResourcePackNormalXChannelProperties {
                    Texture = TextureTags.Normal,
                    Color = ColorChannel.Red,
                    MinValue = 0m,
                    MaxValue = 1m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                NormalY = new ResourcePackNormalYChannelProperties {
                    Texture = TextureTags.Normal,
                    Color = ColorChannel.Green,
                    MinValue = 0m,
                    MaxValue = 1m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                NormalZ = new ResourcePackNormalZChannelProperties {
                    Texture = TextureTags.Normal,
                    Color = ColorChannel.Blue,
                    MinValue = 0m,
                    MaxValue = 1m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                Occlusion = new ResourcePackOcclusionChannelProperties {
                    Texture = TextureTags.Normal,
                    Color = ColorChannel.Magnitude,
                    MinValue = 0m,
                    MaxValue = 1m,
                    RangeMin = 17,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 0.5m,
                    Invert = true,
                },

                Height = new ResourcePackHeightChannelProperties {
                    Texture = TextureTags.Normal,
                    Color = ColorChannel.Alpha,
                    MinValue = 0m,
                    MaxValue = 1m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = true,
                },

                Smooth = new ResourcePackSmoothChannelProperties {
                    Texture = TextureTags.Specular,
                    Color = ColorChannel.Red,
                    MinValue = 0m,
                    MaxValue = 1m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                F0 = new ResourcePackF0ChannelProperties {
                    Texture = TextureTags.Specular,
                    Color = ColorChannel.Green,
                    MinValue = 0m,
                    MaxValue = 0.9m,
                    RangeMin = 0,
                    RangeMax = 229,
                    Shift = 0,
                    Power = 0.5m,
                    Invert = false,
                },

                Metal = new ResourcePackMetalChannelProperties {
                    Texture = TextureTags.Specular,
                    Color = ColorChannel.Green,
                    Sampler = Samplers.Samplers.Nearest,
                    MinValue = 230m,
                    MaxValue = 255m,
                    RangeMin = 230,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                Porosity = new ResourcePackPorosityChannelProperties {
                    Texture = TextureTags.Specular,
                    Color = ColorChannel.Blue,
                    MinValue = 0m,
                    MaxValue = 1m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = 0,
                    Power = 1m,
                    Invert = false,
                },

                //SSS = new ResourcePackSssChannelProperties {
                //    Texture = TextureTags.Specular,
                //    Color = ColorChannel.Alpha,
                //    MinValue = 0m,
                //    MaxValue = 1m,
                //    RangeMin = 0,
                //    RangeMax = 255,
                //    Shift = -1,
                //    Power = 1m,
                //    Invert = false,
                //},

                Emissive = new ResourcePackEmissiveChannelProperties {
                    Texture = TextureTags.Specular,
                    Color = ColorChannel.Alpha,
                    MinValue = 0m,
                    MaxValue = 1m,
                    RangeMin = 0,
                    RangeMax = 255,
                    Shift = -1,
                    Power = 1m,
                    Invert = false,
                },
            };
        }
    }
}
