try {
    $a = [Reflection.Assembly]::LoadFrom('C:\Users\lebao\.nuget\packages\bunit.web\1.28.9\lib\net8.0\Bunit.Web.dll')
    $types = $a.GetTypes()
} catch [Reflection.ReflectionTypeLoadException] {
    $types = $_.Exception.Types
}
$types | Where-Object { $_ -ne $null } | ForEach-Object { $_.FullName } | Select-String -Pattern 'File|Browser|Input'
