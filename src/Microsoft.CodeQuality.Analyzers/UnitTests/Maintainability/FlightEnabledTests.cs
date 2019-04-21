// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public partial class FlightEnabledTestsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer() => new FlightEnabledAnalyzer();
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new FlightEnabledAnalyzer();

        protected new DiagnosticResult GetCSharpResultAt(int line, int column, string methodName, string enabledFlights) =>
            GetCSharpResultAt(line, column, FlightEnabledAnalyzer.Rule, methodName, enabledFlights);

        private readonly string FlightApiSource = @"
public class FlightApi
{
    public static bool FlightEnabled { get; set; }
    public static bool IsFlightEnabled(string flightName)
    {
        return FlightEnabled;
    }
}
";

        [Fact]
        public void Test1()
        {
            VerifyCSharp(FlightApiSource + @"
class Test
{
    void M1()
    {
        if (FlightApi.IsFlightEnabled(""flight1""))
        {
            M2();
        }
        else
        {
            M3();
        }
    }

    void M2()
    {
        if (FlightApi.IsFlightEnabled(""flight2""))
        {
            M3();
        }

        M4();
    }

    void M3()
    {
    }

    void M4()
    {
    }
}
",
            // Test0.cs(13,10): warning CA1510: Method 'Test.M1()', enable flights at invocations and property accesses: 'flight1'
            GetCSharpResultAt(13, 10, "Test.M1()", "flight1"),
            // Test0.cs(25,10): warning CA1510: Method 'Test.M2()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(25, 10, "Test.M2()", ""),
            // Test0.cs(35,10): warning CA1510: Method 'Test.M3()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(35, 10, "Test.M3()", ""),
            // Test0.cs(39,10): warning CA1510: Method 'Test.M4()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(39, 10, "Test.M4()", ""));
        }

        [Fact]
        public void Test2()
        {
            VerifyCSharp(FlightApiSource + @"
class Test
{
    void M1()
    {
        if (FlightApi.IsFlightEnabled(""flight1""))
        {
            M2();
        }
        else
        {
            M3();
        }

        M2();
    }

    void M2()
    {
        if (FlightApi.IsFlightEnabled(""flight2""))
        {
            M3();
        }

        M4();
    }

    void M3()
    {
    }

    void M4()
    {
    }
}
",
            // Test0.cs(13,10): warning CA1510: Method 'Test.M1()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(13, 10, "Test.M1()", ""),
            // Test0.cs(27,10): warning CA1510: Method 'Test.M2()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(27, 10, "Test.M2()", ""),
            // Test0.cs(37,10): warning CA1510: Method 'Test.M3()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(37, 10, "Test.M3()", ""),
            // Test0.cs(41,10): warning CA1510: Method 'Test.M4()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(41, 10, "Test.M4()", ""));
        }

        [Fact]
        public void Test3()
        {
            VerifyCSharp(FlightApiSource + @"
class Test
{
    void M1()
    {
        if (FlightApi.IsFlightEnabled(""flight1""))
        {
            M2();
        }
        else
        {
            M3();
        }
    }

    void M2()
    {
        if (FlightApi.IsFlightEnabled(""flight2""))
        {
            M3();
        }

        M4();
    }

    void M3()
    {
        if (FlightApi.IsFlightEnabled(""flight3""))
        {
            M5();
        }
    }

    void M4()
    {
        if (FlightApi.IsFlightEnabled(""flight4""))
        {
            M5();
        }
    }

    void M5()
    {
    }
}
",
            // Test0.cs(13,10): warning CA1510: Method 'Test.M1()', enable flights at invocations and property accesses: 'flight1'
            GetCSharpResultAt(13, 10, "Test.M1()", "flight1"),
            // Test0.cs(25,10): warning CA1510: Method 'Test.M2()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(25, 10, "Test.M2()", ""),
            // Test0.cs(35,10): warning CA1510: Method 'Test.M3()', enable flights at invocations and property accesses: 'flight3'
            GetCSharpResultAt(35, 10, "Test.M3()", "flight3"),
            // Test0.cs(43,10): warning CA1510: Method 'Test.M4()', enable flights at invocations and property accesses: 'flight4'
            GetCSharpResultAt(43, 10, "Test.M4()", "flight4"),
            // Test0.cs(51,10): warning CA1510: Method 'Test.M5()', enable flights at invocations and property accesses: ''
            GetCSharpResultAt(51, 10, "Test.M5()", ""));
        }
    }
}
