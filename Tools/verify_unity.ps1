param(
    [string]$EditorPath = 'E:\Unity\Editor\6000.6.9f1\Editor\Unity.exe',
    [ValidateSet('PlayMode','EditMode')][string]$Platform = 'PlayMode',
    [switch]$Render
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationRoot = Join-Path ([IO.Path]::GetTempPath()) ('PilgrimageAiUnity-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $validationRoot 'Assets/Scenes'),(Join-Path $validationRoot 'Packages'),(Join-Path $validationRoot 'ProjectSettings') | Out-Null
foreach ($name in @('Scripts','Tests','TextMesh Pro')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('Assets/' + $name)) -Destination (Join-Path $validationRoot 'Assets') -Recurse
    Copy-Item -LiteralPath (Join-Path $projectRoot ('Assets/' + $name + '.meta')) -Destination (Join-Path $validationRoot 'Assets')
}
foreach ($name in @('PilgrimageDemo.unity','PilgrimageDemo.unity.meta')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('Assets/Scenes/' + $name)) -Destination (Join-Path $validationRoot 'Assets/Scenes')
}
foreach ($name in @('ProjectVersion.txt','ProjectSettings.asset')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('ProjectSettings/' + $name)) -Destination (Join-Path $validationRoot 'ProjectSettings')
}
$dependencies = [ordered]@{}
foreach ($packageName in @('com.unity.ugui','com.unity.inputsystem','com.unity.test-framework','com.unity.ext.nunit')) {
    $packageDir = @(Get-ChildItem (Join-Path $projectRoot 'Library/PackageCache') -Directory | Where-Object { $_.Name.StartsWith($packageName + '@') })[0]
    $dependencies[$packageName] = 'file:' + $packageDir.FullName.Replace('\','/')
}
foreach ($module in @('ui','imgui','audio','physics2d','physics','jsonserialize','uielements','imageconversion','screencapture')) {
    $dependencies['com.unity.modules.' + $module] = '1.0.0'
}
[IO.File]::WriteAllText((Join-Path $validationRoot 'Packages/manifest.json'), (@{ dependencies=$dependencies } | ConvertTo-Json -Depth 5))
$arguments = @('-batchmode','-projectPath',$validationRoot,'-runTests','-testPlatform',$Platform,'-testFilter','Pilgrimage.Tests','-testResults',(Join-Path $validationRoot 'results.xml'),'-logFile',(Join-Path $validationRoot 'unity.log'))
if ($Render) { $arguments += @('-screen-width','1920','-screen-height','1080','-pilgrimageScreenshot',(Join-Path $validationRoot 'demo.png'),'-pilgrimageLabScreenshot',(Join-Path $validationRoot 'card-lab.png')) }
else { $arguments += '-nographics' }
Write-Output ('ISOLATED_PROJECT=' + $validationRoot)
# Hidden batch process; original project is never opened or refreshed by this tool.
$quotedArguments = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
$process = Start-Process -FilePath $EditorPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
$resultPath = Join-Path $validationRoot 'results.xml'
if (-not (Test-Path -LiteralPath $resultPath)) {
    Get-Content -LiteralPath (Join-Path $validationRoot 'unity.log') -Tail 35
    throw 'Unity did not produce test results. See isolated unity.log.'
}
[xml]$result = Get-Content -LiteralPath $resultPath -Raw
$result.'test-run' | Select-Object result,total,passed,failed
$result.SelectNodes('//test-case[failure]') | ForEach-Object { Write-Output $_.name; Write-Output $_.failure.InnerText }
if ($result.'test-run'.failed -ne '0') { throw 'Unity tests failed.' }
Write-Output ('Validated artifacts: ' + $validationRoot)
