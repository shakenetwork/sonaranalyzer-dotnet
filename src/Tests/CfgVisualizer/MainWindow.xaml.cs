using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using SonarLint.Helpers.Cfg.Common;
using SonarLint.Helpers.Cfg.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CfgVisualizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DockPanel Panel = new DockPanel();

        public MainWindow()
        {
            InitializeComponent();

            this.Content = Panel;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var graphViewer = new GraphViewer();
            graphViewer.BindToPanel(Panel);
            var graph = new Graph();

            var cfg = GenerateCfg();
            var blocks = cfg.Blocks.ToList();
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var blockName = GetTextForBlock(block, i);
                graph.AddNode(blockName);
                foreach (var successor in block.SuccessorBlocks)
                {
                    graph.AddEdge(blockName, GetTextForBlock(successor, blocks.IndexOf(successor)));
                }
            }

            graph.Attr.LayerDirection = LayerDirection.LR;
            graphViewer.Graph = graph; // throws exception
        }

        private static List<Block> EmptyBlocks = new List<Block>();

        private static string GetTextForBlock(Block block, int index)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{index} - {block.GetType().Name}");

            if (!block.Instructions.Any())
            {
                if (!EmptyBlocks.Contains(block))
                {
                    EmptyBlocks.Add(block);
                }

                sb.AppendLine("Empty");
            }

            foreach (var node in block.Instructions)
            {
                sb.AppendLine(node.ToString());
            }

            var str = sb.ToString();
            var lines = str.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();
            var line = new string('-', 2);

            lines.Insert(1, line);
            return string.Join(Environment.NewLine, lines);
        }

        private static IControlFlowGraph GenerateCfg()
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = workspace.CurrentSolution.AddProject("foo", "foo.dll", LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                    .AddDocument("test", string.Format(TestInputPattern, TestInput));
                var compilation = document.Project.GetCompilationAsync().Result;
                var tree = compilation.SyntaxTrees.First();

                var bar1Method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .First(m => m.Identifier.ValueText == "Bar1");

                var body = bar1Method.Body;
                return ControlFlowGraph.Create(body, compilation.GetSemanticModel(tree));
            }
        }

        internal const string TestInput = @"
            var x = a.b<C>().D();
            if (x == null)
            {
                return null;
            }

            var y = AL.None;
            foreach (var l in att.L)
            {
                switch (l)
                {
                    case 1:
                        y = y.r(AL.CSharp);
                        break;
                    default:
                        break;
                }
            }

            var c = b;
            while (c != null)
            {
                var l = c.L as BE;
                if (l == null ||
                    !SF.AE(l.O, b.O))
                {
                    e.A(c.L);
                    break;
                }

                c = l;
            }
            return l;
";

        internal const string TestInputPattern = @"
namespace NS
{{
  public class Foo
  {{
    public int Bar1()
    {{
{0}
    }}
  }}
}}";
    }
}
