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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SonarQube.CSharp.CodeAnalysis.Common;

namespace SonarQube.CSharp.CodeAnalysis.Descriptor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Write("The application requires three parameters to run: ");
                Write("[Path to RuleDescriptors.xml]");
                Write("[Path to QualityProfile.xml]");
                Write("[Path to SqaleDescriptors.xml]");
                Write("All files will be created by the application");

                return;
            }

            WriteXmlDescriptorFiles(args[0], args[1], args[2]);
        }

        private static void Write(string text)
        {
            Console.WriteLine(text);
        }

        private static void WriteXmlDescriptorFiles(string rulePath, string profilePath, string sqalePath)
        {
            var genericRuleDetails = RuleDetailBuilder.GetAllRuleDetails().ToList();
            var ruleDetails = genericRuleDetails.Select(RuleDetail.Convert).ToList();
            var sqaleDetails = genericRuleDetails.Select(SqaleDescriptor.Convert).ToList();
            
            WriteRuleDescriptorFile(rulePath, ruleDetails);
            WriteQualityProfileFile(profilePath, ruleDetails);
            WriteSqaleDescriptorFile(sqalePath, sqaleDetails);
        }

        private static void WriteSqaleDescriptorFile(string filePath, IEnumerable<SqaleDescriptor> sqaleDescriptions)
        {
            var root = new SqaleRoot();
            root.Sqale.AddRange(sqaleDescriptions
                .Where(descriptor => descriptor != null));
            SerializeObjectToFile(filePath, root);
        }

        private static void WriteQualityProfileFile(string filePath, IEnumerable<RuleDetail> ruleDetails)
        {
            var root = new QualityProfileRoot();
            root.Rules.AddRange(ruleDetails
                .Where(descriptor => descriptor.IsActivatedByDefault)
                .Select(descriptor => new QualityProfileRuleDescriptor
                {
                    Key = descriptor.Key
                }));

            SerializeObjectToFile(filePath, root);
        }

        private static void WriteRuleDescriptorFile(string filePath, IEnumerable<RuleDetail> ruleDetails)
        {
            var root = new RuleDescriptorRoot();
            root.Rules.AddRange(ruleDetails);
            SerializeObjectToFile(filePath, root);
        }

        private static void SerializeObjectToFile(string filePath, object objectToSerialize)
        {
            var settings = new XmlWriterSettings {
                Indent = true, 
                Encoding = Encoding.UTF8,
                IndentChars = "  "
            };
            
            using (var stream = new MemoryStream())
            using (var writer = XmlWriter.Create(stream, settings))
            {
                var serializer = new XmlSerializer(objectToSerialize.GetType());
                serializer.Serialize(writer, objectToSerialize, new XmlSerializerNamespaces(new[] {XmlQualifiedName.Empty}));
                var ruleXml = Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(filePath, ruleXml);
            }
        }
    }
}
