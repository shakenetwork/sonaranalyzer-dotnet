using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.CSharp.CodeAnalysis.Descriptor;
using SonarQube.CSharp.CodeAnalysis.SonarQube.Settings;

namespace SonarQube.CSharp.Test
{
    [TestClass]
    public class RuleTest
    {
        

        private static IList<Type> GetDiagnosticAnalyzerTypes(IList<Assembly> assemblies)
        {
            return assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(DiagnosticAnalyzer)))
                .ToList();
        }

        [TestMethod]
        public void RuleHasResourceHtml()
        {
            var assemblies = RuleFinder.GetRuleAssemblies();
            var analyzers = GetDiagnosticAnalyzerTypes(assemblies);

            var resources = new Dictionary<Assembly, string[]>();
            foreach (var assembly in assemblies)
            {
                resources[assembly] = assembly.GetManifestResourceNames();
            }

            var missingDescriptors = new List<string>();
            foreach (var analyzer in analyzers)
            {
                var ruleDescriptor = analyzer.GetCustomAttributes<RuleAttribute>().First();
                var resource = resources[analyzer.Assembly].SingleOrDefault(r => r.EndsWith(
                    string.Format(CultureInfo.InvariantCulture, RuleFinder.RuleDescriptionPathPattern,
                        ruleDescriptor.Key), StringComparison.OrdinalIgnoreCase));

                if (resource != null)
                {
                    using (var stream = analyzer.Assembly.GetManifestResourceStream(resource))
                    using (var reader = new StreamReader(stream))
                    {
                        var content = reader.ReadToEnd();
                    }
                }
                else
                {
                    missingDescriptors.Add(string.Format("'{0}' ({1})", analyzer.Name, ruleDescriptor.Key));
                }
            }

            if (missingDescriptors.Any())
            {
                throw new Exception(string.Format("Missing HTML description for rule {0}", string.Join(",", missingDescriptors)));
            }
        }

        [TestMethod]
        public void DiagnosticAnalyzerHasRuleAttribute()
        {
            var analyzers = GetDiagnosticAnalyzerTypes(RuleFinder.GetRuleAssemblies());

            foreach (var analyzer in analyzers)
            {
                var ruleDescriptor = analyzer.GetCustomAttributes<RuleAttribute>().SingleOrDefault();
                if (ruleDescriptor == null)
                {
                    throw new Exception(string.Format("RuleAttribute is missing from DiagnosticAnalyzer '{0}'", analyzer.Name));
                }
            }
        }

        [TestMethod]
        public void VisualStudio_NoRuleTemplates()
        {
            var analyzers = GetDiagnosticAnalyzerTypes(new[] { Assembly.LoadFrom(RuleFinder.RuleAssemblyFileName) });

            foreach (var analyzer in analyzers)
            {
                var ruleDescriptor = analyzer.GetCustomAttributes<RuleAttribute>().Single();

                if (ruleDescriptor.Template)
                {
                    throw new Exception(string.Format("Visual Studio rules cannot be templates, remove DiagnosticAnalyzer '{0}'.", analyzer.Name));
                }
            }
        }

        [TestMethod]
        public void VisualStudio_OnlyParameterlessRules()
        {
            var analyzers = GetDiagnosticAnalyzerTypes(new[] { Assembly.LoadFrom(RuleFinder.RuleAssemblyFileName) });

            foreach (var analyzer in analyzers)
            {
                var hasParameter = analyzer.GetProperties().Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any());
                if (hasParameter)
                {
                    throw new Exception(string.Format("Visual Studio rules cannot have parameters, remove DiagnosticAnalyzer '{0}'.", analyzer.Name));
                }
            }
        }

        [TestMethod]
        public void VisualStudio_AllParameterlessRulesNotRuleTemplate()
        {
            var analyzers = GetDiagnosticAnalyzerTypes(new[] { Assembly.LoadFrom(RuleFinder.RuleExtraAssemblyFileName) });

            foreach (var analyzer in analyzers)
            {
                var ruleDescriptor = analyzer.GetCustomAttributes<RuleAttribute>().Single();
                var hasParameter = analyzer.GetProperties().Any(p => p.GetCustomAttributes<RuleParameterAttribute>().Any());
                if (!hasParameter && !ruleDescriptor.Template)
                {
                    throw new Exception(string.Format("DiagnosticAnalyzer '{0}' should be moved to the assembly that implements Visual Studio rules.", analyzer.Name));
                }
            }
        }
    }
}