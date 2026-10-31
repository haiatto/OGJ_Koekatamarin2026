param(
    [string]$Apk = '',
    [string]$AndroidPlayer = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer'
)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
if (!$Apk) { $Apk = Join-Path $repoPath 'QuestApp/Builds/Koekatamarin2026.apk' }
$apkPath = (Resolve-Path -LiteralPath $Apk).Path
$buildTools = Get-ChildItem -LiteralPath (Join-Path $AndroidPlayer 'SDK/build-tools') -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'aapt2.exe') } |
    Sort-Object Name -Descending | Select-Object -First 1
if (!$buildTools) { throw 'Android SDK aapt2 not found' }
$aapt = Join-Path $buildTools.FullName 'aapt2.exe'
$manifest = & $aapt dump xmltree $apkPath --file AndroidManifest.xml
if ($LASTEXITCODE -ne 0) { throw 'APK manifest could not be read' }
$manifestText = $manifest -join "`n"
foreach ($required in @('com.haiatto.koekatamarin2026', 'android.permission.INTERNET',
    'android.permission.RECORD_AUDIO', 'android.permission.CHANGE_WIFI_MULTICAST_STATE',
    'com.oculus.permission.BOUNDARY_VISIBILITY', 'com.oculus.feature.PASSTHROUGH', 'com.oculus.intent.category.VR')) {
    if (!$manifestText.Contains($required)) { throw "Manifest entry missing: $required" }
}
if ($manifestText.Contains('BOUNDARYLESS_APP')) { throw 'Always-boundaryless manifest conflicts with optional VR mode' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($apkPath)
try {
    $native = @($zip.Entries | Where-Object { $_.FullName -match '^lib/.+\.so$' } | ForEach-Object { $_.FullName })
    if (!($native -contains 'lib/arm64-v8a/libil2cpp.so')) { throw 'ARM64 IL2CPP binary missing' }
    if (!($native -contains 'lib/arm64-v8a/libOVRPlugin.so')) { throw 'Android Meta XR native plugin missing' }
    if (@($native | Where-Object { $_ -notmatch '^lib/arm64-v8a/' }).Count -gt 0) { throw 'Unexpected ABI in APK' }
}
finally { $zip.Dispose() }
$java = Join-Path $AndroidPlayer 'OpenJDK/bin/java.exe'
$signer = Join-Path $buildTools.FullName 'lib/apksigner.jar'
& $java -jar $signer verify $apkPath
if ($LASTEXITCODE -ne 0) { throw 'APK signature validation failed' }
$report = [ordered]@{
    apk = (Split-Path -Leaf $apkPath)
    bytes = (Get-Item -LiteralPath $apkPath).Length
    sha256 = (Get-FileHash -LiteralPath $apkPath -Algorithm SHA256).Hash.ToLowerInvariant()
    architecture = 'arm64-v8a'
    scriptingBackend = 'IL2CPP'
    signatureVerified = $true
    requiredManifestEntriesVerified = $true
    alwaysBoundaryless = $false
    nativeLibraries = $native
    deviceTested = $false
}
$report | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath ($apkPath + '.json') -Encoding utf8
$report | ConvertTo-Json -Depth 3
