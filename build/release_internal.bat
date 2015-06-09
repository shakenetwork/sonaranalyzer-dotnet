@echo off
set SolutionRoot=%~dp0..\

if not defined NUGET_PATH ( set NUGET_PATH=nuget )
if not defined MSBUILD_PATH ( set MSBUILD_PATH=msbuild )

rem delete Nuget packages (shouldn't be any)
del /s /q %SolutionRoot%src*.nupkg 

rem call the normal build
call %SolutionRoot%build/cibuild.bat
if %errorlevel% neq 0 exit /b %errorlevel%

rem create rule documentation ZIP
pushd %SolutionRoot%src\CSharp.CodeAnalysis.DocGenerator\bin\Release\
SonarQube.CSharp.CodeAnalysis.DocGenerator.exe
if %errorlevel% neq 0 exit /b %errorlevel%
popd

rem create Nuget packages
pushd %SolutionRoot%src\SonarQube.CSharp.Descriptor\bin\Release\
SonarQube.CSharp.CodeAnalysis.Descriptor.exe rules.xml profile.xml sqale.xml
if %errorlevel% neq 0 exit /b %errorlevel%
popd
forfiles /p %SolutionRoot%src 		/s /m *.nuspec /c "cmd /c %NUGET_PATH% pack @PATH -NoPackageAnalysis -OutputDirectory .\bin\Release%