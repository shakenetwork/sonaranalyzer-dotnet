IF NOT "%IS_PULLREQUEST%"=="true" ( EXIT /b )

curl -L https://github.com/SonarSource-VisualStudio/sonar-msbuild-runner/releases/download/2.0/MSBuild.SonarQube.Runner-2.0.zip > MSBuild.SonarQube.Runner.zip
unzip MSBuild.SonarQube.Runner.zip

MSBuild.SonarQube.Runner begin /k:sonaranalyzer-csharp-vbnet /n:"SonarAnalyzer for C#" /v:latest ^
  /d:sonar.host.url=%SONAR_HOST_URL% ^
  /d:sonar.login=%SONAR_TOKEN% ^
  /d:sonar.github.pullRequest=%PULL_REQUEST% ^
  /d:sonar.github.repository=%GITHUB_REPO% ^
  /d:sonar.github.oauth=%GITHUB_TOKEN% ^
  /d:sonar.analysis.mode=issues ^
  /d:sonar.scanAllFiles=true

%NUGET_PATH% restore SonarLint.sln
%MSBUILD_PATH% SonarLint.sln /t:rebuild /p:Configuration=Release

MSBuild.SonarQube.Runner end /d:sonar.login=%SONAR_TOKEN%