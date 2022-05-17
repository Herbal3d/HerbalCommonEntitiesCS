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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

using OMV = OpenMetaverse;

using org.herbal3d.cs.CommonUtil;

namespace org.herbal3d.cs.CommonEntities {

    public class ImageInfo {
        public EntityHandle handle;
        public OMV.UUID imageIdentifier;
        public bool hasTransprency = false;
        public bool resizable = true;   // true if image can be reduced in size
        public Image image {
            get { return _image; }
            set {
                _image = value;
                if (_image != null) {
                    xSize = image.Width;
                    ySize = image.Height;
                    hasTransprency = _checkForTransparency();
                    _convertToOutputBytes();
                    _computeImageHash();
                    // _log.DebugFormat("{0} SetImage. ID={1}, xSize={2}, ySize={3}, hasTrans={4}",
                    //             _logHeader, handle, xSize, ySize, hasTransprency);
                }
                else {
                    xSize = 0;
                    ySize = 0;
                    hasTransprency = false;
                    _image = null;
                    _convertedImage = null;
                }
            }
        }
        public int xSize = 0;
        public int ySize = 0;

        private Image _image;
        private byte[] _convertedImage;
        private BHash _imageHash;

#pragma warning disable 414
        private readonly string _logHeader = "[ImageInfo]";
#pragma warning restore 414
        private readonly BLogger _log;

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
            ret.image = (Image)_image.Clone();
            return ret;
        }

        // Set the image into this structure and update all the auxillery info.
        // Equivalent to setting this.image but with the option to resize the image.
        public void SetImage(Image pImage, int pXsize = 0, int pYsize = 0) {
            _image = pImage;
            if (pXsize != 0 || pYsize != 0) {
                _constrainTextureSize(Math.Min(pXsize, pYsize));
            }
            image = _image;

        }

        // Create a replacement image that is used when something is needed.
        public static Image CreateFillImage() {
            const int wid = 16;
            var fillInImage = new Bitmap(wid, wid, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Color theColor = Color.FromArgb(128, 202, 213, 170);    // 0x80CAB5AA
            for (int xx = 0; xx < wid; xx++)
                for (int yy = 0; yy < wid; yy++)
                    fillInImage.SetPixel(xx, yy, theColor);
            return fillInImage;
        }

        // Get the image. If there is no image set, return a fabricated image.
        public Image GetImage() {
            if (_image == null) {
                _log.Error("{0} Replacing missing image {1}", _logHeader, imageIdentifier);
                return ImageInfo.CreateFillImage();
            }
            return _image;
        }

        // Get the image as bytes converted into the output format (PNG, JPEG, ...)
        public byte[] GetConvertedImage() {
            return _convertedImage;
        }

        // The hash code for an image is just the hash of its UUID handle.
        public BHash GetBHash() {
            if (_imageHash == null) {
                return handle.GetBHash();
            }
            return _imageHash;
        }

        // Check the image in this TextureInfo for transparency and set this.hasTransparency.
        private bool _checkForTransparency() {
            hasTransprency = false;
            if (image != null) {
                if (Image.IsAlphaPixelFormat(image.PixelFormat)) {
                    // The image could have alpha values in it
                    if (image is Bitmap bitmapImage) {
                        for (int xx = 0; xx < bitmapImage.Width; xx++) {
                            for (int yy = 0; yy < bitmapImage.Height; yy++) {
                                if (bitmapImage.GetPixel(xx, yy).A != 255) {
                                    hasTransprency = true;
                                    break;
                                }
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
            bool ret = _constrainTextureSize(maxTextureSize);
            if (_image != null) {
                _convertToOutputBytes();
                _computeImageHash();
            }
            return ret;
        }

        // The image is supplied as a System.Drawing.Image but it is output
        //     as one of the world known image formats (PNG, JPG, ...).
        // This converts the passed Image to the output format in bytes.
        // This byte form is used for hashing and for output (through GLTF).
        private void _convertToOutputBytes() {
            if (image != null) {
                // ImageConverter is not available in .NET Core
                // ImageConverter converter = new ImageConverter();
                // byte[] data = (byte[])converter.ConvertTo(image, typeof(byte[]));
                // var outFormat = PersistRules.TargetTypeToImageFormat(PersistRules.TextureFormatToTargetType[_params.preferredTextureFormatIfNoTransparency.ToLower()]);
                var outFormat = ImageFormat.Jpeg;
                if (this.hasTransprency) {
                    // outFormat = PersistRules.TargetTypeToImageFormat(PersistRules.TextureFormatToTargetType[_params.preferredTextureFormat.ToLower()]);
                    outFormat = ImageFormat.Png;
                }

                MemoryStream ms = new MemoryStream();
                image.Save(ms, outFormat);
                _convertedImage = ms.ToArray();
            }
        }

        private bool _constrainTextureSize(int maxTextureSize) {
            bool ret = false;
            int size = maxTextureSize;
            if (_image != null && this.resizable && (_image.Width > size || _image.Height > size)) {
                int sizeW = size;
                int sizeH = size;
                /*
                if (inImage.Width > size) {
                    sizeH = (int)(inImage.Height * (size / inImage.Width));
                }
                else {
                    sizeW = (int)(inImage.Width * (size / inImage.Height));
                }
                */
                Image thumbNail = new Bitmap(sizeW, sizeH, _image.PixelFormat);
                using (Graphics g = Graphics.FromImage(thumbNail)) {
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    Rectangle rect = new Rectangle(0, 0, sizeW, sizeH);
                    g.DrawImage(image, rect);
                }
                _image = thumbNail;
                xSize = thumbNail.Width;
                ySize = thumbNail.Height;
                ret = true;
            }
            return ret;
        }

        // Computes a SHA256 hash of the image
        private void _computeImageHash() {
            BHasher hasher = new BHasherSHA256();
            if (_convertedImage != null) {
                _imageHash = hasher.Finish(_convertedImage, 0, _convertedImage.Length);
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
