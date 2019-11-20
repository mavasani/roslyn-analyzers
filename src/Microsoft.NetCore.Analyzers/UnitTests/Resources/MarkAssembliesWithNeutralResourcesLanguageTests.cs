// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Resources.CSharpMarkAssembliesWithNeutralResourcesLanguageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Resources.BasicMarkAssembliesWithNeutralResourcesLanguageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Resources.UnitTests
{
    public class MarkAssembliesWithNeutralResourcesLanguageTests
    {
        private const string CSharpDesignerFile = @"
namespace DesignerFile {
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")]
    internal class Resource1 { }
}";

        private const string BasicDesignerFile = @"
Namespace My.Resources
    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")> _
    Friend Class Resource1
    End Class
End Namespace";

        [Fact]
        public async Task TestCSharpNoResourceFile()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"class C {}");
        }

        [Fact]
        public async Task TestBasicNoResourceFile()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"Class C
End Class");
        }

        [Fact]
        public async Task TestCSharpResourceFile()
        {
            await VerifyCSharpWithDependencies(@"class C {}", VerifyCS.Diagnostic());
        }

        [Fact]
        public async Task TestBasicResourceFile()
        {
            await VerifyBasicWithDependencies(@"Class C
End Class", VerifyVB.Diagnostic());
        }

        [Fact]
        public async Task TestCSharpInvalidAttribute1()
        {
            await VerifyCSharpWithDependencies(@"[assembly: System.Resources.NeutralResourcesLanguage("""")]", VerifyCS.Diagnostic().WithLocation(1, 12));
        }

        [Fact]
        public async Task TestCSharpInvalidAttribute2()
        {
            await VerifyCSharpWithDependencies(@"[assembly: System.Resources.NeutralResourcesLanguage(null)]", VerifyCS.Diagnostic().WithLocation(1, 12));
        }

        [Fact]
        public async Task TestBasicInvalidAttribute1()
        {
            await VerifyBasicWithDependencies(@"<Assembly: System.Resources.NeutralResourcesLanguage("""")>", VerifyVB.Diagnostic().WithLocation(1, 2));
        }

        [Fact]
        public async Task TestBasicInvalidAttribute2()
        {
            await VerifyBasicWithDependencies(@"<Assembly: System.Resources.NeutralResourcesLanguage(Nothing)>", VerifyVB.Diagnostic().WithLocation(1, 2));
        }

        [Fact]
        public async Task TestCSharpvalidAttribute()
        {
            await VerifyCSharpWithDependencies(@"[assembly: System.Resources.NeutralResourcesLanguage(""en"")]");
        }

        [Fact]
        public async Task TestBasicvalidAttribute()
        {
            await VerifyBasicWithDependencies(@"<Assembly: System.Resources.NeutralResourcesLanguage(""en"")>");
        }

        private async Task VerifyCSharpWithDependencies(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test { TestCode = source };

            csharpTest.TestState.Sources.Add(("Test.Designer.cs", CSharpDesignerFile));
            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private async Task VerifyBasicWithDependencies(string source, params DiagnosticResult[] expected)
        {
            var vbTest = new VerifyVB.Test { TestCode = source };

            vbTest.TestState.Sources.Add(("Test.Designer.vb", BasicDesignerFile));
            vbTest.ExpectedDiagnostics.AddRange(expected);

            await vbTest.RunAsync();
        }
    }
}