# Maps LogLevel enum name → ILogger extension method name
$levelMap = @{
    Information = 'LogInformation'; Warning = 'LogWarning'; Error = 'LogError'
    Debug = 'LogDebug'; Trace = 'LogTrace'; Critical = 'LogCritical'
}

function Get-Indent($line) {
    $m = [regex]::Match($line, '^(\s*)')
    return $m.Groups[1].Value
}

function Transform-File($path) {
    $original = Get-Content $path -Raw
    $lines = $original -split "`r?`n"
    $out = [System.Collections.Generic.List[string]]::new()
    $i = 0
    while ($i -lt $lines.Count) {
        $line = $lines[$i]
        # ── remove 'partial' from class declarations ──────────────────────
        $line = $line -replace '\bsealed partial class\b','sealed class'
        $line = $line -replace '\bpublic partial class\b','public class'
        $line = $line -replace '\binternal partial class\b','internal class'
        $line = $line -replace '\bprivate static partial class\b','private static class'
        # ── detect start of [LoggerMessage] block ────────────────────────
        if ($line -match '^\s*\[LoggerMessage') {
            # Collect full attribute (may span multiple lines until ']')
            $attrLines = @($line)
            while ($attrLines[-1] -notmatch '\]\s*$' -and $i + 1 -lt $lines.Count) {
                $i++
                $attrLines += $lines[$i]
            }
            $attrFull = $attrLines -join ' '
            # Extract log level
            $lvl = 'Information'
            if ($attrFull -match 'Level\s*=\s*LogLevel\.(\w+)') { $lvl = $Matches[1] }
            elseif ($attrFull -match 'LogLevel\.(\w+)') { $lvl = $Matches[1] }
            # Extract message
            $msg = ''
            if ($attrFull -match 'Message\s*=\s*"([^"]*)"') { $msg = $Matches[1] }
            elseif ($attrFull -match 'LogLevel\.\w+,\s*"([^"]*)"') { $msg = $Matches[1] }
            elseif ($attrFull -match '"([^"]*)"') { $msg = $Matches[1] }
            # Move to the method declaration line
            $i++
            if ($i -ge $lines.Count) { $out.Add($line); break }
            $methodLine = $lines[$i]
            $indent = Get-Indent $methodLine
            $logMethod = if ($levelMap.ContainsKey($lvl)) { $levelMap[$lvl] } else { 'LogInformation' }
            # ── static partial void ──────────────────────────────────────
            if ($methodLine -match 'private\s+static\s+partial\s+void\s+(\w+)\(([^)]*)\)') {
                $mName  = $Matches[1]
                $params = $Matches[2]
                # Strip 'ILogger logger' (first param) to get log args
                $pList = $params -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
                $argNames = @()
                $hasEx = $false
                $exName = $null
                foreach ($p in $pList) {
                    $parts = $p -split '\s+'
                    $n = $parts[-1]
                    if ($n -eq 'logger') { continue }
                    if ($p -match '\bException\b') { $hasEx = $true; $exName = $n }
                    else { $argNames += $n }
                }
                $argsStr = if ($argNames.Count -gt 0) { ', ' + ($argNames -join ', ') } else { '' }
                if ($hasEx) {
                    $body = "$logMethod($exName, `"$msg`"$argsStr)"
                } else {
                    $body = "$logMethod(`"$msg`"$argsStr)"
                }
                $out.Add("${indent}private static void ${mName}($params) => logger.${body};")
            }
            # ── instance partial void ────────────────────────────────────
            elseif ($methodLine -match 'private\s+partial\s+void\s+(\w+)\(([^)]*)\)') {
                $mName  = $Matches[1]
                $params = $Matches[2]
                $pList = $params -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
                $argNames = @()
                $hasEx = $false
                $exName = $null
                foreach ($p in $pList) {
                    $parts = $p -split '\s+'
                    $n = $parts[-1]
                    if ($p -match '\bException\b') { $hasEx = $true; $exName = $n }
                    else { $argNames += $n }
                }
                $argsStr = if ($argNames.Count -gt 0) { ', ' + ($argNames -join ', ') } else { '' }
                if ($hasEx) {
                    $body = "$logMethod($exName, `"$msg`"$argsStr)"
                } else {
                    $body = "$logMethod(`"$msg`"$argsStr)"
                }
                # Determine logger field name from file (default _logger)
                $fileContent = $original
                $loggerField = '_logger'
                if ($fileContent -match '(ILogger<[^>]+>)\s+(\w+)\s*[;,)]') {
                    $candidate = $Matches[2]
                    if ($candidate -match '^_?[a-z]') { $loggerField = $candidate }
                }
                $out.Add("${indent}private void ${mName}($params) => ${loggerField}.${body};")
            }
            # ── nested class static partial void (internal/public) ───────
            elseif ($methodLine -match '(internal|public)\s+static\s+partial\s+void\s+(\w+)\(([^)]*)\)') {
                $vis    = $Matches[1]
                $mName  = $Matches[2]
                $params = $Matches[3]
                $pList = $params -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
                $argNames = @()
                $hasEx = $false
                $exName = $null
                foreach ($p in $pList) {
                    $parts = $p -split '\s+'
                    $n = $parts[-1]
                    if ($n -eq 'logger') { continue }
                    if ($p -match '\bException\b') { $hasEx = $true; $exName = $n }
                    else { $argNames += $n }
                }
                $argsStr = if ($argNames.Count -gt 0) { ', ' + ($argNames -join ', ') } else { '' }
                if ($hasEx) {
                    $body = "$logMethod($exName, `"$msg`"$argsStr)"
                } else {
                    $body = "$logMethod(`"$msg`"$argsStr)"
                }
                $out.Add("${indent}${vis} static void ${mName}($params) => logger.${body};")
            }
            else {
                # Fallback: keep original attribute and method line unchanged
                foreach ($al in $attrLines) { $out.Add($al) }
                $out.Add($methodLine)
            }
            $i++
            continue
        }
        $out.Add($line)
        $i++
    }
    $newContent = $out -join "`r`n"
    if ($newContent -ne $original) {
        Set-Content -Path $path -Value $newContent -NoNewline
        Write-Host "  FIXED: $($path -replace '.*\\src\\','')"
    }
}

$files = Get-ChildItem -Recurse -Filter "*.cs" -Path "D:\GitHub\Sam.FactoryERP\src" |
    Where-Object { (Get-Content $_.FullName -Raw) -match '\[LoggerMessage' }
Write-Host "Processing $($files.Count) files..."
foreach ($f in $files) { Transform-File $f.FullName }
Write-Host "Done."
