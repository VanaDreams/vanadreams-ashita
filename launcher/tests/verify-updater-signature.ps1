param([Parameter(Mandatory=$true)][string]$SignedExe)

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '..\VanadreamsLauncher\Services\AuthenticodeVerifier.cs'
Add-Type -Path $source -IgnoreWarnings -WarningAction SilentlyContinue
$signed = (Resolve-Path -LiteralPath $SignedExe).Path
$temp = Join-Path ([IO.Path]::GetTempPath()) ('vanadreams-signature-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    if (-not [Vanadreams.Services.AuthenticodeVerifier]::IsSignedByUs($signed)) {
        throw 'The signed release was rejected by the updater.'
    }
    $unsigned = Join-Path $temp 'unsigned.exe'
    [IO.File]::WriteAllText($unsigned, 'unsigned fixture')
    if ([Vanadreams.Services.AuthenticodeVerifier]::IsSignedByUs($unsigned)) {
        throw 'An unsigned file was accepted.'
    }
    $wrong = Join-Path $env:WINDIR 'System32\notepad.exe'
    if ([Vanadreams.Services.AuthenticodeVerifier]::IsSignedByUs($wrong)) {
        throw 'A different trusted publisher was accepted.'
    }
    $tampered = Join-Path $temp 'tampered.exe'
    [IO.File]::Copy($signed, $tampered)
    $bytes = [IO.File]::ReadAllBytes($tampered)
    $bytes[1024] = $bytes[1024] -bxor 1
    [IO.File]::WriteAllBytes($tampered, $bytes)
    if ([Vanadreams.Services.AuthenticodeVerifier]::IsSignedByUs($tampered)) {
        throw 'A tampered signed file was accepted.'
    }
    'Updater signature acceptance: signed publisher accepted; unsigned, wrong publisher and tampered rejected.'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
