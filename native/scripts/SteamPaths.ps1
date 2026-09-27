function Find-Cs2ConfigDirectory([string]$Cs2Directory) {
    if ($Cs2Directory) {
        $cfg = Join-Path $Cs2Directory 'game/csgo/cfg'
        if (!(Test-Path -LiteralPath $cfg -PathType Container)) { throw 'The selected CS2 directory must contain game/csgo/cfg.' }
        return (Resolve-Path -LiteralPath $cfg).Path
    }
    $libraries = [Collections.Generic.List[string]]::new()
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $item = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        foreach ($candidate in @($item.SteamPath,$item.InstallPath)) {
            if ($candidate -and !$libraries.Contains($candidate)) { $libraries.Add($candidate) }
        }
    }
    foreach ($steam in @($libraries.ToArray())) {
        $vdf = Join-Path $steam 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($entry in [regex]::Matches([IO.File]::ReadAllText($vdf),'"path"\s+"([^"]+)"')) {
                $library = $entry.Groups[1].Value.Replace('\\','\')
                if (!$libraries.Contains($library)) { $libraries.Add($library) }
            }
        }
    }
    foreach ($library in $libraries) {
        $cfg = Join-Path $library 'steamapps/common/Counter-Strike Global Offensive/game/csgo/cfg'
        if (Test-Path -LiteralPath $cfg -PathType Container) { return (Resolve-Path -LiteralPath $cfg).Path }
    }
    throw 'CS2 was not found. Run Setup-GSI.cmd -Cs2Directory "<CS2 installation directory>".'
}
