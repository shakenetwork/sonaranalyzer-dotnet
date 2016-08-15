if ($env:IS_PULLREQUEST -eq "true") { 

	#setup Nuget.config
	del $env:APPDATA\NuGet\NuGet.Config
	& $env:NUGET_PATH sources Add -Name repox -Source https://repox.sonarsource.com/api/nuget/sonarsource-nuget-qa/
	$apikey = $env:ARTIFACTORY_DEPLOY_USERNAME+":"+$env:ARTIFACTORY_DEPLOY_PASSWORD
	& $env:NUGET_PATH setapikey $apikey -Source repox
	
	#download MSBuild
	$url = "https://github.com/SonarSource-VisualStudio/sonar-msbuild-runner/releases/download/2.0/MSBuild.SonarQube.Runner-2.0.zip"
    $output = "MSBuild.SonarQube.Runner.zip"    
    Invoke-WebRequest -Uri $url -OutFile $output
	unzip -o MSBuild.SonarQube.Runner.zip

	#generate build version from the build number
	$buildversion="$env:BUILD_NUMBER"
	for($i=1; $i -le 5-$env:BUILD_NUMBER.length; $i++){$buildversion = "0"+$buildversion}

	#Append build number to the version
	(Get-Content .\build\Version.props) -replace 'AssemblyInformationalVersion\)', "AssemblyInformationalVersion).$buildversion" | Set-Content .\build\Version.props
	& $env:MSBUILD_PATH  build/ChangeVersion.proj

	#build
	& $env:NUGET_PATH restore SonarLint.sln
	& $env:MSBUILD_PATH SonarLint.sln /t:rebuild /p:Configuration=Release /p:DeployExtension=false

	#Generate the XML descriptor files for the C# plugin
	pushd src\SonarQube.SonarLint.Descriptor\bin\Release
	.\SonarLint.Descriptor.exe cs
	.\SonarLint.Descriptor.exe vbnet
	popd

	#generate packages
	$files = Get-ChildItem src -recurse *.nuspec
	foreach ($file in $files) {
		$output = $file.directoryname+"\bin\Release"
		& $env:NUGET_PATH pack $file.fullname -NoPackageAnalysis -OutputDirectory $output
	}

    #get version number
    [xml]$versionProps = Get-Content .\build\Version.props
    $version = $versionProps.Project.PropertyGroup.MainVersion+"."+$buildversion

	#upload packages
	$files = Get-ChildItem src -recurse *.nupkg
	foreach ($file in $files) {    
		#upload to nuget repo 
		& $env:NUGET_PATH push $file.fullname -Source repox
		#compute artifact name from filename
		$artifact=$file.name.replace($file.extension,"").replace(".$version","")
        $filePath=$file.fullname
		#upload to maven repo
		& "$env:WINDOWS_MVN_HOME\bin\mvn.bat" deploy:deploy-file -DgroupId="org.sonarsource.dotnet" -DartifactId="$artifact" -Dversion="$version" -Dpackaging="nupkg" -Dfile="$filePath" -DrepositoryId="sonarsource-public-qa" -Durl="https://repox.sonarsource.com/sonarsource-public-qa"
	}

} else {
	write-host -f green "not in a pull request"
}