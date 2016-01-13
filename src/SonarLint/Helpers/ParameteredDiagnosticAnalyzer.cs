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

using Microsoft.CodeAnalysis.Diagnostics;

namespace SonarLint.Helpers
{
    public abstract class ParameteredDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        public sealed override void Initialize(AnalysisContext context)
        {
            var analysisContext = new WrappingAnalysisContext(context);
            Initialize(analysisContext);

            context.RegisterCompilationStartAction(
                cac =>
                {
                    ParameterLoader.SetParameterValues(this, cac.Options);
                    analysisContext.CompilationStartActions.ForEach(startAction => startAction(cac));
                });
        }

        public abstract void Initialize(WrappingAnalysisContext context);
    }
}
