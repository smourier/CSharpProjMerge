using System;
using System.Collections.Generic;
using System.Xml;

namespace CSharpProjMerge.Utilities
{
    public class NetFxProject : MsBuildProject
    {
        private static readonly HashSet<string> _includes = new HashSet<string> {
            "Compile",
            "EmbeddedResource",
            "Content",
            "Page",
            "None",
        };

        public NetFxProject(string filePath)
            : base(filePath)
        {
        }

        protected override string GetFileInclude(XmlElement element)
        {
            if (element == null)
                return null;

            if (!_includes.Contains(element.LocalName))
                return null;

            return base.GetFileInclude(element);
        }

        public virtual void RemoveStrongName()
        {
            var sign = Project.SelectSingleNode("p:PropertyGroup/p:SignAssembly", NamespaceManager);
            if (sign != null && sign.InnerText.EqualsIgnoreCase("true"))
            {
                sign.ParentNode.ParentNode.RemoveChild(sign.ParentNode);
            }

            var keyFile = Project.SelectSingleNode("p:PropertyGroup/p:AssemblyOriginatorKeyFile", NamespaceManager);
            if (keyFile != null)
            {
                var name = keyFile.InnerText;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    RemoveInclude(name);
                }
            }
        }
    }
}
