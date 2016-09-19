function CreateRelease([string] $productName, [string] $version, [string] $expectedSha1, [string] $releaseVersion)
{
    # delete NuGet file if exists
    If (Test-Path "$productName.$version.nupkg"){
        Remove-Item "$productName.$version.nupkg"
    }

    # download nuget package
    $url = "https://repox.sonarsource.com/sonarsource-nuget-releases/$productName.$version.nupkg"
    Invoke-WebRequest -UseBasicParsing -Uri "$url" -OutFile "$productName.$version.nupkg"

    # create working folder
    $currentDir=(Get-Item -Path ".\" -Verbose).FullName
    $workDir=$currentDir + "\$productName"
    New-Item $workDir -type directory -force
    Remove-Item "$workDir\*" -recurse

    # unzip nuget package
    $zipName="$productName.$version.zip"
    Move-Item "$productName.$version.nupkg" $zipName -force
    $shell_app=new-object -com shell.application
    $destination = $shell_app.NameSpace($workDir)
    $zip_file = $shell_app.NameSpace("$currentDir\$zipName")
    Write-Host "Unzipping $workDir\$zipName"
    $destination.CopyHere($zip_file.Items(), 0x14) 

    # get sha1
    $productversion=ls $workDir\analyzers\SonarAnalyzer.dll | % { $_.versioninfo.productversion }
    $sha1=$productversion.Substring($productversion.LastIndexOf('Sha1:')+5)
    if ($sha1 -ne $expectedSha1){
        Write-Host "SHA1 doesn't match expected"
        return
    }

    # change content of nuspec file
    (Get-Content $workDir\$productName.nuspec) -replace "<version>$version</version>", "<version>$releaseVersion</version>" | Set-Content $workDir\$productName.nuspec

    $additionalContent = "<files>
     <file src=""analyzers\SonarAnalyzer.dll"" target=""analyzers"" />
     <file src=""analyzers\$productName.dll"" target=""analyzers"" />
     <file src=""tools\*.ps1"" target=""tools\"" />
    </files>"

    $fileContent = (Get-Content $workDir\$productName.nuspec) -replace "</metadata>", "</metadata>$additionalContent"
    $Utf8NoBomEncoding = New-Object System.Text.UTF8Encoding($False)
    [System.IO.File]::WriteAllLines("$workDir\$productName.nuspec", $fileContent, $Utf8NoBomEncoding)

    # repack with the new name
    &$(env:NUGET_PATH) pack $workDir\$productName.nuspec
}

CreateRelease -productName "SonarAnalyzer.CSharp" -version $(env:BUILD_NAME) -expectedSha1 $(env:SHA1) -releaseVersion $(env:RELEASE_NAME)
CreateRelease -productName "SonarAnalyzer.VisualBasic" -version $(env:BUILD_NAME) -expectedSha1 $(env:SHA1) -releaseVersion $(env:RELEASE_NAME)

# push to repox
