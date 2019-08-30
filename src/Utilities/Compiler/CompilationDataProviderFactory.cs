// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

#pragma warning disable RS1012 // Start action has no registered actions.

namespace Analyzer.Utilities
{
    public sealed class CompilationDataProviderFactory : IEquatable<CompilationDataProviderFactory>
    {
        private readonly CompilationStartAnalysisContext _context;

        private CompilationDataProviderFactory(CompilationStartAnalysisContext context)
        {
            _context = context;
        }

        public Compilation Compilation => _context.Compilation;

        internal CompilationDataProviderFactory Clone() => new CompilationDataProviderFactory(_context);

        internal CompilationDataProvider CreateProvider() => CreateProvider(_context);

        public static CompilationDataProvider CreateProvider(CompilationStartAnalysisContext context)
        {
            var factory = new CompilationDataProviderFactory(context);
            return new CompilationDataProvider(factory);
        }

        public bool Equals(CompilationDataProviderFactory other)
            => _context.Compilation == other._context.Compilation;

        public override bool Equals(object obj)
            => Equals(obj as CompilationDataProviderFactory);

        public override int GetHashCode()
            => _context.Compilation.GetHashCode();
    }
}