@echo off
set SolutionRoot=%~dp0..\

if not defined NUGET_PATH ( set NUGET_PATH=nuget )
if not defined MSBUILD_PATH ( set MSBUILD_PATH=msbuild )
if not defined VSTEST_PATH ( set VSTEST_PATH=vstest.console.exe )

rem build minimal solution
%NUGET_PATH% restore %SolutionRoot%SonarQube.CSharp.CodeAnalysis.sln
if %errorlevel% neq 0 exit /b %errorlevel%
%MSBUILD_PATH% %SolutionRoot%SonarQube.CSharp.CodeAnalysis.sln /t:Rebuild /p:Configuration=Release /p:DeployExtension=false
if %errorlevel% neq 0 exit /b %errorlevel%

rem build full solution with integration pieces
%NUGET_PATH% restore %SolutionRoot%SonarQube.CSharp.CodeAnalysis.Integration.sln 
if %errorlevel% neq 0 exit /b %errorlevel%
%MSBUILD_PATH% %SolutionRoot%SonarQube.CSharp.CodeAnalysis.Integration.sln /t:Rebuild /p:Configuration=Release /p:DeployExtension=false
if %errorlevel% neq 0 exit /b %errorlevel%

rem run tests
%VSTEST_PATH% %SolutionRoot%src\Tests\CSharp.CodeAnalysis.Common.UnitTest\bin\Release\SonarQube.CSharp.CodeAnalysis.Common.UnitTest.dll
if %errorlevel% neq 0 exit /b %errorlevel%
%VSTEST_PATH% %SolutionRoot%src\Tests\CSharp.CodeAnalysis.UnitTest\bin\Release\SonarQube.CSharp.CodeAnalysis.UnitTest.dll
if %errorlevel% neq 0 exit /b %errorlevel%
%VSTEST_PATH% %SolutionRoot%src\Tests\SonarQube.CSharp.CodeAnalysis.UnitTest\bin\Release\SonarQube.CSharp.CodeAnalysis.Integration.UnitTest.dll
if %errorlevel% neq 0 exit /b %errorlevel%
%VSTEST_PATH% %SolutionRoot%src\Tests\CSharp.CodeAnalysis.RulingTest\bin\Release\SonarQube.CSharp.CodeAnalysis.RulingTest.dll
if %errorlevel% neq 0 exit /b %errorlevel%
