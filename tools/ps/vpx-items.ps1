# Read-only: type of the named objects in a .vpx (Wall = no X/Y). Usage: vpx-items.ps1 <table.vpx> name1,name2
param($vpx, [string[]]$names)
Add-Type -Path "C:\vPinball\CabSound\tools\CabSound.TableCheck\bin\Release\net10.0\OpenMcdf.dll"
$types = @{0='Wall';1='Flipper';2='Timer';3='Plunger';4='Textbox';5='Bumper';6='Trigger';7='Light';8='Kicker';9='Decal';10='Gate';11='Spinner';12='Ramp';14='LightCenter';17='DispReel';18='LightSeq';19='Primitive';20='Flasher';21='Rubber';22='HitTarget'}
$cf = New-Object OpenMcdf.CompoundFile($vpx, [OpenMcdf.CFSUpdateMode]::ReadOnly, [OpenMcdf.CFSConfiguration]::Default)
$g = $cf.RootStorage.GetStorage("GameStg")
$want = @{}; $names | % { $want[$_.ToLower()] = $_ }
for ($i=0; $i -lt 5000; $i++) {
  try { $s = $g.GetStream("GameItem$i") } catch { break }
  $b = $s.GetData()
  $t = [BitConverter]::ToInt32($b,0)
  $p = 4
  while ($p -lt $b.Length - 8) {
    $len = [BitConverter]::ToInt32($b,$p); $tag = [Text.Encoding]::ASCII.GetString($b,$p+4,4)
    if ($tag -eq 'NAME') { $n = [BitConverter]::ToInt32($b,$p+8); $name = [Text.Encoding]::Unicode.GetString($b,$p+12,$n); if ($want.ContainsKey($name.ToLower())) { "{0} = {1}" -f $name, $types[$t] }; break }
    if ($len -le 0) { break }; $p += 4 + $len
  }
}
$cf.Close()
