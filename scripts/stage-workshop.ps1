<#
.SYNOPSIS
  Steam Workshop に上げるフォルダを組む（mod ごとに 1 つ）。

.DESCRIPTION
  mod ごとに Release でビルドし、セーブデータの Mods\Upload\<名前>\ に次を置く:
    modinfo.json
    BepInEx\plugins\<名前>\<名前>.dll（と .pdb。Mods Menu は .xml も）
    <mod>\workshop\ の中身（_description.bbcode・_changenote.txt・Thumbnail.png）
  ゲームの Workshop Creator（Shift+F11）でこのフォルダを選んで上げる（手順は docs/modding.md の 5）。
  `_` で始まるファイルと Thumbnail.* は Workshop の中身には入らない（説明・変更点・プレビュー画像として使われる）。

  Mods\Code\ には置かない。手元で試している mod と、購読した Workshop の item が同じ名前で 2 つ読まれないように。
  前に組んだフォルダは、組み直す前に中身ごと消す（古い DLL を残さない）。消すのは Mods\Upload\<名前>\ だけ。

.PARAMETER Mod
  組む mod（既定は 5 つ全部）。

.PARAMETER ModsRoot
  セーブデータの Mods フォルダ。MSIX でパッケージされたアプリの中から動かすと AppData\LocalLow が仮想化されるので、
  そのときは \\localhost\C$\Users\<ユーザー>\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods を渡す。

.PARAMETER GameDir
  ゲームの導入先。渡さなければリポジトリの根の Game.local.props（か環境変数 GK2_GAMEDIR）を使う。

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\stage-workshop.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\stage-workshop.ps1 -Mod CraftTodo
#>
param(
    [string[]] $Mod = @('ModsMenu', 'CraftTodo', 'StockFinder', 'QuickStash', 'TradeFinder'),
    [string] $ModsRoot = (Join-Path $env:USERPROFILE 'AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods'),
    [string] $GameDir = ''
)

try { [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false) } catch { }
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$known = @('ModsMenu', 'CraftTodo', 'StockFinder', 'QuickStash', 'TradeFinder')

if (-not (Test-Path -LiteralPath (Join-Path $ModsRoot 'Code'))) {
    throw "セーブデータの Mods フォルダが見つからない（$ModsRoot に Code が無い）。ゲームを 1 度起動するか、-ModsRoot を渡す"
}
$uploadRoot = Join-Path $ModsRoot 'Upload'
if (-not (Test-Path -LiteralPath $uploadRoot)) {
    New-Item -ItemType Directory -Path $uploadRoot | Out-Null
}

$results = @()
foreach ($name in $Mod) {
    if ($known -notcontains $name) {
        throw "知らない mod: $name（$($known -join ' / ') のどれか）"
    }
    $project = Join-Path $repo "$name\$name\$name.csproj"
    $workshop = Join-Path $repo "$name\workshop"
    $item = Join-Path $uploadRoot $name

    if (-not (Test-Path -LiteralPath (Join-Path $workshop '_description.bbcode'))) {
        throw "$name の説明が無い: $workshop\_description.bbcode"
    }

    if (Test-Path -LiteralPath $item) {
        Remove-Item -LiteralPath $item -Recurse -Force
    }

    Write-Host "== $name をビルドして $item に置く"
    $buildArgs = @('build', $project, '-c', 'Release', '-nologo', '-v', 'q', '-p:DeployToGame=true', "-p:ModsCodePath=$uploadRoot")
    if ($GameDir -ne '') {
        $buildArgs += "-p:GameDir=$GameDir"
    }
    $ErrorActionPreference = 'Continue'
    & dotnet @buildArgs
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($exitCode -ne 0) {
        throw "$name のビルドが失敗した（終了コード $exitCode）"
    }

    Copy-Item -Path (Join-Path $workshop '*') -Destination $item -Recurse -Force

    $dll = Join-Path $item "BepInEx\plugins\$name\$name.dll"
    $modinfo = Join-Path $item 'modinfo.json'
    foreach ($need in @($dll, $modinfo, (Join-Path $item '_description.bbcode'))) {
        if (-not (Test-Path -LiteralPath $need)) {
            throw "$name の item に $need が無い"
        }
    }
    $version = (Get-Content -LiteralPath $modinfo -Raw -Encoding UTF8 | ConvertFrom-Json).version
    $thumbs = @(Get-ChildItem -LiteralPath $item -File | Where-Object { $_.Name -like 'Thumbnail.*' })
    $hasChangeNote = Test-Path -LiteralPath (Join-Path $item '_changenote.txt')
    $content = @(Get-ChildItem -LiteralPath $item -Recurse -File | Where-Object { $_.Name -notlike '_*' -and $_.Name -notlike 'Thumbnail.*' })
    if ($content.Count -eq 0) {
        throw "$name の item に上げる中身が 1 つも無い"
    }
    $results += [pscustomobject]@{
        Mod        = $name
        Version    = $version
        Files      = $content.Count
        Thumbnail  = if ($thumbs.Count -gt 0) { $thumbs[0].Name } else { '(無い: ゲームが黒い画像を作る)' }
        ChangeNote = if ($hasChangeNote) { 'あり' } else { '(無い: Initial upload / Updated になる)' }
        Folder     = $item
    }
}

Write-Host ''
Write-Host "組んだ item: $(@($results).Count) 件"
$results | Format-Table -AutoSize | Out-String -Width 400 | Write-Host
