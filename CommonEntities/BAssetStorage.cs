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
using System.IO;
using System.Text;
using System.Threading.Tasks;

using org.herbal3d.cs.CommonUtil;

namespace org.herbal3d.cs.CommonEntities {

    public class BAssetStorage : IDisposable {
    #pragma warning disable 414
        private readonly string _logHeader = "[BAssetStorage]";
#pragma warning restore 414
        protected string _outputDir;
        protected bool _useDeepFilenames;
        protected readonly IBLogger _log;

        public BAssetStorage(IBLogger logger, string outputDir, bool useDeepFilenames) {
            _log = logger;
            _outputDir = outputDir;
            _useDeepFilenames = useDeepFilenames;
        }

        public Task<byte[]> Fetch(EntityHandle pHandle) {
            return Fetch(pHandle.ToString().Replace("-", ""));
        }

        // Returns a byte array of length zero if item was not fetched.
        public Task<byte[]> Fetch(string pEntityName) {
            return Task<byte[]>.Run(() => {
                byte[] ret = new byte[0];
                string outDir = this.GetStorageDir(pEntityName);
                string absDir = Path.GetFullPath(outDir);
                string absFilename = Path.Combine(absDir, pEntityName);
                try {
                    ret = File.ReadAllBytes(absFilename);
                }
                catch (DirectoryNotFoundException e) {
                    _log.Error("{0} DirectoryNotFound exception fetching {1}", _logHeader, pEntityName);
                    var ee = e; // suppress 'e' not used warning
                    ret = new byte[0];
                }
                catch (FileNotFoundException e) {
                    _log.Error("{0} FileNotFound exception fetching {1}", _logHeader, pEntityName);
                    var ee = e; // suppress 'e' not used warning
                    ret = new byte[0];
                }
                catch (Exception e) {
                    _log.Error("{0} Exception fetching {1}: {2}", _logHeader, pEntityName, e);
                    ret = new byte[0];
                }
                return ret;
            });
        }

        // Given an entity name ("entity.ext"), return a Stream accessing the item
        public Task<Stream> GetStream(string pEntityName) {
            return Task<Stream>.Run(() => {
                Stream ret = null;
                string outDir = this.GetStorageDir(pEntityName);
                string absDir = Path.GetFullPath(outDir);
                string absFilename = Path.Combine(absDir, pEntityName);
                try {
                    ret = File.Open(absFilename, FileMode.Open);
                }
                catch (DirectoryNotFoundException e) {
                    _log.Error("{0} DirectoryNotFound exception fetching {1}", _logHeader, pEntityName);
                    var ee = e; // suppress 'e' not used warning
                    ret = null;
                }
                catch (FileNotFoundException e) {
                    _log.Error("{0} FileNotFound exception fetching {1}", _logHeader, pEntityName);
                    var ee = e; // suppress 'e' not used warning
                    ret = null;
                }
                catch (Exception e) {
                    _log.Error("{0} Exception fetching {1}: {2}", _logHeader, pEntityName, e);
                    ret = null;
                }
                return ret;
            });
        }

        public Task Store(EntityHandle pHandle, byte[] pData) {
            return Store(pHandle.ToString().Replace("-", ""), pData);
        }

        public Task Store(string pEntityName, byte[] pData) {
            return Task<byte[]>.Run(() => {
                string strippedEntityName = Path.GetFileNameWithoutExtension(pEntityName);
                string outDir = this.GetStorageDir(strippedEntityName);
                string absDir = PersistRules.CreateDirectory(outDir);
                string absFilename = Path.Combine(absDir, pEntityName);
                File.WriteAllBytes(absFilename, pData);
            });
            /* alternate version that uses 'await'
            string outDir = this.GetStorageDir(pEntityName);
            string absDir = PersistRules.CreateDirectory(outDir, _params);
            string absFilename = Path.Combine(absDir, pEntityName);
            using (FileStream stream = File.Open(absFilename, FileMode.OpenOrCreate)) {
                await stream.WriteAsync(pData, 0, pData.Length);
            }
            */
        }

        public Task<string> FetchText(EntityHandle pHandle) {
            return FetchText(pHandle.ToString().Replace("-", ""));
        }

        public async Task<string> FetchText(string pEntityName) {
            byte[] data = await Fetch(pEntityName);
            return Encoding.UTF8.GetString(data);
        }

        public Task StoreText(EntityHandle pHandle, string pContents) {
            return StoreText(pHandle.ToString().Replace("-", ""), pContents);
        }

        public async Task StoreText(string pEntityName, string pContents) {
            await Store(pEntityName, Encoding.UTF8.GetBytes(pContents));
        }

        public string GetStorageDir(string pStorageName) {
            string strippedStorageName = Path.GetFileNameWithoutExtension(pStorageName);
            return PersistRules.StorageDirectory(strippedStorageName, _outputDir, _useDeepFilenames);
        }

        public void Dispose() {
        }
    }
}
