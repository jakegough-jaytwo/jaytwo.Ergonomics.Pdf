# Builds an image-heavy deck in Microsoft PowerPoint through COM and saves it as PDF.
# Usage: powerpoint.ps1 -Assets <dir with photo1-3.jpg> -Out <pdf path>
param([Parameter(Mandatory)] [string] $Assets, [Parameter(Mandatory)] [string] $Out)
$ErrorActionPreference = 'Stop'

function Set-DocProperty($props, [string] $name, [string] $value) {
    $prop = [System.__ComObject].InvokeMember('Item', 'GetProperty', $null, $props, @($name))
    [System.__ComObject].InvokeMember('Value', 'SetProperty', $null, $prop, @($value)) | Out-Null
}

$ppt = New-Object -ComObject PowerPoint.Application
try {
    # Presentations.Add(WithWindow = msoFalse)
    $pres = $ppt.Presentations.Add(0)
    Set-DocProperty $pres.BuiltInDocumentProperties 'Title' 'Fleet Photo Review'
    Set-DocProperty $pres.BuiltInDocumentProperties 'Author' 'Example Freight Marketing'
    $i = 0
    foreach ($photo in @('photo1.jpg', 'photo2.jpg', 'photo3.jpg')) {
        $i++
        $slide = $pres.Slides.Add($i, 11)  # ppLayoutTitleOnly
        $slide.Shapes.Item(1).TextFrame.TextRange.Text = "Depot photo $i"
        # AddPicture(FileName, LinkToFile=msoFalse, SaveWithDocument=msoTrue, Left, Top, Width, Height)
        $slide.Shapes.AddPicture((Join-Path $Assets $photo), 0, -1, 60, 110, 600, 400) | Out-Null
    }
    Write-Output 'built'
    $pres.SaveAs($Out, 32)  # ppSaveAsPDF
    Write-Output 'exported'
    $pres.Close()
}
finally {
    $ppt.Quit()
}
