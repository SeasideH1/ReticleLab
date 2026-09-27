#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'install-dev.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Installer parse error' }
$definition = $ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Confirm-ComponentTerms'}, $true)
# Execute only the actual prompt function: never certificate/package operations.
Invoke-Expression $definition.Extent.Text
function Read-Host { param($Prompt) if (!$script:answers.Count) { throw 'Unexpected extra prompt' }; return $script:answers.Dequeue() }
foreach ($case in @(
    @{Input=@('INSTALL'); Expected=$true},
    @{Input=@(' install '); Expected=$true},
    @{Input=@('', 'bad', 'INSTALL'); Expected=$true},
    @{Input=@('Q'); Expected=$false},
    @{Input=@(' cancel '); Expected=$false},
    @{Input=@('', '', ''); Expected=$false},
    @{Input=@($null); Expected=$false}
)) {
    $script:answers = New-Object 'Collections.Generic.Queue[object]'
    foreach ($item in $case.Input) { $script:answers.Enqueue($item) }
    $actual = Confirm-ComponentTerms
    if ($actual -ne $case.Expected) { throw 'Unexpected confirmation result' }
}
Write-Host 'PASS: 7 installer prompt cases. No trust or application changes.'
