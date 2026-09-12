param(
    [Parameter(Mandatory)][string]$ModAssembly,
    [Parameter(Mandatory)][string]$GameAssembly,
    [Parameter(Mandatory)][string]$CecilAssembly
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $CecilAssembly)
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path $GameAssembly))
$mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path $ModAssembly))
try {
    foreach ($name in @('CookingStation', 'Fireplace', 'Smelter', 'ShieldGenerator')) {
        $type = $game.MainModule.Types | Where-Object Name -eq $name
        $rpc = @($type.Methods | Where-Object Name -eq 'RPC_AddFuel')
        if ($rpc.Count -ne 1 -or $rpc[0].Parameters.Count -ne 1 -or
            $rpc[0].Parameters[0].ParameterType.FullName -ne 'System.Int64') {
            throw "$name.RPC_AddFuel no longer has only the implicit sender parameter."
        }
    }
    $checked = 0
    foreach ($type in $mod.MainModule.Types | Where-Object Namespace -eq 'AzuCraftyBoxes.Patches') {
        foreach ($method in $type.Methods | Where-Object HasBody) {
            $instructions = $method.Body.Instructions
            for ($i = 0; $i -lt $instructions.Count; $i++) {
                if ($instructions[$i].OpCode.Name -ne 'ldstr' -or $instructions[$i].Operand -ne 'RPC_AddFuel') { continue }
                # A parameterless params call compiles to Array.Empty<object>() followed by InvokeRPC.
                $array = $instructions[$i + 1].Operand
                $call = $instructions[$i + 2].Operand
                if ($array -isnot [Mono.Cecil.GenericInstanceMethod] -or
                    $array.DeclaringType.FullName -ne 'System.Array' -or $array.Name -ne 'Empty' -or
                    $array.GenericArguments[0].FullName -ne 'System.Object' -or
                    $call -isnot [Mono.Cecil.MethodReference] -or
                    $call.FullName -ne 'System.Void ZNetView::InvokeRPC(System.String,System.Object[])') {
                    throw "Nonempty fuel RPC payload in $($type.FullName).$($method.Name)."
                }
                $checked++
            }
        }
    }
    if ($checked -ne 7) { throw "Expected seven fuel calls; inspected $checked. Review coverage." }
    Write-Output "PASS: all seven fuel calls match the four installed game RPC signatures."
} finally {
    $mod.Dispose()
    $game.Dispose()
}
