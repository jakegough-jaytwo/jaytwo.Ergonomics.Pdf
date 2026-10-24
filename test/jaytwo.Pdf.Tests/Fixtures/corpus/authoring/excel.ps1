# Builds a two-sheet invoice workbook with a chart in Microsoft Excel through COM and exports it to PDF.
# Usage: excel.ps1 -Out <pdf path>
param([Parameter(Mandatory)] [string] $Out)
$ErrorActionPreference = 'Stop'

function Set-DocProperty($props, [string] $name, [string] $value) {
    # Office document property collections are late-bound; PowerShell cannot call Item() on them directly.
    $prop = [System.__ComObject].InvokeMember('Item', 'GetProperty', $null, $props, @($name))
    [System.__ComObject].InvokeMember('Value', 'SetProperty', $null, $prop, @($value)) | Out-Null
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
try {
    $wb = $excel.Workbooks.Add()
    Set-DocProperty $wb.BuiltinDocumentProperties 'Title' 'Invoice INV-2026-0042'
    Set-DocProperty $wb.BuiltinDocumentProperties 'Author' 'Example Freight Billing'
    $ws = $wb.Worksheets.Item(1)
    $ws.Name = 'Invoice'
    $ws.Cells.Item(1, 1).Value2 = 'Example Freight LLC - Invoice INV-2026-0042'
    $ws.Cells.Item(1, 1).Font.Bold = $true
    $ws.Cells.Item(1, 1).Font.Size = 16
    $hdr = @('Line', 'Description', 'Quantity', 'Unit price', 'Amount')
    for ($c = 1; $c -le 5; $c++) { $ws.Cells.Item(3, $c).Value2 = $hdr[$c - 1]; $ws.Cells.Item(3, $c).Font.Bold = $true }
    $arrow = [string][char]0x2192
    for ($r = 1; $r -le 40; $r++) {
        $row = 3 + $r
        $ws.Cells.Item($row, 1).Value2 = [double]$r
        $ws.Cells.Item($row, 2).Value2 = "Pallet move, lane $r (Zurich $arrow Koln)"
        $ws.Cells.Item($row, 3).Value2 = [double](($r % 7) + 1)
        $ws.Cells.Item($row, 4).Value2 = [double](125.5 + $r)
        $ws.Cells.Item($row, 5).Formula = "=C$row*D$row"
    }
    $ws.Cells.Item(45, 4).Value2 = 'Total'
    $ws.Cells.Item(45, 5).Formula = '=SUM(E4:E43)'
    $ws.Range('D4:E45').NumberFormat = '#,##0.00'
    $ws.Columns.AutoFit() | Out-Null
    $ws2 = $wb.Worksheets.Add([Type]::Missing, $ws)
    $ws2.Name = 'Summary'
    $months = @('Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec')
    $ws2.Cells.Item(1, 1).Value2 = 'Month'
    $ws2.Cells.Item(1, 2).Value2 = 'Billed'
    for ($m = 0; $m -lt 12; $m++) { $ws2.Cells.Item($m + 2, 1).Value2 = $months[$m]; $ws2.Cells.Item($m + 2, 2).Value2 = [double](40000 + 1750 * $m) }
    $chart = $ws2.Shapes.AddChart2(201, 51, 200, 10, 520, 300).Chart
    $chart.SetSourceData($ws2.Range('A1:B13'))
    $chart.HasTitle = $true
    $chart.ChartTitle.Text = 'Billed by month'
    $ws2.PageSetup.Orientation = 2  # xlLandscape
    Write-Output 'built'
    # Workbook.ExportAsFixedFormat(xlTypePDF=0, path, Quality=Standard 0, IncludeDocProperties, IgnorePrintAreas)
    $wb.ExportAsFixedFormat(0, $Out, 0, $true, $false)
    Write-Output 'exported'
    $wb.Close($false)
}
finally {
    $excel.Quit()
}

