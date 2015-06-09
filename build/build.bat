@echo off
set SolutionRoot=%~dp0..\

if not defined NUGET_PATH ( set NUGET_PATH=nuget )
if not defined MSBUILD_PATH ( set MSBUILD_PATH=msbuild )

rem build minimal solution
%NUGET_PATH% restore %SolutionRoot%SonarQube.CSharp.CodeAnalysis.sln
if %errorlevel% neq 0 exit /b %errorlevel%
%MSBUILD_PATH% %SolutionRoot%SonarQube.CSharp.CodeAnalysis.sln -t:Rebuild -p:Configuration=Release
if %errorlevel% neq 0 exit /b %errorlevel%
