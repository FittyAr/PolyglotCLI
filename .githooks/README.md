# Pre-push hook

Este directorio tiene un hook `pre-push` que corre `dotnet build` +
`dotnet test` antes de cada `git push`. Si el build falla o los tests
no pasan, el push se bloquea y el error se muestra en pantalla.

## Activarlo (una vez por clone)

```powershell
git config core.hooksPath .githooks
```

Eso le dice a git que busque los hooks en este directorio en lugar
de `.git/hooks/`. Sin este paso, el hook no se ejecuta.

Para comprobar que quedó activo:

```powershell
git config --get core.hooksPath   # debe imprimir .githooks
```

## Bypassearlo en una emergencia

```powershell
git push --no-verify
```

Útil si sabés que el build está roto pero necesitás pushear de
todos modos. No abusar.

## Archivos

- `pre-push` — el hook que git ejecuta. Git solo reconoce el nombre
  exacto del hook (sin extensión), por eso **no** sirve un
  `pre-push.bat` ni un `pre-push.ps1` sueltos. Es un script `sh`
  (Git para Windows trae `sh`) que hace
  `exec powershell.exe -NoProfile -ExecutionPolicy Bypass -File pre-push.ps1`
  (o `pwsh` si no hay `powershell.exe`, p. ej. en Mac/Linux).
- `pre-push.ps1` — la lógica real: corre `dotnet build` +
  `dotnet test` y devuelve exit code 0/1 según el resultado.
- `.gitattributes` — fuerza finales de línea LF en `pre-push`
  aunque `core.autocrlf=true` (con CRLF el shebang de `sh` falla).

Al agregar el hook al repo conviene marcarlo ejecutable (necesario
en Mac/Linux):

```powershell
git add --chmod=+x .githooks/pre-push
```

## Compatibilidad

- Windows: funciona con Git para Windows (usa `powershell.exe`).
- Mac/Linux: requiere `pwsh` (PowerShell 7) en el PATH; si no está,
  el hook avisa y deja pasar el push sin build ni tests.

## Cron de backup

Además del hook, hay un cron `polyglotcli-check` (en el dataDir
del agent) que corre cada 30 min y verifica el estado del build
de forma pasiva. Es la red de seguridad por si alguien pushea
desde un path que bypassa el hook (otro clone, GitHub Desktop,
etc).
