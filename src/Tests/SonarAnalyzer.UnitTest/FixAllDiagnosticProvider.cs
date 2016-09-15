// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the Roslyn project root for license information.

// Taken from http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis.Features/CodeFixes/FixAllOccurrences/FixAllCodeActionContext.DiagnosticProvider.cs
// https://github.com/dotnet/roslyn/blob/a4e375b95953e471660e9686a46893c97db70b0e/src/Features/Core/Portable/CodeFixes/FixAllOccurrences/FixAllCodeActionContext.DiagnosticProvider.cs
// Issue reported to make class public: https://github.com/dotnet/roslyn/issues/5687

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System;
using System.Threading.Tasks;


namespace SonarAnalyzer.UnitTest
{
    /// /// <summary>
    /// FixAll context with some additional information specifically for <see cref="FixAllCodeAction"/>.
    /// </summary>
    internal class FixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableHashSet<string> _diagnosticIds;

        /// <summary>
        /// Delegate to fetch diagnostics for any given document within the given fix all scope.
        /// This delegate is invoked by <see cref="GetDocumentDiagnosticsAsync(Document, CancellationToken)"/> with the given <see cref="_diagnosticIds"/> as arguments.
        /// </summary>
        private readonly Func<Document, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> _getDocumentDiagnosticsAsync;

        /// <summary>
        /// Delegate to fetch diagnostics for any given project within the given fix all scope.
        /// This delegate is invoked by <see cref="GetProjectDiagnosticsAsync(Project, CancellationToken)"/> and <see cref="GetAllDiagnosticsAsync(Project, CancellationToken)"/>
        /// with the given <see cref="_diagnosticIds"/> as arguments.
        /// The boolean argument to the delegate indicates whether or not to return location-based diagnostics, i.e.
        /// (a) False => Return only diagnostics with <see cref="Location.None"/>.
        /// (b) True => Return all project diagnostics, regardless of whether or not they have a location.
        /// </summary>
        private readonly Func<Project, bool, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> _getProjectDiagnosticsAsync;

        public FixAllDiagnosticProvider(
            ImmutableHashSet<string> diagnosticIds,
            Func<Document, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getDocumentDiagnosticsAsync,
            Func<Project, bool, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getProjectDiagnosticsAsync)
        {
            _diagnosticIds = diagnosticIds;
            _getDocumentDiagnosticsAsync = getDocumentDiagnosticsAsync;
            _getProjectDiagnosticsAsync = getProjectDiagnosticsAsync;
        }

        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            return _getDocumentDiagnosticsAsync(document, _diagnosticIds, cancellationToken);
        }

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return _getProjectDiagnosticsAsync(project, true, _diagnosticIds, cancellationToken);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return _getProjectDiagnosticsAsync(project, false, _diagnosticIds, cancellationToken);
        }
    }
}
