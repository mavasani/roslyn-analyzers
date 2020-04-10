﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Reflection;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.VersionCheckAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AnalyzerVersionCheckAnalyzer : DiagnosticAnalyzer
    {
        private const string RuleId = "CA9999";

        // TODO: Below version must be autogenerated using the project properties.
        private const string RequiredMicrosoftCodeAnalysisVersion = "3.0.0";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeAnalysisVersionCheckAnalyzerResources.VersionCheckTitle), MicrosoftCodeAnalysisVersionCheckAnalyzerResources.ResourceManager, typeof(MicrosoftCodeAnalysisVersionCheckAnalyzerResources));
        private static readonly LocalizableString s_localizableMessageFormat = new LocalizableResourceString(nameof(MicrosoftCodeAnalysisVersionCheckAnalyzerResources.VersionCheckMessage), MicrosoftCodeAnalysisVersionCheckAnalyzerResources.ResourceManager, typeof(MicrosoftCodeAnalysisVersionCheckAnalyzerResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeAnalysisVersionCheckAnalyzerResources.VersionCheckDescription), MicrosoftCodeAnalysisVersionCheckAnalyzerResources.ResourceManager, typeof(MicrosoftCodeAnalysisVersionCheckAnalyzerResources));

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
                                                        RuleId,
                                                        s_localizableTitle,
                                                        s_localizableMessageFormat,
                                                        DiagnosticCategory.Reliability,
                                                        DiagnosticSeverity.Warning,
                                                        isEnabledByDefault: true,
                                                        description: s_localizableDescription);

        private static readonly Version s_MicrosoftCodeAnalysisMinVersion = new Version(RequiredMicrosoftCodeAnalysisVersion);
        private static readonly Version s_MicrosoftCodeAnalysisDogfoodVersion = new Version("42.42");
        private static readonly Version s_MicrosoftCodeAnalysisVersion = typeof(AnalysisContext).GetTypeInfo().Assembly.GetName().Version;

        // Analyzers will only execute fine if we are either using dogfood bits of Microsoft.CodeAnalysis or its version is >= s_MicrosoftCodeAnalysisMinVersion
        private static bool s_ShouldExecuteAnalyzers =>
            s_MicrosoftCodeAnalysisVersion >= s_MicrosoftCodeAnalysisDogfoodVersion ||
            s_MicrosoftCodeAnalysisVersion >= s_MicrosoftCodeAnalysisMinVersion;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Suppress RS1013 as CompilationAction is only executed with FSA on and we want the analyzer to run even with FSA off.
#pragma warning disable RS1013 // Start action has no registered non-end actions.
            context.RegisterCompilationStartAction(compilationStartContext =>
#pragma warning restore RS1013 // Start action has no registered non-end actions.
            {
                compilationStartContext.RegisterCompilationEndAction(compilationContext =>
                {
                    if (!s_ShouldExecuteAnalyzers)
                    {
                        // Version mismatch between the analyzer package '{0}' and Microsoft.CodeAnalysis '{1}'. Certain analyzers in this package will not run until the version mismatch is fixed.
                        var arg1 = RequiredMicrosoftCodeAnalysisVersion;
                        var arg2 = s_MicrosoftCodeAnalysisVersion;
                        compilationContext.ReportNoLocationDiagnostic(Rule, arg1, arg2);
                    }
                });
            });
        }
    }
}
