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
    'com.oculus.permission.BOUNDARY_VISIBILITY', 'com.oculus.permission.HAND_TRACKING',
    'com.oculus.permission.USE_SCENE', 'com.oculus.permission.USE_ANCHOR_API',
    'com.oculus.feature.PASSTHROUGH', 'com.oculus.intent.category.VR')) {
    if (!$manifestText.Contains($required)) { throw "Manifest entry missing: $required" }
}
if ($manifestText.Contains('BOUNDARYLESS_APP')) { throw 'Always-boundaryless manifest conflicts with optional VR mode' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($apkPath)
try {
    $native = @($zip.Entries | Where-Object { $_.FullName -match '^lib/.+\.so$' } | ForEach-Object { $_.FullName })
    if (!($native -contains 'lib/arm64-v8a/libil2cpp.so')) { throw 'ARM64 IL2CPP binary missing' }
    if (!($native -contains 'lib/arm64-v8a/libOVRPlugin.so')) { throw 'Android Meta XR native plugin missing' }
    if (!($native -contains 'lib/arm64-v8a/libmrutilitykitshared.so')) { throw 'Android MR Utility Kit native plugin missing' }
    foreach ($library in @('libonnxruntime.so', 'libsherpa-onnx-c-api.so')) {
        if (!($native -contains ('lib/arm64-v8a/' + $library))) { throw "Offline ASR binary missing: $library" }
    }
    if (@($native | Where-Object { $_ -notmatch '^lib/arm64-v8a/' }).Count -gt 0) { throw 'Unexpected ABI in APK' }
    $speechHashes = [ordered]@{
        'model.int8.onnx' = 'c71f0ce00bec95b07744e116345e33d8cbbe08cef896382cf907bf4b51a2cd51'
        'tokens.txt' = 'f449eb28dc567533d7fa59be34e2abca8784f771850c78a47fb731a31429a1dc'
        'Reazon/encoder-epoch-99-avg-1.int8.onnx' = '2c7bd08a8a99f9ddd0d9e458456577b1f6279214e51426f114f9eced44c54e1d'
        'Reazon/decoder-epoch-99-avg-1.onnx' = '58b18211ae06265466bfa17172dab574df94f76c8bcb61a3640c28ba860e4124'
        'Reazon/joiner-epoch-99-avg-1.int8.onnx' = '49cc7ea1d3d35a40a27442db5e89996da64bf0e683a903dce76e99e57a12e4de'
        'Reazon/tokens.txt' = '2c3ac659818a48a0c04010e0593bbc4d7c8a24a054340b01131499c05fd52def'
    }
    if ($zip.GetEntry('assets/Speech/silero_vad.onnx')) { throw 'Obsolete Silero VAD model is still bundled' }
    foreach ($name in $speechHashes.Keys) {
        $entry = $zip.GetEntry('assets/Speech/' + $name)
        if (!$entry) { throw "Bundled speech model missing: $name" }
        $stream = $entry.Open()
        $hasher = [Security.Cryptography.SHA256]::Create()
        try { $digest = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $hasher.Dispose(); $stream.Dispose() }
        if ($digest -ne $speechHashes[$name]) { throw "Bundled speech model hash mismatch: $name" }
    }
    foreach ($notice in @('ATTRIBUTION.txt', 'FunASR-MODEL-LICENSE.txt',
        'sherpa-onnx-LICENSE.txt', 'ONNX-Runtime-LICENSE.txt', 'ONNX-Runtime-ThirdPartyNotices.txt', 'ReazonSpeech-LICENSE.txt')) {
        if (!$zip.GetEntry('assets/Speech/Notices/' + $notice)) { throw "Speech license missing: $notice" }
    }
    foreach ($notice in @('Mikachan-LICENSE.txt', 'LightNovelPOP-LICENSE.txt', 'NotoSans-LICENSE.txt',
        'UXR-QuestMeshing-LICENSE.txt', 'UXR-QuestMeshing-ThirdPartyNotices.txt', 'Utils-Singleton-LICENSE.txt')) {
        if (!$zip.GetEntry('assets/Notices/' + $notice)) { throw "Bundled license missing: $notice" }
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
    sileroVadBundled = $false
    speechLicensesBundled = $true
    fontLicensesBundled = $true
    meshingLicensesBundled = $true
    deviceTested = $false
}
$report | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath ($apkPath + '.json') -Encoding utf8
$report | ConvertTo-Json -Depth 3
