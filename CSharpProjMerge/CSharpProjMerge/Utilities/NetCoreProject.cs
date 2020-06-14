using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace CSharpProjMerge.Utilities
{
    // https://docs.microsoft.com/en-us/dotnet/core/project-sdk/overview
    public class NetCoreProject : MsBuildProject
    {
        public NetCoreProject(string filePath)
            : base(filePath)
        {
        }

        protected override string ProjectPrefix => string.Empty;
        protected override string ProjectNamespaceUri => string.Empty;

        public override IEnumerable<XmlElement> ItemGroupIncludes
        {
            get
            {
                foreach (var file in Directory.EnumerateFiles(ProjectDirectoryPath, "*.*", SearchOption.AllDirectories))
                {
                    XmlElement include = null;
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".cs" || ext == ".vb")
                    {
                        include = Document.CreateElement("Compile", ProjectNamespaceUri);
                    }
                    else if (ext == ".resx")
                    {
                        include = Document.CreateElement("EmbeddedResource", ProjectNamespaceUri);
                    }

                    if (include != null)
                    {
                        include.SetAttribute("Include", file);
                        yield return include;
                    }
                }
            }
        }
    }
}
