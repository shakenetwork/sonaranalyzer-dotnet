param($installPath, $toolsPath, $package, $project)

if ($project.DTE.Version -eq '14.0')
{
	$analyzersPath = join-path $toolsPath "analyzers"
	$analyzersPath = join-path $analyzersPath "C#"
	$analyzerFilePath = join-path $analyzersPath "SonarLint.dll"
	$analyzerCommonFilePath = join-path $analyzersPath "SonarLint.Common.dll"
	if ($project.Object.AnalyzerReferences -ne $null)
	{
		if ($project.Object.AnalyzerReferences -Contains $analyzerFilePath)
        {
            $project.Object.AnalyzerReferences.Remove($analyzerFilePath)
        }
		if ($project.Object.AnalyzerReferences -Contains $analyzerCommonFilePath)
        {
            $project.Object.AnalyzerReferences.Remove($analyzerCommonFilePath)
        }
	}	
}
