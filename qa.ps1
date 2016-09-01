
#build tests
& $env:MSBUILD_PATH /p:configuration=Release /p:DeployExtension=false /p:ZipPackageCompressionLevel=normal /v:m

#download nuget package
$password = convertto-securestring -String "$env:REPOX_QAPUBLICADMIN_PASSWORD" -AsPlainText -Force
$cred = new-object -typename System.Management.Automation.PSCredential -argumentlist $env:REPOX_QAPUBLICADMIN_USERNAME, $password
$ARTIFACTORY_SRC_REPO="sonarsource-nuget-qa"
$url = "$env:ARTIFACTORY_URL/$ARTIFACTORY_SRC_REPO/$ARTIFACT"
Invoke-WebRequest -UseBasicParsing -Uri $url -Credential $cred

#unzip nuget package
$zipName=$env:FILENAME.Substring(0, $env:FILENAME.LastIndexOf('.'))+".zip"
mv $env:FILENAME $zipName
$shell_app=new-object -com shell.application
$currentdir=(Get-Item -Path ".\" -Verbose).FullName
$destination = $shell_app.NameSpace($currentdir)
$zip_file = $shell_app.NameSpace("$currentdir\$zipName")
$destination.CopyHere($zip_file.Items())

#move dlls to correct locations
mv analyzers\*.dll src\SonarLint.CSharp\bin\Release

#run tests
& $env:VSTEST_PATH .\src\Tests\SonarLint.SonarQube.Integration.UnitTest\bin\Release\SonarLint.SonarQube.Integration.UnitTest.dll
& $env:VSTEST_PATH .\src\Tests\SonarLint.UnitTest\bin\Release\SonarLint.UnitTest.dll
 
#run regression-test
.\its\regression-test.bat