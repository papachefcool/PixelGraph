﻿using Microsoft.Extensions.Logging;
using PixelGraph.Common.Extensions;
using PixelGraph.Common.ImageProcessors;
using PixelGraph.Common.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PixelGraph.Common.Textures.Graphing.Builders
{
    public interface IImportGraphBuilder
    {
        Task ImportAsync(CancellationToken token = default);
    }

    internal class ImportGraphBuilder : TextureGraphBuilder, IImportGraphBuilder
    {
        private readonly ILogger<ImportGraphBuilder> logger;


        public ImportGraphBuilder(
            ILogger<ImportGraphBuilder> logger,
            IServiceProvider provider,
            ITextureGraphContext context,
            ITextureGraph graph,
            IInputReader reader,
            IOutputWriter writer,
            IImageWriter imageWriter)
            //ITextureRegionEnumerator regions)
            : base(provider, context, graph, reader, writer, imageWriter, logger)
        {
            this.logger = logger;
        }
        
        public async Task ImportAsync(CancellationToken token = default)
        {
            Context.ApplyOutputEncoding();

            await ProcessAllTexturesAsync(false, token);
            await CopyPropertiesAsync(token);
            await ImportMetaAsync(token);
        }

        protected override async Task SaveImagePartAsync<TPixel>(Image<TPixel> image, TexturePublishPart part, ImageChannels type, string destFile, string textureTag, CancellationToken token)
        {
            var srcWidth = image.Width;
            var srcHeight = image.Height;

            if (srcWidth == 1 && srcHeight == 1) {
                // TODO: set material values instead?

                await ImageWriter.WriteAsync(image, type, destFile, token);
            }
            else {
                // TODO: set material values instead?

                var firstFrame = part.Frames.First();
                var frameCount = part.Frames.Length;
                var partWidth = (int)(firstFrame.SourceBounds.Width * srcWidth);
                var partHeight = (int)(firstFrame.SourceBounds.Height * srcHeight * frameCount);
                using var regionImage = new Image<TPixel>(partWidth, partHeight);

                var options = new CopyRegionProcessor<TPixel>.Options {
                    SourceImage = image,
                };

                var processor = new CopyRegionProcessor<TPixel>(options);

                foreach (var frame in part.Frames) {
                    options.SourceX = (int) (frame.SourceBounds.X * srcWidth);
                    options.SourceY = (int) (frame.SourceBounds.Y * srcHeight);

                    var outBounds = frame.DestBounds.ScaleTo(partWidth, partHeight);
                    regionImage.Mutate(c => c.ApplyProcessor(processor, outBounds));
                }

                await ImageWriter.WriteAsync(regionImage, type, destFile, token);
            }

            logger.LogInformation("Imported material texture '{destFile}'.", destFile);
        }
    }
}
