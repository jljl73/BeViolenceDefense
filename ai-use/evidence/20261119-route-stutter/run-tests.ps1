param([string[]]$Fixtures)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$buildDir = Join-Path $repo 'BalanceSim/obj/route-stutter-tests'
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
$stubPath = Join-Path $buildDir 'UnityStubs.cs'
$stub = [IO.File]::ReadAllText((Join-Path $repo 'BalanceSim/UnityStubs.cs'))
$stub.Replace('public struct Color {', 'public struct Color { public static Color white => new(1, 1, 1);') | Set-Content $stubPath
$sources = @((Join-Path $PSScriptRoot 'RegressionRunner.cs'), (Join-Path $PSScriptRoot 'Diagnosis.cs'), $stubPath)
$scripts = Join-Path $repo 'ProjectBeViolenceDefense/Assets/02_Scripts'
$sources += @('03_Enemies/EnemyUnit.cs', '00_Core/CombatPositioning.cs', '00_Core/BattleClock.cs', '04_Combat/DefenseBattle.cs', '05_World/DefenseGrid.cs') | ForEach-Object { Join-Path $scripts $_ }
$testRoot = Join-Path $repo 'ProjectBeViolenceDefense/Assets/09_Tests/Editor'
$sources += @('CombatPositioningTests', 'RerouteAfterCombatTests', 'SimulationDeterminismTests', 'MapScaleTests', 'ShieldTests', 'TauntTests') | ForEach-Object { Join-Path $testRoot ($_ + '.cs') }
$nunit = (Get-ChildItem (Join-Path $repo 'ProjectBeViolenceDefense/Library/PackageCache') -Recurse -Filter nunit.framework.dll | Select-Object -First 1).FullName
Copy-Item -LiteralPath $nunit -Destination $buildDir -Force
$dotnetRoot = Split-Path (Get-Command dotnet).Source
$sdk = (Get-ChildItem (Join-Path $dotnetRoot 'sdk') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).FullName
$refRoot = (Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).FullName
$refs = (Get-ChildItem (Join-Path $refRoot 'ref') -Directory | Select-Object -First 1).FullName
$runtime = (Get-ChildItem (Join-Path $dotnetRoot 'shared/Microsoft.NETCore.App') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).Name
$output = Join-Path $buildDir 'RegressionRunner.dll'
$compilerArgs = @('/nologo', '/noconfig', '/nostdlib+', '/target:exe', '/langversion:latest', '/optimize+', '/main:RegressionRunner', ('/out:' + $output), ('/reference:' + $nunit))
$compilerArgs += Get-ChildItem $refs -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$compilerArgs += $sources
dotnet (Join-Path $sdk 'Roslyn/bincore/csc.dll') @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
@{ runtimeOptions = @{ tfm = (Split-Path $refs -Leaf); framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime } } } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $buildDir 'RegressionRunner.runtimeconfig.json')
dotnet $output @Fixtures
if ($LASTEXITCODE -ne 0) { throw "Regression checks failed: $LASTEXITCODE" }
