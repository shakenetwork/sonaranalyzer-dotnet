$ErrorActionPreference = "Stop"

#cleanup
$strFileName="%USERPROFILE%\AppData\Local\Microsoft\MSBuild\14.0\Microsoft.Common.targets\ImportBefore\SonarLint.Testing.ImportBefore.targets" 
If (Test-Path $strFileName){
	Remove-Item $strFileName
}

#nuget restore
& $env:NUGET_PATH restore SonarLint.sln

#build tests
& $env:MSBUILD_PATH /p:configuration=Release /p:DeployExtension=false /p:ZipPackageCompressionLevel=normal /v:m /p:defineConstants=SignAssembly /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=%CERT_PATH%

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
$destination.CopyHere($zip_file.Items(), 0x14) 

#get sha1
$productversion="empty"
if (Test-Path .\analyzers\SonarAnalyzer.dll) {
  $productversion=ls .\analyzers\SonarAnalyzer.dll | % { $_.versioninfo.productversion }
}else{
  if (Test-Path .\assembly\SonarAnalyzer.dll) {
    $productversion=ls .\assembly\SonarAnalyzer.dll | % { $_.versioninfo.productversion }
  }   
}

if ($productversion -eq "empty") {
    Write-Host "Couldn't determine sha1"
    exit 1
} 

$sha1=$productversion.Substring($productversion.LastIndexOf('Sha1:')+5)
Write-Host "Checking out $sha1"
$s="SHA1=$sha1"
$s | out-file ".\sha1.properties"

Write-Host "GITHUB_BRANCH $env:GITHUB_BRANCH"
if (($env:GITHUB_BRANCH -eq "master") -or ($env:GITHUB_BRANCH -eq "refs/heads/master")) {
    $env:GITHUB_BRANCH=$env:GITHUB_BRANCH.Substring(11)
}
$s="GITHUB_BRANCH=$env:GITHUB_BRANCH"
Write-Host "$s"
$s | out-file ".\branch.properties"

#checkout commit
git pull origin $env:GITHUB_BRANCH
git checkout -f $sha1

#move dlls to correct locations
Write-Host "Installing downloaded dlls"
$dllpath="empty"
if ($env:FILENAME -like '*CSharp*') {
    $dllpath="SonarLint.CSharp"
}
if ($env:FILENAME -like '*VisualBasic*') {
    $dllpath="SonarLint.VisualBasic"
}
if ($env:FILENAME -like '*Runner*') {
    $dllpath="SonarQube.SonarLint.Runner"
}
Copy-Item .\analyzers\*.dll .\src\$dllpath\bin\Release -force
Copy-Item .\analyzers\*.dll .\its\binaries -force

#run tests
Write-Host "Start tests"
& $env:VSTEST_PATH .\src\Tests\SonarLint.SonarQube.Integration.UnitTest\bin\Release\SonarLint.SonarQube.Integration.UnitTest.dll
& $env:VSTEST_PATH .\src\Tests\SonarLint.UnitTest\bin\Release\SonarLint.UnitTest.dll
 
#run regression-test
Write-Host "Start regression tests"
cd its
git submodule update --init --recursive --depth 1
cmd /c .\regression-test.bat