@echo off
set SolutionRoot=%~dp0..\

if not defined BuildConfiguration ( set BuildConfiguration=Release )

rem create Nuget packages
pushd %SolutionRoot%src\SonarQube.SonarLint.Descriptor\bin\%BuildConfiguration%\
SonarLint.Descriptor.exe cs
if %errorlevel% neq 0 exit /b %errorlevel%
SonarLint.Descriptor.exe vbnet
if %errorlevel% neq 0 exit /b %errorlevel%
popd
