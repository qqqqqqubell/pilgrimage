param([Parameter(Mandatory=$true)][string]$PlayerPath,[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$player=[IO.Path]::GetFullPath($PlayerPath)
if (!(Test-Path -LiteralPath $player)) { throw 'Build the Development Windows player first with verify_unity.ps1 -BuildLanPlayer.' }
if (!$OutputDirectory) { $OutputDirectory=Join-Path ([IO.Path]::GetTempPath()) ('PilgrimageLanRuns-'+[Guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Write-Output ('LAN_ARTIFACTS='+$OutputDirectory)
$cases=@(@{humans=2;mode='finish'},@{humans=3;mode='finish'},@{humans=4;mode='finish'},@{humans=2;mode='disconnect'},@{humans=2;mode='close'})
$caseIndex=0
foreach($case in $cases) {
    $port=19777+$caseIndex; $caseIndex++
    $prefix=$case.mode+'-'+$case.humans
    $processes=@();$paths=@()
    try {
        for($peer=0;$peer -lt $case.humans;$peer++) {
            $role=if($peer -eq 0){'host'}else{'client'}
            $resultPath=Join-Path $OutputDirectory ($prefix+'-'+$peer+'.json');$paths+=$resultPath
            $args=@('-batchmode','-nographics','-job-worker-count','2','-pilgrimageLanSmoke',$role,'-pilgrimageLanHumans',[string]$case.humans,'-pilgrimageLanPeer',[string]$peer,'-pilgrimageLanScenario',$case.mode,'-pilgrimageLanPort',[string]$port,'-pilgrimageLanResult',$resultPath,'-logFile',(Join-Path $OutputDirectory ($prefix+'-'+$peer+'.log')))
            $processes+=Start-Process -FilePath $player -ArgumentList @($args|ForEach-Object{'"'+$_+'"'}) -WindowStyle Hidden -PassThru
            Start-Sleep -Milliseconds 750
        }
        $deadline=[DateTime]::UtcNow.AddSeconds(260)
        while(@($processes|Where-Object{!$_.HasExited}).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
        $results=@($paths|ForEach-Object {if(!(Test-Path -LiteralPath $_)){throw ('Missing result: '+$_)};Get-Content -LiteralPath $_ -Raw|ConvertFrom-Json})
        foreach($r in $results){
            if(!$r.assignedUnique -or !$r.debugBlocked -or !$r.privateViews -or !$r.peopleConserved -or !$r.identitiesConserved) {throw ('Privacy/assignment/debug/conservation assertion failed: '+($r|ConvertTo-Json -Compress))}
            if($case.mode -eq 'finish' -and ($r.outcome -ne 'finished' -or $r.round -ne 40 -or $r.reveals -lt 1)){throw ('40-round session failed: '+($r|ConvertTo-Json -Compress))}
        }
        if(@($results|ForEach-Object{$_.seed}|Select-Object -Unique).Count -ne 1){throw 'Assignment seeds differ between clients.'}
        if(@($results|ForEach-Object{($_.assignment -join ',')}|Select-Object -Unique).Count -ne 1){throw 'Assignment maps differ between clients.'}
        if($case.mode -eq 'finish' -and @($results|ForEach-Object{$_.publicState}|Select-Object -Unique).Count -ne 1){throw 'Final public states differ between clients.'}
        if($case.mode -eq 'disconnect' -and ($results[0].outcome -ne 'finished' -or !$results[0].aiTakeover -or $results[1].outcome -ne 'left')){throw 'Disconnected human was not taken over by AI.'}
        if($case.mode -eq 'close' -and ($results[1].outcome -ne 'closed' -or !$results[1].menuReturned)){throw 'Host closure did not return client to menu.'}
        Write-Output ('PASS '+$prefix+' : '+(($results|ForEach-Object{$_.role+'='+$_.outcome+' R'+$_.round}) -join ' / '))
    } finally {
        foreach($process in $processes){if(!$process.HasExited){Stop-Process -Id $process.Id -Force}}
    }
}
Write-Output 'Different-PC physical LAN test is not covered by this same-PC loopback suite.'
