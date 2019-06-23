﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DirectX12GameEngine.Core.Assets;
using DirectX12GameEngine.Graphics;
using DirectX12GameEngine.Rendering;
using DirectX12GameEngine.Rendering.Materials;

namespace DirectX12GameEngine.Assets
{
    [AssetContentType(typeof(Material))]
    public class MaterialAsset : AssetWithSource<Material>
    {
        private readonly ContentManager contentManager;
        private readonly ShaderContentManager shaderContentManager;
        private readonly GraphicsDevice device;

        public MaterialAsset(ContentManager contentManager, ShaderContentManager shaderContentManager, GraphicsDevice device)
        {
            this.contentManager = contentManager;
            this.shaderContentManager = shaderContentManager;
            this.device = device;
        }

        public MaterialAttributes Attributes { get; set; } = new MaterialAttributes();

        public async override Task CreateAssetAsync(Material material)
        {
            MaterialDescriptor descriptor;

            if (string.IsNullOrEmpty(Source))
            {
                descriptor = new MaterialDescriptor { MaterialId = Id, Attributes = Attributes };
            }
            else
            {
                string path = Path.Combine(contentManager.RootPath, Source);
                int index = GetIndex(ref path);
                string extension = Path.GetExtension(path);

                if (extension == ".gltf" || extension == ".glb")
                {
                    MaterialAttributes materialAttributes = await new GltfModelLoader(device).LoadMaterialAsync(path, index);

                    // TODO: Combine material attributes.

                    descriptor = new MaterialDescriptor { MaterialId = Id, Attributes = materialAttributes };
                }
                else
                {
                    throw new NotSupportedException("This file type is not supported.");
                }
            }

            material.Descriptor = descriptor;

            MaterialGeneratorContext context = new MaterialGeneratorContext(device, material, shaderContentManager);
            await MaterialGenerator.GenerateAsync(descriptor, context);
        }

        private static int GetIndex(ref string path)
        {
            Match match = Regex.Match(path, @"\[\d+\]$");
            int index = 0;

            if (match.Success)
            {
                path = path.Remove(match.Index);
                index = int.Parse(match.Value.Trim('[', ']'));
            }

            return index;
        }
    }
}