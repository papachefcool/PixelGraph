﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PixelGraph.Common.Material;
using PixelGraph.Common.ResourcePack;
using PixelGraph.Common.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PixelGraph.Common.IO.Publishing
{
    public interface IPublisher
    {
        Task PublishAsync(ResourcePackContext context, bool clean, CancellationToken token = default);
    }

    internal class Publisher : IPublisher
    {
        private readonly IServiceProvider provider;
        private readonly IInputReader reader;
        private readonly IOutputWriter writer;
        private readonly ILogger logger;
        private readonly IFileLoader loader;


        public Publisher(
            ILogger<Publisher> logger,
            IServiceProvider provider,
            IFileLoader loader,
            IInputReader reader,
            IOutputWriter writer)
        {
            this.provider = provider;
            this.loader = loader;
            this.reader = reader;
            this.writer = writer;
            this.logger = logger;
        }

        public async Task PublishAsync(ResourcePackContext context, bool clean, CancellationToken token = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            loader.EnableAutoMaterial = context.AutoMaterial;

            if (clean) {
                logger.LogDebug("Cleaning destination...");

                try {
                    writer.Clean();

                    logger.LogInformation("Destination directory clean.");
                }
                catch (Exception error) {
                    logger.LogError(error, "Failed to clean destination!");
                    throw new ApplicationException("Failed to clean destination!", error);
                }
            }

            await PublishPackMetaAsync(context.Profile, token);

            await PublishContentAsync(context, token);
        }

        private async Task PublishContentAsync(ResourcePackContext packContext, CancellationToken token = default)
        {
            var genericPublisher = new GenericTexturePublisher(packContext.Profile, reader, writer);
            var packWriteTime = reader.GetWriteTime(packContext.Profile.LocalFile) ?? DateTime.Now;

            await foreach (var fileObj in loader.LoadAsync(token)) {
                token.ThrowIfCancellationRequested();

                switch (fileObj) {
                    case MaterialProperties material: {
                        using var scope = provider.CreateScope();
                        var graphContext = scope.ServiceProvider.GetRequiredService<ITextureGraphContext>();
                        var graphBuilder = scope.ServiceProvider.GetRequiredService<ITextureGraphBuilder>();

                        graphContext.Input = packContext.Input;
                        graphContext.Profile = packContext.Profile;
                        graphContext.Material = material;
                        graphContext.UseGlobalOutput = packContext.UseGlobalOutput;

                        await graphBuilder.ProcessInputGraphAsync(token);
                        break;
                    }

                    case string localFile:
                        var sourceTime = reader.GetWriteTime(localFile);
                        var destinationTime = writer.GetWriteTime(localFile);

                        var file = Path.GetFileName(localFile);
                        if (fileIgnoreList.Contains(file)) {
                            logger.LogDebug("Skipping ignored file {localFile}.", localFile);
                            continue;
                        }

                        if (IsUpToDate(packWriteTime, sourceTime, destinationTime)) {
                            logger.LogDebug("Skipping up-to-date untracked file {localFile}.", localFile);
                            continue;
                        }

                        if (IsGenericResizable(localFile)) {
                            await genericPublisher.PublishAsync(localFile, token);
                        }
                        else {
                            await using var srcStream = reader.Open(localFile);
                            await using var destStream = writer.Open(localFile);
                            await srcStream.CopyToAsync(destStream, token);
                        }

                        logger.LogInformation("Published untracked file {localFile}.", localFile);
                        break;
                }
            }
        }

        private Task PublishPackMetaAsync(ResourcePackProfileProperties pack, CancellationToken token)
        {
            var packMeta = new PackMetadata {
                PackFormat = pack.Format ?? ResourcePackProfileProperties.DefaultFormat,
                Description = pack.Description ?? string.Empty,
            };

            if (pack.Tags != null) {
                packMeta.Description += $"\n{string.Join(' ', pack.Tags)}";
            }

            var data = new {pack = packMeta};
            using var stream = writer.Open("pack.mcmeta");
            return WriteAsync(stream, data, Formatting.Indented, token);
        }

        private static async Task WriteAsync(Stream stream, object content, Formatting formatting, CancellationToken token)
        {
            await using var writer = new StreamWriter(stream);
            using var jsonWriter = new JsonTextWriter(writer) {Formatting = formatting};

            await JToken.FromObject(content).WriteToAsync(jsonWriter, token);
        }

        private static bool IsUpToDate(DateTime profileWriteTime, DateTime? sourceWriteTime, DateTime? destWriteTime)
        {
            if (!destWriteTime.HasValue || !sourceWriteTime.HasValue) return false;
            if (profileWriteTime > destWriteTime.Value) return false;
            return sourceWriteTime <= destWriteTime.Value;
        }

        private static bool IsGenericResizable(string localFile)
        {
            var extension = Path.GetExtension(localFile);
            if (!ImageExtensions.Supports(extension)) return false;

            // Do not resize pack icon
            var path = Path.GetDirectoryName(localFile);
            var name = Path.GetFileNameWithoutExtension(localFile);
            if (string.IsNullOrEmpty(path) && string.Equals("pack", name, StringComparison.InvariantCultureIgnoreCase)) return false;

            return !resizeIgnoreList.Any(x => localFile.StartsWith(x, StringComparison.InvariantCultureIgnoreCase));
        }

        private static readonly string[] resizeIgnoreList = {
            Path.Combine("assets", "minecraft", "textures", "font"),
            Path.Combine("assets", "minecraft", "textures", "gui"),
            Path.Combine("assets", "minecraft", "textures", "colormap"),
            Path.Combine("assets", "minecraft", "textures", "misc"),
            Path.Combine("assets", "minecraft", "optifine", "colormap"),
            Path.Combine("pack", "minecraft", "optifine", "colormap"),
        };

        private static readonly HashSet<string> fileIgnoreList = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) {
            "input.yml",
            "source.txt",
            "readme.txt",
            "readme.md",
        };
    }
}
