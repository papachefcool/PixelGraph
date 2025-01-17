﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PixelGraph.Common.Extensions;
using PixelGraph.Common.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageExtensions = PixelGraph.Common.IO.ImageExtensions;

namespace PixelGraph.Common.Textures.Graphing.Builders
{
    internal abstract class TextureGraphBuilder
    {
        private readonly ILogger logger;

        protected IServiceProvider Provider {get;}
        protected IInputReader Reader {get;}
        protected IOutputWriter Writer {get;}
        protected IImageWriter ImageWriter {get;}
        protected ITextureGraphContext Context {get;}
        protected ITextureGraph Graph {get;}


        protected TextureGraphBuilder(
            IServiceProvider provider,
            ITextureGraphContext context,
            ITextureGraph graph,
            IInputReader reader,
            IOutputWriter writer,
            IImageWriter imageWriter,
            ILogger logger)
        {
            this.logger = logger;

            Provider = provider;
            Reader = reader;
            Writer = writer;
            ImageWriter = imageWriter;
            Context = context;
            Graph = graph;
        }

        /// <summary>
        /// Publishes all textures with mapped output.
        /// </summary>
        /// <returns>An array of all published texture tags.</returns>
        protected async Task ProcessAllTexturesAsync(bool createEmpty, CancellationToken token)
        {
            var allOutputTags = Context.OutputEncoding
                .Select(e => e.Texture).Distinct().ToArray();

            try {
                await Graph.PreBuildNormalTextureAsync(token);
            }
            catch (HeightSourceEmptyException) {}

            foreach (var tag in allOutputTags)
                await Graph.MapAsync(tag, createEmpty, null, null, token);

            Context.MaxFrameCount = Graph.GetMaxFrameCount();
            
            foreach (var tag in allOutputTags) {
                var tagOutputEncoding = Context.OutputEncoding
                    .Where(e => TextureTags.Is(e.Texture, tag)).ToArray();

                if (tagOutputEncoding.Any()) {
                    var hasAlpha = tagOutputEncoding.Any(c => c.Color == ColorChannel.Alpha);
                    var hasColor = tagOutputEncoding.Any(c => c.Color != ColorChannel.Red);

                    if (hasAlpha) {
                        using var image = await Graph.CreateImageAsync<Rgba32>(tag, createEmpty, token);
                        await ProcessTextureAsync(image, tag, ImageChannels.ColorAlpha, token);
                    }
                    else if (hasColor) {
                        using var image = await Graph.CreateImageAsync<Rgb24>(tag, createEmpty, token);
                        await ProcessTextureAsync(image, tag, ImageChannels.Color, token);
                    }
                    else {
                        using var image = await Graph.CreateImageAsync<L8>(tag, createEmpty, token);
                        await ProcessTextureAsync(image, tag, ImageChannels.Gray, token);
                    }
                }

                if (Context.IsAnimated || Context.IsImport)
                    await CopyMetaAsync(tag, token);
            }
        }

        protected virtual async Task ProcessTextureAsync<TPixel>(Image<TPixel> image, string textureTag, ImageChannels type, CancellationToken token = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (image == null) {
                logger.LogWarning("No texture sources found for item {DisplayName} texture {textureTag}.", Context.Material.DisplayName, textureTag);
                return;
            }

            var sourcePath = Context.Material.LocalPath;
            if (!Context.PublishAsGlobal || (Context.IsMaterialCtm && !Context.Material.UseGlobalMatching))
                sourcePath = PathEx.Join(sourcePath, Context.Material.Name);

            var maxFrameCount = Graph.GetMaxFrameCount();
            var usePlaceholder = Context.Material.CTM?.Placeholder ?? false;
            var ext = NamingStructure.GetExtension(Context.Profile);

            var regions = Provider.GetRequiredService<ITextureRegionEnumerator>();
            regions.SourceFrameCount = maxFrameCount;
            regions.DestFrameCount = maxFrameCount;

            foreach (var part in regions.GetAllPublishRegions()) {
                string destFile;
                if (usePlaceholder && part.TileIndex == 0) {
                    var placeholderPath = PathEx.Join("assets", "minecraft", "textures", "block");
                    if (!Context.Mapping.TryMap(placeholderPath, Context.Material.Name, out var destPath, out var destName)) continue;

                    var destTagName = NamingStructure.Get(textureTag, destName, ext, Context.PublishAsGlobal);
                    destFile = PathEx.Join(destPath, destTagName);
                }
                else {
                    if (!Context.Mapping.TryMap(sourcePath, part.Name, out var destPath, out var destName)) continue;

                    var destTagName = NamingStructure.Get(textureTag, destName, ext, Context.PublishAsGlobal);
                    destFile = PathEx.Join(destPath, destTagName);
                }

                await SaveImagePartAsync(image, part, type, destFile, textureTag, token);
            }

            //if (Context.Material.PublishInventory ?? false) {
            //    var partFrame = Regions.GetPublishPartFrame(0, maxFrameCount, 0);
            //    // TODO: create inventory texture?
            //}
        }

        protected abstract Task SaveImagePartAsync<TPixel>(Image<TPixel> image, TexturePublishPart part, ImageChannels type, string destFile, string textureTag, CancellationToken token)
            where TPixel : unmanaged, IPixel<TPixel>;

        protected async Task CopyPropertiesAsync(CancellationToken token)
        {
            var propsFileIn = NamingStructure.GetInputPropertiesName(Context.Material);
            if (!Reader.FileExists(propsFileIn)) return;

            var propsFileOut = NamingStructure.GetOutputPropertiesName(Context.Material, Context.PublishAsGlobal);

            await using var sourceStream = Reader.Open(propsFileIn);
            await Writer.OpenAsync(propsFileOut, async destStream => {
                await sourceStream.CopyToAsync(destStream, token);
            }, token);
        }

        protected async Task ImportMetaAsync(CancellationToken token)
        {
            var path = Context.Material.LocalPath;

            foreach (var file in Reader.EnumerateFiles(path, "*.mcmeta")) {
                var name = Path.GetFileNameWithoutExtension(file);

                var ext = Path.GetExtension(name);
                if (!ImageExtensions.Supports(ext)) continue;

                name = Path.GetFileNameWithoutExtension(name);
                if (!Context.Material.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) continue;

                var metaFileOut = NamingStructure.GetInputMetaName(Context.Material);
                await CopyMetaFileAsync(file, metaFileOut, token);
            }
        }

        protected async Task CopyMetaAsync(string tag, CancellationToken token)
        {
            var metaFileIn = NamingStructure.GetInputMetaName(Context.Material, tag);

            if (!Reader.FileExists(metaFileIn)) {
                metaFileIn = NamingStructure.GetInputMetaName(Context.Material);
                if (!Reader.FileExists(metaFileIn)) return;
            }

            var partNames = Context.IsMaterialMultiPart
                ? Context.Material.Parts.Select(p => p.Name)
                : Enumerable.Repeat(Context.Material.Name, 1);

            foreach (var partName in partNames) {
                var metaFileOut = NamingStructure.GetOutputMetaName(Context.Profile, Context.Material, partName, tag, Context.PublishAsGlobal);
                await CopyMetaFileAsync(metaFileIn, metaFileOut, token);
            }
        }

        private async Task CopyMetaFileAsync(string metaFileIn, string metaFileOut, CancellationToken token)
        {
            await using var sourceStream = Reader.Open(metaFileIn);
            await Writer.OpenAsync(metaFileOut, async destStream => {
                await sourceStream.CopyToAsync(destStream, token);
            }, token);
        }
    }
}
