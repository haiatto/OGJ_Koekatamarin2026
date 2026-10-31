param(
    [ValidateSet('Import','TextResources','Setup','Tests','Android','Preview')]
    [string]$Stage = 'Import',
    [string]$Editor = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe',
    [switch]$WithoutEditorXr
)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoPath 'QuestApp'
$logPath = Join-Path $repoPath '.work'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null
if (!(Test-Path -LiteralPath $Editor)) { throw "Unity Editor not found: $Editor" }
$unityArgs = @('-batchmode', '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + (Join-Path $logPath "unity-$Stage.log") + '"'))
if ($Stage -ne 'Preview') { $unityArgs += '-nographics' }
else { $unityArgs += '-force-d3d11' }
switch ($Stage) {
    'Import' { $unityArgs += '-quit' }
    'TextResources' { $unityArgs += @('-executeMethod', 'Koekatamarin.Editor.ProjectSetup.ImportTextResources') }
    'Setup' { $unityArgs += @('-quit', '-executeMethod', 'Koekatamarin.Editor.ProjectSetup.Generate', '-buildTarget', 'Android') }
    'Tests' { $unityArgs += @('-runTests', '-testPlatform', 'EditMode', '-testFilter', 'Koekatamarin.Tests', '-testResults', ('"' + (Join-Path $logPath 'test-results.xml') + '"')) }
    'Android' { $unityArgs += @('-quit', '-executeMethod', 'Koekatamarin.Editor.ProjectSetup.BuildAndroid', '-buildTarget', 'Android') }
    'Preview' { $unityArgs += @('-executeMethod', 'Koekatamarin.Editor.PreviewCapture.Begin') }
}
$pluginBackups = @{}
try {
    if ($WithoutEditorXr) {
        # Only the local package-cache importer setting changes. Android AARs are untouched.
        $cacheRoot = Join-Path $projectPath 'Library/PackageCache'
        foreach ($packageDir in Get-ChildItem -LiteralPath $cacheRoot -Directory -Filter 'com.meta.xr.sdk.core*') {
            $metaPath = Join-Path $packageDir.FullName 'Plugins/Win64OpenXR/OVRPlugin.dll.meta'
            if (!(Test-Path -LiteralPath $metaPath)) { continue }
            $original = [IO.File]::ReadAllText($metaPath)
            $pluginBackups[$metaPath] = $original
            $disabled = $original -replace '(?m)^(\s*Exclude Editor:) 0', '$1 1'
            $disabled = $disabled -replace '(?m)^(    Editor:\r?\n      enabled:) 1', '$1 0'
            [IO.File]::WriteAllText($metaPath, $disabled)
        }
    }
    $unityProcess = Start-Process -FilePath $Editor -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
    $unityProcess.WaitForExit()
}
finally {
    foreach ($metaPath in $pluginBackups.Keys) {
        if (Test-Path -LiteralPath $metaPath) { [IO.File]::WriteAllText($metaPath, $pluginBackups[$metaPath]) }
    }
}
if ($unityProcess.ExitCode -ne 0) { throw "Unity $Stage failed ($($unityProcess.ExitCode)); see .work/unity-$Stage.log" }
if ($Stage -eq 'Tests') {
    [xml]$testReport = Get-Content -LiteralPath (Join-Path $logPath 'test-results.xml')
    if ($testReport.'test-run'.result -ne 'Passed') { throw 'Unity tests did not pass' }
    Write-Output "Tests passed: $($testReport.'test-run'.passed)"
}
Write-Output "Unity $Stage completed"
