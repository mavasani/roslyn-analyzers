// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary> 
    /// CA1720-redefined: Identifiers should not contain type names 
    /// Cause: 
    /// The name of a parameter in an externally visible member contains a data type name.
    /// -or-
    /// The name of an externally visible member contains a language-specific data type name.
    ///  
    /// Description: 
    /// Names of parameters and members are better used to communicate their meaning than  
    /// to describe their type, which is expected to be provided by development tools. For names of members,  
    /// if a data type name must be used, use a language-independent name instead of a language-specific one.  
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class IdentifiersShouldNotContainTypeNames : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1720";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftApiDesignGuidelinesAnalyzersResources.IdentifiersShouldNotContainTypeNamesTitle), MicrosoftApiDesignGuidelinesAnalyzersResources.ResourceManager, typeof(MicrosoftApiDesignGuidelinesAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftApiDesignGuidelinesAnalyzersResources.IdentifiersShouldNotContainTypeNamesMessage), MicrosoftApiDesignGuidelinesAnalyzersResources.ResourceManager, typeof(MicrosoftApiDesignGuidelinesAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftApiDesignGuidelinesAnalyzersResources.IdentifiersShouldNotContainTypeNamesDescription), MicrosoftApiDesignGuidelinesAnalyzersResources.ResourceManager, typeof(MicrosoftApiDesignGuidelinesAnalyzersResources));

        private static readonly ImmutableHashSet<string> s_IntgeralTypeNames =
            ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, new[]
            {
                "int8",
                "uint8",
                "short",
                "ushort",
                "int",
                "uint",
                "integer",
                "uinteger",
                "long",
                "ulong",
                "unsigned",
                "signed"
            });

        private static readonly ImmutableHashSet<string> s_languageSpecificTypeNames =
            ImmutableDictionary.CreateRange<SpecialType, ImmutableHashSet<string>>(StringComparer.OrdinalIgnoreCase, new[]
            {
                new KeyValuePair<SpecialType, ImmutableHashSet<string>>(SpecialType.System_Char, ImmutableHashSet.Create("char", "wchar")),
                new KeyValuePair<SpecialType, ImmutableHashSet<string>>(SpecialType.System_Char, ImmutableHashSet.Create("char", "wchar")),

                "float",
                "float32",
                "float64"
            });

        private static readonly ImmutableHashSet<string> s_languageIndependentTypeNames =
            ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, new[]
            {
                "object",
                "obj",
                "boolean",
                "char",
                "string",
                "sbyte",
                "byte",
                "ubyte",
                "int16",
                "uint16",
                "int32",
                "uint32",
                "int64",
                "uint64",
                "intptr",
                "ptr",
                "pointer",
                "uintptr",
                "uptr",
                "upointer",
                "single",
                "double",
                "decimal",
                "guid"
            });

        // FxCop compat
        private static readonly ImmutableHashSet<string> s_ignorableTypeNames =
            ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, new[]
            {
                "ConnectionString",
                "QueryString",
                "CharSet"
            });

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1720-identifiers-should-not-contain-type-names",
                                                                             customTags: WellKnownDiagnosticTags.Telemetry);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                // Analyze named types and fields.
                compilationStartAnalysisContext.RegisterSymbolAction(
                    symbolContext => AnalyzeSymbol(symbolContext.Symbol, symbolContext),
                    SymbolKind.NamedType,
                    SymbolKind.Field);

                // Analyze properties and methods, and their parameters.
                compilationStartAnalysisContext.RegisterSymbolAction(
                    symbolContext =>
                    {
                        // Although indexers aren't IMethodSymbols, their accessors are, and we can get their parameters from them
                        if (symbolContext.Symbol is IMethodSymbol method)
                        {
                            // If this method contains parameters with names violating this rule, we only want to flag them
                            // if this method is not overriding another or implementing an interface. Otherwise, changing the
                            // parameter names will violate CA1725 - Parameter names should match base declaration.
                            if (method.OverriddenMethod == null && !method.IsImplementationOfAnyInterfaceMember())
                            {
                                foreach (var param in method.Parameters)
                                {
                                    AnalyzeSymbol(param, symbolContext);
                                }
                            }
                        }

                        AnalyzeSymbol(symbolContext.Symbol, symbolContext);
                    },
                    SymbolKind.Property,
                    SymbolKind.Method);
            });
        }

        private static void AnalyzeSymbol(ISymbol symbol, SymbolAnalysisContext context)
        {
            // FxCop compat: only analyze externally visible symbols which have a special type.
            if (!symbol.IsExternallyVisible())
            {
                return;
            }

            var type = symbol.GetMemerOrLocalOrParameterType();
            if (type == null || type.SpecialType == SpecialType.n)

            var identifier = symbol.Name;
            var words = WordParser.Parse(symbol, WordParserOptions.SplitCompoundWords).ToImmutableArray();

            // We need to make sure that we look at three token compound words.
            // such as 'UIntPtr', before we look at two token compound words, such 
            // as 'IntPtr' and one token words, such as 'Ptr'. This is so that we 
            // only fire once on each word, whether it is combined with another word
            // or not.
            int wordsCount = words.Length;
            for (int i = 0; i < wordsCount; i++)
            {
                string nextNextWord = i + 2 < wordsCount ? words[i + 2] : null;
                string nextWord = i + 1 < wordsCount ? words[i + 1] : null;
                string word = words[i];

                if (nextNextWord != null)
                {
                    if (CheckForTypeName(target, type, word + nextWord + nextNextWord))
                    {
                        // Skip over the next two words so we
                        // do not fire twice on the same word
                        i += 2;
                        continue;
                    }
                }

                if (nextWord != null)
                {
                    if (CheckForTypeName(target, type, word + nextWord))
                    {
                        // Skip over the next word so we do 
                        // not fire twice on the same word
                        i += 1;
                        continue;
                    }
                }

                CheckForTypeName(target, type, word);
            }

            if (s_languageIndependentTypeNames.Contains(identifier))
            {
                Diagnostic diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], identifier);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}