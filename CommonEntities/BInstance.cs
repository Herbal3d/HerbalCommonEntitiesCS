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

using OMV = OpenMetaverse;

namespace org.herbal3d.cs.CommonEntities {
    public class BInstance {
        public EntityHandle handle = new EntityHandleUUID();
        public OMV.Vector3 Position = OMV.Vector3.Zero;
        public OMV.Quaternion Rotation = OMV.Quaternion.Identity;
        public CoordAxis coordAxis = new CoordAxis(CoordAxis.RightHand_Zup);    // SL coordinates
        public Displayable Representation;

        public BInstance() {
        }
    }
}
