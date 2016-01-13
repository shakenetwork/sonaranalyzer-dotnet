param($installPath, $toolsPath, $package, $project)

if ($project.DTE.Version -ne '14.0')
{
    throw 'The package can only be installed on Visual Studio 2015.'
}

if ($project.Object.AnalyzerReferences -eq $null)
{
    throw 'The package cannot be installed as an analyzer reference.'
}

# $project.Type gives the language name like (C# or VB.NET)
$languageSpecificAnalyzer = ""
if($project.Type -eq "C#")
{
    $languageSpecificAnalyzer = "SonarLint.CSharp.dll"
}
if($project.Type -eq "VB.NET")
{
    $languageSpecificAnalyzer = "SonarLint.VisualBasic.dll"
}
if($languageSpecificAnalyzer -eq "")
{
    return
}


$analyzersPath = split-path -path $toolsPath -parent
$analyzersPath = join-path $analyzersPath "analyzers"

$analyzerFilePath = join-path $analyzersPath "SonarLint.dll"
$project.Object.AnalyzerReferences.Add($analyzerFilePath)

$analyzerFilePath = join-path $analyzersPath $languageSpecificAnalyzer
$project.Object.AnalyzerReferences.Add($analyzerFilePath)