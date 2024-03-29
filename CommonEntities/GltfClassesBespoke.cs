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
using System.Linq;
using System.Collections.Generic;
using System.IO;

using org.herbal3d.cs.CommonUtil;

// I hoped to keep the Gltf classes separate from the OMV requirement but
//    it doesn't make sense to copy all the mesh info into new structures.
using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.cs.CommonEntities {

    // Parameters used by the Gltf code
    public class gltfParamsB: PersistRulesParams {
        public string inputOAR = "";
        public string uriBase = "./";
        public int verticesMaxForBuffer = 50000;
        public string gltfCopyright = "Copyright 2022. All rights reserved";
        public bool addUniqueCodes = true;
        public bool doubleSided = true;
        public int textureMaxSize = 256;
        public bool logBuilding = false;
        public bool logGltfBuilding = false;
        public string versionLong = "1.1.1-20220101-12345678";
    }

    // The base class for all of the different types.
    public abstract class GltfClassB {
        public GltfB gltfRoot;
        public string ID;
        public int referenceID;
        public abstract Object AsJSON();    // return object that's serializable as JSON

        protected BLogger _log;
        protected gltfParamsB _params;

        public PersistRules.AssetType AssetType = PersistRules.AssetType.Unknown;

        // Return the filename for storing this object. Return null if doesn't store.
        public virtual string GetFilename(string pLongName) {
            // often UUID's are turned to strings with hyphens. Make sure they are gone.
            // return PersistRules.GetFilename(this.AssetType, this.ID, pLongName, _params).Replace("-", "");
            return PersistRules.GetFilename(this.AssetType, this.ID, pLongName, _params);
        }
        public virtual string GetURI(string pURIBase, string pStorageName) {
            return PersistRules.ReferenceURL(pURIBase, pStorageName);
        }

        public GltfClassB() { }
        public GltfClassB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) {
            BaseInit(pRoot, pID, pLog, pParams);
        }

        protected void BaseInit(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) {
            gltfRoot = pRoot;
            ID = pID;
            _log = pLog;
            _params = pParams;
            referenceID = -1;   // illegal value that could show up when debugging
        }

        // Output messge of --LogGltfBuilding was specified
        protected void LogGltf(string msg, params Object[] args) {
            if (_params.logGltfBuilding) {
                _log.Debug(msg, args);
            }
        }
    }

    // Base class of a list of a type.
    public abstract class GltfListClassB<T> : Dictionary<BHash, T> {
        public GltfB gltfRoot;
        public GltfListClassB(GltfB pRoot) {
            gltfRoot = pRoot;
        }

        // Gltfv2 references items by array index. Make sure all reference index
        //    numbers are up to date.
        public void UpdateGltfv2ReferenceIndexes() {
            int refIndex = 0;
            foreach (T entry in this.Values) {
#pragma warning disable IDE0019 // Use pattern matching
                GltfClassB entryGltf = entry as GltfClassB;
#pragma warning restore IDE0019 // Use pattern matching
                if (entryGltf != null) {
                    entryGltf.referenceID = refIndex;
                }
                refIndex++;
            }
        }

        // Return an array of the referenceIDs of the values in this collection
        public Array AsArrayOfIDs() {
            return this.Values.OfType<GltfClassB>()
            .Select(val => {
                return val.referenceID;
            }).ToArray();
        }

        public Array AsArrayOfValues() {
            // return this.Values.OfType<GltfClass>().ToArray();
            return this.Values.OfType<GltfClassB>()
            .Select( val => {
                return val.AsJSON();
            }).ToArray();
        }

        // Return a dictionary map of value.Id => value.AsJSON
        public Dictionary<string, Object> ToJSONMapOfNames() {
            return this.Values.OfType<GltfClassB>()
            .ToDictionary(t => t.ID, t => t.AsJSON());
        }
    }

    public class GltfVector16B : GltfClassB {
        public float[] vector = new float[16];
        public GltfVector16B() : base() {
        }

        public override Object AsJSON() {
            return vector;
        }
    }

    // =============================================================
    public class GltfB : GltfClassB {
#pragma warning disable 414     // disable 'assigned but not used' warning
        private static readonly string _logHeader = "[Gltf]";
#pragma warning restore 414

        public GltfAttributesB extensionsUsed;   // list of extensions used herein

        public GltfSceneB defaultScene;   // ID of default scene
        public OMV.UUID SceneUUID;

        public readonly OMV.UUID GltfUUID; // Used to identify buffer
        public readonly string IdentifyingString;   // built from GltfUUID


        public GltfAssetB asset;
        public GltfScenesB scenes;       // scenes that make up this package
        public GltfNodesB nodes;         // nodes in the scenes
        public GltfMeshesB meshes;       // the meshes for the nodes
        public GltfMaterialsB materials; // materials that make up the meshes
        public GltfAccessorsB accessors; // access to the mesh bin data
        public GltfBufferViewsB bufferViews; //
        public GltfBuffersB buffers;
        public GltfTechniquesB techniques;
        public GltfProgramsB programs;
        public GltfShadersB shaders;
        public GltfTexturesB textures;
        public GltfImagesB images;
        public GltfSamplersB samplers;

        public GltfPrimitivesB primitives;

        public GltfSamplerB defaultSampler;

        public GltfB(string pSceneName, BLogger pLog, gltfParamsB pParams) :
                            base(null, pSceneName, pLog, pParams) {
            gltfRoot = this;
            AssetType = PersistRules.AssetType.Scene;
            GltfUUID = OMV.UUID.Random();
            IdentifyingString = GltfUUID.ToString().Replace("-", "");

            extensionsUsed = new GltfAttributesB();
            asset = new GltfAssetB(this, _log, _params);
            scenes = new GltfScenesB(this);
            nodes = new GltfNodesB(this);
            meshes = new GltfMeshesB(this);
            materials = new GltfMaterialsB(this);
            accessors = new GltfAccessorsB(this);
            bufferViews = new GltfBufferViewsB(this);
            buffers = new GltfBuffersB(this);
            techniques = new GltfTechniquesB(this);
            programs = new GltfProgramsB(this);
            shaders = new GltfShadersB(this);
            textures = new GltfTexturesB(this);
            images = new GltfImagesB(this);
            samplers = new GltfSamplersB(this);

            primitives = new GltfPrimitivesB(this);

            // 20170201: ThreeJS defaults to GL_CLAMP but GLTF should default to GL_REPEAT/WRAP
            // Create a sampler for all the textures that forces WRAPing
            defaultSampler = new GltfSamplerB(gltfRoot, "simpleTextureRepeat", _log, _params) {
                name = "simpleTextureRepeat",
                magFilter = WebGLConstants.LINEAR,
                minFilter = WebGLConstants.LINEAR_MIPMAP_LINEAR,
                wrapS = WebGLConstants.REPEAT,
                wrapT = WebGLConstants.REPEAT
            };
        }

        // Say this scene is using the extension.
        public void UsingExtension(string extName) {
            if (!extensionsUsed.ContainsKey(extName)) {
                extensionsUsed.Add(extName, null);
            }
        }

        // Add all the objects from a scene into this empty Gltf instance.
        public void LoadScene(BScene scene) {

            _log.Debug("Gltf.LoadScene: loading scene {0}", scene.name);
            GltfSceneB gltfScene = new GltfSceneB(this, scene.name, _log, _params);
            defaultScene = gltfScene;

            // Adding the nodes creates all the GltfMesh's, etc.
            scene.instances.ForEach(pInstance => {
                Displayable rootDisp = pInstance.Representation;
                // _log.DebugFormat("Gltf.LoadScene: Loading node {0}", rootDisp.name);    // DEBUG DEBUG
                GltfNodeB rootNode = GltfNodeB.GltfNodeFactory(gltfRoot, rootDisp, _log, _params);
                rootNode.translation = pInstance.Position;
                rootNode.rotation = pInstance.Rotation;
                // The hash of the node list is never used so we just make something up.
                gltfScene.nodes.Add(new BHashULong(gltfScene.nodes.Count), rootNode);
            });

            // Load the pointed to items first and then the complex items

            // Meshes, etc  have been added to the scene. Pass over all
            //   the meshes and create the Buffers, BufferViews, and Accessors.
            _log.Debug("Gltf.LoadScene: starting building buffers and accessors ");    // DEBUG DEBUG
            BuildAccessorsAndBuffers();
            _log.Debug("Gltf.LoadScene: updating reference indexes");    // DEBUG DEBUG
            UpdateGltfv2ReferenceIndexes();
            _log.Debug("Gltf.LoadScene: done loading");
        }

        // GLTF v2 has all item references as an index to that item.
        // Call this routine just before outputting the scene/model to
        //    compute all the indexes and output array positions.
        private void UpdateGltfv2ReferenceIndexes() {
            // extensionsUsed.UpdateGltfv2ReferenceIndexes();
            // asset.UpdateGltfv2ReferenceIndexes();
            scenes.UpdateGltfv2ReferenceIndexes();
            nodes.UpdateGltfv2ReferenceIndexes();
            meshes.UpdateGltfv2ReferenceIndexes();
            materials.UpdateGltfv2ReferenceIndexes();
            accessors.UpdateGltfv2ReferenceIndexes();
            bufferViews.UpdateGltfv2ReferenceIndexes();
            buffers.UpdateGltfv2ReferenceIndexes();
            techniques.UpdateGltfv2ReferenceIndexes();
            programs.UpdateGltfv2ReferenceIndexes();
            shaders.UpdateGltfv2ReferenceIndexes();
            textures.UpdateGltfv2ReferenceIndexes();
            images.UpdateGltfv2ReferenceIndexes();
            samplers.UpdateGltfv2ReferenceIndexes();

            primitives.UpdateGltfv2ReferenceIndexes();
        }

        // After all the nodes have been added to a Gltf class, build all the
        //    dependent structures
        public void BuildAccessorsAndBuffers() {
            int maxVerticesPerBuffer = _params.verticesMaxForBuffer;

            // Partition the meshes into smaller groups based on number of vertices going out
            List<GltfPrimitiveB> partial = new List<GltfPrimitiveB>();
            int totalVertices = 0;
            foreach (var prim in primitives.Values) {
                // If adding this mesh will push the total vertices in this buffer over the max, flush this buffer.
                if ((totalVertices + prim.meshInfo.vertexs.Count) > maxVerticesPerBuffer) {
                    BuildBufferForSomeMeshes(partial);
                    partial.Clear();
                    totalVertices = 0;
                }
                totalVertices += prim.meshInfo.vertexs.Count;
                partial.Add(prim);
            };
            if (partial.Count > 0) {
                BuildBufferForSomeMeshes(partial);
            }
        }

        public void BuildBufferForSomeMeshes(List<GltfPrimitiveB> somePrimitives) {
            // Pass over all the vertices in all the meshes and collect common vertices into 'vertexCollection'
            int numMeshes = 0;
            int numVerts = 0;
            Dictionary<BHash, uint> vertexIndex = new Dictionary<BHash, uint>();
            List<OMVR.Vertex> vertexCollection = new List<OMVR.Vertex>();
            ushort vertInd = 0;
            // This generates a collection of unique vertices (vertexCollection) and a dictionary
            //    that maps a vertex to its index (vertexIndex). The latter is used later to remap
            //    the existing indices values to new ones for the new unique vertex list.
            somePrimitives.ForEach(prim => {
                numMeshes++;
                prim.meshInfo.vertexs.ForEach(vert => {
                    numVerts++;
                    BHash vertHash = MeshInfo.VertexBHash(vert);
                    if (!vertexIndex.ContainsKey(vertHash)) {
                        vertexIndex.Add(vertHash, vertInd);
                        vertexCollection.Add(vert);
                        vertInd++;
                    }
                });
            });
            LogGltf("{0} BuildBuffers: total meshes = {1}", _logHeader, numMeshes);
            LogGltf("{0} BuildBuffers: total vertices = {1}", _logHeader, numVerts);
            LogGltf("{0} BuildBuffers: total unique vertices = {1}", _logHeader, vertInd);

            // Remap all the indices to the new, compacted vertex collection.
            //     mesh.underlyingMesh.face to mesh.newIndices
            // TODO: if num verts > ushort.maxValue, create array if uint's
            int numIndices = 0;
            somePrimitives.ForEach(prim => {
                MeshInfo meshInfo = prim.meshInfo;
                uint[] newIndices = new uint[meshInfo.indices.Count];
                for (int ii = 0; ii < meshInfo.indices.Count; ii++) {
                    OMVR.Vertex aVert = meshInfo.vertexs[meshInfo.indices[ii]];
                    BHash vertHash = MeshInfo.VertexBHash(aVert);
                    newIndices[ii] = vertexIndex[vertHash];
                }
                prim.newIndices = newIndices;
                numIndices += newIndices.Length;
                if (newIndices.Length == 0) {
                    _log.Error("{0} zero indices count", _logHeader);
                }
            });

            // The vertices have been unique'ified into 'vertexCollection' and each mesh has
            //    updated indices in GltfMesh.newIndices.
            int sizeofOneVertex = sizeof(float) * 8;
            int sizeofVertices = vertexCollection.Count * sizeofOneVertex;
            // If all the indices fit into a ushort, use those rather than a uint
            int sizeofOneIndices = numIndices < ushort.MaxValue ? sizeof(ushort) : sizeof(uint);
            int sizeofIndices = numIndices * sizeofOneIndices;
            // The offsets must be multiples of a good access unit so pad to a good alignment
            int padUnit = sizeof(float) * 8;
            int paddedSizeofIndices = sizeofIndices;
            // There might be padding for each mesh. An over estimate but hopefully not too bad.
            // paddedSizeofIndices += somePrimitives.Count * sizeof(float);
            paddedSizeofIndices += (padUnit - (paddedSizeofIndices % padUnit)) % padUnit;

            // A key added to the buffer, vertices, and indices names to uniquify them
            string buffNum = String.Format("{0:000}", buffers.Count + 1);
            string buffName = this.defaultScene.name + "_buffer" + buffNum;
            byte[] binBuffRaw = new byte[paddedSizeofIndices + sizeofVertices];
            GltfBufferB binBuff = new GltfBufferB(gltfRoot, buffName, _log, _params) {
                bufferBytes = binBuffRaw
            };
            LogGltf("{0} BuildBuffers: oneVertSz={1}, vertSz={2}, oneIndSz={3}, indSz={4}, patUnit={5}, padIndSz={6}",
                        _logHeader, sizeofOneVertex, sizeofVertices,
                        sizeofOneIndices, sizeofIndices, padUnit, paddedSizeofIndices);

            // Copy the vertices into the output binary buffer 
            // Buffer.BlockCopy only moves primitives. Copy the vertices into a float array.
            // This also separates the verts from normals from texCoord since the Babylon
            //     Gltf reader doesn't handle stride.
            float[] floatVertexRemapped = new float[vertexCollection.Count * sizeof(float) * 8];
            int vertexBase = 0;
            int normalBase = vertexCollection.Count * 3;
            int texCoordBase = normalBase + vertexCollection.Count * 3;
            int jj = 0; int kk = 0;
            vertexCollection.ForEach(vert => {
                floatVertexRemapped[vertexBase + 0 + jj] = vert.Position.X;
                floatVertexRemapped[vertexBase + 1 + jj] = vert.Position.Y;
                floatVertexRemapped[vertexBase + 2 + jj] = vert.Position.Z;
                floatVertexRemapped[normalBase + 0 + jj] = vert.Normal.X;
                floatVertexRemapped[normalBase + 1 + jj] = vert.Normal.Y;
                floatVertexRemapped[normalBase + 2 + jj] = vert.Normal.Z;
                floatVertexRemapped[texCoordBase + 0 + kk] = vert.TexCoord.X;
                floatVertexRemapped[texCoordBase + 1 + kk] = vert.TexCoord.Y;
                jj += 3;
                kk += 2;
            });
            Buffer.BlockCopy(floatVertexRemapped, 0, binBuffRaw, paddedSizeofIndices, sizeofVertices);
            floatVertexRemapped = null;

            // Create BufferView's for each of the four sections of the buffer
            GltfBufferViewB binIndicesView = new GltfBufferViewB(gltfRoot, "indices" + buffNum, _log, _params) {
                buffer = binBuff,
                byteOffset = 0,
                byteLength = paddedSizeofIndices,
                byteStride = sizeofOneIndices
            };
            binIndicesView.target = WebGLConstants.ELEMENT_ARRAY_BUFFER;

            GltfBufferViewB binVerticesView = new GltfBufferViewB(gltfRoot, "viewVertices" + buffNum, _log, _params) {
                buffer = binBuff,
                byteOffset = paddedSizeofIndices,
                byteLength = vertexCollection.Count * 3 * sizeof(float),
                byteStride = 3 * sizeof(float)
            };
            binVerticesView.target = WebGLConstants.ARRAY_BUFFER;

            GltfBufferViewB binNormalsView = new GltfBufferViewB(gltfRoot, "normals" + buffNum, _log, _params) {
                buffer = binBuff,
                byteOffset = binVerticesView.byteOffset + binVerticesView.byteLength,
                byteLength = vertexCollection.Count * 3 * sizeof(float),
                byteStride = 3 * sizeof(float)
            };
            binNormalsView.target = WebGLConstants.ARRAY_BUFFER;

            GltfBufferViewB binTexCoordView = new GltfBufferViewB(gltfRoot, "texCoord" + buffNum, _log, _params) {
                buffer = binBuff,
                byteOffset = binNormalsView.byteOffset + binNormalsView.byteLength,
                byteLength = vertexCollection.Count * 2 * sizeof(float),
                byteStride = 2 * sizeof(float)
            };
            binTexCoordView.target = WebGLConstants.ARRAY_BUFFER;

            // Gltf requires min and max values for all the mesh vertex collections
            float vminx, vminy, vminz;
            vminx = vminy = vminz = float.MaxValue;
            float vmaxx, vmaxy, vmaxz;
            vmaxx = vmaxy = vmaxz = float.MinValue;

            float nminx, nminy, nminz;
            nminx = nminy = nminz = float.MaxValue;
            float nmaxx, nmaxy, nmaxz;
            nmaxx = nmaxy = nmaxz = float.MinValue;

            float uminx, uminy;
            uminx = uminy = float.MaxValue;
            float umaxx, umaxy;
            umaxx = umaxy = float.MinValue;

            vertexCollection.ForEach(vert => {
                vminx = vminx > vert.Position.X ? vert.Position.X : vminx;
                vminy = vminy > vert.Position.Y ? vert.Position.Y : vminy;
                vminz = vminz > vert.Position.Z ? vert.Position.Z : vminz;
                vmaxx = vmaxx < vert.Position.X ? vert.Position.X : vmaxx;
                vmaxy = vmaxy < vert.Position.Y ? vert.Position.Y : vmaxy;
                vmaxz = vmaxz < vert.Position.Z ? vert.Position.Z : vmaxz;

                nminx = nminx > vert.Normal.X ? vert.Normal.X : nminx;
                nminy = nminy > vert.Normal.Y ? vert.Normal.Y : nminy;
                nminz = nminz > vert.Normal.Z ? vert.Normal.Z : nminz;
                nmaxx = nmaxx < vert.Normal.X ? vert.Normal.X : nmaxx;
                nmaxy = nmaxy < vert.Normal.Y ? vert.Normal.Y : nmaxy;
                nmaxz = nmaxz < vert.Normal.Z ? vert.Normal.Z : nmaxz;

                uminx = uminx > vert.TexCoord.X ? vert.TexCoord.X : uminx;
                uminy = uminy > vert.TexCoord.Y ? vert.TexCoord.Y : uminy;
                umaxx = umaxx < vert.TexCoord.X ? vert.TexCoord.X : umaxx;
                umaxy = umaxy < vert.TexCoord.Y ? vert.TexCoord.Y : umaxy;
            });
            // Add a little buffer to values which fixes problems with rounding errors
            /*
            vmin = TweekMinMax(vmin);
            vmax = TweekMinMax(vmax);
            nmin = TweekMinMax(nmin);
            nmax = TweekMinMax(nmax);
            umin = TweekMinMax(umin);
            umax = TweekMinMax(umax);
            */

            // Build one large group of vertices/normals/UVs that the individual mesh
            //     indices will reference. The vertices have been uniquified above.
            GltfAccessorB vertexAccessor = new GltfAccessorB(gltfRoot, buffName + "_accCVer", _log, _params) {
                bufferView = binVerticesView,
                count = vertexCollection.Count,
                byteOffset = 0,
                componentType = WebGLConstants.FLOAT,
                type = "VEC3",
                min = new Object[3] { vminx, vminy, vminz },
                max = new Object[3] { vmaxx, vmaxy, vmaxz }
            };

            GltfAccessorB normalsAccessor = new GltfAccessorB(gltfRoot, buffName + "_accNor", _log, _params) {
                bufferView = binNormalsView,
                count = vertexCollection.Count,
                byteOffset = 0,
                componentType = WebGLConstants.FLOAT,
                type = "VEC3",
                min = new Object[3] { nminx, nminy, nminz },
                max = new Object[3] { nmaxx, nmaxy, nmaxz }
            };

            GltfAccessorB UVAccessor = new GltfAccessorB(gltfRoot, buffName + "_accUV", _log, _params) {
                bufferView = binTexCoordView,
                count = vertexCollection.Count,
                byteOffset = 0,
                componentType = WebGLConstants.FLOAT,
                type = "VEC2"
            };
            // The values for TexCoords sometimes get odd
            if (!Single.IsNaN(uminx) && uminx > -1000000 && uminx < 1000000
                    && !Single.IsNaN(uminy) && uminy > -1000000 && uminy < 1000000) {
                UVAccessor.min = new Object[2] { uminx, uminy };
            }
            if (!Single.IsNaN(umaxx) && umaxx > -1000000 && umaxx < 1000000
                    && !Single.IsNaN(umaxy) && umaxy > -1000000 && umaxy < 1000000) {
                UVAccessor.max = new Object[2] { umaxx, umaxy };
            }

            // For each mesh, copy the indices into the binary output buffer and create the accessors
            //    that point from the mesh into the binary info.
            int indicesOffset = 0;
            somePrimitives.ForEach((Action<GltfPrimitiveB>)(prim => {
                int numPrimIndices = prim.newIndices.Length;
                int meshIndicesSize = numPrimIndices * sizeofOneIndices;
                // Above, the indices are built using uint's so, if sending the shorter form, repack indices.
                if (sizeofOneIndices == sizeof(ushort)) {
                    ushort[] shortIndices = new ushort[numPrimIndices];
                    for (int ii = 0; ii < numPrimIndices; ii++) {
                        shortIndices[ii] = (ushort)prim.newIndices[ii];
                    }
                    Buffer.BlockCopy(shortIndices, 0, binBuffRaw, indicesOffset, meshIndicesSize);
                }
                else {
                    Buffer.BlockCopy(prim.newIndices, 0, binBuffRaw, indicesOffset, meshIndicesSize);
                }

                GltfAccessorB indicesAccessor = new GltfAccessorB(gltfRoot, prim.ID + "_accInd", _log, _params) {
                    bufferView = binIndicesView,
                    count = prim.newIndices.Length,
                    byteOffset = indicesOffset,
                    componentType = sizeofOneIndices == sizeof(ushort) ? WebGLConstants.UNSIGNED_SHORT : WebGLConstants.UNSIGNED_INT,
                    type = "SCALAR"
                };
                uint imin = uint.MaxValue; uint imax = 0;
                for (int ii = 0; ii < prim.newIndices.Length; ii++) {
                    imin = Math.Min(imin, prim.newIndices[ii]);
                    imax = Math.Max(imax, prim.newIndices[ii]);
                }
                indicesAccessor.min = new Object[1] { imin };
                indicesAccessor.max = new Object[1] { imax };

                // _log.DebugFormat("{0} indices: meshIndSize={1}, cnt={2}, offset={3}", LogHeader,
                //                 meshIndicesSize, indicesAccessor.count, indicesOffset);

                indicesOffset += meshIndicesSize;

                prim.indices = indicesAccessor;
                prim.position = vertexAccessor;
                prim.normals = normalsAccessor;
                prim.texcoord = UVAccessor;
            }));
        }

        private OMV.Vector3 TweekMinMax(OMV.Vector3 pVec) {
            pVec.X = TweekMinMaxVal(pVec.X);
            pVec.Y = TweekMinMaxVal(pVec.Y);
            pVec.Z = TweekMinMaxVal(pVec.Z);
            return pVec;
        }
        private OMV.Vector2 TweekMinMax(OMV.Vector2 pVec) {
            pVec.X = TweekMinMaxVal(pVec.X);
            pVec.Y = TweekMinMaxVal(pVec.Y);
            return pVec;
        }
        private float TweekMinMaxVal(float x) {
            return x + Math.Sign(x) * 0.0001f;
        }

        public void ToJSON(StreamWriter outt) {
            UpdateGltfv2ReferenceIndexes();
            JSONHelpers.SimpleJSONOutput(outt, this.AsJSON());
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (defaultScene != null) {
                ret.Add("scene", defaultScene.referenceID);
            }

            if (asset.values.Count > 0) {
                ret.Add("asset", asset.AsJSON());
            }

            if (scenes.Count > 0) {
                ret.Add("scenes", scenes.AsArrayOfValues());
            }

            if (nodes.Count > 0) {
                ret.Add("nodes", nodes.AsArrayOfValues());
            }

            if (meshes.Count > 0) {
                ret.Add("meshes", meshes.AsArrayOfValues());
            }

            if (accessors.Count > 0) {
                ret.Add("accessors", accessors.AsArrayOfValues());
            }

            if (bufferViews.Count > 0) {
                ret.Add("bufferViews", bufferViews.AsArrayOfValues());
            }

            if (materials.Count > 0) {
                ret.Add("materials", materials.AsArrayOfValues());
            }

            if (techniques.Count > 0) {
                ret.Add("techniques", techniques.AsArrayOfValues());
            }

            if (textures.Count > 0) {
                ret.Add("textures", textures.AsArrayOfValues());
            }

            if (images.Count > 0) {
                ret.Add("images", images.AsArrayOfValues());
            }

            if (samplers.Count > 0) {
                ret.Add("samplers", samplers.AsArrayOfValues());
            }

            if (programs.Count > 0) {
                ret.Add("programs", programs.AsArrayOfValues());
            }

            if (shaders.Count > 0) {
                ret.Add("shaders", shaders.AsArrayOfValues());
            }

            if (buffers.Count > 0) {
                ret.Add("buffers", buffers.AsArrayOfValues());
            }

            if (extensionsUsed.Count > 0) {
                ret.Add("extensionsUsed", extensionsUsed.AsJSON());
            }

            if (extensionsUsed.Count > 0) {
                ret.Add("extensionsRequired", extensionsUsed.AsJSON());
            }

            return ret;
        } 

        // Write the binary files into the persist computed target directory
        public void WriteBinaryFiles(BAssetStorage pStorage) {
            foreach (var buff in buffers.Values) {
                buff.WriteBuffer(pStorage);
            }
        }
        public void WriteImages(BAssetStorage pStorage) {
            foreach (var img in images.Values) {
                img.WriteImage(pStorage);
            }
        }
    }


    // =============================================================
    // A simple collection to keep name/value strings
    // The value is an object so it can hold strings, numbers, or arrays and have the
    //     values serialized properly in the output JSON.
    public class GltfAttributesB : Dictionary<string, Object> {
        public GltfAttributesB() : base() {
        }

        public Object AsJSON() {
            return this;
        }
    }

    // =============================================================
    public class GltfAssetB : GltfClassB {
        public GltfAttributesB values;
        public GltfAttributesB extras;

        public GltfAssetB(GltfB pRoot, BLogger pLog, gltfParamsB pParams) : base(pRoot, "", pLog, pParams) {
            values = new GltfAttributesB {
                { "generator", "convoar" },
                { "version", "2.0" },   // the GLTF specification version
                { "copyright", _params.gltfCopyright }
            };
            extras = new GltfAttributesB {
                { "convoar", new GltfAttributesB {
                    { "convoarVersion", _params.versionLong },
                    { "conversionDate", DateTime.UtcNow.ToString() },
                    { "OARFilename", _params.inputOAR }
                } }
            };
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>(values);
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfScenesB : GltfListClassB<GltfSceneB> {
        public GltfScenesB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfSceneB : GltfClassB {
        public GltfNodesB nodes;      // IDs of top level nodes in the scene
        public string name;
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;

        public GltfSceneB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            nodes = new GltfNodesB(gltfRoot);
            name = pID;
            extensions = new GltfExtensionsB(pRoot);
            extras = new GltfAttributesB();
            gltfRoot.scenes.Add(new BHashULong(gltfRoot.scenes.Count), this);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (nodes != null && nodes.Count > 0) ret.Add("nodes", nodes.AsArrayOfIDs());
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfNodesB : GltfListClassB<GltfNodeB> {
        public GltfNodesB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfNodeB : GltfClassB {
        public string camera;       // non-empty if a camera definition
        public GltfNodesB children;
        public string skin;
        // has either 'matrix' or 'rotation/scale/translation'
        public OMV.Matrix4 matrix;
        public GltfMeshB mesh;
        public OMV.Quaternion rotation;
        public OMV.Vector3 scale;
        public OMV.Vector3 translation;
        public string[] weights;   // weights of morph tragets
        public string name;
        public GltfExtensionsB extensions;   // more JSON describing the extensions used
        public GltfAttributesB extras;       // more JSON with additional, beyond-the-standard values

        // Add a node that is not top level in a scene
        // Does not add to the built node collection
        public GltfNodeB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            NodeInit(pRoot);
            LogGltf("{0} GltfNode: created empty. ID={1}", "Gltf", ID);
        }

        public GltfNodeB(GltfB pRoot, Displayable pDisplayable, BLogger pLog, gltfParamsB pParams)
                            : base(pRoot, pDisplayable.baseUUID.ToString() + "_node", pLog, pParams) {
            LogGltf("{0} GltfNode: starting node create. ID={1}, disphash={2}, dispRendHandle={3}",
                        "Gltf", ID, pDisplayable.GetBHash(), pDisplayable.renderable.handle);
            NodeInit(pRoot);
            InitFromDisplayable(pDisplayable);
            LogGltf("{0} GltfNode: created from Displayable. ID={1}, disphash={2}, pos={3}, rot={4}, mesh={5}, numCh={6}",
                        "Gltf", ID, pDisplayable.GetBHash(), translation, rotation, mesh.handle, children.Count);
        }

        // Base initialization of the node instance
        private void NodeInit(GltfB pRoot) {
            children = new GltfNodesB(pRoot);
            matrix = OMV.Matrix4.Zero;
            rotation = new OMV.Quaternion();
            scale = OMV.Vector3.One;
            translation = new OMV.Vector3(0, 0, 0);
            extensions = new GltfExtensionsB(pRoot);
            extras = new GltfAttributesB();
        }

        private void InitFromDisplayable(Displayable pDisplayable) {
            name = pDisplayable.name;
            translation = pDisplayable.offsetPosition;
            rotation = pDisplayable.offsetRotation;
            scale = pDisplayable.scale;
            // only know how to handle a displayable of meshes
            mesh = GltfMeshB.GltfMeshFactory(gltfRoot, pDisplayable.renderable, _log, _params);

            foreach (var child in pDisplayable.children) {
                var node = GltfNodeB.GltfNodeFactory(gltfRoot, child, _log, _params);
                this.children.Add(new BHashULong(this.children.Count), node);
            }
        }

        // Get an existing instance of a node or create a new one
        public static GltfNodeB GltfNodeFactory(GltfB pRoot, Displayable pDisplayable, BLogger pLog, gltfParamsB pParams) {
            BHash displayableHash = pDisplayable.GetBHash();
            if (!pRoot.nodes.TryGetValue(displayableHash, out GltfNodeB node)) {
                // This is the only place we should be creating nodes
                node = new GltfNodeB(pRoot, pDisplayable, pLog, pParams);
                // This 'if' solved the odd case where we created the node as a child of this node
                if (!pRoot.nodes.ContainsKey(displayableHash))
                    pRoot.nodes.Add(displayableHash, node);
            }
            return node;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (matrix != OMV.Matrix4.Zero) {
                ret.Add("matrix", matrix);
            }
            else {
                ret.Add("translation", translation);
                ret.Add("scale", scale);
                ret.Add("rotation", rotation);
            }
            if (children.Count > 0) {
                ret.Add("children", children.AsArrayOfIDs());
            }
            ret.Add("mesh", mesh.referenceID);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfMeshesB : GltfListClassB<GltfMeshB> {
        public GltfMeshesB(GltfB pRoot) : base(pRoot) {
        }

        public bool GetByUUID(OMV.UUID pUUID, out GltfMeshB theMesh) {
            string sUUID = pUUID.ToString();
            foreach (GltfMeshB mesh in this.Values) {
                if (mesh.handle.ToString() == sUUID) {
                    theMesh = mesh;
                    return true;
                }
            }
            theMesh = null;
            return false;
        }
    }

    public class GltfMeshB : GltfClassB {
        public GltfPrimitivesB primitives;
        public string[] weights;    // weights to apply with morph targets
        public string name;
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;

        public EntityHandle handle;
        public BHash bHash;
        public Displayable underlyingDisplayable;

        public GltfMeshB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            primitives = new GltfPrimitivesB(gltfRoot);
            extensions = new GltfExtensionsB(pRoot);
            extras = new GltfAttributesB();
            handle = new EntityHandleUUID();
            LogGltf("{0} GltfMesh: created empty. ID={1}, handle={2}, numPrim={3}",
                        "Gltf", ID, handle, primitives.Count);
        }

        public GltfMeshB(GltfB pRoot, DisplayableRenderable pDR, 
                            BLogger pLog, gltfParamsB pParams) : base(pRoot, pDR.handle.ToString() + "_dr", pLog, pParams) {
            primitives = new GltfPrimitivesB(gltfRoot);
            extensions = new GltfExtensionsB(pRoot);
            extras = new GltfAttributesB();
            handle = new EntityHandleUUID();
            if (pDR is RenderableMeshGroup rmg) {
                // Add the meshes in the RenderableMeshGroup as primitives in this mesh
                rmg.meshes.ForEach(oneMesh => {
                    // _log.DebugFormat("GltfMesh. create primitive: numVerts={0}, numInd={1}", // DEBUG DEBUG
                    //         oneMesh.mesh.vertexs.Count, oneMesh.mesh.indices.Count);  // DEBUG DEBUG
                    GltfPrimitiveB prim = GltfPrimitiveB.GltfPrimitiveFactory(pRoot, oneMesh, _log, _params);
                    primitives.Add(new BHashULong(primitives.Count), prim);
                });
            }
            BHasher hasher = new BHasherSHA256();
            foreach (var prim in primitives.Values) {
                hasher.Add(prim.bHash);
            };
            bHash = hasher.Finish();
            if (_params.addUniqueCodes) {
                // Add a unique code to the extras section
                extras.Add("uniqueHash", bHash.ToString());
            }
            gltfRoot.meshes.Add(pDR.GetBHash(), this);
            LogGltf("{0} GltfMesh: created from DisplayableRenderable. ID={1}, handle={2}, numPrimitives={3}",
                        "Gltf", ID, handle, primitives.Count);
        }

        public static GltfMeshB GltfMeshFactory(GltfB pRoot, DisplayableRenderable pDR, 
                                    BLogger pLog, gltfParamsB pParams) {
            if (!pRoot.meshes.TryGetValue(pDR.GetBHash(), out GltfMeshB mesh)) {
                mesh = new GltfMeshB(pRoot, pDR, pLog, pParams);
            }
            return mesh;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (primitives != null && primitives.Count > 0) ret.Add("primitives", primitives.AsArrayOfValues());
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfPrimitivesB : GltfListClassB<GltfPrimitiveB> {
        public GltfPrimitivesB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfPrimitiveB : GltfClassB {
        public GltfAccessorB indices;
        public MaterialInfo matInfo;
        public int mode;
        public string[] targets;    // TODO: morph targets
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;

        public MeshInfo meshInfo;
        public BHash bHash;          // generated from meshes and materials for this primitive
        public uint[] newIndices; // remapped indices posinting to global vertex list
        public GltfAccessorB normals;
        public GltfAccessorB position;
        public GltfAccessorB texcoord;
        public GltfMaterialB material;

        public GltfPrimitiveB(GltfB pRoot, BLogger pLog, gltfParamsB pParams) : base(pRoot, "primitive", pLog, pParams) {
            mode = 4;
            LogGltf("{0} GltfPrimitive: created empty. ID={1}", "Gltf", ID);
        }

        public GltfPrimitiveB(GltfB pRoot, RenderableMesh pRenderableMesh, 
                        BLogger pLog, gltfParamsB pParams) : base(pRoot, "primitive", pLog, pParams) {
            mode = 4;
            meshInfo = pRenderableMesh.mesh;
            matInfo = pRenderableMesh.material;
            material = GltfMaterialB.GltfMaterialFactory(pRoot, matInfo, pLog, pParams);
            extensions = new GltfExtensionsB(pRoot);
            extras = new GltfAttributesB();

            // My hash is the same as the underlying renderable mesh/material
            bHash = pRenderableMesh.GetBHash();
            ID = bHash.ToString();

            LogGltf("{0} GltfPrimitive: created. ID={1}, mesh={2}, hash={3}", "Gltf", ID, meshInfo, bHash);
            pRoot.primitives.Add(bHash, this);
        }

        public static GltfPrimitiveB GltfPrimitiveFactory(GltfB pRoot, RenderableMesh pRenderableMesh,
                            BLogger pLog, gltfParamsB pParams) {
            if (!pRoot.primitives.TryGetValue(pRenderableMesh.GetBHash(), out GltfPrimitiveB prim)) {
                prim = new GltfPrimitiveB(pRoot, pRenderableMesh, pLog, pParams);
            }
            return prim;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object> {
                { "mode", mode }
            };
            if (indices != null) ret.Add("indices", indices.referenceID);
            if (material != null) ret.Add("material", material.referenceID);

            var attribs = new Dictionary<string, Object>();
            if (normals != null) attribs.Add("NORMAL", normals.referenceID);
            if (position != null) attribs.Add("POSITION", position.referenceID);
            if (texcoord != null) attribs.Add("TEXCOORD_0", texcoord.referenceID);
            ret.Add("attributes", attribs);

            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());

            return ret;
        }
    }

    // =============================================================
    public class GltfMaterialsB : GltfListClassB<GltfMaterialB> {
        public GltfMaterialsB(GltfB pRoot) : base(pRoot) {
        }
    }

    public abstract class GltfMaterialB : GltfClassB {
        public string name;
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;
        public string[] pbrMetallicRoughness;   // not used: 
        public GltfImageB normalTexture;
        public GltfImageB occlusionTexture;
        public GltfImageB emissiveTexture;
        public OMV.Vector3? emmisiveFactor;
        public string alphaMode;    // one of "OPAQUE", "MASK", "BLEND"
        public float? alphaCutoff;
        public bool? doubleSided;         // whether surface has backside ('true' or 'false')
        
        // parameters coming from OpenSim
        public OMV.Vector4? ambient;      // ambient color of surface (OMV.Vector4)
        public OMV.Color4? diffuse;       // diffuse color of surface (OMV.Vector4 or textureID)
        public GltfTextureB diffuseTexture;  // diffuse color of surface (OMV.Vector4 or textureID)
        public float? emission;           // light emitted by surface (OMV.Vector4 or textureID)
        public float? specular;           // color reflected by surface (OMV.Vector4 or textureID)
        public float? shininess;          // specular reflection from surface (float)
        public float? glow;               // glow to add to the surface (float)
        public float? transparency;       // transparency of surface (float)
        public bool? transparent;         // whether the surface has transparency ('true' or 'false;)

        public GltfAttributesB topLevelValues;   // top level values that are output as part of the material

        protected void MaterialInit(GltfB pRoot, MaterialInfo matInfo, 
                                BLogger pLog, gltfParamsB pParams) {
            name = "mat-" + matInfo.GetBHash().ToString();
            extras = new GltfAttributesB();
            topLevelValues = new GltfAttributesB();
            extensions = new GltfExtensionsB(pRoot);
            BaseInit(pRoot, matInfo.handle.ToString() + "_mat", pLog, pParams);
            gltfRoot.materials.Add(matInfo.GetBHash(), this);

            OMV.Color4 surfaceColor = matInfo.RGBA;
            OMV.Color4 aColor = OMV.Color4.Black;

            diffuse = surfaceColor;
            if (surfaceColor.A != 1.0f) {
                transparency = surfaceColor.A;
                transparent = true;
            }
            doubleSided = _params.doubleSided;
            if (matInfo.shiny != OMV.Shininess.None) {
                shininess = Util.Clamp((float)matInfo.shiny / 256f, 0f, 1f);
            }

            if (matInfo.glow != 0f) {
                glow = Util.Clamp(matInfo.glow, 0f, 1f);
            }

            if (matInfo.image != null) {
                ImageInfo imageToUse = CheckForResizedImage(matInfo.image);
                GltfImageB newImage = GltfImageB.GltfImageFactory(pRoot, imageToUse, _log, _params);
                diffuseTexture = GltfTextureB.GltfTextureFactory(pRoot, imageToUse, newImage, _log, _params);

                if (diffuseTexture.source != null && diffuseTexture.source.imageInfo.hasTransprency) {
                    // 'Transparent' says the image has some alpha that needs blending
                    // the spec says default value is 'false' so only specify if 'true'
                    transparent = true;
                }
            }

            LogGltf("{0} GltfMaterial: created. ID={1}, name='{2}', numExt={3}",
                        "Gltf", ID, name, extensions.Count);
        }

        // NOTE: needed version that didn't have enclosing {} for some reason
        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (topLevelValues != null && topLevelValues.Count > 0) {
                foreach (var key in topLevelValues.Keys) {
                    ret.Add(key, topLevelValues[key]);
                }
            }
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;

        }

        // For Gltf (and the web browser) we can use reduced size images.
        // Check if that is being done and find the reference to the resized image
        private ImageInfo CheckForResizedImage(ImageInfo origImage) {
            ImageInfo ret = origImage;
            int maxSize = _params.textureMaxSize;
            if (origImage.resizable && maxSize > 0 && maxSize < 10000) {
                if (origImage.xSize > maxSize || origImage.ySize > maxSize) {
                    origImage.ConstrainTextureSize(maxSize);
                }
            }
            return ret;
        }

        public static GltfMaterialB GltfMaterialFactory(GltfB pRoot, MaterialInfo matInfo,
                            BLogger pLog, gltfParamsB pParams) {
            if (!pRoot.materials.TryGetValue(matInfo.GetBHash(), out GltfMaterialB mat)) {
                // mat = new GltfMaterialCommon2B(pRoot, matInfo);
                mat = new GltfMaterialPbrMetallicRoughnessB(pRoot, matInfo, pLog, pParams);
                // mat = new GltfMaterialPbrSpecularGlossinessB(pRoot, matInfo);
            }
            return mat;
        }
    }

    // Material as a HDR_Common_Material for GLTF version 2
    public class GltfMaterialCommon2B : GltfMaterialB {
        GltfExtensionB materialCommonExt;

        public GltfMaterialCommon2B(GltfB pRoot, MaterialInfo matInfo, 
                                BLogger pLog, gltfParamsB pParams) {
            MaterialInit(pRoot, matInfo, pLog, pParams);
        }

        public override Object AsJSON() {
            materialCommonExt = new GltfExtensionB(gltfRoot, "KHR_materials_common", _log, _params);
            // Pack the material set values into the extension
            materialCommonExt.values.Add("type", "commonBlinn");
            materialCommonExt.values.Add("diffuseFactor", diffuse.Value);
            if (diffuseTexture != null) {
                materialCommonExt.values.Add("diffuseTexture", diffuseTexture.referenceID);
            }
            if (specular.HasValue) {
                materialCommonExt.values.Add("specularFactor", specular.Value);
            }
            if (shininess.HasValue) {
                materialCommonExt.values.Add("shininessFactor", shininess.Value);
            }
            if (transparent.HasValue) {
                // OPAQUE, MASK, or BLEND
                this.topLevelValues.Add("alphaMode", "BLEND");
                // this.values.Add("alphaCutoff", 0.5f);
            }
            if (doubleSided.HasValue) {
                this.topLevelValues.Add("doubleSided", doubleSided.Value);
            }

            if (materialCommonExt.Count() > 0) {
                extensions.Add(materialCommonExt.ID, materialCommonExt);
            }

            return base.AsJSON();
        }
    }

    // Material as a pbrMetallicRoughness
    public class GltfMaterialPbrMetallicRoughnessB : GltfMaterialB {

        public GltfMaterialPbrMetallicRoughnessB(GltfB pRoot, MaterialInfo matInfo, 
                                BLogger pLog, gltfParamsB pParams) {
            MaterialInit(pRoot, matInfo, pLog, pParams);
            this.name = "pbr" + this.name;
        }

        public override Object AsJSON() {
            var pbr = new Dictionary<string, Object>();
            if (diffuse.HasValue) {
                pbr.Add("baseColorFactor", diffuse.Value);
            }
            if (diffuseTexture != null) {
                pbr.Add("baseColorTexture", diffuseTexture.TextureInfo());
            }
            if (specular.HasValue) {
                // 0..1: 1 means 'rough', 0 means 'smooth', linear scale
                pbr.Add("roughnessFactor", specular.Value);
            }
            if (shininess.HasValue) {
                // 0..1: 1 means 'metal', 0 means dieletic, linear scale
                pbr.Add("metallicFactor", shininess.Value);
            }
            else {
                // if no shineess is specified, this is not a metal
                pbr.Add("metallicFactor", 0f);
            }
            if (glow.HasValue) {
                pbr.Add("emissiveFactor", new OMV.Vector3(glow.Value, glow.Value, glow.Value));
            }
            if (pbr.Count > 0)
                topLevelValues.Add("pbrMetallicRoughness", pbr);

            if (transparent.HasValue) {
                // OPAQUE, MASK, or BLEND
                this.topLevelValues.Add("alphaMode", "BLEND");
                // this.values.Add("alphaCutoff", 0.5f);
            }
            if (doubleSided.HasValue) {
                this.topLevelValues.Add("doubleSided", doubleSided.Value);
            }
            return base.AsJSON();
        }
    }

    // Material as a HDR_pbr_specularGlossiness

    // =============================================================
    public class GltfAccessorsB : GltfListClassB<GltfAccessorB> {
        public GltfAccessorsB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfAccessorB : GltfClassB {
        public GltfBufferViewB bufferView;
        public int byteOffset;
        public uint componentType;
        public int count;
        public string type;
        public Object[] min;
        public Object[] max;

        public GltfAccessorB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.accessors.Add(new BHashULong(gltfRoot.accessors.Count), this);
            LogGltf("{0} GltfAccessor: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object> {
                { "bufferView", bufferView.referenceID },
                { "byteOffset", byteOffset },
                { "componentType", componentType },
                { "count", count }
            };
            if (!String.IsNullOrEmpty(type)) ret.Add("type", type);
            if (min != null && min.Length > 0) ret.Add("min", min);
            if (max != null && max.Length > 0) ret.Add("max", max);
            return ret;
        }
    }

    // =============================================================
    public class GltfBuffersB : GltfListClassB<GltfBufferB> {
        public GltfBuffersB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfBufferB : GltfClassB {
        public byte[] bufferBytes;
        public string name;
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;

        private readonly OMV.UUID _uuid; // Used to identify buffer
        private readonly string _identifyingString;

        public GltfBufferB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            AssetType = PersistRules.AssetType.Buff;
            extensions = new GltfExtensionsB(pRoot);
            extras = new GltfAttributesB();
            _uuid = OMV.UUID.Random();
            _identifyingString = _uuid.ToString().Replace("-", "");
            // Buffs go into the roots collection. Index is not used.
            gltfRoot.buffers.Add(new BHashULong(gltfRoot.buffers.Count), this);
            LogGltf("{0} GltfBuffer: created. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            ret.Add("byteLength", bufferBytes.Length);
            string outFilename = this.GetFilename(_identifyingString);
            ret.Add("uri", this.GetURI(_params.uriBase, outFilename));
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }

        public async void WriteBuffer(BAssetStorage pStorage) {
            string outFilename = this.GetFilename(_identifyingString);
            await pStorage.Store(outFilename, bufferBytes);
            // _log.DebugFormat("{0} WriteBinaryFiles: filename={1}", LogHeader, outFilename);
        }
    }

    // =============================================================
    public class GltfBufferViewsB : GltfListClassB<GltfBufferViewB> {
        public GltfBufferViewsB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfBufferViewB : GltfClassB {
        public GltfBufferB buffer;
        public int? byteOffset;
        public int? byteLength;
        public int? byteStride;
        public uint? target;
        public string name;
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;

        public GltfBufferViewB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.bufferViews.Add(new BHashULong(gltfRoot.bufferViews.Count), this);
            name = pID;
            extensions = new GltfExtensionsB(pRoot);
            extras = new GltfAttributesB();
            LogGltf("{0} GltfBufferView: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            ret.Add("buffer", buffer.referenceID);
            ret.Add("byteOffset", byteOffset);
            ret.Add("byteLength", byteLength);
            // ret.Add("byteStride", byteStride);
            if (target.HasValue) ret.Add("target", target.Value);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfTechniquesB : GltfListClassB<GltfTechniqueB> {
        public GltfTechniquesB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfTechniqueB : GltfClassB {
        public GltfTechniqueB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.techniques.Add(new BHashULong(gltfRoot.techniques.Count), this);
            LogGltf("{0} GltfTechnique: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            // TODO:
            return ret;
        }
    }

    // =============================================================
    public class GltfProgramsB : GltfListClassB<GltfProgramB> {
        public GltfProgramsB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfProgramB : GltfClassB {
        public GltfProgramB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.programs.Add(new BHashULong(gltfRoot.programs.Count), this);
            LogGltf("{0} GltfTechnique: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            // TODO:
            return ret;
        }
    }

    // =============================================================
    public class GltfShadersB : GltfListClassB<GltfShaderB> {
        public GltfShadersB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfShaderB : GltfClassB {
        public GltfShaderB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.shaders.Add(new BHashULong(gltfRoot.shaders.Count), this);
            LogGltf("{0} GltfShader: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            // TODO:
            return ret;
        }
    }

    // =============================================================
    public class GltfTexturesB : GltfListClassB<GltfTextureB> {
        public GltfTexturesB(GltfB pRoot) : base(pRoot) {
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfTextureB theTexture) {
            foreach (var tex in this.Values) {
                if (tex.underlyingUUID != null && tex.underlyingUUID == aUUID) {
                    theTexture = tex;
                    return true;
                }
            }
            theTexture = null;
            return false;
        }
    }

    public class GltfTextureB : GltfClassB {
        public GltfSamplerB sampler;
        public GltfImageB source;
        public string name;
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;

        public OMV.UUID underlyingUUID;
        // public uint target;
        // public uint type;
        // public uint format;
        // public uint internalFormat;

        public GltfTextureB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            // gltfRoot.textures.Add(this);
            LogGltf("{0} GltfTexture: created empty. ID={1}", "Gltf", ID);
        }

        public GltfTextureB(GltfB pRoot, ImageInfo pImageInfo, GltfImageB pImage,
                    BLogger pLog, gltfParamsB pParams) : base(pRoot, pImageInfo.handle.ToString() + "_tex", pLog, pParams) {
            if (pImageInfo.handle is EntityHandleUUID handleU) {
                underlyingUUID = handleU.GetUUID();
            }
            // this.target = WebGLConstants.TEXTURE_2D;
            // this.type = WebGLConstants.UNSIGNED_BYTE;
            // this.format = WebGLConstants.RGBA;
            // this.internalFormat = WebGLConstants.RGBA;
            this.sampler = pRoot.defaultSampler;
            this.source = pImage;

            gltfRoot.textures.Add(pImageInfo.GetBHash(), this);
            LogGltf("{0} GltfTexture: created. ID={1}, uuid={2}, srcID={3}",
                    "Gltf", ID, underlyingUUID, source.ID);
        }

        public static GltfTextureB GltfTextureFactory(GltfB pRoot, ImageInfo pImageInfo, GltfImageB pImage,
                            BLogger pLog, gltfParamsB pParams) {
            if (!pRoot.textures.TryGetValue(pImageInfo.GetBHash(), out GltfTextureB tex)) {
                tex = new GltfTextureB(pRoot, pImageInfo, pImage, pLog, pParams);
            }
            return tex;
        }

        public override Object AsJSON() {
            GltfAttributesB ret = new GltfAttributesB();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (source != null) ret.Add("source", source.referenceID);
            if (sampler != null) ret.Add("sampler", sampler.referenceID);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }

        public GltfAttributesB TextureInfo() {
            GltfAttributesB ret = new GltfAttributesB {
                { "index", referenceID }
            };
            return ret;
        }
    }

    // =============================================================
    public class GltfImagesB : GltfListClassB<GltfImageB> {
        public GltfImagesB(GltfB pRoot) : base(pRoot) {
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfImageB theImage) {
            foreach (GltfImageB img in this.Values) {
                if (img.underlyingUUID != null && img.underlyingUUID == aUUID) {
                    theImage = img;
                    return true;
                }
            }
            theImage = null;
            return false;
        }
    }

    public class GltfImageB : GltfClassB {
        public OMV.UUID underlyingUUID;
        public ImageInfo imageInfo;
    
        public GltfImageB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            // gltfRoot.images.Add(this);
            LogGltf("{0} GltfImage: created empty. ID={1}", "Gltf", ID);
        }

        public GltfImageB(GltfB pRoot, ImageInfo pImageInfo, BLogger pLog, gltfParamsB pParams)
                                : base(pRoot, pImageInfo.handle.ToString() + "_img", pLog, pParams) {
            imageInfo = pImageInfo;
            AssetType = imageInfo.hasTransprency ? PersistRules.AssetType.ImageTrans : PersistRules.AssetType.Image;
            if (pImageInfo.handle is EntityHandleUUID handleU) {
                underlyingUUID = handleU.GetUUID();
            }
            gltfRoot.images.Add(pImageInfo.GetBHash(), this);
            LogGltf("{0} GltfImage: created. ID={1}, uuid={2}, imgInfoHandle={3}",
                    "Gltf", ID, underlyingUUID, imageInfo.handle);
        }

        public static GltfImageB GltfImageFactory(GltfB pRoot, ImageInfo pImageInfo, BLogger pLog, gltfParamsB pParams) {
            if (!pRoot.images.TryGetValue(pImageInfo.GetBHash(), out GltfImageB img)) {
                img = new GltfImageB(pRoot, pImageInfo, pLog, pParams);
            }
            return img;
        }

        public async void WriteImage(BAssetStorage pStorage) {
            string imgFilename = this.GetFilename();
            var targetType = PersistRules.FigureOutTargetTypeFromAssetType(AssetType, _params);
            using (var stream = new MemoryStream()) {
                imageInfo.GetImage().Save(stream, PersistRules.TargetTypeToImageFormat[targetType]);
                await pStorage.Store(imgFilename, stream.ToArray());
            }
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object> {
                { "uri", PersistRules.ReferenceURL(_params.uriBase, this.GetFilename()) }
            };
            return ret;
        }

        // An images filename includes size info
        public string GetFilename() {
            // return base.GetFilename(String.Format("{0}_{1}_{2}", underlyingUUID, imageInfo.xSize, imageInfo.ySize));
            return base.GetFilename(
                    String.Format("{0}_{1}_{2}", imageInfo.GetBHash(), imageInfo.xSize, imageInfo.ySize));
        }
    }

    // =============================================================
    public class GltfSamplersB : GltfListClassB<GltfSamplerB> {
        public GltfSamplersB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfSamplerB : GltfClassB {
        public uint? magFilter;
        public uint? minFilter;
        public uint? wrapS;
        public uint? wrapT;
        public string name;
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;

        public GltfSamplerB(GltfB pRoot, string pID, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.samplers.Add(new BHashULong(gltfRoot.samplers.Count), this);
            LogGltf("{0} GltfSampler: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (magFilter != null) ret.Add("magFilter", magFilter);
            if (magFilter != null) ret.Add("minFilter", minFilter);
            if (magFilter != null) ret.Add("wrapS", wrapS);
            if (magFilter != null) ret.Add("wrapT", wrapT);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfLightsB : GltfListClassB<GltfLightB> {
        public GltfLightsB(GltfB pRoot) : base(pRoot) {
        }
    }

    public class GltfLightB : GltfClassB {
        public string name;
        public GltfExtensionsB extensions;
        public GltfAttributesB extras;

        public GltfLightB(GltfB pRoot, string pID, MaterialInfo pMatInfo, BLogger pLog, gltfParamsB pParams) : base(pRoot, pID, pLog, pParams) {
            // gltfRoot.lights.Add(new BHashULong(gltfRoot.samplers.Count), this);
            LogGltf("{0} GltfLight: created empty. ID={1}", "Gltf", ID);
        }

        public static GltfLightB GltfLightFactory(GltfB pRoot, MaterialInfo pMatInfo) {
            GltfLightB lit = null;
            // if (!pRoot.lights.TryGetValue(pMatInfo.GetBHash(), out lit)) {
            //     lit = new GltfLight(pRoot, "light-" + pMatInfo.GetBHash().ToString(), pMatInfo);
            // }
            return lit;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfExtensionsB : Dictionary<string, GltfExtensionB> {
        public GltfExtensionsB(GltfB pRoot) : base() {
        }

        public Object AsJSON() {
            return this;
        }
    }

    public class GltfExtensionB : GltfClassB {
        public GltfAttributesB values;
        // possible entries in 'values'
        public static string valAmbient = "ambient";    // ambient color of surface (OMV.Vector4)
        public static string valDiffuse = "diffuse";    // diffuse color of surface (OMV.Vector4 or textureID)
        public static string valDoubleSided = "doubleSided";    // whether surface has backside ('true' or 'false')
        public static string valEmission = "emission";    // light emitted by surface (OMV.Vector4 or textureID)
        public static string valSpecular = "specular";    // color reflected by surface (OMV.Vector4 or textureID)
        public static string valShininess = "shininess";  // specular reflection from surface (float)
        public static string valTransparency = "transparency";  // transparency of surface (float)
        public static string valTransparent = "transparent";  // whether the surface has transparency ('true' or 'false;)

        public GltfExtensionB(GltfB pRoot, string extensionName, BLogger pLog, gltfParamsB pParams) : base(pRoot, extensionName, pLog, pParams) {
            pRoot.UsingExtension(extensionName);
            values = new GltfAttributesB();
            LogGltf("{0} GltfExtension: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            return values;
        }

        public int Count() {
            return values.Count;
        }
    }
}
