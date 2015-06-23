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
call %SolutionRoot%build/generate_documentation.bat

rem create descriptors
call %SolutionRoot%build/generate_rule_descriptor.bat

rem create Nuget packages
forfiles /p %SolutionRoot%src 		/s /m *.nuspec /c "cmd /c %NUGET_PATH% pack @PATH -NoPackageAnalysis -OutputDirectory .\bin\Release%