/*
 * Copyright (c) 2016 Robert Adams
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
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using org.herbal3d.cs.CommonUtil;

using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace org.herbal3d.cs.CommonEntities {

    public class PrimToMesh {
        private MeshmerizerR _mesher;
        static private readonly String _logHeader = "[PrimToMesh]";

        private readonly BLogger _log;
        private readonly BConverterOSParams _params;

        public PrimToMesh(BLogger pLog, BConverterOSParams pParams) {
            _mesher = new MeshmerizerR();
            _log = pLog;
            _params = pParams;
        }

        /// <summary>
        /// Create and return a set of meshes/materials that make the passed SOP.
        /// This just deals the making a mesh from the SOP and getting the material/texture of the meshes
        ///    into the caches.
        /// </summary>
        /// <param name="prim">the prim to convert</param>
        /// <param name="sog">the Scene Object Group that contains the prim</param>
        /// <param name="sop">the Scene Object Part that contains the prim</param>
        /// <param name="assetManager">asset fetcher to use to get required pieces (image for sculpty, ...)</param>
        /// <param name="lod"></param>
        /// <returns> Returns 'null' if the SOG/SOP could not be converted into a displayable </returns>
        public async Task<Displayable> CreateMeshResource(SceneObjectGroup sog, SceneObjectPart sop,
                    OMV.Primitive prim, AssetManager assetManager, OMVR.DetailLevel lod) {

            Displayable displayable = null;
            try {
                if (prim.Sculpt != null) {
                    if (prim.Sculpt.Type == OMV.SculptType.Mesh) {
                        LogBProgress("{0}: CreateMeshResource: creating mesh", _logHeader);
                        // _tats.numMeshAssets++;
                        var dispable = await MeshFromPrimMeshData(sog, sop, prim, assetManager, lod);
                        displayable = new Displayable(dispable,
                                                sop.Name, sop.UUID,
                                                sop.OffsetPosition,
                                                sop.RotationOffset,
                                                sop.Scale,
                                                CollectObjectAttributes(sop));
                        // displayable.Validate("mesh", _log);
                    }
                    else {
                        LogBProgress("{0}: CreateMeshResource: creating sculpty", _logHeader);
                        // _stats.numSculpties++;
                        var dispable = await MeshFromPrimSculptData(sog, sop, prim, assetManager, lod);
                        displayable = new Displayable(dispable,
                                                sop.Name,
                                                sop.UUID,
                                                sop.OffsetPosition,
                                                sop.RotationOffset,
                                                sop.Scale,
                                                CollectObjectAttributes(sop));
                        // displayable.Validate("sculpt", _log);
                    }
                }
                else {
                    LogBProgress("{0}: CreateMeshResource: creating primshape", _logHeader);
                    // _stats.numSimplePrims++;
                    var dispable = await MeshFromPrimShapeData(sog, sop, prim, assetManager, lod);
                    displayable = new Displayable(dispable,
                                            sop.Name,
                                            sop.UUID,
                                            sop.OffsetPosition,
                                            sop.RotationOffset,
                                            sop.Scale,
                                            CollectObjectAttributes(sop));
                    // displayable.Validate("prim", _log);
                }
            }
            catch (Exception e) {
                string errorMsg = String.Format("{0} CreateMeshResource: exception meshing {1}: {2}",
                            _logHeader, sop.UUID, e);
                _log.Error(errorMsg);
                throw new Exception(errorMsg);
            }
            return displayable;
        }

        // Attributes from the SceneObjectPart are passed in an 'object attributes' structure.
        // Build the attribute list for this SOP.
        private BAttributes CollectObjectAttributes(SceneObjectPart pSop) {
            BAttributes attributes = new BAttributes();
            attributes.Add("IsRoot", pSop.IsRoot);
            attributes.Add("HasScriptsInInventory", pSop.Inventory.ContainsScripts());
            attributes.Add("IsPhysical", (pSop.PhysActor != null && pSop.PhysActor.IsPhysical));
            return attributes;
        }

        /// <summary>
        /// Convert the mesh into a DisplayableRenderable.
        /// 
        /// Note that this could return a copy that is already in the assets storage (remove duplicates).
        /// To do this, it must convert this passed mesh into a DisplayableRenderable to create the
        /// meshes hash and then using that hash to look for the mesh in the asset store.
        /// </summary>
        /// <param name="sog"></param>
        /// <param name="sop"></param>
        /// <param name="prim"></param>
        /// <param name="assetManager"></param>
        /// <param name="lod"></param>
        /// <returns></returns>
        private async Task<DisplayableRenderable> MeshFromPrimShapeData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, AssetManager assetManager, OMVR.DetailLevel lod) {
            // Convert the prim into a mesh
            OMVR.FacetedMesh mesh = _mesher.GenerateFacetedMesh(prim, lod);
            // Convert the mesh into a displayable
            DisplayableRenderable dr = await ConvertFacetedMeshToDisplayable(assetManager, mesh, prim.Textures.DefaultTexture, prim.Scale);
            // Get the hash for this converted prim/mesh
            BHash drHash = dr.GetBHash();
            // Get the displayable that is already in the asset store or use the one we just built
            DisplayableRenderable realDR = assetManager.Assets.GetRenderable(drHash, () => { return dr; });
            LogBProgress("{0} MeshFromPrimShapeData. numGenedMeshed={1}",
                    _logHeader, ((RenderableMeshGroup)realDR).meshes.Count);
            return realDR;
        }

        private async Task<DisplayableRenderable> MeshFromPrimSculptData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, AssetManager assetManager, OMVR.DetailLevel lod) {
            DisplayableRenderable realDR = null;
            try {
                // Get the asset that the sculpty is built on
                EntityHandleUUID texHandle = new EntityHandleUUID(prim.Sculpt.SculptTexture);
                Bitmap sculptTexture = null;
                try {
                    Image img = await assetManager.OSAssets.FetchTextureAsImage(texHandle);

                    // If image has transparancy, remove it.
                    // A common thing is to lay a transparency layer over the sculpt texture to make it harder to copy.
                    sculptTexture = img as Bitmap;
                    if (Image.IsAlphaPixelFormat(img.PixelFormat)) {
                        // TODO: There has got to be a quicker way to set the alpha layer to a value
                        for (int xx = 0; xx < sculptTexture.Width; xx++) {
                            for (int yy = 0; yy < sculptTexture.Height; yy++) {
                                Color pix = sculptTexture.GetPixel(xx, yy);
                                sculptTexture.SetPixel(xx, yy, Color.FromArgb(255, pix.R, pix.G, pix.B));
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    _log.Error("{0} MeshFromPrimSculptData: exception fetching texture {1}. Building replacement",
                        _logHeader, texHandle);
                    // For some reason can't get the texture. So fake something
                    // Loosely based on C++ code detailed in
                    //       https://wiki.secondlife.com/wiki/Sculpted_Prims:_Technical_Explanation
                    var wid = 32;
                    var hig = 32;
                    sculptTexture = new Bitmap(wid, hig);
                    for (int yy = 0; yy < hig; ++yy) {
                        for (int xx = 0; xx < wid; ++xx) {
                            var r = (float)xx / (wid / 2);
                            var g = (float)yy / (hig / 2);
                            var b = 1.0;
                            if (xx % 2 == 1) b = 0.5;
                            if (yy % 2 == 1) b = 0.5;
                            if (xx == wid - 1) b = 0.98;
                            if (yy == hig - 1) b = 0.98;
                            if (xx == wid / 2 && (yy == 0 || yy == hig-1)) b = 0.7;
                            sculptTexture.SetPixel(xx, yy, Color.FromArgb(255, (int)r, (int)g, (int)b));
                        }
                    }
                }

                OMVR.FacetedMesh fMesh = _mesher.GenerateFacetedSculptMesh(prim, sculptTexture, lod);
                DisplayableRenderable dr =
                        await ConvertFacetedMeshToDisplayable(assetManager, fMesh, prim.Textures.DefaultTexture, prim.Scale);
                // Get the hash for this converted prim/mesh
                BHash drHash = dr.GetBHash();
                // Get the displayable that is already in the asset store or use the one we just built
                realDR = assetManager.Assets.GetRenderable(drHash, () => { return dr; });
                LogBProgress("{0} MeshFromPrimSculptData. numFaces={1}, numGenedMeshed={2}",
                                _logHeader, fMesh.Faces.Count, ((RenderableMeshGroup)realDR).meshes.Count);
            }
            catch (Exception e) {
                string errorMsg = String.Format("{0} MeshFromPrimSculptData: exception meshing {1}: {2}",
                            _logHeader, sop.UUID, e);
                _log.Error(errorMsg);
                throw new Exception(errorMsg);
            }
            return realDR;
        }

        private async Task<DisplayableRenderable> MeshFromPrimMeshData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, AssetManager assetManager, OMVR.DetailLevel lod) {

            DisplayableRenderable realDR = null;
            Exception failure = null;

            try {
                EntityHandleUUID meshHandle = new EntityHandleUUID(prim.Sculpt.SculptTexture);
                var meshBytes = await assetManager.OSAssets.FetchRawAsset(meshHandle);
                // OMVA.AssetMesh meshAsset = new OMVA.AssetMesh(prim.ID, meshBytes);
                // if (OMVR.FacetedMesh.TryDecodeFromAsset(prim, meshAsset, lod, out fMesh)) {
                OMVR.FacetedMesh fMesh = null;
                try {
                    fMesh = _mesher.GenerateFacetedMeshMesh(prim, meshBytes);
                }
                catch (Exception e) {
                    _log.Error("{0} MeshFromPrimMeshData: Exception from GenerateFacetedMeshMesh: {1}", _logHeader, e);
                    fMesh = null;
                }
                if (fMesh != null) {
                    DisplayableRenderable dr = await ConvertFacetedMeshToDisplayable(assetManager, fMesh, prim.Textures.DefaultTexture, prim.Scale);
                    // Don't know the hash of the DisplayableRenderable until after it has been created.
                    // Now use the hash to see if this has already been done.
                    // If this DisplayableRenderable has already been built, use the other one and throw this away.
                    BHash drHash = dr.GetBHash();
                    realDR = assetManager.Assets.GetRenderable(drHash, () => { return dr; });
                }
                else {
                    failure = new Exception("MeshFromPrimMeshData: could not decode mesh information from asset. ID="
                                    + prim.ID.ToString());
                }
            }
            catch (Exception e) {
                failure = new Exception(
                    String.Format("MeshFromPrimMeshData: could not decode mesh information from asset. ID={0}: {1}",
                                    prim.ID, e)
                );
            }
            if (failure != null) {
                throw failure;
            }
            return realDR;
        }

        /// <summary>
        /// Given a FacetedMesh, create a DisplayableRenderable (a list of RenderableMesh's with materials).
        /// This also creates underlying MesnInfo, MaterialInfo, and ImageInfo in the AssetFetcher.
        /// </summary>
        /// <param name="assetManager"></param>
        /// <param name="fmesh">The FacetedMesh to convert into Renderables</param>
        /// <param name="defaultTexture">If a face doesn't have a texture defined, use this one.
        /// This is an OMV.Primitive.TextureEntryFace that includes a lot of OpenSimulator material info.</param>
        /// <param name="primScale">Scaling for the base prim that is used when appliying any texture
        /// to the face (updating UV).</param>
        /// <returns></returns>
        private async Task<DisplayableRenderable> ConvertFacetedMeshToDisplayable(AssetManager assetManager, OMVR.FacetedMesh fmesh,
                        OMV.Primitive.TextureEntryFace defaultTexture, OMV.Vector3 primScale) {
            RenderableMeshGroup ret = new RenderableMeshGroup();
            foreach (var face in fmesh.Faces) {
                if (face.Indices.Count > 0) {
                    try {
                        ret.meshes.Add(await ConvertFaceToRenderableMesh(face, assetManager, defaultTexture, primScale));
                    }
                    catch (Exception e) {
                        _log.Error("{0} ConvertFacetedMeshToDisplayable: exception: {1}", _logHeader, e);
                    }
                }
            }
            // _log.DebugFormat("{0} ConvertFacetedMeshToDisplayable: complete. numMeshes={1}", _logHeader, ret.meshes.Count);
            return ret;
        }

        private async Task<RenderableMesh> ConvertFaceToRenderableMesh(OMVR.Face face, AssetManager assetManager,
                        OMV.Primitive.TextureEntryFace defaultTexture, OMV.Vector3 primScale) {
            RenderableMesh rmesh = new RenderableMesh {
                num = face.ID
            };

            // Copy one face's mesh imformation from the FacetedMesh into a MeshInfo
            MeshInfo meshInfo = new MeshInfo {
                vertexs = face.Vertices.Select(v => {
                    // Some normals are returned as zero from the meshmerizer
                    if (v.Normal == OMV.Vector3.Zero) {
                        v.Normal = OMV.Vector3.UnitX;
                    }
                    return v;
                }).ToList(),
                // vertexs = new List<OMVR.Vertex>(face.Vertices),
                indices = face.Indices.ConvertAll(ii => (int)ii),
                faceCenter = face.Center,
                scale = primScale
            };
            LogBProgress("{0} ConvertFaceToRenderableMesh: faceId={1}, numVert={2}, numInd={3}",
                 _logHeader, face.ID, meshInfo.vertexs.Count, meshInfo.indices.Count);

            if (!_params.displayTimeScaling) {
                if (ScaleMeshes(meshInfo, primScale)) {
                    LogBProgress("{0} ConvertFaceToRenderableMesh: scaled mesh to {1}",
                             _logHeader, primScale);
                }
                meshInfo.scale = OMV.Vector3.One;
            }

            // Find or create the MaterialInfo for this face.
            MaterialInfo matInfo = new MaterialInfo(face, defaultTexture, _params.doubleSided);
            if (matInfo.textureID != null
                        && matInfo.textureID != OMV.UUID.Zero
                        && matInfo.textureID != OMV.Primitive.TextureEntry.WHITE_TEXTURE) {
                // Textures/images use the UUID from OpenSim and the hash is just the hash of the UUID
                EntityHandleUUID textureHandle = new EntityHandleUUID((OMV.UUID)matInfo.textureID);
                BHash textureHash = new BHashULong(textureHandle.GetUUID().GetHashCode());
                ImageInfo lookupImageInfo = await assetManager.Assets.GetImageInfo(textureHash, async () => {
                    // The image is not in the cache yet so create an ImageInfo entry for it
                    // Note that image gets the same UUID as the OpenSim texture
                    ImageInfo imageInfo = new ImageInfo(textureHandle, _log);
                    try {
                        var img = await assetManager.OSAssets.FetchTextureAsImage(textureHandle);
                        imageInfo.SetImage(img);
                    }
                    catch (Exception e) {
                        // Failure getting the image
                        _log.Error("{0} Failure fetching material texture. id={1}. {2}",
                                    _logHeader, matInfo.textureID, e.Message);
                        // Create a simple, single color image to fill in for the missing image
                        imageInfo.SetImage(ImageInfo.CreateFillImage());
                    }
                    imageInfo.imageIdentifier = (OMV.UUID)matInfo.textureID;
                    LogBProgress("{0} ConvertFaceToRenderableMesh: create ImageInfo. hash={1}, id={2}",
                                    _logHeader, textureHash, imageInfo.handle);
                    return imageInfo;
                });
                matInfo.image = lookupImageInfo;

                // Update the UV information for the texture mapping
                LogBProgress("{0} ConvertFaceToRenderableMesh: Converting tex coords using {1} texture",
                             _logHeader, face.TextureFace == null ? "default" : "face");
                _mesher.TransformTexCoords(meshInfo.vertexs, meshInfo.faceCenter,
                        face.TextureFace ?? defaultTexture,  primScale);
            }

            // See that the material is in the cache
            MaterialInfo lookupMatInfo = assetManager.Assets.GetMaterialInfo(matInfo.GetBHash(), () => { return matInfo; });
            rmesh.material = lookupMatInfo;

            // See that the mesh is in the cache
            MeshInfo lookupMeshInfo = assetManager.Assets.GetMeshInfo(meshInfo.GetBHash(true), () => { return meshInfo; });
            rmesh.mesh = lookupMeshInfo;
            if (lookupMeshInfo.indices.Count == 0) {    // DEBUG DEBUG
                _log.Error("{0} indices count of zero. rmesh={1}", _logHeader, rmesh.ToString());
            }   // DEBUG DEBUG

            LogBProgress("{0} ConvertFaceToRenderableMesh: rmesh.mesh={1}, rmesh.material={2}",
                             _logHeader, rmesh.mesh, rmesh.material);

            return rmesh;
        }

        // Returns an ExtendedPrimGroup with a mesh for the passed heightmap.
        // Note that the returned EPG does not include any face information -- the caller must add a texture.
        public async Task<DisplayableRenderable> MeshFromHeightMap( float[,] pHeightMap, int regionSizeX, int regionSizeY,
                    AssetManager assetManager, OMV.Primitive.TextureEntryFace defaultTexture) {

            // OMVR.Face rawMesh = m_mesher.TerrainMesh(pHeightMap, 0, pHeightMap.GetLength(0)-1, 0, pHeightMap.GetLength(1)-1);
            _log.Debug("{0} MeshFromHeightMap: heightmap=<{1},{2}>, regionSize=<{3},{4}>",
                    _logHeader, pHeightMap.GetLength(0), pHeightMap.GetLength(1), regionSizeX, regionSizeY);
            OMVR.Face rawMesh = Terrain.TerrainMesh(pHeightMap, (float)regionSizeX, (float)regionSizeY);

            RenderableMesh rm = await ConvertFaceToRenderableMesh(rawMesh, assetManager, defaultTexture, new OMV.Vector3(1, 1, 1));

            RenderableMeshGroup rmg = new RenderableMeshGroup();
            rmg.meshes.Add(rm);

            return rmg;
        }

        // Walk through all the vertices and scale the included meshes
        // Returns 'true' of the mesh was changed.
        public static bool ScaleMeshes(MeshInfo meshInfo, OMV.Vector3 scale) {
            bool ret = false;
            if (scale.X != 1.0 || scale.Y != 1.0 || scale.Z != 1.0) {
                ret = true;
                for (int ii = 0; ii < meshInfo.vertexs.Count; ii++) {
                    OMVR.Vertex aVert = meshInfo.vertexs[ii];
                    aVert.Position *= scale;
                    meshInfo.vertexs[ii] = aVert;
                }
            }
            return ret;
        }
        // Loop over all the vertices in an ExtendedPrim and perform some operation on them
        public delegate void OperateOnVertex(ref OMVR.Vertex vert);
        public static void OnAllVertex(MeshInfo mi, OperateOnVertex vertOp) {
            for (int jj = 0; jj < mi.vertexs.Count; jj++) {
                OMVR.Vertex aVert = mi.vertexs[jj];
                vertOp(ref aVert);
                mi.vertexs[jj] = aVert;
            }
        }

        public void LogBProgress(string msg, params Object[] args) {
            if (_params.logBuilding) {
                _log.Debug(msg, args);
            }
        }

        public void DumpVertices(string head, List<ushort> indices, List<OMVR.Vertex> vertices) {
            StringBuilder buff = new StringBuilder();
            buff.Append(head);
            indices.ForEach(ind => {
                buff.AppendFormat("{0}, ", vertices[ind]);
            });
            _log.Debug(buff.ToString());
        }

        public void DumpVertices(string head, List<int> indices, List<OMVR.Vertex> vertices) {
            StringBuilder buff = new StringBuilder();
            buff.Append(head);
            indices.ForEach(ind => {
                buff.AppendFormat("{0}, ", vertices[ind]);
            });
            _log.Debug(buff.ToString());
        }
    }
}
