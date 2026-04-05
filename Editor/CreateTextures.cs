using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace UIBlueprintEditor.Editor
{
    public class CreateTextures
    {
        static bool debugging = UIEditor.debugging;

        static Dictionary<dynamic, dynamic> mappingMinValue = UIEditor.mappingMinValue;
        static Dictionary<dynamic, dynamic> mappingMaxValue = UIEditor.mappingMaxValue;
        static Dictionary<dynamic, BitmapImage> mappingTexture = UIEditor.mappingTexture;

        // this is a separate method so we can check the TextureId for each bitmap entity
        // which should make loading times faster since a texture doesn't need to be created for every output entry
        public static void GetTextures(dynamic rootObject, string textureId)
        {
            // loops through every texture mapping asset in the ui blueprint
            foreach (var textureItem in rootObject.Object.Internal.TextureMappings)
            {
                if (debugging)
                {
                    App.Logger.Log("texture");
                }

                // get the texture mapping asset from the PointerRef
                var textureMapGuid = ((PointerRef)textureItem).External.FileGuid;
                var textureMapEbx = App.AssetManager.GetEbxEntry(textureMapGuid);

                EbxAsset textureMapAsset = App.AssetManager.GetEbx(textureMapEbx);
                dynamic rootObjectTextureMap = textureMapAsset.RootObject;

                // loops through each output in the texture mapping asset
                foreach (dynamic outputEntry in rootObjectTextureMap.Output)
                {
                    string entryId = outputEntry.Id.ToString();

                    // skip if this isn't the texture we need, or if we already loaded it
                    if (entryId != textureId || mappingTexture.ContainsKey(entryId))
                        continue;

                    var uvRect = outputEntry.UvRect;

                    // store min (x, y) and max (z, w) as separate anonymous-style objects
                    mappingMinValue.Add(entryId, new { x = (double)uvRect.x, y = (double)uvRect.y });
                    mappingMaxValue.Add(entryId, new { x = (double)uvRect.z, y = (double)uvRect.w });

                    // TextureRef is a raw ulong res hash as a hex string
                    ulong textureResHash = Convert.ToUInt64(outputEntry.TextureRef.ToString(), 16);

                    ResAssetEntry resEntry = App.AssetManager.GetResEntry(textureResHash);

                    if (resEntry == null)
                    {
                        App.Logger.LogError($"Could not find res entry for TextureRef '{outputEntry.TextureRef}' (id: {entryId})");
                        continue;
                    }

                    Texture texture = App.AssetManager.GetResAs<Texture>(resEntry);

                    TextureExporterToMemory.Export(texture);
                    byte[] textureBytes = TextureExporterToMemory.textureBytes;

                    BitmapImage bitmap = CreateBitmap(textureBytes);
                    mappingTexture.Add(entryId, bitmap);

                    if (debugging)
                        App.Logger.Log("Loaded texture id: " + entryId);
                }
            }
        }

        // returns a bitmap image that is written to a MemoryStream
        public static BitmapImage CreateBitmap(byte[] textureBytes)
        {
            var bitmap = new BitmapImage();

            using (var stream = new MemoryStream(textureBytes))
            {
                bitmap.BeginInit();

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;

                bitmap.EndInit();
            }

            return bitmap;
        }
    }
}