/*
 * SonarQube C# Code Analysis
 * Copyright (C) 2015 SonarSource
 * dev@sonar.codehaus.org
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
        private const string ResourcesFolderName = "DocResources";
        private const string ToZipFolderName = "ToZip";
        private const string OutputZipFileName = "rule-documentation.zip";
        private const string DestinationFolderPattern = ToZipFolderName + "/{0}";
        private const string DestinationFilePattern = DestinationFolderPattern + "/{1}.html";
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
            resourcesFolder.MoveTo(string.Format(DestinationFolderPattern, productVersion));
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
