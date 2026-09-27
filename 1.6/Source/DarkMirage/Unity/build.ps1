param([string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity editor not found: $UnityPath" }
$buildLog = Join-Path $PSScriptRoot 'build.log'
# This project uses built-in rendering only; no UPM packages need to be resolved.
$p = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-nographics','-noUpm','-quit',
    '-projectPath',('"'+$PSScriptRoot+'"'),'-executeMethod','BuildMirage.Build',
    '-logFile',('"'+$buildLog+'"')) -WindowStyle Hidden -Wait -PassThru
if ($p.ExitCode -ne 0 -or !(Select-String -LiteralPath $buildLog -Pattern 'DARK_MIRAGE_BUILD_OK' -Quiet)) {
    throw "Unity shader build failed. See $buildLog"
}
Write-Output 'Built 1.6/AssetBundles/darkmirage_win'
