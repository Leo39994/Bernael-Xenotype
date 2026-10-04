param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity editor not found: $UnityPath" }
$darkMagicLog = Join-Path $PSScriptRoot 'dark-magic-build.log'
$darkMagicProcess = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-nographics','-noUpm','-quit',
    '-projectPath',('"'+$PSScriptRoot+'"'),'-executeMethod','BuildDarkMagic.Build',
    '-logFile',('"'+$darkMagicLog+'"')) -WindowStyle Hidden -Wait -PassThru
if ($darkMagicProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $darkMagicLog -Pattern 'DARK_MAGIC_BUILD_OK' -Quiet) -or
    (Select-String -LiteralPath $darkMagicLog -Pattern 'Shader error|error CS[0-9]' -Quiet)) {
    throw "Dark magic shader build failed. See $darkMagicLog"
}
Write-Output 'Built 1.6/AssetBundles/darkmagic_win'
