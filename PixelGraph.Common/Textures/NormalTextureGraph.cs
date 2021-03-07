﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PixelGraph.Common.Encoding;
using PixelGraph.Common.ImageProcessors;
using PixelGraph.Common.IO;
using PixelGraph.Common.Material;
using PixelGraph.Common.ResourcePack;
using PixelGraph.Common.Samplers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PixelGraph.Common.Textures
{
    public interface INormalTextureGraph
    {
        public Image<Rgb24> Texture {get;}

        Task<bool> TryBuildNormalMapAsync(CancellationToken token = default);
        Task<Image<Rgb24>> GenerateAsync(CancellationToken token = default);
    }

    internal class NormalTextureGraph : INormalTextureGraph, IDisposable
    {
        private readonly IServiceProvider provider;
        private readonly ITextureGraphContext context;
        private readonly IInputReader reader;
        private readonly ILogger logger;

        public Image<Rgb24> Texture {get; private set;}


        public NormalTextureGraph(
            ILogger<NormalTextureGraph> logger,
            IServiceProvider provider,
            IInputReader reader,
            ITextureGraphContext context)
        {
            this.provider = provider;
            this.context = context;
            this.reader = reader;
            this.logger = logger;
        }

        public void Dispose()
        {
            Texture?.Dispose();
        }

        public async Task<bool> TryBuildNormalMapAsync(CancellationToken token = default)
        {
            // Try to compose from existing channels first
            var normalXChannel = context.InputEncoding.FirstOrDefault(e => EncodingChannel.Is(e.ID, EncodingChannel.NormalX));
            var normalYChannel = context.InputEncoding.FirstOrDefault(e => EncodingChannel.Is(e.ID, EncodingChannel.NormalY));
            var normalZChannel = context.InputEncoding.FirstOrDefault(e => EncodingChannel.Is(e.ID, EncodingChannel.NormalZ));

            var hasNormalX = normalXChannel?.HasMapping ?? false;
            var hasNormalY = normalYChannel?.HasMapping ?? false;

            if (hasNormalX && hasNormalY) {
                using var scope = provider.CreateScope();

                var normalContext = scope.ServiceProvider.GetRequiredService<ITextureGraphContext>();

                // make image from normal X & Y; z if found
                normalContext.Input = context.Input;
                normalContext.Profile = context.Profile;
                normalContext.Material = context.Material;

                using var builder = scope.ServiceProvider.GetRequiredService<ITextureBuilder<Rgb24>>();

                builder.InputChannels = new [] {normalXChannel, normalYChannel, normalZChannel}
                    .Where(x => x != null).ToArray();

                builder.OutputChannels = new ResourcePackChannelProperties[] {
                    new ResourcePackNormalXChannelProperties {
                        Texture = TextureTags.Normal,
                        Color = ColorChannel.Red,
                    },
                    new ResourcePackNormalYChannelProperties {
                        Texture = TextureTags.Normal,
                        Color = ColorChannel.Green,
                    },
                    new ResourcePackNormalZChannelProperties {
                        Texture = TextureTags.Normal,
                        Color = ColorChannel.Blue,
                    },
                };

                await builder.BuildAsync(false, token);

                Texture = builder.ImageResult?.CloneAs<Rgb24>();
            }

            var autoGenNormal = context.Profile?.AutoGenerateNormal
                ?? ResourcePackProfileProperties.AutoGenerateNormalDefault;

            if (Texture == null && autoGenNormal)
                Texture = await GenerateAsync(token);

            if (Texture == null) return false;

            var options = new NormalRotateProcessor.Options {
                NormalX = ColorChannel.Red,
                NormalY = ColorChannel.Green,
                NormalZ = ColorChannel.Blue,
                CurveX = (float?)context.Material.Normal?.CurveX ?? 0f,
                CurveY = (float?)context.Material.Normal?.CurveY ?? 0f,
                Noise = (float?)context.Material.Normal?.Noise ?? 0f,
            };

            var processor = new NormalRotateProcessor(options);
            Texture.Mutate(c => c.ApplyProcessor(processor));

            // apply magnitude channels
            var magnitudeChannels = context.OutputEncoding
                .Where(c => TextureTags.Is(c.Texture, TextureTags.Normal))
                .Where(c => c.Color == ColorChannel.Magnitude).ToArray();

            if (magnitudeChannels.Length > 1) {
                logger.LogWarning("Only a single encoding channel can be mapped to normal-magnitude! using first mapping. material={DisplayName}", context.Material.DisplayName);
            }

            var magnitudeChannel = magnitudeChannels.FirstOrDefault();
            if (magnitudeChannel != null) await ApplyMagnitudeAsync(magnitudeChannel, token);

            return true;
        }

        public async Task<Image<Rgb24>> GenerateAsync(CancellationToken token = default)
        {
            logger.LogInformation("Generating normal map for texture {DisplayName}.", context.Material.DisplayName);

            var heightChannel = context.InputEncoding.FirstOrDefault(e => EncodingChannel.Is(e.ID, EncodingChannel.Height));

            if (heightChannel == null || !heightChannel.HasMapping)
                throw new HeightSourceEmptyException("No height sources mapped!");

            Image<Rgba32> heightTexture = null;

            try {
                float scale;
                (heightTexture, scale) = await LoadHeightTextureAsync(heightChannel, token);

                if (heightTexture == null) {
                    var up = new Rgb24(127, 127, 255);
                    var size = context.GetBufferSize(1f);
                    return new Image<Rgb24>(Configuration.Default, size?.Width ?? 1, size?.Height ?? 1, up);
                }

                var builder = new NormalMapBuilder {
                    HeightImage = heightTexture,
                    HeightChannel = heightChannel.Color ?? ColorChannel.None,
                    Filter = context.Material.Normal?.Filter ?? MaterialNormalProperties.DefaultFilter,
                    Strength = (float?)context.Material.Normal?.Strength ?? MaterialNormalProperties.DefaultStrength,
                    WrapX = context.WrapX,
                    WrapY = context.WrapY,

                    // TODO: testing
                    VarianceStrength = 0.998f,
                    LowFreqDownscale = 4,
                    VarianceBlur = 3f,
                };

                // WARN: testing, scale strength by resolution scaling to preserve slope
                builder.Strength *= scale;

                // WARN: temporary hard-coded
                builder.LowFreqStrength = builder.Strength / 4f;

                return builder.Build();
            }
            finally {
                heightTexture?.Dispose();
            }
        }

        private async Task ApplyMagnitudeAsync(ResourcePackChannelProperties magnitudeChannel, CancellationToken token)
        {
            var localFile = reader.EnumerateTextures(context.Material, magnitudeChannel.Texture).FirstOrDefault();

            if (localFile != null) {
                await using var sourceStream = reader.Open(localFile);
                using var sourceImage = await Image.LoadAsync<Rgba32>(Configuration.Default, sourceStream, token);
                if (sourceImage == null) return;

                var inputChannel = context.InputEncoding.FirstOrDefault(e => EncodingChannel.Is(e.ID, magnitudeChannel.ID));
                if (inputChannel == null) return;

                var options = new NormalMagnitudeProcessor<Rgba32>.Options {
                    MagSource = sourceImage,
                    Scale = context.Material.GetChannelScale(magnitudeChannel.ID),
                };

                options.ApplyInputChannel(inputChannel);
                options.ApplyOutputChannel(magnitudeChannel);

                var processor = new NormalMagnitudeProcessor<Rgba32>(options);
                Texture.Mutate(c => c.ApplyProcessor(processor));
            }
            else if (EncodingChannel.Is(magnitudeChannel.ID, EncodingChannel.Occlusion)) {
                var occlusionGraph = provider.GetRequiredService<IOcclusionTextureGraph>();
                await occlusionGraph.ApplyMagnitudeAsync(Texture, magnitudeChannel, token);
            }
            else {
                throw new SourceEmptyException("No sources found for applying normal magnitude!");
            }
        }

        private async Task<(Image<Rgba32>, float)> LoadHeightTextureAsync(ResourcePackChannelProperties channel, CancellationToken token)
        {
            var file = reader.EnumerateTextures(context.Material, TextureTags.Bump).FirstOrDefault();

            if (file == null) {
                file = reader.EnumerateTextures(context.Material, channel.Texture).FirstOrDefault();
                if (file == null) return (null, 0f);
            }

            await using var stream = reader.Open(file);

            Image<Rgba32> heightTexture = null;
            try {
                heightTexture = await Image.LoadAsync<Rgba32>(Configuration.Default, stream, token);
                if (heightTexture == null) throw new SourceEmptyException("No height source textures found!");

                // scale height texture instead of using samplers
                var aspect = (float)heightTexture.Height / heightTexture.Width;
                var bufferSize = context.GetBufferSize(aspect);

                var scale = 1f;
                if (bufferSize.HasValue && heightTexture.Width != bufferSize.Value.Width) {
                    scale = (float)bufferSize.Value.Width / heightTexture.Width;
                    var scaledWidth = (int)MathF.Ceiling(heightTexture.Width * scale);
                    var scaledHeight = (int)MathF.Ceiling(heightTexture.Height * scale);

                    var samplerName = channel?.Sampler ?? context.DefaultSampler;
                    var sampler = Sampler<Rgba32>.Create(samplerName);
                    sampler.Image = heightTexture;
                    sampler.WrapX = context.WrapX;
                    sampler.WrapY = context.WrapY;
                    sampler.RangeX = sampler.RangeY = 1f / scale;

                    var options = new ResizeProcessor<Rgba32>.Options {
                        Sampler = sampler,
                    };

                    var processor = new ResizeProcessor<Rgba32>(options);

                    Image<Rgba32> heightCopy = null;
                    try {
                        heightCopy = heightTexture;
                        heightTexture = new Image<Rgba32>(Configuration.Default, scaledWidth, scaledHeight);
                        heightTexture.Mutate(c => c.ApplyProcessor(processor));
                    }
                    finally {
                        heightCopy?.Dispose();
                    }
                }
                
                return (heightTexture, scale);
            }
            catch (SourceEmptyException) {throw;}
            catch {
                //logger.LogWarning("Failed to load texture {file}!", file);
                heightTexture?.Dispose();
                throw;
            }
        }
    }
}