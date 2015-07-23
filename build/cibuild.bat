@echo off
set SolutionRoot=%~dp0..\

if not defined NUGET_PATH ( set NUGET_PATH=nuget )
if not defined MSBUILD_PATH ( set MSBUILD_PATH=msbuild )
if not defined VSTEST_PATH ( set VSTEST_PATH=vstest.console.exe )

rem build solution
call %SolutionRoot%build\build.bat

rem run tests
%VSTEST_PATH% %SolutionRoot%src\Tests\SonarLint.Common.UnitTest\bin\Release\SonarLint.Common.UnitTest.dll
if %errorlevel% neq 0 exit /b %errorlevel%
%VSTEST_PATH% %SolutionRoot%src\Tests\SonarLint.UnitTest\bin\Release\SonarLint.UnitTest.dll
if %errorlevel% neq 0 exit /b %errorlevel%
%VSTEST_PATH% %SolutionRoot%src\Tests\SonarLint.SonarQube.Integration.UnitTest\bin\Release\SonarLint.SonarQube.Integration.UnitTest.dll
if %errorlevel% neq 0 exit /b %errorlevel%
%VSTEST_PATH% %SolutionRoot%src\Tests\SonarLint.RulingTest\bin\Release\SonarLint.RulingTest.dll
if %errorlevel% neq 0 exit /b %errorlevel%
