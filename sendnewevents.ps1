PARAM(
    [switch]$noDelay = $false
)

$groupList = @( "info", "warning", "error");

<#
$groupPos = 0;
1..50 | foreach{
    $body_json = @"
{
    "userName": "user$_",
    "groupList": [
        "$($groupList[$groupPos])"
    ]
}
"@
    #echo "SET registration: `"$($body_json -replace '\r*\n', '')`""
    Invoke-RestMethod -Uri http://localhost:4300/registration -Method POST -Body $body_json -ContentType "application/json"  | out-null
    if ($groupPos -eq 2)
    {
        $groupPos = 0;
    }
    else
    {
        $groupPos++;
    }
}

Read-Host "Users registered. Press any key to send new events..."
#>

0..100000000 | foreach{

    $body_json = @"
    {
        "Type": "$($groupList[(Get-Random -Minimum 0 -Maximum 3)])", 
        "Name": "Event $_", 
        "Description": "Description for Event $_" 
    }
"@
    
    Invoke-RestMethod -Uri http://localhost:4300/event -Method POST -Body $body_json -ContentType "application/json" | out-null
    if (!$noDelay)
    {
        Start-Sleep -Seconds 3
    }
}
