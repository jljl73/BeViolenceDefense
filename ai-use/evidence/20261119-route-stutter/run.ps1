param([switch]$ExpectOriginal)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$buildDir = Join-Path $repo 'BalanceSim/obj/route-stutter-diagnosis'
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
$sources = @(
    (Join-Path $PSScriptRoot 'Diagnosis.cs'),
    (Join-Path $repo 'BalanceSim/UnityStubs.cs'),
    (Join-Path $repo 'ProjectBeViolenceDefense/Assets/02_Scripts/03_Enemies/EnemyUnit.cs'),
    (Join-Path $repo 'ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/CombatPositioning.cs'),
    (Join-Path $repo 'ProjectBeViolenceDefense/Assets/02_Scripts/04_Combat/DefenseBattle.cs'),
    (Join-Path $repo 'ProjectBeViolenceDefense/Assets/02_Scripts/05_World/DefenseGrid.cs')
)
$dotnetRoot = Split-Path (Get-Command dotnet).Source
$sdk = (Get-ChildItem (Join-Path $dotnetRoot 'sdk') -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$refRoot = (Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
$refs = (Get-ChildItem (Join-Path $refRoot 'ref') -Directory | Select-Object -First 1).FullName
$runtime = (Get-ChildItem (Join-Path $dotnetRoot 'shared/Microsoft.NETCore.App') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).Name
$output = Join-Path $buildDir 'Diagnosis.dll'
$compilerArgs = @('/nologo', '/noconfig', '/nostdlib+', '/target:exe', '/langversion:latest', '/optimize+', ('/out:' + $output))
$compilerArgs += Get-ChildItem $refs -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$compilerArgs += $sources
dotnet (Join-Path $sdk 'Roslyn/bincore/csc.dll') @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
@{ runtimeOptions = @{ tfm = (Split-Path $refs -Leaf); framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime } } } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $buildDir 'Diagnosis.runtimeconfig.json')
$runArgs = @($output)
if ($ExpectOriginal) { $runArgs += '--expect-original' }
dotnet @runArgs
if ($LASTEXITCODE -ne 0) { throw "Diagnosis failed: $LASTEXITCODE" }
