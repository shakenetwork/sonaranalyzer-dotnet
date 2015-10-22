param($installPath, $toolsPath, $package, $project)

if ($project.DTE.Version -eq '14.0')
{
	$analyzersPath = split-path -path $toolsPath -parent
	$analyzersPath = join-path $analyzersPath "analyzers"
	$analyzersPath = join-path $analyzersPath "dotnet"
	$analyzerFilePath = join-path $analyzersPath "SonarLint.dll"
	if ($project.Object.AnalyzerReferences -ne $null)
	{
		if ($project.Object.AnalyzerReferences -Contains $analyzerFilePath)
        {
            $project.Object.AnalyzerReferences.Remove($analyzerFilePath)
        }
	}
}
