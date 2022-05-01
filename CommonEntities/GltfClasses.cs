/*
 * Copyright (c) 2017 Robert Adams
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

//RA using SharpGLTF.Schema2;
//RA using SharpGLTF.Scenes;
//RA using SharpGLTF.Geometry;
//RA using SharpGLTF.Materials;
//RA using SharpGLTF.Memory;
//RA using SixLabors.ImageSharp;

using org.herbal3d.cs.CommonUtil;

// I hoped to keep the Gltf classes separate from the OMV requirement but
//    it doesn't make sense to copy all the mesh info into new structures.
using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.cs.CommonEntities {

    // using MESHBUILDER = MeshBuilder<SharpGLTF.Geometry.VertexTypes.VertexPositionNormal, SharpGLTF.Geometry.VertexTypes.VertexColor1Texture1>;
    //RA using MESHBUILDER = MeshBuilder<SharpGLTF.Geometry.VertexTypes.VertexPositionNormal, SharpGLTF.Geometry.VertexTypes.VertexColor1Texture1>;
    //RA using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPositionNormal;

    // Parameters used by the Gltf code
    public class gltfParams: PersistRulesParams {
        public string uriBase = "./";
        public int verticesMaxForBuffer = 50000;
        public string gltfCopyright = "Copyright 2022. All rights reserved";
        public bool addUniqueCodes = true;
        public bool doubleSided = true;
        public int textureMaxSize = 256;
        public bool logBuilding = false;
        public bool logGltfBuilding = false;
    }

    // =============================================================
    public class Gltf {
#pragma warning disable 414     // disable 'assigned but not used' warning
        private static readonly string _logHeader = "[Gltf]";
#pragma warning restore 414

        string _sceneName;
        BLogger _log;
        gltfParams _params;

        //RA ModelRoot _modelRoot;
        //RA Scene _scene;
        //RA SceneBuilder _sceneBuilder;
    
        public Gltf(string pSceneName, BLogger pLog, gltfParams pParams) {
            _sceneName = pSceneName;
            _log = pLog;
            _params = pParams;
        }

        // Add all the objects from a scene into this empty Gltf instance.
        public void LoadScene(BScene pScene) {

        /*
            var gModel = ModelRoot.CreateModel();
            var gScene = gModel.UseScene(pScene.name);

            pScene.instances.ForEach(inst => {
                var gNode = gScene.CreateNode(inst.Representation.name)
                    .WithLocalTranslation(inst.Position)
                    .WithLocalRotation(inst.Rotation);
                var renderable = inst.Representation.renderable as RenderableMeshGroup;
                if (renderable != null) {
                    var gMesh = gNode.Mesh = gModel.CreateMesh();
                    renderable.meshes.ForEach(aMesh => {
                        var gPrim = gMesh.CreatePrimitive()
                        .WithVertexAccessor("POSITION", aMesh.mesh.vertexs.Select(vert => {
                            return new Vector3(vert.Position.X, vert.Position.Y, vert.Position.Z);
                        }).ToArray())
                        .WithVertexAccessor("TEXCOORD_0", aMesh.mesh.vertexs.Select(vert => {
                            return new Vector2(vert.TexCoord.X, vert.TexCoord.Y);
                        }).ToArray())
                        .WithIndicesAccessor(PrimitiveType.TRIANGLES, aMesh.mesh.indices)
                        .WithMaterial(GetMaterial(gModel, aMesh));
                    });
                }
            };
            */

            _log.Debug("Gltf.LoadScene: loading scene {0}", pScene.name);
        }

        /*
        private Dictionary<BHash, Material> _meshCollection = new Dictionary<BHash, Material>();
        private Material GetMaterial(ModelRoot pModel, RenderableMesh pRenderable) {
            Material mat;
            MaterialInfo matInfo = pRenderable.material;
            var hash = matInfo.GetBHash();

            if (!_meshCollection.TryGetValue(hash, out mat)) {
                var diffuseColor = matInfo.RGBA;
                mat = pModel.CreateMaterial(pRenderable.material.GetBHash().ToString())
                    .WithPBRMetallicRoughness()
                    .WithAlpha(SharpGLTF.Materials.AlphaMode.BLEND)
                    .WithDoubleSide(_params.doubleSided);
                if (matInfo.shiny != OMV.Shininess.None) {
                    mat.WithChannelFactor("MetallicRoughness", "MetallicFactor", Util.Clamp((float)matInfo.shiny / 256f, 0f, 1f));
                }
                Nullable<MemoryImage> img = GetImage(matInfo.image);
                var diffuseColorVector = new Vector4(diffuseColor.R, diffuseColor.G, diffuseColor.B, diffuseColor.A));
                if (img != null) {
                    mat.WithChannelTexture("baseColorTexture", 1, GetImage(pModel, matInfo.image));
                }
                else {
                    mat.WithDiffuse(diffuseColorVector);
                }

                _meshCollection.Add(hash, mat);
            };
            return mat;
        }
        private Dictionary<BHash, SharpGLTF.Schema2.Image> _imageCollection = new Dictionary<BHash, SharpGLTF.Schema2.Image>();
        private SharpGLTF.Schema2.Image GetImage(ModelRoot pModel, ImageInfo pImage) {
            SharpGLTF.Schema2.Image ret = null;

            if (pImage != null) {
                if (!_imageCollection.TryGetValue(pImage.GetBHash(), out SharpGLTF.Schema2.Image img)) {
                    var memoryImg = new MemoryImage(pImage.GetConvertedImage());
                    img = pModel.CreateImage(pImage.imageIdentifier.ToString());
                    img.Content = memoryImg;

                    _imageCollection.Add(pImage.GetBHash(), img);
                    ret = img;
                }
            }
            return ret;
        }
        */

        public void ToJSON(StreamWriter outt) {
        }

        // Write the binary files into the persist computed target directory
        public void WriteBinaryFiles(BAssetStorage pStorage) {
        }
        public void WriteImages(BAssetStorage pStorage) {
        }
    }
}
