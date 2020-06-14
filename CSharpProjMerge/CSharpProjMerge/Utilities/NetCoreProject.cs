using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace CSharpProjMerge.Utilities
{
    // https://docs.microsoft.com/en-us/dotnet/core/project-sdk/overview
    public class NetCoreProject : MsBuildProject
    {
        private static readonly HashSet<string> _fxRefs = new HashSet<string> {
            "System",
            "System.Core",
            "System.Drawing",
            "System.Windows.Forms",
            "Microsoft.CSharp",
        };

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

        protected virtual bool ChangeToPackageReference(XmlElement reference)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            var include = reference.GetInclude();
            if (_fxRefs.Contains(include))
                return false;

            return true;
        }

        public override XmlElement EnsureReference(XmlElement reference)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            if (reference.Name == "Reference")
            {
                var clone = ImportElement(reference, "PackageReference");
                if (!ChangeToPackageReference(clone))
                    return null;

                return base.EnsureReference(clone);
            }

            if (_fxRefs.Contains(reference.GetInclude()))
                return null;

            return base.EnsureReference(reference);
        }
    }
}
