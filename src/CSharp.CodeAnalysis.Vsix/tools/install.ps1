param($installPath, $toolsPath, $package, $project)

if ($project.DTE.Version -ne '14.0')
{
    throw 'The package can only be installed on Visual Studio 2015.'
}

$analyzersPath = join-path $toolsPath "analyzers"
$analyzersPath = join-path $analyzersPath "C#"
$analyzerFilePath = join-path $analyzersPath "SonarQube.CSharp.CodeAnalysis.dll"

if ($project.Object.AnalyzerReferences -eq $null)
{
	throw 'The package cannot be installed as an analyzer reference.'
}

$project.Object.AnalyzerReferences.Add($analyzerFilePath)