$headers = @{
    'apikey' = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inp3bGN6c3ZzaXh1aXljZmljampnIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzc5ODUzNjcsImV4cCI6MjA5MzU2MTM2N30.olF3cnXlrR-cCa9i2wUn-_kxkCNX6IAInEjaw0-PN0w'
    'Authorization' = 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inp3bGN6c3ZzaXh1aXljZmljampnIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzc5ODUzNjcsImV4cCI6MjA5MzU2MTM2N30.olF3cnXlrR-cCa9i2wUn-_kxkCNX6IAInEjaw0-PN0w'
    'Accept' = 'application/json'
}
$tables = @('audit_logs', 'logs', 'activity_logs', 'audit_trail', 'system_user_logs', 'system_activity')
foreach ($t in $tables) {
    $url = "https://zwlczsvsixuiycficjjg.supabase.co/rest/v1/" + $t + "?select=*&limit=1"
    try {
        $res = Invoke-WebRequest -Uri $url -Headers $headers -Method Get
        Write-Output "$($t) exists! Status: $($res.StatusCode)"
    } catch {
        if ($_.Exception.Message -notlike "*404*") {
            Write-Output "$($t) error: $_"
        } else {
            Write-Output "$($t) - 404 (Not Found)"
        }
    }
}
