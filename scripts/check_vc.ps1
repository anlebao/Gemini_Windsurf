Add-Type -Path 'C:\Users\lebao\.nuget\packages\microsoft.entityframeworkcore\8.0.8\lib\net8.0\Microsoft.EntityFrameworkCore.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Users\lebao\.nuget\packages\microsoft.entityframeworkcore\8.0.8\lib\net8.0\Microsoft.EntityFrameworkCore.dll')
$types = $asm.GetTypes() | Where-Object { $_.Name -eq 'ValueConverter`2' }
$types | ForEach-Object {
    Write-Host "Type: $($_.FullName)"
    $_.GetMethods([System.Reflection.BindingFlags]'Public,NonPublic,Instance,DeclaredOnly') | ForEach-Object {
        Write-Host "  $($_.Name)  IsVirtual=$($_.IsVirtual) IsPublic=$($_.IsPublic)"
    }
}
