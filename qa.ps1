
#cleanup
del %USERPROFILE%\AppData\Local\Microsoft\MSBuild\14.0\Microsoft.Common.targets\ImportBefore\SonarLint.Testing.ImportBefore.targets 

#nuget restore
& $env:NUGET_PATH restore SonarLint.sln

#build tests
& $env:MSBUILD_PATH /p:configuration=Release /p:DeployExtension=false /p:ZipPackageCompressionLevel=normal /v:m

#download nuget package
$ARTIFACTORY_SRC_REPO="sonarsource-nuget-qa"
$url = "$env:ARTIFACTORY_URL/$ARTIFACTORY_SRC_REPO/$env:FILENAME"
Write-Host "Downloading $url"
$pair = "$($env:REPOX_QAPUBLICADMIN_USERNAME):$($env:REPOX_QAPUBLICADMIN_PASSWORD)"
$encodedCreds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($pair))
$basicAuthValue = "Basic $encodedCreds"
$Headers = @{Authorization = $basicAuthValue}
Invoke-WebRequest -UseBasicParsing -Uri "$url" -Headers $Headers -OutFile $env:FILENAME


#unzip nuget package
$zipName=$env:FILENAME.Substring(0, $env:FILENAME.LastIndexOf('.'))+".zip"
Move-Item $env:FILENAME $zipName -force
$shell_app=new-object -com shell.application
$currentdir=(Get-Item -Path ".\" -Verbose).FullName
$destination = $shell_app.NameSpace($currentdir)
$zip_file = $shell_app.NameSpace("$currentdir\$zipName")
Write-Host "Unzipping $currentdir\$zipName"
$destination.CopyHere($zip_file.Items())

#move dlls to correct locations
Write-Host "Installing downloaded dlls"
Move-Item .\analyzers\*.dll .\src\SonarLint.CSharp\bin\Release -force

#run tests
Write-Host "Start tests"
& $env:VSTEST_PATH .\src\Tests\SonarLint.SonarQube.Integration.UnitTest\bin\Release\SonarLint.SonarQube.Integration.UnitTest.dll
& $env:VSTEST_PATH .\src\Tests\SonarLint.UnitTest\bin\Release\SonarLint.UnitTest.dll
 
#run regression-test
Write-Host "Start regression tests"
cd its
git submodule update --init --recursive --depth 1
cmd /c .\regression-test.bat