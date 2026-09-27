param([string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity editor not found: $UnityPath" }
$soulBuildLog = Join-Path $PSScriptRoot 'soul-build.log'
$soulProcess = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-nographics','-noUpm','-quit',
    '-projectPath',('"'+$PSScriptRoot+'"'),'-executeMethod','BuildSoulDrain.Build',
    '-logFile',('"'+$soulBuildLog+'"')) -WindowStyle Hidden -Wait -PassThru
if ($soulProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $soulBuildLog -Pattern 'SOUL_DRAIN_BUILD_OK' -Quiet) -or
    (Select-String -LiteralPath $soulBuildLog -Pattern 'Shader error|error CS[0-9]' -Quiet)) {
    throw "Soul Drain shader build failed. See $soulBuildLog"
}
Write-Output 'Built 1.6/AssetBundles/souldrain_win'
