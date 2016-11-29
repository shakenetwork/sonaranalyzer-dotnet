# Usage:
# rspec.ps1 -operation update
#
# Will download only the updated RSPEC files
#
# or
# rspec.ps1 -operation add -language cs|vbnet -rulekey <rule-key>
#
# Add will download the specified rule json and html files. If you want to add a 
# rule to both c# and vb.net execute the operation for each language.
#
# NOTE: All operations recreate the projects' resources, do not edit manually.
param (
    [Parameter(ParameterSetName="update,refresh", Mandatory=$true, HelpMessage="Operation: refresh, update or add")]
    [Parameter(ParameterSetName="add", Mandatory=$true, HelpMessage="Operation: refresh, update or add")]
    [ValidateSet("refresh", "update", "add")]
    [string]
    $Operation,
    [Parameter(ParameterSetName="add", Mandatory=$true, HelpMessage="Language: cs or vbnet")]
    [ValidateSet("cs", "vbnet")]
    [string]
    $Language,
    [Parameter(ParameterSetName="add", Mandatory=$true, HelpMessage="Rule Key, for example S1234")]
    [string]
    $RuleKey
)

$env:rule_api_path = "c:\\work\\rule-api-1.16.0.840.jar" # TODO: remove me
if (-Not (Test-Path $env:rule_api_path))
{
    throw "Download the latest version of rule-api jar from repox and set the %rule_api_path% environment variable with the full path of the jar."
}

$resgenPath = "${Env:ProgramFiles(x86)}\\Microsoft SDKs\\Windows\\v10.0A\\bin\\NETFX 4.6.1 Tools\\ResGen.exe"
    Write-Output $resgenPath

# if (-Not (Test-Path $resgenPath))
# {
#     Write-Output $resgenPath
#     throw "You need to install the Windows SDK before using this script."
# }

$categories_map = 
@{
    "BUG" = "Sonar Bug";
    "CODE_SMELL" = "Sonar Code Smell";
    "VULNERABILITY" = "Sonar Vulnerability";
}

$severities_map = 
@{
    "Critical" = "Critical";
    "Major" = "Major";
    "Minor" = "Minor";
    "Info" = "Info";
    "Blocker" = "Blocker";
}

$remediations_map = 
@{
    "" = "";
    "Constant/Issue" = "Constant/Issue";
}

$projects_map =
@{
    "cs" = "SonarAnalyzer.CSharp";
    "vbnet" = "SonarAnalyzer.VisualBasic";
}

$rule_api_languages =
@{
    "cs" = "c#";
    "vbnet" = "vb.net";
}

$resource_languages =
@{
    "cs" = "cs";
    "vbnet" = "vb";
}

# Returns the path to the folder where the RSPEC html and json files for the specified language will be downloaded.
function GetRspecDownloadPath()
{
    param (
        [Parameter(Mandatory=$true)]
        [string]
        $lang
    )

    $rspecFolder = "${PSScriptRoot}\\..\\rspec\\${lang}"
    if (-Not (Test-Path $rspecFolder))
    {
        New-Item $rspecFolder | Out-Null
    }

    return $rspecFolder
}

# Returns a string array with rule keys for the specified language.
function GetRules()
{
    param 
    (
        [Parameter(Mandatory=$true)]
        [string]
        $lang
    )
    $suffix = $rule_api_languages.Get_Item($lang)

    $htmlFiles = Get-ChildItem "$(GetRspecDownloadPath $lang)\\*" -Include "*.html"
    foreach ($htmlFile in $htmlFiles)
    {
        if ($htmlFile -Match "(S\d+)_(${suffix}).html")
        {
            $Matches[1]
        }
    }
}

# Copies the downloaded RSPEC html files for all rules in the specified language
# to 'SonarAnalyzer.Utilities\Rules.Description'. If a rule is present in the otherLanguageRules
# collection, a language suffix will be added to the target file name, so that the VB.NET and
# C# files could have different html resources.  
function CopyResources()
{
    param
    (
        [Parameter(Mandatory=$true)]
        [string]
        $lang,
        $rules,
        $otherLanguageRules
    )
    $rspecFolder = GetRspecDownloadPath $lang
    $source_suffix = "_" + $rule_api_languages.Get_Item($lang)
    foreach ($rule in $rules)
    {
        $suffix = ""
        if ($otherLanguageRules -contains $rule)
        {
            $suffix = "_$($resource_languages.Get_Item($lang))"
        }

        Copy-Item "${rspecFolder}\\${rule}${source_suffix}.html" "${PSScriptRoot}\\..\\src\\SonarAnalyzer.Utilities\\Rules.Description\\${rule}${suffix}.html"
    }
}

function CreateStringResources()
{
    param ($lang, $rules)

    $rspecFolder = GetRspecDownloadPath $lang
    $suffix = $rule_api_languages.Get_Item($lang)

    $sonarWayRules = Get-Content -Raw "${rspecFolder}\\Sonar_way_profile.json" | ConvertFrom-Json

    $resources = New-Object System.Collections.ArrayList

    foreach ($rule in $rules)
    {
        $json = Get-Content -Raw "${rspecFolder}\\${rule}_${suffix}.json" | ConvertFrom-Json
        $html = Get-Content -Raw "${rspecFolder}\\${rule}_${suffix}.html"

        # take the first paragraph of the HTML file
        if ($html -Match "<p>((.|\n)*?)</p>")
        {
            # strip HTML tags and new lines
            $description = $Matches[1] -replace '<[^>]*>', ''
            $description = $description -replace '\n|( +)', ' '
        }
        else 
        {
            throw "The downloaded HTML for rule '${rule}' does not contain any paragraphs."
        }

        [void]$resources.Add("${rule}_Description=${description}")
        [void]$resources.Add("${rule}_Title=$(${json}.title)")
        [void]$resources.Add("${rule}_Category=$($categories_map.Get_Item(${json}.type))")
        [void]$resources.Add("${rule}_IsActivatedByDefault=$(${sonarWayRules}.ruleKeys -Contains ${rule})")
        [void]$resources.Add("${rule}_Severity=$($severities_map.Get_Item(${json}.defaultSeverity))") # TODO see how can we implement lowering the severity for certain rules
        [void]$resources.Add("${rule}_Tags=" + (${json}.tags -Join ","))

        if (${json}.remediation.func)
        {
            [void]$resources.Add("${rule}_Remediation=$($remediations_map.Get_Item(${json}.remediation.func))")
            [void]$resources.Add("${rule}_RemediationCost=$(${json}.remediation.constantCost)") # TODO see if we have remediations other than constantConst and fix them
        }
    }

    # improve readability of the generated file
    [void]$resources.Sort()

    $rawResourcesPath = "${PSScriptRoot}\\${lang}_strings.restext"
    $resourcesPath = "${PSScriptRoot}\\..\\src\\$($projects_map.Get_Item($lang))\\RspecStrings.resx"

    Set-Content $rawResourcesPath $resources

    Invoke-Expression "& `"${resgenPath}`" ${rawResourcesPath} ${resourcesPath}" # to compile add this parameter /str:$($resource_languages.Get_Item($lang)),SonarAnalyzer.Rules"
}

switch ($Operation) 
{
    "refresh"
    {
        # just recreate the resources from the existing files in SCM
    }
    "update"
    {
        java -jar $env:rule_api_path update -directory $(GetRspecDownloadPath "cs") -language $($rule_api_languages.Get_Item("cs"))
        java -jar $env:rule_api_path update -directory $(GetRspecDownloadPath "vbnet") -language $($rule_api_languages.Get_Item("vbnet"))
    }
    "add"
    {
        java -jar $env:rule_api_path generate -directory $(GetRspecDownloadPath $Language) -language $($rule_api_languages.Get_Item($Language)) -rule $RuleKey
    }
}

$csRules = GetRules "cs"
$vbRules = GetRules "vbnet"

CopyResources "cs" $csRules $vbRules
CopyResources "vbnet" $vbRules $csRules
CreateStringResources "cs" $csRules
CreateStringResources "vbnet" $vbRules