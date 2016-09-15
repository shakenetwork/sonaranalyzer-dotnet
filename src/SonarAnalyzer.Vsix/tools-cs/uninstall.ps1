param($installPath, $toolsPath, $package, $project)

if($project.Type -ne "C#")
{
    return
}

if ($project.DTE.Version -eq '14.0')
{
    if ($project.Object.AnalyzerReferences -ne $null)
    {
        $analyzersPath = split-path -path $toolsPath -parent
        $analyzersPath = join-path $analyzersPath "analyzers"

        $analyzerFilePath = join-path $analyzersPath "SonarAnalyzer.dll"
        try
        {
            $project.Object.AnalyzerReferences.Remove($analyzerFilePath)
        }
        catch
        {
        }

        $analyzerFilePath = join-path $analyzersPath "SonarAnalyzer.CSharp.dll"
        try
        {
            $project.Object.AnalyzerReferences.Remove($analyzerFilePath)
        }
        catch
        {
        }
    }
}
