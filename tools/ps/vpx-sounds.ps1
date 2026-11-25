# Read-only: every sound in a .vpx: output, name, channels, length (wav), file. Usage: vpx-sounds.ps1 <table.vpx>
param($vpx)
Add-Type -Path "C:\vPinball\CabSound\tools\CabSound.TableCheck\bin\Release\net10.0\OpenMcdf.dll"
$cf = New-Object OpenMcdf.CompoundFile($vpx, [OpenMcdf.CFSUpdateMode]::ReadOnly, [OpenMcdf.CFSConfiguration]::Default)
$g = $cf.RootStorage.GetStorage("GameStg")
for ($i=0; $i -lt 5000; $i++) {
  try { $b = $g.GetStream("Sound$i").GetData() } catch { break }
  $p = 0
  function Str { $n = [BitConverter]::ToInt32($b,$script:p); $s = [Text.Encoding]::GetEncoding(28591).GetString($b,$script:p+4,$n); $script:p += 4 + $n; $s }
  $name = Str; $path = Str; $null = Str
  $ch = 0; $rate = 0
  if ($path.ToLower().EndsWith(".wav")) { $ch = [BitConverter]::ToUInt16($b,$p+2); $rate = [BitConverter]::ToInt32($b,$p+4); $bps=[BitConverter]::ToInt32($b,$p+8); $p += 18 }
  $len = [BitConverter]::ToInt32($b,$p); $p += 4 + $len
  $out = if ($b[$p] -eq 1) { "Backglass" } else { "Table" }
  $secs = if ($ch -gt 0) { [math]::Round($len / $bps, 1) } else { "?" }
  "{0}`t{1}`t{2}ch`t{3}s`t{4}" -f $out, $name, $ch, $secs, $path
}
$cf.Close()
