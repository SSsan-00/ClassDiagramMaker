# Third-Party Notices

ClassDiagramMaker uses the following third-party components.

## Runtime Dependencies

| Component | Version | License | Purpose |
|---|---:|---|---|
| DocumentFormat.OpenXml | 3.5.1 | MIT | Excel `.xlsx` generation |
| DocumentFormat.OpenXml.Framework | 3.5.1 | MIT | Transitive dependency of DocumentFormat.OpenXml |
| System.IO.Packaging | 8.0.1 | MIT | Transitive dependency of DocumentFormat.OpenXml |
| Microsoft.CodeAnalysis.CSharp | 4.12.0 | MIT | C# syntax and semantic analysis |
| Microsoft.CodeAnalysis.Common | 4.12.0 | MIT | Transitive dependency of Microsoft.CodeAnalysis.CSharp |
| Microsoft.CodeAnalysis.Analyzers | 3.3.4 | MIT | Transitive dependency of Microsoft.CodeAnalysis.CSharp |
| System.Collections.Immutable | 8.0.0 | MIT | Transitive dependency of Microsoft.CodeAnalysis.CSharp |
| System.Reflection.Metadata | 8.0.0 | MIT | Transitive dependency of Microsoft.CodeAnalysis.CSharp |

## Test Dependencies

| Component | Version | License | Purpose |
|---|---:|---|---|
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Test execution |
| xunit | 2.9.3 | Apache-2.0 | Unit testing |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | Visual Studio test runner integration |

The test dependencies are not required by the bootstrap source package and are not needed to run the application.
