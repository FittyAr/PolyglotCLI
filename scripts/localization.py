"""Valida las tablas de textos de la interfaz (PolyglotCLI.web/Localization/Strings).

  python scripts/localization.py

Comprueba que:
  - toda clave usada en el código (L["..."], Loc.T("..."), CoreText.Get/Format("...")) exista en en.json;
  - cada idioma tenga exactamente las claves de en.json, sin valores vacíos;
  - los marcadores {0}, {1}… de cada traducción coincidan con los del inglés.

Para agregar un texto: usar la clave en el código y agregarla a los 10 archivos
<idioma>.json (en.json es la referencia). Sale con código 1 si hay errores.
La prueba LocalizationTablesTests del proyecto de tests hace las mismas
comprobaciones de tablas en cada build.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STRINGS = ROOT / "PolyglotCLI.web" / "Localization" / "Strings"
CODE_DIRS = [ROOT / "PolyglotCLI.web", ROOT / "PolyglotCLI.Maui", ROOT / "PolyglotCLI.core"]
BASE = "en"
LANGS = ["en", "es", "pt", "fr", "de", "it", "ru", "zh-Hans", "ja", "ko"]

KEY_USE = re.compile(r'(?:\bL\[|Loc\.T\(|\bLoc\[|CoreText\.(?:Get|Format)\()\s*"([A-Za-z0-9_.]+)"')
PLACEHOLDER = re.compile(r"\{(\d+)(?:[^}]*)\}")


def load(path: Path) -> dict:
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def used_keys() -> set:
    used = set()
    for d in CODE_DIRS:
        for f in list(d.rglob("*.razor")) + list(d.rglob("*.cs")):
            if any(p in ("bin", "obj") for p in f.parts):
                continue
            # Sin comentarios de línea (los ejemplos de uso en /// no cuentan).
            code = "\n".join(
                line for line in f.read_text(encoding="utf-8", errors="ignore").splitlines()
                if not line.lstrip().startswith("//")
            )
            used.update(KEY_USE.findall(code))
    return used


def check() -> int:
    errors = 0
    base = load(STRINGS / f"{BASE}.json")
    used = used_keys()

    for key in sorted(used - base.keys()):
        print(f"[código] clave sin texto en {BASE}.json: {key}")
        errors += 1
    for key in sorted(base.keys() - used):
        print(f"[aviso] clave de {BASE}.json sin uso en el código: {key}")

    for lang in LANGS:
        if lang == BASE:
            continue
        path = STRINGS / f"{lang}.json"
        if not path.exists():
            print(f"[{lang}] falta {path.name}")
            errors += 1
            continue
        table = load(path)
        for key in sorted(base.keys() - table.keys()):
            print(f"[{lang}] falta: {key}")
            errors += 1
        for key in sorted(table.keys() - base.keys()):
            print(f"[{lang}] sobra: {key}")
            errors += 1
        for key in base.keys() & table.keys():
            if set(PLACEHOLDER.findall(base[key])) != set(PLACEHOLDER.findall(table[key])):
                print(f"[{lang}] marcadores distintos en {key}: {table[key]!r}")
                errors += 1
            if not table[key].strip():
                print(f"[{lang}] vacía: {key}")
                errors += 1

    print("OK" if errors == 0 else f"{errors} errores")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(check())
