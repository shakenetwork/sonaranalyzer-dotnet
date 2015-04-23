param($installPath, $toolsPath, $package, $project)

if ($project.DTE.Version -ne '14.0')
{
    throw 'The package can only be installed on Visual Studio 2015 CTP.'
}

$analyzersPath = join-path $toolsPath "analyzers"
$analyzersPath = join-path $analyzersPath "C#"
$analyzerFilePath = join-path $analyzersPath "SonarQube.CSharp.CodeAnalysis.dll"
$project.Object.AnalyzerReferences.Add($analyzerFilePath)