/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using SonarQube.CSharp.CodeAnalysis.Common;

namespace SonarQube.CSharp.CodeAnalysis.DocGenerator
{
    public class Program
    {
        private const string TemplateInternalName = "main-html";
        private static readonly string ResourcesFolderName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DocResources");
        private static readonly string ToZipFolderName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ToZip");
        private static readonly string OutputZipFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rule-documentation.zip");
        private static readonly string DestinationFolderPattern = ToZipFolderName + "/{0}";
        private static readonly string DestinationFilePattern = DestinationFolderPattern + "/{1}.html";
        private const string TemplateHtmlResourceName = "SonarQube.CSharp.CodeAnalysis.DocGenerator.DocResources.main.template.html";

        static void Main(string[] args)
        {
            var ruleDetails = RuleDetailBuilder.GetParameterlessRuleDetails().ToList();
            var productVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion;
            var ruleDescriptionType = typeof(RuleDescription);

            CreateZipFolder();
            CopyStaticResources(productVersion);

            using (var engine = 
                RazorEngineService.Create(new TemplateServiceConfiguration
            {
                DisableTempFileLocking = true,
                CachingProvider = new DefaultCachingProvider(t => { })
            }))
            {
                engine.AddTemplate(TemplateInternalName, GetTemplateText());
                engine.Compile(TemplateInternalName, ruleDescriptionType);

                foreach (var detail in ruleDetails)
                {
                    var ruleDescription = RuleDescription.Convert(detail);
                    ruleDescription.Version = productVersion;
                    File.WriteAllText(string.Format(DestinationFilePattern, productVersion, detail.Key),
                        engine.Run(TemplateInternalName, ruleDescriptionType, ruleDescription));
                }
            }

            ZipFolder();
        }

        private static void CopyStaticResources(string productVersion)
        {
            var resourcesFolder = new DirectoryInfo(ResourcesFolderName);
            CopyDirectory(resourcesFolder, new DirectoryInfo(string.Format(DestinationFolderPattern, productVersion)));
        }

        private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
        {
            if (!destination.Exists)
            {
                destination.Create();
            }

            foreach (var dir in source.GetDirectories("*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.FullName.Replace(source.FullName, destination.FullName));
            }

            foreach (var file in source.GetFiles("*.*", SearchOption.AllDirectories))
            {
                file.CopyTo(file.FullName.Replace(source.FullName, destination.FullName), true);
            }
        }

        private static void CreateZipFolder()
        {
            if (Directory.Exists(ToZipFolderName))
            {
                Directory.Delete(ToZipFolderName, true);
            }
            Directory.CreateDirectory(ToZipFolderName);
        }

        private static void ZipFolder()
        {
            if (File.Exists(OutputZipFileName))
            {
                File.Delete(OutputZipFileName);
            }
            
            ZipFile.CreateFromDirectory(ToZipFolderName, OutputZipFileName);
        }

        private static string GetTemplateText()
        {
            var assembly = typeof (Program).Assembly;
            var templateResource = assembly.GetManifestResourceNames()
                .Single(s => s == TemplateHtmlResourceName);

            using (var stream = assembly.GetManifestResourceStream(templateResource))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
