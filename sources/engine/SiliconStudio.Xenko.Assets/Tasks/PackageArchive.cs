﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SiliconStudio.Assets;
using SiliconStudio.Core;
using SiliconStudio.Core.Diagnostics;
using SiliconStudio.Core.IO;
using SiliconStudio.Packages;

namespace SiliconStudio.Xenko.Assets.Tasks
{
    internal static class PackageArchive
    {
        public static void Build(ILogger log, Package package, string outputDirectory = null)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            var meta = new ManifestMetadata();
            PackageStore.ToNugetManifest(package.Meta, meta);

            // Sanity check: Xenko version should be same between NuGet package and Xenko package
            var nugetVersion = new PackageVersion(XenkoVersion.NuGetVersion).Version;
            var packageVersion = package.Meta.Version.Version;

            if (nugetVersion != packageVersion)
            {
                log.Error($"Package has mismatching version: NuGet package version is {nugetVersion} and Xenko Package version is {packageVersion}");
                return;
            }

            // Override version with NuGet version (4th number is different in Xenko package)
            meta.Version = XenkoVersion.NuGetVersion;

            var builder = new NugetPackageBuilder();
            builder.Populate(meta);

            var currentAssemblyLocation = Assembly.GetExecutingAssembly().Location;
            var mainPlatformDirectory = Path.GetFileName(Path.GetDirectoryName(currentAssemblyLocation));

            // TODO this is not working 
            // We are excluding everything that is in a folder that starts with a dot (ie. .shadow, .vs)
            var files = new List<ManifestFile>()
                {
                    NewFile(@"Bin\**\*.exe", "Bin", @"Bin\**\.*\**\*.exe;Bin\**\Tools\**.exe"),
                    NewFile(@"Bin\**\*.so", "Bin", @"Bin\**\.*\**\*.so;Bin\Windows\lib\**\*.so"),
                    NewFile(@"Bin\**\*.ssdeps", "Bin", @"Bin\**\.*\**\*.ssdeps"),
                    NewFile(@"Bin\**\*.a", "Bin", @"Bin\**\.*\**\*.a"),
                    NewFile(@"Bin\**\*.md", "Bin", @"Bin\**\.*\**\*.md"),
                    NewFile(@"Bin\**\*.html", "Bin", @"Bin\**\.*\**\*.html"),
                    NewFile(@"Bin\**\*.config", "Bin", @"Bin\**\.*\**\*.config"),
                    NewFile(@"Bin\**\*.dll", "Bin", @"Bin\**\.*\**\*.dll;Bin\Windows\lib\**\*.dll"),
                    NewFile(@"Bin\**\*.xml", "Bin", @"Bin\**\.*\**\*.xml"),
                    NewFile(@"Bin\**\*.usrdoc", "Bin", @"Bin\**\.*\**\*.usrdoc"),
                    NewFile(@"Bin\**\*.winmd", "Bin", @"Bin\**\.*\**\*.winmd"),
                    NewFile(@"Bin\**\*.sh", "Bin", @"Bin\**\.*\**\*.sh"),
                    NewFile(@"Bin\**\*.json", "Bin", @"Bin\**\.*\**\*.json"),
                    NewFile(@"deps\AssemblyProcessor\*.exe", @"deps/AssemblyProcessor"),
                    NewFile(@"deps\AssemblyProcessor\*.dll", @"deps/AssemblyProcessor"),
                    NewFile(@"deps\CoreFX\**\*.*", @"deps\CoreFX"),
                    NewFile($@"Bin\{mainPlatformDirectory}\ios-tcprelay\*.py",$@"Bin\{mainPlatformDirectory}\ios-tcprelay"),
                    NewFile(@"Targets\*.targets", "Targets"),
                    NewFile($@"Bin\{mainPlatformDirectory}\SiliconStudio.*.pdb", $@"Bin\{mainPlatformDirectory}", @"Bin\**\SiliconStudio.Xenko.Importer*.pdb;Bin\**\SiliconStudio.Xenko.Assimp.Translation.pdb"),
                };

            // Handle Assets
            var rootDir = package.RootDirectory;

            var newPackage = new Package { Meta = package.Meta };

            foreach (var profile in package.Profiles)
            {
                var target = "Assets/" + profile.Name;
                foreach (var assetFolder in profile.AssetFolders)
                {
                    // TODO: handle exclude in asset folders
                    //files.Add(NewFile(source, target, @"**\*.cs;**\*.hlsl;**\*.csproj;**\*.csproj.user;**\obj\**"));
                    files.Add(NewFile(assetFolder.Path.MakeRelative(rootDir) + "/**/*.xksl", target));
                    files.Add(NewFile(assetFolder.Path.MakeRelative(rootDir) + "/**/*.xkfx", target));
                    files.Add(NewFile(assetFolder.Path.MakeRelative(rootDir) + "/**/*.xkfnt", target));
                    files.Add(NewFile(assetFolder.Path.MakeRelative(rootDir) + "/**/*.xksheet", target));
                    files.Add(NewFile(assetFolder.Path.MakeRelative(rootDir) + "/**/*.xkuilib", target));
                    files.Add(NewFile(assetFolder.Path.MakeRelative(rootDir) + "/**/*.xkgfxcomp", target));
                    files.Add(NewFile(assetFolder.Path.MakeRelative(rootDir) + "/**/UIDesigns.dds", target));
                }

                var targetProfile = new PackageProfile(profile.Name);
                targetProfile.AssetFolders.Add(new AssetFolder(target));
                newPackage.Profiles.Add(targetProfile);
            }

            //Handle RootAssets
            foreach (var rootAsset in package.RootAssets)
            {
                newPackage.RootAssets.Add(rootAsset);
            }

            // Handle templates
            var targetFolder = new TemplateFolder("Templates");

            foreach (var templateFolder in package.TemplateFolders)
            {
                var source = templateFolder.Path.MakeRelative(rootDir) + "/**";
                UDirectory target = targetFolder.Path;
                if (templateFolder.Group != null)
                {
                    target = UPath.Combine(target, templateFolder.Group);
                }

                var excludeFiles = templateFolder.Exclude;
                files.Add(NewFile(source, target, excludeFiles));

                // Add template files
                foreach (var templateFile in templateFolder.Files)
                {
                    var newTemplateFile = templateFile.MakeRelative(templateFolder.Path);
                    if (templateFolder.Group != null)
                    {
                        newTemplateFile = UPath.Combine(templateFolder.Group, newTemplateFile);
                    }

                    newTemplateFile = UPath.Combine(targetFolder.Path, newTemplateFile);
                    targetFolder.Files.Add(newTemplateFile);
                }
            }

            // Create temp package for archive
            newPackage.TemplateFolders.Add(targetFolder);
            var newPackageFileName = "temp" + Guid.NewGuid() + ".xkpkg";
            newPackage.FullPath = package.RootDirectory + "/" + newPackageFileName;
            var result = newPackage.Save();
            if (result.HasErrors)
            {
                throw new InvalidOperationException(result.ToText());
                // TODO throw error
            }
            files.Add(NewFile(newPackageFileName, package.Meta.Name + Package.PackageFileExtension));

            // Add files
            builder.PopulateFiles(package.RootDirectory, files);

            outputDirectory = outputDirectory ?? Environment.CurrentDirectory;

            // Save the nupkg
            var outputPath = GetOutputPath(builder,  outputDirectory);
            bool isExistingPackage = File.Exists(outputPath);
            if (isExistingPackage)
            {
                File.Delete(outputPath);
            }
            try
            {
                using (Stream stream = File.Create(outputPath))
                {
                    builder.Save(stream);
                }
            }
            catch
            {
                if (!isExistingPackage && File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
                throw;
            }

            File.Delete(newPackage.FullPath);
        }

        private static ManifestFile NewFile(string source, string target, string exclude = null)
        {
            return new ManifestFile()
                {
                    Source = source.Replace('/', '\\'),
                    Target = target.Replace('/', '\\'),
                    Exclude = exclude
                };
        }

        private static string GetOutputPath(NugetPackageBuilder builder, string outputDirectory)
        {
            string version = builder.Version.ToString();

            // Output file is {id}.{version}
            string outputFile = builder.Id + "." + version;
            outputFile += PackageConstants.PackageExtension;

            return Path.Combine(outputDirectory, outputFile);
        }
    }
}
