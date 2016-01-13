param($installPath, $toolsPath, $package, $project)

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

if ($project.DTE.Version -eq '14.0')
{
    if ($project.Object.AnalyzerReferences -ne $null)
    {
        $analyzersPath = split-path -path $toolsPath -parent
        $analyzersPath = join-path $analyzersPath "analyzers"

        $analyzerFilePath = join-path $analyzersPath "SonarLint.dll"
        try
        {
            $project.Object.AnalyzerReferences.Remove($analyzerFilePath)
        }
        catch
        {
        }

        $analyzerFilePath = join-path $analyzersPath $languageSpecificAnalyzer
        try
        {
            $project.Object.AnalyzerReferences.Remove($analyzerFilePath)
        }
        catch
        {
        }
    }
}
