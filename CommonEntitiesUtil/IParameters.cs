/*
 * Copyright (c) 2019 Robert Adams
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
using System.Text;

namespace org.herbal3d.cs.CommonEntitiesUtil {
    public interface IParameters {
        // Basic parameter fetching interface which returns the parameter
        //   value of the requested parameter name.
        T P<T>(string paramName);
    }

    public class ParameterCollection : IParameters {

        Dictionary<string, object> _params = new Dictionary<string, object>();

        public ParameterCollection() {
        }

        public T P<T>(string pName) {
            T ret = default(T);
            Object val;
            if (_params.TryGetValue(pName.ToLower(), out val)) {
                if (val is T) {
                    ret = (T)val;
                }
            }
            return ret;
        }

        public ParameterCollection Add(string pName, Object pValue) {
            _params.Add(pName.ToLower(), pValue);
            return this;
        }
        
    }
}
