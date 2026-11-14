param([string]$EditorData = 'E:\Unity\Editor\6000.6.9f1\Editor\Data')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
# Validation artifacts go to the OS temporary directory, never project Library/Temp/Logs.
$validationDir = Join-Path ([System.IO.Path]::GetTempPath()) ('PilgrimageV2Validation-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $validationDir | Out-Null
$mono = Join-Path $EditorData 'MonoBleedingEdge\bin\mono.exe'
$compiler = Join-Path $EditorData 'MonoBleedingEdge\lib\mono\4.5\csc.exe'
$api = Join-Path $EditorData 'MonoBleedingEdge\lib\mono\4.8-api'
$baseRefs = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'Facades\netstandard.dll') | ForEach-Object { '-r:' + (Join-Path $api $_) }
$engineRefs = @(Get-ChildItem (Join-Path $EditorData 'Managed\UnityEngine') -Filter '*.dll' | ForEach-Object { '-r:' + $_.FullName })
$runtimeSources = @(Get-ChildItem 'Assets\Scripts\Pilgrimage' -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(UI|Editor)\\' } | ForEach-Object { $_.FullName })
$uiSources = @(Get-ChildItem 'Assets\Scripts\Pilgrimage\UI' -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName })
$editorSources = @(Get-ChildItem 'Assets\Scripts\Pilgrimage\Editor' -Filter '*.cs' | ForEach-Object { $_.FullName })
$testSources = @(Get-ChildItem 'Assets\Tests\EditMode' -Filter '*.cs' | ForEach-Object { $_.FullName })
$uiRefs = @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll') | ForEach-Object { '-r:' + (Join-Path $projectRoot ('Library\ScriptAssemblies\' + $_)) }
$runtimeDll = Join-Path $validationDir 'Pilgrimage.Runtime.dll'
$uiDll = Join-Path $validationDir 'Pilgrimage.UI.dll'
& $mono $compiler -nologo -noconfig -nostdlib+ -target:library ('-out:' + $runtimeDll) $baseRefs $engineRefs $runtimeSources
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed' }
& $mono $compiler -nologo -noconfig -nostdlib+ -target:library ('-out:' + $uiDll) $baseRefs $engineRefs $uiRefs ('-r:' + $runtimeDll) $uiSources
if ($LASTEXITCODE -ne 0) { throw 'UI compilation failed' }
& $mono $compiler -nologo -noconfig -nostdlib+ -target:library ('-out:' + (Join-Path $validationDir 'Pilgrimage.Editor.dll')) $baseRefs $engineRefs $uiRefs ('-r:' + $runtimeDll) ('-r:' + $uiDll) $editorSources
if ($LASTEXITCODE -ne 0) { throw 'Editor compilation failed' }
$nunit = @(Get-ChildItem 'Library\PackageCache\com.unity.ext.nunit*\net472\unity-custom\nunit.framework.dll')[0].FullName
Copy-Item -LiteralPath $nunit -Destination (Join-Path $validationDir 'nunit.framework.dll')
& $mono $compiler -nologo -noconfig -nostdlib+ -target:library ('-out:' + (Join-Path $validationDir 'Pilgrimage.Tests.dll')) $baseRefs $engineRefs ('-r:' + $runtimeDll) ('-r:' + $nunit) $testSources
if ($LASTEXITCODE -ne 0) { throw 'EditMode test compilation failed' }
$playModeSources = @(Get-ChildItem 'Assets\Tests\PlayMode' -Filter '*.cs' | ForEach-Object { $_.FullName })
$runnerRef = '-r:' + (Join-Path $projectRoot 'Library\ScriptAssemblies\UnityEngine.TestRunner.dll')
& $mono $compiler -nologo -noconfig -nostdlib+ -define:UNITY_EDITOR -target:library ('-out:' + (Join-Path $validationDir 'Pilgrimage.PlayModeTests.dll')) $baseRefs $engineRefs $uiRefs $runnerRef ('-r:' + $runtimeDll) ('-r:' + $uiDll) ('-r:' + $nunit) $playModeSources
if ($LASTEXITCODE -ne 0) { throw 'PlayMode test compilation failed' }
$runner = @'
using System;
using System.Reflection;
using NUnit.Framework;
public static class ValidationRunner {
  public static int Main() {
    int passed = 0, failed = 0;
    foreach (Type fixture in new[] { typeof(Pilgrimage.Tests.CardPrototypeTests), typeof(Pilgrimage.Tests.GameSessionTests) }) {
      object instance = Activator.CreateInstance(fixture);
      foreach (MethodInfo test in fixture.GetMethods()) {
        if (!Attribute.IsDefined(test, typeof(TestAttribute))) continue;
        try { test.Invoke(instance, null); Console.WriteLine("PASS " + test.Name); passed++; }
        catch (Exception e) { Console.WriteLine("FAIL " + test.Name + ": " + (e.InnerException ?? e)); failed++; }
      }
    }
    Console.WriteLine($"{passed} passed, {failed} failed");
    return failed == 0 && passed > 0 ? 0 : 1;
  }
}
'@
$runnerPath = Join-Path $validationDir 'RunTests.cs'
[System.IO.File]::WriteAllText($runnerPath, $runner)
$modelSources = @($runtimeSources | Where-Object { [System.IO.File]::ReadAllText($_) -notmatch 'using UnityEngine;' })
& $mono $compiler -nologo -target:exe ('-out:' + (Join-Path $validationDir 'ModelTests.exe')) ('-r:' + $nunit) $modelSources $testSources $runnerPath
if ($LASTEXITCODE -ne 0) { throw 'Model test runner compilation failed' }
& $mono (Join-Path $validationDir 'ModelTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Model tests failed' }
Write-Output 'Runtime / uGUI-TMP-InputSystem UI / Editor / EditMode + PlayMode tests: compilation passed.'
Write-Output ('Validation artifacts: ' + $validationDir)
