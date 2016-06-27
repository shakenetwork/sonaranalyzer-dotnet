/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
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
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using SonarLint.Helpers.FlowAnalysis.Common;
using SonarLint.Helpers.FlowAnalysis.CSharp;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.LogicReliability)]
    [SqaleConstantRemediation("10min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Bug)]
    public class EmptyNullableValueAccess : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3655";
        internal const string Title = "Empty nullable value should not be accessed";
        internal const string Description =
            "Nullable value types can hold either a value or \"null\". The value held in the nullable type can be accessed with " +
            "the \"Value\" property, but \".Value\" throws an \"InvalidOperationException\" when if the nullable type's value is " +
            "\"null\". To avoid the exception, a nullable type should always be tested before \".Value\" is accessed.";
        internal const string MessageFormat = "\"{0}\" is null.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Blocker;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterExplodedGraphBasedAnalysis((e, c) => CheckEmptyNullableAccess(e, c));
        }

        private static void CheckEmptyNullableAccess(ExplodedGraph explodedGraph, SyntaxNodeAnalysisContext context)
        {
            var nullPointerCheck = new NullValueAccessedCheck(explodedGraph);
            explodedGraph.AddExplodedGraphCheck(nullPointerCheck);

            var nullIdentifiers = new HashSet<IdentifierNameSyntax>();
            var nonNullIdentifiers = new HashSet<IdentifierNameSyntax>();

            EventHandler<MemberAccessedEventArgs> nullValueAccessedHandler =
                (sender, args) => CollectMemberAccesses(args, nullIdentifiers, nonNullIdentifiers);

            nullPointerCheck.ValuePropertyAccessed += nullValueAccessedHandler;

            try
            {
                explodedGraph.Walk();
            }
            finally
            {
                nullPointerCheck.ValuePropertyAccessed -= nullValueAccessedHandler;
            }

            foreach (var nullIdentifier in nullIdentifiers)
            {
                if (nonNullIdentifiers.Contains(nullIdentifier))
                {
                    // Only report on cases where we are (almost) sure
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(Rule, nullIdentifier.Parent.GetLocation(), nullIdentifier.Identifier.ValueText));
            }
        }

        private static void CollectMemberAccesses(MemberAccessedEventArgs args, HashSet<IdentifierNameSyntax> nullIdentifiers,
            HashSet<IdentifierNameSyntax> nonNullIdentifiers)
        {
            if (args.IsNull)
            {
                nullIdentifiers.Add(args.Identifier);
            }
            else
            {
                nonNullIdentifiers.Add(args.Identifier);
            }
        }

        internal sealed class NullValueAccessedCheck : ExplodedGraphCheck
        {
            public event EventHandler<MemberAccessedEventArgs> ValuePropertyAccessed;

            public NullValueAccessedCheck(ExplodedGraph explodedGraph)
                : base(explodedGraph)
            {
            }

            private void OnValuePropertyAccessed(IdentifierNameSyntax identifier, bool isNull)
            {
                ValuePropertyAccessed?.Invoke(this, new MemberAccessedEventArgs
                {
                    Identifier = identifier,
                    IsNull = isNull
                });
            }

            public override ProgramState ProcessInstruction(ProgramPoint programPoint, ProgramState programState)
            {
                var instruction = programPoint.Block.Instructions[programPoint.Offset];

                return instruction.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                    ? ProcessMemberAccess(programState, instruction)
                    : programState;
            }

            private ProgramState ProcessMemberAccess(ProgramState programState, SyntaxNode instruction)
            {
                var memberAccess = (MemberAccessExpressionSyntax)instruction;
                var identifier = memberAccess.Expression.RemoveParentheses() as IdentifierNameSyntax;
                if (identifier == null ||
                    memberAccess.Name.Identifier.ValueText != "Value")
                {
                    return programState;
                }

                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                if (!explodedGraph.IsLocalScoped(symbol))
                {
                    return programState;
                }

                var type = ExplodedGraph.GetTypeOfSymbol(symbol);
                if (type == null ||
                    !type.OriginalDefinition.Is(KnownType.System_Nullable_T))
                {
                    return programState;
                }

                var sv = programState.GetSymbolValue(symbol);
                if (sv == SymbolicValue.Null)
                {
                    OnValuePropertyAccessed(identifier, true);
                    return null;
                }
                else
                {
                    OnValuePropertyAccessed(identifier, false);
                    return programState;
                }
            }
        }
    }
}