@echo off
set SolutionRoot=%~dp0..\

if not defined BuildConfiguration ( set BuildConfiguration=Release )

rem create rule documentation ZIP
pushd %SolutionRoot%src\CSharp.CodeAnalysis.DocGenerator\bin\%BuildConfiguration%\
SonarLint.DocGenerator.exe
if %errorlevel% neq 0 exit /b %errorlevel%
popd
