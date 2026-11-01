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
    foreach ($library in @('libonnxruntime.so', 'libsherpa-onnx-c-api.so')) {
        if (!($native -contains ('lib/arm64-v8a/' + $library))) { throw "Offline ASR binary missing: $library" }
    }
    if (@($native | Where-Object { $_ -notmatch '^lib/arm64-v8a/' }).Count -gt 0) { throw 'Unexpected ABI in APK' }
    $speechHashes = [ordered]@{
        'model.int8.onnx' = 'c71f0ce00bec95b07744e116345e33d8cbbe08cef896382cf907bf4b51a2cd51'
        'tokens.txt' = 'f449eb28dc567533d7fa59be34e2abca8784f771850c78a47fb731a31429a1dc'
        'silero_vad.onnx' = '9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6'
    }
    foreach ($name in $speechHashes.Keys) {
        $entry = $zip.GetEntry('assets/Speech/' + $name)
        if (!$entry) { throw "Bundled speech model missing: $name" }
        $stream = $entry.Open()
        $hasher = [Security.Cryptography.SHA256]::Create()
        try { $digest = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $hasher.Dispose(); $stream.Dispose() }
        if ($digest -ne $speechHashes[$name]) { throw "Bundled speech model hash mismatch: $name" }
    }
    foreach ($notice in @('ATTRIBUTION.txt', 'FunASR-MODEL-LICENSE.txt', 'Silero-VAD-LICENSE.txt',
        'sherpa-onnx-LICENSE.txt', 'ONNX-Runtime-LICENSE.txt', 'ONNX-Runtime-ThirdPartyNotices.txt')) {
        if (!$zip.GetEntry('assets/Speech/Notices/' + $notice)) { throw "Speech license missing: $notice" }
    }
}
finally { $zip.Dispose() }
$java = Join-Path $AndroidPlayer 'OpenJDK/bin/java.exe'
$signer = Join-Path $buildTools.FullName 'lib/apksigner.jar'
& $java -jar $signer verify $apkPath
if ($LASTEXITCODE -ne 0) { throw 'APK signature validation failed' }
$apkStream = [IO.File]::OpenRead($apkPath)
$apkHasher = [Security.Cryptography.SHA256]::Create()
try { $apkDigest = [BitConverter]::ToString($apkHasher.ComputeHash($apkStream)).Replace('-', '').ToLowerInvariant() }
finally { $apkHasher.Dispose(); $apkStream.Dispose() }
$report = [ordered]@{
    apk = (Split-Path -Leaf $apkPath)
    bytes = (Get-Item -LiteralPath $apkPath).Length
    sha256 = $apkDigest
    architecture = 'arm64-v8a'
    scriptingBackend = 'IL2CPP'
    signatureVerified = $true
    requiredManifestEntriesVerified = $true
    alwaysBoundaryless = $false
    nativeLibraries = $native
    offlineSpeechModelHashes = $speechHashes
    speechLicensesBundled = $true
    deviceTested = $false
}
$report | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath ($apkPath + '.json') -Encoding utf8
$report | ConvertTo-Json -Depth 3
