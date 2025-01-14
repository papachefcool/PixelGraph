﻿using HelixToolkit.SharpDX.Core;
using HelixToolkit.SharpDX.Core.Model;
using HelixToolkit.SharpDX.Core.Render;
using HelixToolkit.SharpDX.Core.ShaderManager;
using HelixToolkit.SharpDX.Core.Shaders;
using HelixToolkit.SharpDX.Core.Utilities;
using PixelGraph.Rendering.Shaders;
using SharpDX.Direct3D11;
using System.Runtime.CompilerServices;
using PixelShader = HelixToolkit.SharpDX.Core.Shaders.PixelShader;

namespace PixelGraph.Rendering.Materials
{
    internal class CustomNormalsMaterialVariable : MaterialVariable
    {
        private const int NUMTEXTURES = 2;
        private const int NUMSAMPLERS = 2;

        private const int
            OpacityMapIdx = 0,
            NormalMapIdx = 1;

        private const int
            SurfaceSamplerIdx = 0,
            HeightSamplerIdx = 1;

        private readonly CustomNormalsMaterialCore material;

        private readonly ITextureResourceManager textureManager;
        private readonly IStatePoolManager statePoolManager;
        private readonly ShaderResourceViewProxy[] textureResources;
        private readonly SamplerStateProxy[] samplerResources;

        private int texOpacitySlot, texNormalSlot;
        private int samplerSurfaceSlot, samplerHeightSlot;
        private uint textureIndex;

        public ShaderPass MaterialPass { get; }
        public ShaderPass MaterialOITPass { get; }
        public ShaderPass ShadowPass { get; }
        public ShaderPass WireframePass { get; } 
        public ShaderPass WireframeOITPass { get; }
        public ShaderPass DepthPass { get; }

        private bool HasTextures => textureIndex != 0;


        public CustomNormalsMaterialVariable(IEffectsManager manager, IRenderTechnique technique, CustomNormalsMaterialCore core)
            : base(manager, technique, DefaultMeshConstantBufferDesc, core)
        {
            textureResources = new ShaderResourceViewProxy[NUMTEXTURES];
            samplerResources = new SamplerStateProxy[NUMSAMPLERS];

            textureManager = manager.MaterialTextureManager;
            statePoolManager = manager.StateManager;
            material = core;

            MaterialPass = technique[CustomPassNames.Normals];
            MaterialOITPass = technique[CustomPassNames.NormalsOIT];
            WireframePass = technique[DefaultPassNames.Wireframe];
            WireframeOITPass = technique[DefaultPassNames.WireframeOITPass];
            ShadowPass = technique[DefaultPassNames.ShadowPass];
            DepthPass = technique[DefaultPassNames.DepthPrepass];

            UpdateMappings(MaterialPass);
            CreateTextureViews();
            CreateSamplers();
        }

        protected override void OnInitialPropertyBindings()
        {
            AddPropertyBinding(nameof(CustomNormalsMaterialCore.OpacityMap), () => {
                CreateTextureView(material.OpacityMap, OpacityMapIdx);
            });

            AddPropertyBinding(nameof(CustomNormalsMaterialCore.NormalHeightMap), () => {
                CreateTextureView(material.NormalHeightMap, NormalMapIdx);
            });

            AddPropertyBinding(nameof(CustomNormalsMaterialCore.SurfaceMapSampler), () => {
                CreateSampler(material.SurfaceMapSampler, SurfaceSamplerIdx);
            });

            AddPropertyBinding(nameof(CustomNormalsMaterialCore.HeightMapSampler), () => {
                CreateSampler(material.HeightMapSampler, HeightSamplerIdx);
            });
        }

        public override bool BindMaterialResources(RenderContext context, DeviceContextProxy deviceContext, ShaderPass shaderPass)
        {
            if (HasTextures) {
                OnBindMaterialTextures(context, deviceContext, shaderPass.PixelShader);
            }

            return true;
        }

        public override ShaderPass GetPass(RenderType renderType, RenderContext context)
        {
            return renderType == RenderType.Transparent && context.IsOITPass
                ? MaterialOITPass : MaterialPass;
        }

        public override ShaderPass GetShadowPass(RenderType renderType, RenderContext context)
        {
            return ShadowPass;
        }

        public override ShaderPass GetWireframePass(RenderType renderType, RenderContext context)
        {
            return renderType == RenderType.Transparent && context.IsOITPass
                ? WireframeOITPass : WireframePass;
        }

        public override ShaderPass GetDepthPass(RenderType renderType, RenderContext context)
        {
            return DepthPass;
        }

        public override void Draw(DeviceContextProxy deviceContext, IAttachableBufferModel bufferModel, int instanceCount)
        {
            DrawIndexed(deviceContext, bufferModel.IndexBuffer.ElementCount, instanceCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateTextureView(TextureModel texture, int index)
        {
            var newTexture = texture == null
                ? null : textureManager.Register(texture);

            RemoveAndDispose(ref textureResources[index]);
            textureResources[index] = Collect(newTexture);

            if (textureResources[index] != null) {
                textureIndex |= 1u << index;
            }
            else {
                textureIndex &= ~(1u << index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMappings(ShaderPass shaderPass)
        {
            texOpacitySlot = shaderPass.PixelShader.ShaderResourceViewMapping.TryGetBindSlot(CustomBufferNames.AlbedoAlphaTB);
            texNormalSlot = shaderPass.PixelShader.ShaderResourceViewMapping.TryGetBindSlot(CustomBufferNames.NormalHeightTB);

            samplerSurfaceSlot = shaderPass.PixelShader.SamplerMapping.TryGetBindSlot(CustomSamplerStateNames.SurfaceSampler);
            samplerHeightSlot = shaderPass.PixelShader.SamplerMapping.TryGetBindSlot(CustomSamplerStateNames.HeightSampler);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnBindMaterialTextures(RenderContext context, DeviceContextProxy deviceContext, PixelShader shader)
        {
            if (shader.IsNULL) return;
            
            shader.BindTexture(deviceContext, texOpacitySlot, textureResources[OpacityMapIdx]);
            shader.BindTexture(deviceContext, texNormalSlot, textureResources[NormalMapIdx]);

            shader.BindSampler(deviceContext, samplerSurfaceSlot, samplerResources[SurfaceSamplerIdx]);
            shader.BindSampler(deviceContext, samplerHeightSlot, samplerResources[HeightSamplerIdx]);
        }

        private void CreateTextureViews()
        {
            if (material != null) {
                CreateTextureView(material.OpacityMap, OpacityMapIdx);
                CreateTextureView(material.NormalHeightMap, NormalMapIdx);
            }
            else {
                for (var i = 0; i < NUMTEXTURES; ++i)
                    RemoveAndDispose(ref textureResources[i]);

                textureIndex = 0;
            }
        }

        private void CreateSamplers()
        {
            var newSurfaceSampler = statePoolManager.Register(material.SurfaceMapSampler);
            var newHeightSampler = statePoolManager.Register(material.HeightMapSampler);

            RemoveAndDispose(ref samplerResources[SurfaceSamplerIdx]);
            RemoveAndDispose(ref samplerResources[HeightSamplerIdx]);

            if (material != null) {
                samplerResources[SurfaceSamplerIdx] = Collect(newSurfaceSampler);
                samplerResources[HeightSamplerIdx] = Collect(newHeightSampler);
            }
        }

        private void CreateSampler(SamplerStateDescription desc, int index)
        {
            var newRes = statePoolManager.Register(desc);
            RemoveAndDispose(ref samplerResources[index]);
            samplerResources[index] = Collect(newRes);
        }
    }
}
