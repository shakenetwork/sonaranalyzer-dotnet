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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SonarQube.CSharp.CodeAnalysis.Descriptor.RuleDescriptors;

namespace SonarQube.CSharp.CodeAnalysis.Descriptor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("The application requires three parameters to run: ");
                Console.WriteLine("[Path to RuleDescriptors.xml]");
                Console.WriteLine("[Path to QualityProfile.xml]");
                Console.WriteLine("[Path to SqaleDescriptors.xml]");
                Console.WriteLine("All files will be created by the application");

                return;
            }

            WriteXmlDescriptorFiles(args[0], args[1], args[2]);
        }

        private static void WriteXmlDescriptorFiles(string rulePath, string profilePath, string sqalePath)
        {
            var fullRuleDescriptors =
                new RuleFinder()
                    .GetRuleDescriptors()
                    .ToList();

            WriteRuleDescriptorFile(rulePath, fullRuleDescriptors);
            WriteQualityProfileFile(profilePath, fullRuleDescriptors);
            WriteSqaleDescriptorFile(sqalePath, fullRuleDescriptors);
        }

        private static void WriteSqaleDescriptorFile(string filePath, IEnumerable<FullRuleDescriptor> fullRuleDescriptors)
        {
            var root = new SqaleRoot();
            foreach (var fullRuleDescriptor in fullRuleDescriptors
                .Where(fullRuleDescriptor => fullRuleDescriptor.SqaleDescriptor != null))
            {
                root.Sqale.Add(fullRuleDescriptor.SqaleDescriptor);
            }

            SerializeObjectToFile(filePath, root);
        }

        private static void WriteQualityProfileFile(string filePath, IEnumerable<FullRuleDescriptor> fullRuleDescriptors)
        {
            var root = new QualityProfileRoot();
            foreach (var fullRuleDescriptor in fullRuleDescriptors.Where(full => full.RuleDescriptor.IsActivatedByDefault))
            {
                root.Rules.Add(new QualityProfileRuleDescriptor
                {
                    Key = fullRuleDescriptor.RuleDescriptor.Key
                });
            }

            SerializeObjectToFile(filePath, root);
        }

        private static void WriteRuleDescriptorFile(string filePath, IEnumerable<FullRuleDescriptor> fullRuleDescriptors)
        {
            var root = new RuleDescriptorRoot();
            foreach (var fullRuleDescriptor in fullRuleDescriptors)
            {
                root.Rules.Add(fullRuleDescriptor.RuleDescriptor);
            }

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
