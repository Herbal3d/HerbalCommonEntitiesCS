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

using org.herbal3d.cs.CommonEntitiesUtil;

using OMV = OpenMetaverse;

namespace org.herbal3d.cs.CommonEntities {
    /// <summary>
    /// A set of classes that hold viewer displayable items. These can be
    /// meshes, procedures, or whatever.
    /// </summary>

    // This is a rough map of how the OpenSimulator structures map onto the BInstance structures
    // BInstance                            SOG
    //     World Position
    //     Representation => Displayable
    // Displayable                          Root SOP
    //     offset
    //     renderable => DisplayableRenderable
    //     children
    //          [Displayable]               linkset SOPs
    // DisplayableRenderable == RenderableMeshGroup
    //     [Meshes] prim faces
    // Meshes
    //     mesh
    //     material
    //
    // This is a rough map of how the BInstance structures map onto GLTF
    // Node                             SOG/SOP
    //     World Position/Offset
    //     Mesh                         DisplayableRenderable of RootSOP
    //     Children
    //          [Node]                  linkset SOPs
    // Mesh
    //     [Primitives]                 DisplayableRenderable/RenderableMeshGroup - prim faces

    public class Displayable {
        public EntityHandle handle;
        public string name = "no name";

        public OMV.Vector3 offsetPosition = OMV.Vector3.Zero;
        public OMV.Quaternion offsetRotation = OMV.Quaternion.Identity;
        public OMV.Vector3 scale = OMV.Vector3.One;

        // Information on how to display
        public DisplayableRenderable renderable = null;
        public List<Displayable> children = new List<Displayable>();

        // Information from OpenSimulator
        public OMV.UUID baseUUID = OMV.UUID.Zero;   // the UUID of the original object that careated is displayable
        public BAttributes attributes = new BAttributes();

        private readonly IParameters _params;

        public Displayable(IParameters pParams) {
            handle = new EntityHandleUUID();
            _params = pParams;
        }

        public Displayable(DisplayableRenderable pRenderable, IParameters pParams) : this(pParams) {
            renderable = pRenderable;
        }

        public Displayable(DisplayableRenderable pRenderable, 
                        string pName, OMV.UUID pUUID,
                        OMV.Vector3 pOffsetPosition, OMV.Quaternion pOffsetRotation, OMV.Vector3 pScale,
                        BAttributes pObjectParams, IParameters pParams) : this(pParams) {
            name = pName;
            baseUUID = pUUID;
            // If not a root prim, add the offset to the root. 
            // The root Displayable will be zeros (not world position which is in the BInstance).
            if (pObjectParams.ContainsKey("IsRoot") && (bool)pObjectParams["IsRoot"]) {
                offsetPosition = pOffsetPosition;
                offsetRotation = pOffsetRotation;

            }
            if (_params.P<bool>("DisplayTimeScaling")) {
                scale = pScale;
            }

            attributes = pObjectParams;
            renderable = pRenderable;
        }

        // The hash of a Displayable is the hash of all its meshes
        public BHash GetBHash() {
            /*
            BHasher hasher = new BHasherMdjb2();
            if (renderable is RenderableMeshGroup meshGroup) {
                meshGroup.meshes.ForEach(renderableMesh => {
                    hasher.Add(renderableMesh.mesh.GetBHash().ToULong());
                });
            }
            return hasher.Finish();
            */
            return handle.GetBHash();
        }

        public T Attribute<T>(string pAttributeName) {
            T ret = default(T);
            object val;
            if (attributes.TryGetValue(pAttributeName, out val)) {
                try {
                    ret = (T)val;
                }
                catch {
                    ret = default(T);
                }
            }
            return ret;
        }

        public bool IsRoot {
            get {
                // The 'IsRoot' attribute is from OpenSimulator.
                // Should this be checking the number of children?
                return this.Attribute<bool>("IsRoot");
            }
        }
    }

    /// <summary>
    /// The parent class of the renderable parts of the displayable.
    /// Could be a mesh or procedure or whatever.
    /// </summary>
    public abstract class DisplayableRenderable {
        public EntityHandle handle;
        public DisplayableRenderable() {
            handle = new EntityHandleUUID();
        }
        public virtual BHash GetBHash() {
            return handle.GetBHash();
        }
    }

    /// <summary>
    /// A group of meshes that make up a renderable item.
    /// For OpenSimulator conversions, this is usually prim faces.
    /// </summary>
    public class RenderableMeshGroup : DisplayableRenderable {
        // The meshes that make up this Renderable
        public List<RenderableMesh> meshes;

        public RenderableMeshGroup() : base() {
            meshes = new List<RenderableMesh>();
        }

        // A DisplayableRenderable made of meshes has the hash of all its meshes and materials
        public override BHash GetBHash() {
            BHasher hasher = new BHasherMdjb2();
            meshes.ForEach(m => {
                m.GetBHash(hasher);
            });
            return hasher.Finish();
        }
    }
        
    // A renderable mesh has both mesh and material
    public class RenderableMesh {
        public int num;                 // number of this face on the prim
        public MeshInfo mesh;
        public MaterialInfo material;

        public BHash GetBHash() {
            BHasher hasher = new BHasherMdjb2();
            GetBHash(hasher);
            return hasher.Finish();
        }
        // Add my hashes to an in-progress hashing
        public void GetBHash(BHasher hasher) {
            hasher.Add(mesh.GetBHash());
            hasher.Add(material.GetBHash());
        }

        public override string ToString() {
            return "mesh=" + mesh.ToString() + "/mat=" + material.ToString();
        }
    }
} 
