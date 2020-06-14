using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace CSharpProjMerge.Utilities
{
    // trying to use MsBuild programmatically (roslyn, etc.) is like summoning the demons from hell
    // so we do this ourselves (we don't need super powerful stuff anyway)
    public class MsBuildProject : IEquatable<MsBuildProject>
    {
        public const string MsBuildNamespaceUri = "http://schemas.microsoft.com/developer/msbuild/2003";
        public static readonly XmlNamespaceManager NamespaceManager = new XmlNamespaceManager(new NameTable());

        static MsBuildProject()
        {
            NamespaceManager.AddNamespace("p", MsBuildNamespaceUri);
        }

        public MsBuildProject(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            ProjectFilePath = Path.GetFullPath(filePath);
            ProjectDirectoryPath = Path.GetDirectoryName(ProjectFilePath);
            Document = new XmlDocument();
            Document.Load(ProjectFilePath);
            Project = Document.SelectSingleNode(ProjectPrefix + "Project", NamespaceManager) as XmlElement;
        }

        public string ProjectFilePath { get; }
        public string ProjectDirectoryPath { get; }
        public XmlDocument Document { get; }
        public XmlElement Project { get; protected set; }
        protected virtual string ProjectPrefix => "p:";
        protected virtual string ProjectNamespaceUri => MsBuildNamespaceUri;

        public virtual IEnumerable<XmlElement> ItemGroupIncludes
        {
            get
            {
                foreach (var itemGroup in ItemGroups)
                {
                    foreach (var include in itemGroup.SelectNodes("*[@Include]").OfType<XmlElement>())
                    {
                        yield return include;
                    }
                }
            }
        }

        public virtual IEnumerable<XmlElement> ItemGroups
        {
            get
            {
                foreach (var itemGroup in Project.SelectNodes(ProjectPrefix + "ItemGroup", NamespaceManager).OfType<XmlElement>())
                {
                    yield return itemGroup;
                }
            }
        }

        public virtual IEnumerable<XmlElement> References
        {
            get
            {
                foreach (var itemGroup in ItemGroups)
                {
                    foreach (var packageReference in itemGroup.SelectNodes(ProjectPrefix + "Reference", NamespaceManager).OfType<XmlElement>())
                    {
                        yield return packageReference;
                    }

                    foreach (var packageReference in itemGroup.SelectNodes(ProjectPrefix + "PackageReference", NamespaceManager).OfType<XmlElement>())
                    {
                        yield return packageReference;
                    }
                }
            }
        }

        public virtual IEnumerable<string> IncludedFilePaths
        {
            get
            {
                foreach (var include in ItemGroupIncludes)
                {
                    var file = GetFileInclude(include);
                    if (file != null)
                        yield return file;
                }
            }
        }

        public virtual IEnumerable<XmlElement> ProjectReferences
        {
            get
            {
                foreach (var itemGroup in ItemGroups)
                {
                    foreach (var projectReference in itemGroup.SelectNodes(ProjectPrefix + "ProjectReference", NamespaceManager).OfType<XmlElement>())
                    {
                        yield return projectReference;
                    }
                }
            }
        }

        public virtual IEnumerable<MsBuildProject> ReferencedProjects
        {
            get
            {
                foreach (var pr in ProjectReferences)
                {
                    var path = pr.GetInclude();
                    if (path == null)
                        continue;

                    path = Path.GetFullPath(Path.Combine(ProjectDirectoryPath, path));
                    var proj = FromFilePath(path);
                    if (proj != null)
                        yield return proj;
                }
            }
        }

        public virtual IReadOnlyList<MsBuildProject> AllReferencedProjects
        {
            get
            {
                var list = new HashSet<MsBuildProject>();
                foreach (var proj in ReferencedProjects)
                {
                    list.Add(proj);
                    foreach (var child in proj.AllReferencedProjects)
                    {
                        list.Add(child);
                    }
                }
                return list.ToArray();
            }
        }

        public virtual XmlElement ApplicationIcon => Project.SelectSingleNode(ProjectPrefix + "PropertyGroup/" + ProjectPrefix + "ApplicationIcon", NamespaceManager) as XmlElement;

        public override string ToString() => ProjectFilePath;
        public override bool Equals(object obj) => Equals(obj as MsBuildProject);
        public override int GetHashCode() => ProjectFilePath.GetHashCode();
        public bool Equals(MsBuildProject other) => other != null && ProjectFilePath.EqualsIgnoreCase(other.ProjectFilePath);

        public virtual void RemoveProjectReference(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var remove = ProjectReferences.FirstOrDefault(e => e.GetInclude().EqualsIgnoreCase(filePath));
            if (remove != null)
            {
                remove.ParentNode.RemoveChild(remove);
            }
        }

        public virtual XmlElement EnsureReference(XmlElement reference)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            if (reference.ParentNode != null)
            {
                foreach (var existing in References)
                {
                    if (existing.GetInclude().EqualsIgnoreCase(reference.GetInclude()))
                        return existing;
                }
            }

            var targetGroup = Project.SelectSingleNode(ProjectPrefix + "ItemGroup/" + ProjectPrefix + reference.Name + "/..", NamespaceManager);
            if (targetGroup == null)
            {
                targetGroup = Document.CreateElement("ItemGroup", ProjectNamespaceUri);
                Project.AppendChild(targetGroup);
            }

            var newReference = ImportElement(reference);
            targetGroup.AppendChild(newReference);
            return newReference;
        }

        // can't use ImportNode because we don't want imported namespace, we want to use ours
        protected virtual XmlElement ImportElement(XmlElement input, string localName = null)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            var clone = Document.CreateElement(localName ?? input.LocalName, ProjectNamespaceUri);
            foreach (XmlAttribute att in input.Attributes)
            {
                // don't use att's namespace
                clone.SetAttribute(att.LocalName, att.Value);
            }

            foreach (var childNode in input.ChildNodes)
            {
                if (childNode is XmlElement childElement)
                {
                    var newChild = ImportElement(childElement);
                    clone.AppendChild(newChild);
                    continue;
                }

                if (childNode is XmlText childText)
                {
                    var newChild = Document.CreateTextNode(childText.Value);
                    clone.AppendChild(newChild);
                    continue;
                }
            }
            return clone;
        }

        public virtual bool IsReferenceInclude(XmlElement include)
        {
            if (include == null)
                return false;

            return include.Name == "Reference" || include.Name == "PackageReference";
        }

        public virtual bool IsAssemblyInfo(XmlElement include)
        {
            if (include == null)
                return false;

            var path = include.GetInclude();
            if (path != null)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.EqualsIgnoreCase("assemblyinfo") || name.EndsWith(".assemblyinfo", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (name.EndsWith(".assemblyattributes", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public virtual XmlElement EnsureInclude(XmlElement include)
        {
            if (include == null)
                throw new ArgumentNullException(nameof(include));

            if (include.ParentNode != null)
            {
                foreach (var existing in ItemGroupIncludes)
                {
                    if (existing.GetInclude().EqualsIgnoreCase(include.GetInclude()))
                        return existing;
                }
            }

            var targetGroup = Project.SelectSingleNode(ProjectPrefix + "ItemGroup/" + ProjectPrefix + include.Name + "/..", NamespaceManager);
            if (targetGroup == null)
            {
                targetGroup = Document.CreateElement("ItemGroup", ProjectNamespaceUri);
                Project.AppendChild(targetGroup);
            }

            var newInclude = ImportElement(include);
            targetGroup.AppendChild(newInclude);
            return newInclude;
        }

        public virtual void RemoveInclude(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var remove = GetIncludeElement(filePath);
            if (remove != null)
            {
                remove.ParentNode.RemoveChild(remove);
            }
        }

        public virtual XmlElement GetIncludeElement(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            return ItemGroupIncludes.FirstOrDefault(e => GetIncludeFilePath(e).EqualsIgnoreCase(filePath));
        }

        public virtual void Save(string filePath = null)
        {
            filePath = filePath ?? ProjectFilePath;
            Document.Save(filePath);
        }

        protected virtual string GetFileInclude(XmlElement element)
        {
            if (element == null)
                return null;

            var include = GetIncludeFilePath(element);
            if (include == null)
                return null;

            return include;
        }

        public virtual XmlElement GetPropertyGroup(string condition)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            return Project.SelectSingleNode(ProjectPrefix + "PropertyGroup[@Condition=\"" + condition + "\"]", NamespaceManager) as XmlElement;
        }

        public virtual void SetProperty(string groupCondition, string name, string text)
        {
            if (groupCondition == null)
                throw new ArgumentNullException(nameof(groupCondition));

            var pg = GetPropertyGroup(groupCondition);
            SetProperty(pg, name, text);
        }

        public static MsBuildProject FromFilePath(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".csproj" || ext == ".vbproj")
            {
                var doc = new XmlDocument();
                doc.Load(filePath);
                var netCore = doc.DocumentElement?.GetAttribute("Sdk")?.StartsWith("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase);
                if (netCore == true)
                    return new NetCoreProject(filePath);

                return new NetFxProject(filePath);
            }

            return new MsBuildProject(filePath);
        }

        public static void SetProperty(XmlElement propertyGroup, string name, string text)
        {
            if (propertyGroup == null)
                throw new ArgumentNullException(nameof(propertyGroup));

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            var prop = propertyGroup.SelectSingleNode("p:" + name, NamespaceManager);
            if (prop == null)
            {
                prop = propertyGroup.OwnerDocument.CreateElement(name, MsBuildNamespaceUri);
                propertyGroup.AppendChild(prop);
            }

            prop.InnerText = text;
        }

        public static string GetIncludeFilePath(XmlElement element)
        {
            if (element == null)
                return null;

            return element.GetInclude();
        }
    }
}
