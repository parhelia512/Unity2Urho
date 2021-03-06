﻿using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityToCustomEngineExporter.Editor.Urho3D
{
    public class CubemapExporter
    {
        private readonly Urho3DEngine _engine;

        public CubemapExporter(Urho3DEngine engine)
        {
            _engine = engine;
        }

        public static bool EnsureReadableTexture(Cubemap texture)
        {
            if (null == texture) return false;

            var assetPath = AssetDatabase.GetAssetPath(texture);
            var tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (tImporter != null)
            {
                //tImporter.textureType = TextureImporterType.Default;
                if (tImporter.isReadable != true)
                {
                    tImporter.isReadable = true;
                    AssetDatabase.ImportAsset(assetPath);
                    AssetDatabase.Refresh();
                }

                return true;
            }

            return false;
        }

        public void Cubemap(Cubemap texture)
        {
            if (!EnsureReadableTexture(texture))
                return;

            var resourceName = EvaluateCubemapName(texture);
            using (var writer =
                _engine.TryCreateXml(texture.GetKey(), resourceName, ExportUtils.GetLastWriteTimeUtc(texture)))
            {
                if (writer != null)
                {
                    var ddsName = resourceName.Replace(".xml", ".dds");
                    var srgb = true;
                    DDS.SaveAsRgbaDds(texture, _engine.GetTargetFilePath(ddsName), srgb);
                    writer.WriteStartElement("cubemap");
                    writer.WriteWhitespace(Environment.NewLine);
                    writer.WriteStartElement("srgb");
                    writer.WriteAttributeString("enable", srgb ? "true" : "false");
                    writer.WriteEndElement();
                    writer.WriteStartElement("image");
                    writer.WriteAttributeString("name", Path.GetFileName(ddsName));
                    writer.WriteEndElement();
                    writer.WriteWhitespace(Environment.NewLine);
                    writer.WriteEndElement();
                }
            }
        }

        public string EvaluateCubemapName(Cubemap cubemap)
        {
            return ExportUtils.ReplaceExtension(ExportUtils.GetRelPathFromAsset(_engine.Options.Subfolder, cubemap), ".xml");
        }
    }
}