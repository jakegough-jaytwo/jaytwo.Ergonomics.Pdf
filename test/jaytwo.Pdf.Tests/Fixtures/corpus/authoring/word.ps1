# Exports word-report.html to PDF with Microsoft Word through COM (tagged, heading bookmarks, document properties).
# Usage: word.ps1 -Source <word-report.html> -Out <pdf path> [-Print <pdf path for Microsoft Print to PDF>]
param([Parameter(Mandatory)] [string] $Source, [Parameter(Mandatory)] [string] $Out, [string] $Print)
$ErrorActionPreference = 'Stop'
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
    # Open(FileName, ConfirmConversions, ReadOnly, AddToRecentFiles)
    $doc = $word.Documents.Open($Source, $false, $true, $false)
    $doc.ActiveWindow.View.Type = 3  # wdPrintView
    Write-Output 'opened'
    # ExportAsFixedFormat(path, wdExportFormatPDF=17, OpenAfterExport, OptimizeFor=Print 0, Range=All 0, From, To,
    #   Item=DocumentContent 0, IncludeDocProps, KeepIRM, CreateBookmarks=Heading 1, DocStructureTags, BitmapMissingFonts, UseISO19005_1)
    $doc.ExportAsFixedFormat($Out, 17, $false, 0, 0, 1, 1, 0, $true, $true, 1, $true, $true, $false)
    Write-Output 'exported'
    if ($Print) {
        # PrintOut(Background, Append, Range=All 0, OutputFileName, From, To, Item, Copies, Pages, PageType, PrintToFile)
        $word.ActivePrinter = 'Microsoft Print to PDF'
        $doc.PrintOut($false, $false, 0, $Print, '', '', 0, 1, '', 0, $true)
        Write-Output 'printed'
    }
    $doc.Close(0)
}
finally {
    $word.Quit()
}
