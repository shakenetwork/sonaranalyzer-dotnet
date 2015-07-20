param($installPath, $toolsPath, $package, $project)

if ($project.DTE.Version -eq '14.0')
{
	$analyzersPath = join-path $toolsPath "analyzers"
	$analyzersPath = join-path $analyzersPath "C#"
	$analyzerFilePath = join-path $analyzersPath "SonarLint.dll"
	if ($project.Object.AnalyzerReferences -ne $null)
	{
		$project.Object.AnalyzerReferences.Remove($analyzerFilePath)
	}	
}
