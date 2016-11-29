## How to increase SonarLint VS version

1. Open `Version.props` and enter the new version
2. Run `msbuild ChangeVersion.proj`


## Rule descriptions

### Prerequisites

Download the latest rules-api [internal tool] from repox, set the %rspec_api_path% environment variable with the full path to the jar, i.e. `c:\work\tools\rule-api-1.16.0.840.jar` 

## Update descriptions

1. Open PowerShell console, navigate to the `sonaranalyzer-dotnet` repository, i.e. `cd c:\work\sonaranalyzer-dotnet`
2. Go to `.\build`, i.e. `cd .\build`
3. Run `.\rspec.ps1 -operation update` and wait for it to complete. This will update the existing resources in the SonarAnalyzer.CSharp and SonarAnalyzer.VisualBasic projects
4. Commit the updated files in the `.\rspec\**`, `.\SonarAnalyzer.CSharp\**` and `.\SonarAnalyzer.VisualBasic\**` folders

## Add new rule

1. Open PowerShell console, navigate to the `sonaranalyzer-dotnet` repository, i.e. `cd c:\work\sonaranalyzer-dotnet`
2. Go to `.\build`, i.e. `cd .\build`
3. Run `.\rspec.ps1 -operation add -language <lang> -rulekey <rule-key>` where `<lang>` is `cs` for C# and `vbnet` for VB.NET, and `<rule-key>` is rule key, including the leading S, e.g. S1234. This will add new resources in the SonarAnalyzer.CSharp and SonarAnalyzer.VisualBasic projects for the specified rule
4. Commit the updated files in the `.\rspec\**`, `.\SonarAnalyzer.CSharp\**` and `.\SonarAnalyzer.VisualBasic\**` folders
