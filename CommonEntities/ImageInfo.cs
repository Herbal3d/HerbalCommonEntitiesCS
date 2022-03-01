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
// using System.Drawing;
// using System.Drawing.Drawing2D;
// using System.Drawing.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using OMV = OpenMetaverse;

using org.herbal3d.cs.CommonUtil;

namespace org.herbal3d.cs.CommonEntities {

    public class ImageInfo {
        public EntityHandle handle;
        public OMV.UUID imageIdentifier;
        public bool hasTransprency = false;
        public bool resizable = true;   // true if image can be reduced in size
        public Image<Rgba32> image = null;
        public int xSize = 0;
        public int ySize = 0;

        private BHash _imageHash;

#pragma warning disable 414
        private readonly string _logHeader = "[ImageInfo]";
#pragma warning restore 414
        private readonly BLogger _log;
        private readonly IParameters _params;

        public ImageInfo(BLogger pLog) : this(new EntityHandleUUID(), pLog){
        }

        public ImageInfo(EntityHandle pHandle, BLogger pLog) {
            handle = pHandle;
            imageIdentifier = handle.GetUUID(); // image is unique unless underlying set
            _log = pLog;
        }

        // Create a new ImageInfo that has a copy of all the information from this one.
        // THis creates a copy of the image so it can be modified without touching the original.
        public ImageInfo Clone() {
            ImageInfo ret = new ImageInfo(_log);
            if (image != null) {
                // Not sure which operation to use. Which is the quickest?
                // ret.SetImage(image.Clone(x => x.Resize(image.Width, image.Height)));
                // ret.SetImage(image.Clone(x => x.Crop(image.Width, image.Height)));
                ret.SetImage(image.CloneAs<Rgba32>());
            }
            return ret;
        }

        // Set the image into this structure and update all the auxillery info
        public void SetImage(Image pImage) {
            image = pImage as Image<Rgba32>;
            if (image is null) {
                throw new ArgumentException("ImageInfo.SetImage: Passed image not convertable to Image<Rgba32>");
            }
            xSize = image.Width;
            ySize = image.Height;
            hasTransprency = CheckForTransparency();
            ComputeImageHash();
            // _log.DebugFormat("{0} SetImage. ID={1}, xSize={2}, ySize={3}, hasTrans={4}",
            //             _logHeader, handle, xSize, ySize, hasTransprency);
        }

        // The hash code for an image is just the hash of its UUID handle.
        public BHash GetBHash() {
            if (_imageHash == null) {
                return handle.GetBHash();
            }
            return _imageHash;
        }

        // Check the image in this TextureInfo for transparency and set this.hasTransparency.
        // Since images without transparancy just have the alpha channel as 255, we
        //    must scan the pixels to verify there is not transparancy.
        // One can pass the previous value of hasTransparancy to skip a rescan.
        public bool CheckForTransparency(bool? alreadyHas = false) {
            bool hasTransprency = alreadyHas ?? false;
            Rgba32 transparent = Color.Transparent;
            if (image != null && !hasTransprency) {
                if (image.PixelType.AlphaRepresentation != null) {
                    for (int xx = 0; xx < image.Width; xx++) {
                        for (int yy = 0; yy < image.Height; yy++) {
                            var pixel = image[xx, yy];
                            if (pixel.A != transparent.A) {
                                hasTransprency = true;
                                break;
                            }
                        }
                    }
                }
            }
            return hasTransprency;
        }

        /// <summary>
        /// If the image is larger than a max, resize the image.
        /// </summary>
        /// <param name="maxTextureSize"></param>
        /// <returns>'true' if the image was converted</returns>
        public bool ConstrainTextureSize(int maxTextureSize) {
            bool ret = false;
            int size = maxTextureSize;
            if (image != null && (image.Width > size || image.Height > size)) {
                /*
                int sizeW = size;
                int sizeH = size;
                */
                int sizeW = image.Width;
                int sizeH = image.Height;
                if (image.Width > size) {
                    sizeH = (int)(image.Height * (size / image.Width));
                }
                else {
                    sizeW = (int)(image.Width * (size / image.Height));
                }
                /*
                Image thumbNail = new Bitmap(sizeW, sizeH, image.PixelFormat);
                using (Graphics g = Graphics.FromImage(thumbNail)) {
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    Rectangle rect = new Rectangle(0, 0, sizeW, sizeH);
                    g.DrawImage(image, rect);
                }
                image = thumbNail;
                xSize = thumbNail.Width;
                ySize = thumbNail.Height;
                */
                image = image.Clone(x => x.Resize(sizeW, sizeH));
                ComputeImageHash();
                ret = true;
            }
            return ret;
        }

        // Computes a SHA256 hash of the image
        public void ComputeImageHash() {
            BHasher hasher = new BHasherSHA256();
            if (image != null) {
                /*
                // ImageConverter is not available in .NET Core
                // ImageConverter converter = new ImageConverter();
                // byte[] data = (byte[])converter.ConvertTo(image, typeof(byte[]));
                MemoryStream ms = new MemoryStream();
                image.Save(ms, ImageFormat.Png);
                byte[] data = ms.ToArray();
                _imageHash = hasher.Finish(data, 0, data.Length);
                */
                // byte[] pixelBytes = new byte[image.Width * image.Height * Unsafe.Sizeof<Rgba32>()];
                int pixelBytesSize = image.Width * image.Height * 4;
                byte[] pixelBytes = new byte[pixelBytesSize];
                image.CopyPixelDataTo(pixelBytes);
                _imageHash = hasher.Finish(pixelBytes, 0, pixelBytesSize - 1);
            }
            else {
                // If there isn't an image, use the UUID
                _imageHash = hasher.Finish(imageIdentifier.GetBytes(), 0, 16);
            }
        }

        public override string ToString()
        {
            return String.Format("id={0},{1}x{2}{3}", handle.GetUUID(),
                            xSize, ySize,
                            hasTransprency ? "/hasTrans" : "");
        }


    }

}
