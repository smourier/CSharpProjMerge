using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using CSharpProjMerge.Utilities;

namespace CSharpProjMerge
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("CSharpProjMerge - Copyright (C) 2019-" + DateTime.Now.Year + " Simon Mourier. All rights reserved.");
            Console.WriteLine();
            if (CommandLine.HelpRequested || args.Length < 2)
            {
                Help();
                return;
            }

            var inputFilePath = CommandLine.GetNullifiedArgument(0);
            var outputFilePath = CommandLine.GetNullifiedArgument(1);
            if (inputFilePath == null || outputFilePath == null)
            {
                Help();
                return;
            }

            inputFilePath = Path.GetFullPath(inputFilePath);
            outputFilePath = Path.GetFullPath(outputFilePath);

            Console.WriteLine("Input       : " + inputFilePath);
            Console.WriteLine("Output      : " + outputFilePath);
            Console.WriteLine();
            Merge(inputFilePath, outputFilePath);
        }

        static void Help()
        {
            Console.WriteLine(Assembly.GetEntryAssembly().GetName().Name.ToUpperInvariant() + " <input .csproj file path> <output .csproj file path>");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("    This tool is used to merge C# a project and its referenced projects into a single C# project.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine();
            Console.WriteLine("    " + Assembly.GetEntryAssembly().GetName().Name.ToUpperInvariant() + " c:\\myproj1\\myproject1.csproj c:\\myproj2\\myproject2.csproj");
            Console.WriteLine();
            Console.WriteLine("    Merges myproject1 and its references into myproject2.");
            Console.WriteLine();
        }

        static void Merge(string inputFilePath, string outputFilePath)
        {
            var proj = MsBuildProject.FromFilePath(inputFilePath);
            foreach (var include in proj.ItemGroupIncludes)
            {
                fixInclude(proj, include);
            }

            foreach (var rp in proj.AllReferencedProjects)
            {
                foreach (var pr in rp.References)
                {
                    proj.EnsureReference(pr);
                }

                foreach (var include in rp.ItemGroupIncludes)
                {
                    var path = include.GetInclude();
                    if (path != null)
                    {
                        var name = Path.GetFileNameWithoutExtension(path);
                        if (name.EqualsIgnoreCase("assemblyinfo") || name.EndsWith(".assemblyinfo", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (name.EndsWith(".assemblyattributes", StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    var newInclude = proj.EnsureInclude(include);
                    fixInclude(rp, newInclude);
                }
            }

            foreach (var rp in proj.ProjectReferences)
            {
                proj.RemoveProjectReference(rp.GetInclude());
            }

            // various paths
            var icon = proj.ApplicationIcon;
            if (icon != null)
            {
                var text = icon.InnerText.Nullify();
                if (text != null)
                {
                    var path = Path.GetFullPath(Path.Combine(proj.ProjectDirectoryPath, text));
                    icon.InnerText = path;
                }
            }

            IOUtilities.EnsureFileDirectory(outputFilePath);
            proj.Save(outputFilePath);

            void fixInclude(MsBuildProject project, XmlElement includeElement)
            {
                if (includeElement.Name == "PackageReference" ||
                    includeElement.Name == "Reference")
                    return;

                var includePath = includeElement.GetInclude();
                Console.WriteLine(includeElement.Name + ": " + includePath);
                var path = Path.GetFullPath(Path.Combine(project.ProjectDirectoryPath, includePath));
                includeElement.SetAttribute("Include", path);
            }
        }
    }
}
