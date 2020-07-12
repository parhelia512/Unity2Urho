﻿using System;
using System.Globalization;
using System.Xml;
using Assets.Scripts.UnityToCustomEngineExporter.Editor.Urho3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityToCustomEngineExporter.Editor.Urho3D
{
    public abstract class AbstractMaterialExporter
    {
        protected AbstractMaterialExporter(Urho3DEngine engine)
        {
            if (engine == null)
                throw new ArgumentNullException(nameof(engine));
            Engine = engine;
        }

        public abstract int ExporterPriority { get; }
        public Urho3DEngine Engine { get; }

        private static void WriteAlphaTest(XmlWriter writer)
        {
            writer.WriteWhitespace("\t");
            writer.WriteStartElement("shader");
            writer.WriteAttributeString("psdefines", "ALPHAMASK");
            writer.WriteEndElement();
            writer.WriteWhitespace("\n");
        }

        public abstract bool CanExportMaterial(Material material);

        public abstract void ExportMaterial(Material material);

        public virtual string EvaluateMaterialName(Material material)
        {
            if (material == null)
                return null;
            var assetPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;
            if (assetPath.EndsWith(".mat", StringComparison.InvariantCultureIgnoreCase))
                return ExportUtils.ReplaceExtension(ExportUtils.GetRelPathFromAssetPath(Engine.Subfolder, assetPath),
                    ".xml");
            var newExt = "/" + ExportUtils.SafeFileName(material.name) + ".xml";
            return ExportUtils.ReplaceExtension(ExportUtils.GetRelPathFromAssetPath(Engine.Subfolder, assetPath),
                newExt);
        }

        protected virtual void SetupFlags(Material material, ShaderArguments arguments)
        {
            arguments.Shader = material.shader.name;
            arguments.Transparent = material.renderQueue == (int) RenderQueue.Transparent;
            arguments.AlphaTest = material.renderQueue == (int) RenderQueue.AlphaTest;
            arguments.HasEmission = material.IsKeywordEnabled("_EMISSION");
            if (material.HasProperty("_MainTex"))
            {
                arguments.MainTextureOffset = material.mainTextureOffset;
                arguments.MainTextureScale = material.mainTextureScale;
            }
        }

        protected string FormatRGB(Color32 color)
        {
            return string.Format("{0:x2}{1:x2}{2:x2}", color.r, color.g, color.b);
        }

        protected void WriteTechnique(XmlWriter writer, string name)
        {
            writer.WriteWhitespace("\t");
            writer.WriteStartElement("technique");
            writer.WriteAttributeString("name", name);
            writer.WriteEndElement();
            writer.WriteWhitespace("\n");
        }

        protected bool WriteTexture(Texture texture, XmlWriter writer, string name)
        {
            Engine.ScheduleAssetExport(texture);
            var urhoAssetName = Engine.EvaluateTextrueName(texture);
            return WriteTexture(urhoAssetName, writer, name);
        }

        protected bool WriteTexture(string urhoAssetName, XmlWriter writer, string name)
        {
            if (string.IsNullOrWhiteSpace(urhoAssetName))
                return false;
            {
                writer.WriteWhitespace("\t");
                writer.WriteStartElement("texture");
                writer.WriteAttributeString("unit", name);
                writer.WriteAttributeString("name", urhoAssetName);
                writer.WriteEndElement();
                writer.WriteWhitespace(Environment.NewLine);
            }
            return true;
        }

        protected void WriteMaterial(XmlWriter writer, string shaderName, UrhoPBRMaterial urhoMaterial)
        {
            writer.WriteStartElement("material");
            writer.WriteWhitespace(Environment.NewLine);

            WriteTechnique(writer, urhoMaterial.Technique);

            WriteTexture(urhoMaterial.BaseColorTexture, writer, "diffuse");
            WriteTexture(urhoMaterial.NormalTexture, writer, "normal");
            WriteTexture(urhoMaterial.MetallicRoughnessTexture, writer, "specular");
            if (!string.IsNullOrWhiteSpace(urhoMaterial.EmissiveTexture))
                WriteTexture(urhoMaterial.EmissiveTexture, writer, "emissive");
            else
                WriteTexture(urhoMaterial.AOTexture, writer, "emissive");

            writer.WriteParameter("MatEmissiveColor", urhoMaterial.EmissiveColor);
            writer.WriteParameter("MatDiffColor", urhoMaterial.BaseColor);
            writer.WriteParameter("MatEnvMapColor", urhoMaterial.MatEnvMapColor);
            writer.WriteParameter("MatSpecColor", urhoMaterial.MatSpecColor);
            writer.WriteParameter("Roughness", urhoMaterial.Roughness);
            writer.WriteParameter("Metallic", urhoMaterial.Metallic);
            writer.WriteParameter("UOffset", urhoMaterial.UOffset);
            writer.WriteParameter("VOffset", urhoMaterial.VOffset);

            foreach (var extraParameter in urhoMaterial.ExtraParameters)
            {
                var val = extraParameter.Value;
                if (val is float floatValue)
                    writer.WriteParameter(extraParameter.Key, floatValue);
                else if (val is Vector2 vec2Value)
                    writer.WriteParameter(extraParameter.Key, vec2Value);
                else if (val is Vector3 vec3Value)
                    writer.WriteParameter(extraParameter.Key, vec3Value);
                else if (val is Vector4 vec4Value)
                    writer.WriteParameter(extraParameter.Key, vec4Value);
                else if (val is Quaternion quatValue)
                    writer.WriteParameter(extraParameter.Key, quatValue);
                else if (val is Color colorValue)
                    writer.WriteParameter(extraParameter.Key, colorValue);
                else if (val is Color32 color32Value)
                    writer.WriteParameter(extraParameter.Key, color32Value);
                else if (val is string strValue) writer.WriteParameter(extraParameter.Key, strValue);
            }

            if (urhoMaterial.PixelShaderDefines.Count != 0 || urhoMaterial.VertexShaderDefines.Count != 0)
            {
                writer.WriteWhitespace("\t");
                writer.WriteStartElement("shader");
                writer.WriteAttributeString("psdefines", string.Join(" ", urhoMaterial.PixelShaderDefines));
                writer.WriteAttributeString("vsdefines", string.Join(" ", urhoMaterial.VertexShaderDefines));
                writer.WriteEndElement();
                writer.WriteWhitespace(Environment.NewLine);
            }

            writer.WriteEndElement();
        }

        protected void WriteCommonParameters(XmlWriter writer, ShaderArguments arguments)
        {
            writer.WriteParameter("UOffset", new Vector4(arguments.MainTextureScale.x, 0, 0,
                arguments.MainTextureOffset.x));
            writer.WriteParameter("VOffset", new Vector4(0, arguments.MainTextureScale.y, 0,
                arguments.MainTextureOffset.y));
            if (arguments.AlphaTest) WriteAlphaTest(writer);
        }

        protected string BuildAOTextureName(Texture occlusion, float occlusionStrength)
        {
            if (occlusion == null)
                return null;
            if (occlusionStrength <= 0)
                return null;
            var baseName = Engine.EvaluateTextrueName(occlusion);
            if (occlusionStrength >= 0.999f)
                return ExportUtils.ReplaceExtension(baseName, ".AO.png");
            return ExportUtils.ReplaceExtension(baseName,
                string.Format(CultureInfo.InvariantCulture, ".AO.{0:0.000}.png", occlusionStrength));
        }
    }
}