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
using System.Text;

using OMV = OpenMetaverse;

namespace org.herbal3d.cs.CommonEntities {
    // Some static routines for creating JSON output
    public class JSONHelpers {

        // Simple JSON serializer. Recognizes most object types and recursivily writes
        //    pretty formatted JSON to the output stream.
        public static void SimpleJSONOutput(StreamWriter outt, Object val) {
            SimpleOutputValue(outt, val, 0);
        }

        public static void SimpleOutputValue(StreamWriter outt, Object val, int level) {
            if (val == null) {
                // _log.ErrorFormat("SimpleJSONOutput: called with NULL value");
                val = "null";
            }
            if (val is string) {
                // escape any double quotes in the string value
                outt.Write("\"" + ((string)val).Replace("\"", "\\\"") + "\"");
            }
            else if (val is bool bval) {
                outt.Write(bval ? "true" : "false");
            }
            else if (val is OMV.Color4 col) {
                outt.Write(ParamsToJSONArray(col.R, col.G, col.B, col.A));
            }
            else if (val is OMV.Matrix4 mat) {
                outt.Write(ParamsToJSONArray(
                    mat[0, 0], mat[0, 1], mat[0, 2], mat[0, 3],
                    mat[1, 0], mat[1, 1], mat[1, 2], mat[1, 3],
                    mat[2, 0], mat[2, 1], mat[2, 2], mat[2, 3],
                    mat[3, 0], mat[3, 1], mat[3, 2], mat[3, 3]
                ));
            }
            else if (val is OMV.Vector3 vect) {
                outt.Write(ParamsToJSONArray(vect.X, vect.Y, vect.Z));
            }
            else if (val is OMV.Quaternion quan) {
                outt.Write(ParamsToJSONArray(quan.X, quan.Y, quan.Z, quan.W));
            }
            // else if (val.GetType().IsArray) {
            else if (val is Array) {
                outt.Write(" [ ");
                // Object[] values = (Object[])val;
                Array values = val as Array;
                bool first = true;
                for (int ii = 0; ii < values.Length; ii++) {
                    if (!first) outt.Write(",");
                    first = false;
                    SimpleOutputValue(outt, values.GetValue(ii), level + 1);
                }
                outt.Write(" ]");
            }
            else if (val is Dictionary<string, Object> dict) {
                outt.Write(" { ");
                bool first = true;
                foreach (var key in dict.Keys) {
                    if (!first) outt.Write(",");
                    first = false;
                    outt.Write("\n" + Indent(level) + "\"" + key + "\": ");
                    SimpleOutputValue(outt, dict[key], level + 1);
                }
                outt.Write("\n" + Indent(level) + " }");
            }
            // Have had problems with NaN values showing up in JSON as "NaN" string
            //   rather than a number and causing parsing errors. This code tells the
            //   user something is wrong but puts a number in the output JSON.
            else if (val is float && Single.IsNaN((float)val)) {
                // _log.ErrorFormat("JSONHelpers: Value is Single.NaN!!");
                outt.Write("0");
            }
            else if (val is double && Double.IsNaN((double)val)) {
                // _log.ErrorFormat("JSONHelpers: Value is Double.NaN!!");
                outt.Write("0");
            }
            else if (val is float) {
                // there is some problems with floats being different as binary
                //     and as text string. Don't know if limiting significant
                //     digits might help.
                // outt.Write(((float)val).ToString("0.#######"));
                outt.Write(((float)val).ToString("G9"));
            }
            else {
                var ret = val.ToString();
                if (ret == "NaN") {
                    // _log.ErrorFormat("JSONHelpers: Value is NaN!!");
                }
                else {
                    outt.Write(val);
                }
            }
        }

        public static string Indent(int level) {
            return "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t".Substring(0, level);
        }

        // Create a short array from a list of numbers. Used for vector items.
        // The values passed MUST be numbers or things that ToString() can expand.
        public static string ParamsToJSONArray(params Object[] vals) {
            StringBuilder buff = new StringBuilder();
            buff.Append(" [ ");
            bool first = true;
            // var stringVals = vals.Select(x => x.ToString()).ToArray();
            // buff.Append(String.Join(",", stringVals));
            foreach (Object obj in vals) {
                if (!first) buff.Append(", ");
                buff.Append(obj.ToString());
                first = false;
            }
            buff.Append(" ] ");
            return buff.ToString();
        }
    }
}
