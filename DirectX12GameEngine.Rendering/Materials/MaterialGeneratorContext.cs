﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DirectX12GameEngine.Core.Assets;
using DirectX12GameEngine.Graphics;
using DirectX12GameEngine.Shaders;
using SharpDX.Direct3D12;
using SharpDX.DXGI;

using PipelineState = DirectX12GameEngine.Graphics.PipelineState;

namespace DirectX12GameEngine.Rendering.Materials
{
    public class MaterialGeneratorContext
    {
        private readonly Stack<IMaterialDescriptor> materialDescriptorStack = new Stack<IMaterialDescriptor>();

        public MaterialGeneratorContext(GraphicsDevice device, Material material, ContentManager contentManager)
        {
            GraphicsDevice = device;
            Material = material;
            Content = contentManager;
        }

        public GraphicsDevice GraphicsDevice { get; }

        public Material Material { get; }

        public ContentManager Content { get; }

        public IList<Graphics.Buffer> ConstantBuffers { get; } = new List<Graphics.Buffer>();

        public IList<GraphicsResource> Samplers { get; } = new List<GraphicsResource>();

        public IList<Texture> Textures { get; } = new List<Texture>();

        public IMaterialDescriptor? MaterialDescriptor => materialDescriptorStack.Count > 0 ? materialDescriptorStack.Peek() : null;

        public MaterialPass? MaterialPass { get; private set; }

        public int PassCount { get; private set; } = 1;

        public int PassIndex { get; private set; }

        public void PushMaterialDescriptor(IMaterialDescriptor descriptor)
        {
            materialDescriptorStack.Push(descriptor);
        }

        public IMaterialDescriptor? PopMaterialDescriptor()
        {
            return materialDescriptorStack.Count > 0 ? materialDescriptorStack.Pop() : null;
        }

        public MaterialPass PushPass()
        {
            MaterialPass materialPass = new MaterialPass { PassIndex = PassIndex };
            Material.Passes.Add(materialPass);

            MaterialPass = materialPass;

            return materialPass;
        }

        public MaterialPass? PopPass()
        {
            PassIndex++;

            MaterialPass? materialPass = MaterialPass;
            MaterialPass = null;

            Textures.Clear();

            return materialPass;
        }

        public async Task<PipelineState> CreateGraphicsPipelineStateAsync()
        {
            if (MaterialDescriptor is null) throw new InvalidOperationException("The current material descriptor cannot be null when creating a pipeline state.");

            InputElement[] inputElements = new[]
            {
                new InputElement("Position", 0, Format.R32G32B32_Float, 0),
                new InputElement("Normal", 0, Format.R32G32B32_Float, 1),
                new InputElement("Tangent", 0, Format.R32G32B32A32_Float, 2),
                new InputElement("TexCoord", 0, Format.R32G32_Float, 3)
            };

            (ShaderBytecode VertexShader, ShaderBytecode PixelShader, ShaderBytecode HullShader, ShaderBytecode DomainShader, ShaderBytecode GeometryShader) shaders = default;

            string fileName = $"Shader_{MaterialDescriptor.MaterialId}";

            ShaderGenerationResult result;

            if (!await Content.ExistsAsync(fileName))
            {
                ShaderGenerator shaderGenerator = new ShaderGenerator(MaterialDescriptor.Attributes);
                result = await Task.Run(() => shaderGenerator.GenerateShader());

                await Content.SaveAsync(fileName, result);
            }
            else
            {
                result = await Content.LoadAsync<ShaderGenerationResult>(fileName);
            }

            string shaderSource = result.ShaderSource ?? throw new Exception();

            shaders.VertexShader = result.VertexShader is null ? throw new Exception("Vertex shader must be present.") : ShaderCompiler.CompileShader(shaderSource, SharpDX.D3DCompiler.ShaderVersion.VertexShader, result.VertexShader);
            shaders.PixelShader = result.PixelShader is null ? throw new Exception("Pixel shader must be present.") : ShaderCompiler.CompileShader(shaderSource, SharpDX.D3DCompiler.ShaderVersion.PixelShader, result.PixelShader);
            shaders.HullShader = result.HullShader is null ? default : ShaderCompiler.CompileShader(shaderSource, SharpDX.D3DCompiler.ShaderVersion.HullShader, result.HullShader);
            shaders.DomainShader = result.DomainShader is null ? default : ShaderCompiler.CompileShader(shaderSource, SharpDX.D3DCompiler.ShaderVersion.DomainShader, result.DomainShader);
            shaders.GeometryShader = result.GeometryShader is null ? default : ShaderCompiler.CompileShader(shaderSource, SharpDX.D3DCompiler.ShaderVersion.GeometryShader, result.GeometryShader);

            RootSignature rootSignature = CreateRootSignature();

            return new PipelineState(GraphicsDevice, inputElements, rootSignature,
                shaders.VertexShader, shaders.PixelShader, shaders.HullShader, shaders.DomainShader, shaders.GeometryShader);
        }

        public RootSignature CreateRootSignature()
        {
            List<RootParameter> rootParameters = new List<RootParameter>
            {
                new RootParameter(ShaderVisibility.All,
                    new RootConstants(0, 0, 1)),
                new RootParameter(ShaderVisibility.All,
                    new DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 1)),
                new RootParameter(ShaderVisibility.All,
                    new DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 2)),
                new RootParameter(ShaderVisibility.All,
                    new DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 3)),
                new RootParameter(ShaderVisibility.All,
                    new DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 4))
            };

            if (ConstantBuffers.Count > 0)
            {
                rootParameters.Add(new RootParameter(ShaderVisibility.All,
                    new DescriptorRange(DescriptorRangeType.ConstantBufferView, ConstantBuffers.Count, 5)));
            }

            if (Samplers.Count > 0)
            {
                rootParameters.Add(new RootParameter(ShaderVisibility.All,
                    new DescriptorRange(DescriptorRangeType.Sampler, Samplers.Count, 1)));
            }

            if (Textures.Count > 0)
            {
                rootParameters.Add(new RootParameter(ShaderVisibility.All,
                    new DescriptorRange(DescriptorRangeType.ShaderResourceView, Textures.Count, 0)));
            }

            StaticSamplerDescription[] staticSamplers = new StaticSamplerDescription[]
            {
                new StaticSamplerDescription(ShaderVisibility.All, 0, 0)
            };

            RootSignatureDescription rootSignatureDescription = new RootSignatureDescription(
                RootSignatureFlags.AllowInputAssemblerInputLayout, rootParameters.ToArray(), staticSamplers);

            return GraphicsDevice.CreateRootSignature(rootSignatureDescription);
        }
    }
}
