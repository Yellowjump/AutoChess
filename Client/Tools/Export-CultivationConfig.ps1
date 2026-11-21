param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sourcePath = Join-Path $repositoryRoot 'Config/CultivationLevel.xlsx'
$csvPath = Join-Path $repositoryRoot 'Client/Assets/GameMain/Scripts/WorldTimelineDebug/Resources/WorldTimeline/CultivationLevels.csv'
$txtPath = Join-Path $repositoryRoot 'Client/Assets/GameMain/Data/DataTables/CultivationLevel.txt'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($sourcePath)
try {
    function Read-ZipText([string]$name) {
        $entry = $archive.GetEntry($name)
        if ($null -eq $entry) { return $null }
        $reader = New-Object IO.StreamReader($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    $strings = [Collections.Generic.List[string]]::new()
    $sharedText = Read-ZipText 'xl/sharedStrings.xml'
    if ($sharedText) {
        [xml]$sharedXml = $sharedText
        foreach ($item in $sharedXml.SelectNodes("//*[local-name()='si']")) {
            $strings.Add((($item.SelectNodes(".//*[local-name()='t']") | ForEach-Object { $_.InnerText }) -join ''))
        }
    }
    [xml]$sheet = Read-ZipText 'xl/worksheets/sheet1.xml'
    $csvLines = [Collections.Generic.List[string]]::new()
    $txtLines = [Collections.Generic.List[string]]::new()
    $expected = 'Level,ExperienceToNext,Attribute,SMin,SMax,AMin,AMax,BMin,BMax,CMin,CMax'
    $ids = [Collections.Generic.HashSet[int]]::new()
    foreach ($row in $sheet.SelectNodes("//*[local-name()='sheetData']/*[local-name()='row']")) {
        $values = New-Object string[] 13
        foreach ($cell in $row.SelectNodes("./*[local-name()='c']")) {
            $letters = $cell.GetAttribute('r') -replace '\d',''
            $column = 0
            foreach ($letter in $letters.ToCharArray()) { $column = $column * 26 + ([int]$letter - 64) }
            if ($column -gt 13) { throw 'Growth table must use columns A through M only.' }
            $value = $cell.SelectSingleNode("./*[local-name()='v']")
            $text = if ($value) { $value.InnerText } else { '' }
            switch ($cell.GetAttribute('t')) {
                's' { $text = $strings[[int]$text] }
                'inlineStr' { $text = (($cell.SelectNodes(".//*[local-name()='t']") | ForEach-Object { $_.InnerText }) -join '') }
            }
            if ($cell.SelectSingleNode("./*[local-name()='f']")) { throw 'Growth configuration requires literal values, not formulas.' }
            if ($text -match "[`t`r`n,]") { throw "Unsupported separator in growth table cell $($cell.GetAttribute('r'))." }
            $values[$column - 1] = $text
        }
        if ([int]$row.GetAttribute('r') -eq 2) {
            if (($values[2..12] -join ',') -ne $expected) { throw 'Growth table field header does not match the expected format.' }
            $csvLines.Add($expected)
        }
        if ([int]$row.GetAttribute('r') -ge 5 -and $values[1]) {
            if (-not $ids.Add([int]$values[1])) { throw 'Duplicate growth record ID.' }
            foreach ($column in @(1,2,3,5,6,7,8,9,10,11,12)) { $null = [long]::Parse($values[$column], [Globalization.CultureInfo]::InvariantCulture) }
            if ($values[4] -notin @('Health','Attack','Lifespan')) { throw 'Unknown growth attribute.' }
            $csvLines.Add(($values[2..12] -join ','))
        }
        $txtLines.Add(($values -join "`t"))
    }
    if ($csvLines.Count -lt 4) { throw 'Growth table has no complete level.' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($txtPath)) | Out-Null
    $encoding = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllLines($csvPath, $csvLines, $encoding)
    [IO.File]::WriteAllLines($txtPath, $txtLines, $encoding)
    Write-Output "Exported $($ids.Count) growth rows from Config/CultivationLevel.xlsx."
} finally { $archive.Dispose() }
